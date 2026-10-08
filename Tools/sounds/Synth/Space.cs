using System;

namespace Sigilos.Sounds.Synth
{
	/// <summary>
	/// O espaço em volta do efeito: uma sala pequena (Freeverb mono, oito pentes e quatro passa-tudo) e um eco
	/// com perda de agudos. A parte molhada sai com a mesma energia da seca vezes <c>mix</c>, então
	/// <c>mix</c> quer dizer o mesmo em qualquer efeito. Eventos que se repetem pedem caudas curtas.
	/// </summary>
	public static class Space
	{
		private static readonly int[] Combs = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
		private static readonly int[] Allpasses = { 556, 441, 341, 225 };

		public static double[] Reverb(double[] dry, int sampleRate, double mix, double seconds, double damp, double predelay)
		{
			var scale = sampleRate / 44100.0 * 0.85;
			var pre = (int)(predelay * sampleRate);
			var length = dry.Length + pre + (int)(seconds * sampleRate);
			var wet = new double[length];
			foreach (var size in Combs)
			{
				var delay = Math.Max(1, (int)(size * scale));
				var feedback = Math.Pow(10, -3.0 * delay / (sampleRate * Math.Max(0.05, seconds)));
				var buffer = new double[delay];
				var store = 0.0;
				var index = 0;
				for (var i = 0; i < length; i++)
				{
					var source = i - pre;
					var input = source >= 0 && source < dry.Length ? dry[source] : 0;
					var output = buffer[index];
					store = output * (1 - damp) + store * damp;
					buffer[index] = input + store * feedback;
					index = (index + 1) % delay;
					wet[i] += output;
				}
			}
			foreach (var size in Allpasses)
			{
				var delay = Math.Max(1, (int)(size * scale));
				var buffer = new double[delay];
				var index = 0;
				for (var i = 0; i < length; i++)
				{
					var buffered = buffer[index];
					var output = buffered - wet[i];
					buffer[index] = wet[i] + buffered * 0.5;
					index = (index + 1) % delay;
					wet[i] = output;
				}
			}
			return Blend(dry, wet, mix);
		}

		public static double[] Echo(double[] dry, int sampleRate, double time, double feedback, double mix)
		{
			var delay = Math.Max(1, (int)(time * sampleRate));
			var repeats = Math.Log(0.001) / Math.Log(Math.Clamp(feedback, 0.01, 0.95));
			var length = dry.Length + (int)(delay * repeats);
			var wet = new double[length];
			var line = new double[delay];
			var smooth = 0.0;
			var index = 0;
			for (var i = 0; i < length; i++)
			{
				var input = i < dry.Length ? dry[i] : 0;
				var output = line[index];
				smooth += 0.35 * (output - smooth);
				line[index] = input + smooth * feedback;
				index = (index + 1) % delay;
				wet[i] = output;
			}
			return Blend(dry, wet, mix);
		}

		private static double[] Blend(double[] dry, double[] wet, double mix)
		{
			var dryEnergy = LayerRenderer.Rms(dry) * Math.Sqrt(dry.Length);
			var wetEnergy = LayerRenderer.Rms(wet) * Math.Sqrt(wet.Length);
			var gain = wetEnergy > 1e-12 ? mix * dryEnergy / wetEnergy : 0;
			var result = new double[wet.Length];
			for (var i = 0; i < result.Length; i++)
				result[i] = (i < dry.Length ? dry[i] : 0) + wet[i] * gain;
			return result;
		}
	}
}
