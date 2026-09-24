using System;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Os oito atributos de uma unidade. Vida, Ataque, Defesa e Velocidade são números absolutos;
	/// Crítico, Dano crítico, Foco e Resistência são frações (0,15 = 15%).
	/// </summary>
	public sealed record StatBlock
	{
		public double Health { get; init; }
		public double Attack { get; init; }
		public double Defense { get; init; }
		public double Speed { get; init; }
		public double Crit { get; init; }
		public double CritDamage { get; init; }
		public double Focus { get; init; }
		public double Resistance { get; init; }

		public double Get(Stat stat) => stat switch
		{
			Stat.Health => Health,
			Stat.Attack => Attack,
			Stat.Defense => Defense,
			Stat.Speed => Speed,
			Stat.Crit => Crit,
			Stat.CritDamage => CritDamage,
			Stat.Focus => Focus,
			Stat.Resistance => Resistance,
			_ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
		};

		public StatBlock With(Stat stat, double value) => stat switch
		{
			Stat.Health => this with { Health = value },
			Stat.Attack => this with { Attack = value },
			Stat.Defense => this with { Defense = value },
			Stat.Speed => this with { Speed = value },
			Stat.Crit => this with { Crit = value },
			Stat.CritDamage => this with { CritDamage = value },
			Stat.Focus => this with { Focus = value },
			Stat.Resistance => this with { Resistance = value },
			_ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
		};

		/// <summary>Multiplica os quatro atributos absolutos. As frações ficam como estão.</summary>
		public StatBlock ScaleAbsolute(double factor) => this with
		{
			Health = Health * factor,
			Attack = Attack * factor,
			Defense = Defense * factor,
			Speed = Speed * factor,
		};
	}
}
