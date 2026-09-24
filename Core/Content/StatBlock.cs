using System;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Os oito atributos de uma unidade. Vida, Ataque, Defesa e Velocidade são números absolutos;
	/// Crítico, Dano crítico, Resistência e Precisão são frações (0,15 = 15%).
	/// </summary>
	public sealed record StatBlock
	{
		public double Health { get; init; }
		public double Attack { get; init; }
		public double Defense { get; init; }
		public double Speed { get; init; }
		public double Crit { get; init; }
		public double CritDamage { get; init; }
		public double Resistance { get; init; }
		public double Accuracy { get; init; }

		public double Get(Stat stat) => stat switch
		{
			Stat.Health => Health,
			Stat.Attack => Attack,
			Stat.Defense => Defense,
			Stat.Speed => Speed,
			Stat.Crit => Crit,
			Stat.CritDamage => CritDamage,
			Stat.Accuracy => Accuracy,
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
			Stat.Accuracy => this with { Accuracy = value },
			Stat.Resistance => this with { Resistance = value },
			_ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
		};

		/// <summary>Vida, Ataque, Defesa e Velocidade são números; os outros quatro são frações.</summary>
		public static bool IsAbsolute(Stat stat) => stat is Stat.Health or Stat.Attack or Stat.Defense or Stat.Speed;

		/// <summary>Soma atributo a atributo.</summary>
		public StatBlock Plus(StatBlock other) => new()
		{
			Health = Health + other.Health,
			Attack = Attack + other.Attack,
			Defense = Defense + other.Defense,
			Speed = Speed + other.Speed,
			Crit = Crit + other.Crit,
			CritDamage = CritDamage + other.CritDamage,
			Accuracy = Accuracy + other.Accuracy,
			Resistance = Resistance + other.Resistance,
		};
	}
}
