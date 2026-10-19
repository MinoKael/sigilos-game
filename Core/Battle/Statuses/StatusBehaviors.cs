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
			[StatusKind.Stigma] = new StigmaStatus(),
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
			[StatusKind.Curse] = new CurseStatus(),
			[StatusKind.Sleep] = new SleepStatus(),
			[StatusKind.Unrecoverable] = new UnrecoverableStatus(),
			[StatusKind.Silence] = new SilenceStatus(),
			[StatusKind.Oblivion] = new OblivionStatus(),
			[StatusKind.Phylactery] = new PhylacteryStatus(),
		};

		/// <summary>
		/// O equivalente negativo de um efeito positivo (Inverter efeitos). O que não está aqui vira
		/// <see cref="Fallback"/>.
		/// </summary>
		private static readonly Dictionary<StatusKind, StatusKind> Opposites = new()
		{
			[StatusKind.AttackUp] = StatusKind.AttackDown,
			[StatusKind.DefenseUp] = StatusKind.DefenseBreak,
			[StatusKind.SpeedUp] = StatusKind.SpeedDown,
			[StatusKind.Blessing] = StatusKind.Unrecoverable,
			[StatusKind.Foresight] = StatusKind.Blind,
			[StatusKind.Hidden] = StatusKind.Sleep,
		};

		/// <summary>O efeito negativo em que um positivo se inverte quando não tem equivalente.</summary>
		public const StatusKind Fallback = StatusKind.Stigma;

		/// <summary>O equivalente negativo de um positivo, se ele tem um (sem equivalente, <see cref="OppositeOf"/> dá o <see cref="Fallback"/>).</summary>
		public static StatusKind? EquivalentOf(StatusKind kind) => Opposites.TryGetValue(kind, out var opposite) ? opposite : null;

		public static StatusKind OppositeOf(StatusKind kind) => Opposites.TryGetValue(kind, out var opposite) ? opposite : Fallback;

		public static StatusBehavior Of(StatusKind kind) => Table[kind];
	}
}
