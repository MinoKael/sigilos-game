using System;
using System.Collections.Generic;

namespace Sigilos.Sounds.Synth
{
	/// <summary>
	/// As notas da biblioteca, todas em volta de Ré: a interface, o grimório e as recompensas em Ré maior e
	/// pentatônica, o céu em Ré lídio (o Sol sustenido sonhador), as trevas em Ré frígio. O intervalo de
	/// assinatura do Sigilos é a quinta empilhada Ré–Lá–Mi.
	/// </summary>
	public static class Notes
	{
		public const int D2 = 38, A2 = 45, D3 = 50, F3 = 53, A3 = 57, D4 = 62, Ds4 = 63, E4 = 64, F4 = 65, Fs4 = 66, G4 = 67, Gs4 = 68, A4 = 69, As4 = 70, B4 = 71;
		public const int C5 = 72, Cs5 = 73, D5 = 74, Ds5 = 75, E5 = 76, F5 = 77, Fs5 = 78, G5 = 79, Gs5 = 80, A5 = 81, As5 = 82, B5 = 83;
		public const int C6 = 84, Cs6 = 85, D6 = 86, Ds6 = 87, E6 = 88, F6 = 89, Fs6 = 90, G6 = 91, Gs6 = 92, A6 = 93, B6 = 95;
		public const int Cs7 = 97, D7 = 98, E7 = 100, Fs7 = 102, A7 = 105;

		public static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
		public static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 };
		public static readonly int[] Lydian = { 0, 2, 4, 6, 7, 9, 11 };
		public static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };

		/// <summary>A frequência de uma nota MIDI (Lá 4 = 69 = 440 Hz).</summary>
		public static double Hz(double midi) => 440 * Math.Pow(2, (midi - 69) / 12);

		/// <summary>A nota da escala de Ré mais perto de <paramref name="midi"/>.</summary>
		public static int Snap(double midi, IReadOnlyList<int> scale)
		{
			var best = (int)Math.Round(midi);
			var distance = double.MaxValue;
			for (var octave = 2; octave <= 8; octave++)
				foreach (var step in scale)
				{
					var note = 2 + 12 * octave + step;
					var d = Math.Abs(note - midi);
					if (d < distance)
					{
						distance = d;
						best = note;
					}
				}
			return best;
		}

		/// <summary>As notas da escala de Ré de <paramref name="from"/> até <paramref name="to"/>, subindo.</summary>
		public static List<int> Run(int from, int to, IReadOnlyList<int> scale)
		{
			var notes = new List<int>();
			for (var note = from; note <= to; note++)
				if (Contains(scale, ((note - 2) % 12 + 12) % 12))
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
