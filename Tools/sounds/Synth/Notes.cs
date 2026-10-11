using System;
using System.Collections.Generic;

namespace Sigilos.Sounds.Synth
{
	/// <summary>
	/// As notas da biblioteca, na tonalidade da música do jogo: Mi menor / Sol maior. A melodia fica na
	/// pentatônica (Mi Sol Lá Si Ré), que soa junto com qualquer acorde da música; a escala inteira só para
	/// passagens. O intervalo de assinatura do Sigilos é a quinta empilhada Ré–Lá–Mi, que resolve em Mi.
	/// </summary>
	public static class Notes
	{
		/// <summary>A tônica, Mi, como passo da oitava (Dó = 0).</summary>
		private const int Root = 4;

		public const int E2 = 40, G2 = 43, A2 = 45, B2 = 47, D3 = 50, E3 = 52, Fs3 = 54, G3 = 55, A3 = 57, B3 = 59;
		public const int C4 = 60, D4 = 62, E4 = 64, Fs4 = 66, G4 = 67, A4 = 69, B4 = 71;
		public const int C5 = 72, D5 = 74, E5 = 76, Fs5 = 78, G5 = 79, A5 = 81, B5 = 83;
		public const int C6 = 84, D6 = 86, E6 = 88, Fs6 = 90, G6 = 91, A6 = 93, B6 = 95;
		public const int D7 = 98, E7 = 100, G7 = 103, A7 = 105;

		/// <summary>Mi menor natural, as notas de Sol maior.</summary>
		public static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };

		/// <summary>Mi Sol Lá Si Ré: nenhuma nota briga com a música.</summary>
		public static readonly int[] Pentatonic = { 0, 3, 5, 7, 10 };

		/// <summary>A frequência de uma nota MIDI (Lá 4 = 69 = 440 Hz).</summary>
		public static double Hz(double midi) => 440 * Math.Pow(2, (midi - 69) / 12);

		/// <summary>A nota da escala de Mi mais perto de <paramref name="midi"/>.</summary>
		public static int Snap(double midi, IReadOnlyList<int> scale)
		{
			var best = (int)Math.Round(midi);
			var distance = double.MaxValue;
			for (var octave = 2; octave <= 8; octave++)
				foreach (var step in scale)
				{
					var note = Root + 12 * octave + step;
					var d = Math.Abs(note - midi);
					if (d < distance)
					{
						distance = d;
						best = note;
					}
				}
			return best;
		}

		/// <summary>As notas da escala de Mi de <paramref name="from"/> até <paramref name="to"/>, subindo.</summary>
		public static List<int> Run(int from, int to, IReadOnlyList<int> scale)
		{
			var notes = new List<int>();
			for (var note = from; note <= to; note++)
				if (Contains(scale, ((note - Root) % 12 + 12) % 12))
					notes.Add(note);
			return notes;
		}

		private static bool Contains(IReadOnlyList<int> scale, int step)
		{
			foreach (var s in scale)
				if (s == step)
					return true;
			return false;
		}
	}
}
