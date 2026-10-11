using System;

namespace Sigilos.Sounds.Synth
{
	/// <summary>
	/// Um filtro que pode andar: o corte vai de <see cref="From"/> a <see cref="To"/> ao longo da camada (em
	/// escala de oitavas, com o expoente <see cref="Curve"/>) e ainda pode balançar com um LFO de
	/// <see cref="LfoOctaves"/> oitavas a <see cref="LfoRate"/> Hz (água, vento, chama).
	/// </summary>
	public sealed class FilterSpec
	{
		public FilterSpec(FilterKind kind, double from, double to, double q, double gainDb = 0)
		{
			Kind = kind;
			From = from;
			To = to > 0 ? to : from;
			Q = q;
			GainDb = gainDb;
		}

		public FilterKind Kind { get; }
		public double From { get; }
		public double To { get; }
		public double Q { get; }
		public double GainDb { get; }
		public double Curve { get; set; } = 1;
		public double LfoRate { get; set; }
		public double LfoOctaves { get; set; }
		public double LfoPhase { get; set; }

		/// <summary>O corte no ponto <paramref name="progress"/> (0 a 1) da camada, no segundo <paramref name="t"/>.</summary>
		public double Cutoff(double progress, double t)
		{
			var hz = From == To ? From : From * Math.Pow(To / From, Math.Pow(progress, Curve));
			if (LfoOctaves > 0)
				hz *= Math.Pow(2, LfoOctaves * Math.Sin(2 * Math.PI * (LfoRate * t + LfoPhase)));
			return hz;
		}
	}
}
