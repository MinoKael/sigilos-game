namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Uma regra em vigor numa unidade: a estratégia (<see cref="Behavior"/>) e os números desta
	/// aplicação. Os efeitos de status são regras com duração (<see cref="StatusEffect"/>); a Passiva e
	/// os conjuntos de runas são regras que valem a luta inteira.
	/// </summary>
	public class UnitRule
	{
		internal UnitRule(UnitBehavior behavior, double value = 0, BattleUnit? source = null)
		{
			Behavior = behavior;
			Value = value;
			Source = source;
		}

		/// <summary>O que a regra faz em cada momento da luta.</summary>
		internal UnitBehavior Behavior { get; }

		/// <summary>Em quem a regra está. Ligado quando ela entra na unidade.</summary>
		public BattleUnit Owner { get; internal set; } = null!;

		/// <summary>O número da regra: o dano que o escudo ainda absorve, a fração da Passiva, a chance do conjunto.</summary>
		public double Value { get; set; }

		/// <summary>Quem pôs o efeito: em quem o provocado tem que mirar, de quem é o Ataque da Bomba.</summary>
		public BattleUnit? Source { get; set; }
	}
}
