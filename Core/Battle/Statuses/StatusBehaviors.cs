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
			[StatusKind.Affliction] = new DamageOverTime(BattleRules.AfflictionFraction, BattleRules.MaxStatuses),
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
			[StatusKind.SpeedDown] = new StatChange(Stat.Speed, -BattleRules.SpeedDownPenalty),
			[StatusKind.CritUp] = new StatChange(Stat.Crit, BattleRules.CritUpBonus, additive: true),
			[StatusKind.CritResist] = new CritResistStatus(),
			[StatusKind.Blessing] = new HealOverTime(BattleRules.BlessingFraction),
			[StatusKind.Counter] = new CounterStatus(),
			[StatusKind.Revive] = new ReviveStatus(),
			[StatusKind.Karma] = new KarmaStatus(),
			[StatusKind.Sleep] = new SleepStatus(),
			[StatusKind.Unrecoverable] = new UnrecoverableStatus(),
			[StatusKind.Silence] = new SilenceStatus(),
			[StatusKind.Oblivion] = new OblivionStatus(),
		};

		public static StatusBehavior Of(StatusKind kind) => Table[kind];
	}
}
