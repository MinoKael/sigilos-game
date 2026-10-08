using System;

namespace Sigilos.Sounds.Synth
{
	/// <summary>
	/// O volume de uma camada no tempo: subida (<see cref="Attack"/>), topo (<see cref="Hold"/>), queda até o
	/// <see cref="Sustain"/> (<see cref="Decay"/>), o sustain parado (<see cref="SustainTime"/>) e a soltura
	/// (<see cref="Release"/>). <see cref="Curve"/> é o expoente da queda e da soltura (1 reta, 3 bem
	/// percussivo); <see cref="AttackCurve"/> o da subida (acima de 1 começa devagar, como um som invertido).
	/// A duração da camada é a soma das fases.
	/// </summary>
	public readonly record struct Envelope(double Attack, double Hold, double Decay, double Sustain, double SustainTime, double Release, double Curve = 3, double AttackCurve = 1)
	{
		public double Length => Attack + Hold + Decay + SustainTime + Release;

		/// <summary>Golpe: sobe em <paramref name="attack"/> e cai a zero em <paramref name="decay"/>.</summary>
		public static Envelope Perc(double attack, double decay, double curve = 3) => new(attack, 0, decay, 0, 0, 0, curve);

		/// <summary>Golpe com um topo parado antes de cair.</summary>
		public static Envelope Hit(double attack, double hold, double decay, double curve = 3) => new(attack, hold, decay, 0, 0, 0, curve);

		/// <summary>Cresce devagar até o topo e solta: energia acumulando, sons invertidos, sopros.</summary>
		public static Envelope Swell(double attack, double release, double curve = 2) => new(attack, 0, 0, 1, 0, release, curve, 2);

		/// <summary>Tapete: sobe, fica <paramref name="hold"/> e solta.</summary>
		public static Envelope Pad(double attack, double hold, double release) => new(attack, 0, 0, 1, hold, release, 2, 1.5);

		/// <summary>Sopro que sobe numa parte do tempo e cai no resto (passagens de ar, folhas).</summary>
		public static Envelope Gust(double length, double rise = 0.6) => new(length * rise, 0, length * (1 - rise), 0, 0, 0, 2, 1.6);

		public double At(double t)
		{
			if (t < 0)
				return 0;
			if (t < Attack)
				return Math.Pow(t / Attack, AttackCurve);
			t -= Attack;
			if (t < Hold)
				return 1;
			t -= Hold;
			if (t < Decay)
				return Sustain + (1 - Sustain) * Math.Pow(1 - t / Decay, Curve);
			t -= Decay;
			if (t < SustainTime)
				return Sustain;
			t -= SustainTime;
			if (t < Release)
				return Sustain * Math.Pow(1 - t / Release, Curve);
			return 0;
		}
	}
}
