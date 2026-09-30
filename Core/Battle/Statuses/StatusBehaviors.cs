using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// A estratégia de cada <see cref="StatusKind"/>. Os números moram em <see cref="BattleRules"/>; aqui
	/// só se diz que estratégia cada efeito usa.
	/// </summary>
	internal static class StatusBehaviors
	{
		private static readonly Dictionary<StatusKind, StatusBehavior> Table = new()
		{
			[StatusKind.Shield] = new ShieldStatus(),
			[StatusKind.Burn] = new DamageOverTime(BattleRules.BurnFraction, BattleRules.MaxBurnStacks),
			[StatusKind.Poison] = new DamageOverTime(BattleRules.PoisonFraction, BattleRules.MaxPoisonStacks),
			[StatusKind.Bomb] = new BombStatus(),
			[StatusKind.Stun] = new StunStatus(),
			[StatusKind.Taunt] = new TauntStatus(),
			[StatusKind.Hidden] = new HiddenStatus(),
			[StatusKind.Curse] = new CurseStatus(),
			[StatusKind.Blind] = new BlindStatus(),
			[StatusKind.Aegis] = new AegisStatus(),
			[StatusKind.Foresight] = new ForesightStatus(),
			[StatusKind.Immunity] = new ImmunityStatus(),
			[StatusKind.AttackUp] = new StatChange(Stat.Attack, BattleRules.AttackUpBonus),
			[StatusKind.AttackDown] = new StatChange(Stat.Attack, -BattleRules.AttackDownPenalty),
			[StatusKind.DefenseUp] = new StatChange(Stat.Defense, BattleRules.DefenseUpBonus),
			[StatusKind.DefenseBreak] = new StatChange(Stat.Defense, -BattleRules.DefenseDownPenalty),
			[StatusKind.SpeedUp] = new StatChange(Stat.Speed, BattleRules.SpeedUpBonus),
		};

		public static StatusBehavior Of(StatusKind kind) => Table[kind];
	}
}
