using System;
using System.IO;
using System.Text;
using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Mastering
{
	/// <summary>WAV PCM de 16 bits, mono. A gravação usa dither triangular com sorteio próprio: a mesma semente dá os mesmos bytes.</summary>
	public static class WavFile
	{
		public static void Write(string path, double[] samples, int sampleRate, Rng dither)
		{
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
			{
				var bytes = samples.Length * 2;
				writer.Write(Encoding.ASCII.GetBytes("RIFF"));
				writer.Write(36 + bytes);
				writer.Write(Encoding.ASCII.GetBytes("WAVE"));
				writer.Write(Encoding.ASCII.GetBytes("fmt "));
				writer.Write(16);
				writer.Write((short)1);
				writer.Write((short)1);
				writer.Write(sampleRate);
				writer.Write(sampleRate * 2);
				writer.Write((short)2);
				writer.Write((short)16);
				writer.Write(Encoding.ASCII.GetBytes("data"));
				writer.Write(bytes);
				foreach (var sample in samples)
				{
					var value = sample * 32767 + (dither.Next() - dither.Next());
					writer.Write((short)Math.Clamp(Math.Round(value), -32767, 32767));
				}
			}
			File.WriteAllBytes(path, stream.ToArray());
		}

		/// <summary>Lê de volta um WAV desta ferramenta: as amostras (de −1 a 1) e a taxa.</summary>
		public static (double[] Samples, int SampleRate) Read(string path)
		{
			var data = File.ReadAllBytes(path);
			if (data.Length < 12 || Encoding.ASCII.GetString(data, 0, 4) != "RIFF" || Encoding.ASCII.GetString(data, 8, 4) != "WAVE")
				throw new InvalidDataException($"{path} não é um WAV.");
			var sampleRate = 0;
			var position = 12;
			while (position + 8 <= data.Length)
			{
				var id = Encoding.ASCII.GetString(data, position, 4);
				var size = BitConverter.ToInt32(data, position + 4);
				var body = position + 8;
				if (id == "fmt ")
					sampleRate = BitConverter.ToInt32(data, body + 4);
				else if (id == "data")
				{
					var count = Math.Min(size, data.Length - body) / 2;
					var samples = new double[count];
					for (var i = 0; i < count; i++)
						samples[i] = BitConverter.ToInt16(data, body + i * 2) / 32768.0;
					return (samples, sampleRate);
				}
				position = body + size + (size & 1);
			}
			throw new InvalidDataException($"{path} não tem amostras.");
		}
	}
}
