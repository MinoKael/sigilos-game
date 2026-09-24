using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Quem lança um efeito: uma unidade em campo ou o Conjurador. Junta o que o resolvedor de efeitos
	/// precisa saber dos dois, para ele não ter que perguntar "é unidade ou é Conjurador?" a cada linha.
	/// </summary>
	public sealed class Caster
	{
		private Caster(Side side, BattleUnit? unit, double attack, double crit, double critDamage, double focus, Element? element, double skillPower)
		{
			Side = side;
			Unit = unit;
			Attack = attack;
			Crit = crit;
			CritDamage = critDamage;
			Focus = focus;
			Element = element;
			SkillPower = skillPower;
		}

		public Side Side { get; }

		/// <summary>Nulo quando quem lança é o Conjurador.</summary>
		public BattleUnit? Unit { get; }

		public double Attack { get; }
		public double Crit { get; }
		public double CritDamage { get; }
		public double Focus { get; }

		/// <summary>O Conjurador não tem elemento: suas páginas são neutras.</summary>
		public Element? Element { get; }

		public double SkillPower { get; }

		public static Caster Of(BattleUnit unit) => new(
			unit.Side,
			unit,
			unit.Attack,
			unit.Stats.Crit,
			unit.Stats.CritDamage,
			unit.Stats.Focus,
			unit.Element,
			unit.SkillPower);

		public static Caster Of(ConjurerSeat conjurer) => new(Side.Allies, null, conjurer.Power, 0, 0, 0, null, 1);
	}
}
