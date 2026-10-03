using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// A estratégia de cada <see cref="PassiveKind"/>. A Passiva é uma regra que vale a luta inteira: o
	/// número dela (<see cref="PassiveDefinition.Value"/>, ou o do Despertar) chega em
	/// <see cref="UnitRule.Value"/>.
	///
	/// Passiva nova: o nome em <see cref="PassiveKind"/>, uma classe que herda de
	/// <see cref="UnitBehavior"/> e sobrescreve o momento dela, a linha aqui e o texto em Data/texts
	/// ("passive.Nome").
	/// </summary>
	internal static class PassiveBehaviors
	{
		private static readonly Dictionary<PassiveKind, UnitBehavior> Table = new()
		{
			[PassiveKind.SpeedWhenLowest] = new SpeedWhenLowestPassive(),
			[PassiveKind.ShieldOnDeath] = new ShieldOnDeathPassive(),
			[PassiveKind.RebirthOnce] = new RebirthOncePassive(),
			[PassiveKind.DamageReduction] = new DamageReductionPassive(),
			[PassiveKind.BonusVsDebuffed] = new BonusVsDebuffedPassive(),
			[PassiveKind.BonusVsWounded] = new BonusVsWoundedPassive(),
			[PassiveKind.ImpetoAtWaveStart] = new ImpetoAtWaveStartPassive(),
			[PassiveKind.RegenEachTurn] = new RegenEachTurnPassive(),
			[PassiveKind.AfflictionOnHit] = new StatusOnHitPassive(StatusKind.Affliction, BattleRules.AfflictionOnHitTurns),
			[PassiveKind.CooldownEachTurn] = new CooldownEachTurnPassive(),
			[PassiveKind.HealAllyEachTurn] = new HealAllyEachTurnPassive(),
			[PassiveKind.Thorns] = new ThornsPassive(),
			[PassiveKind.StunAttacker] = new StunAttackerPassive(),
			[PassiveKind.Lifesteal] = new LifestealPassive(),
			[PassiveKind.CurseOnHit] = new StatusOnHitPassive(StatusKind.Curse, BattleRules.CurseOnHitTurns),
			[PassiveKind.Dodge] = new DodgePassive(),
			[PassiveKind.CleanseAllyEachTurn] = new CleanseAllyEachTurnPassive(),
		};

		public static UnitBehavior Of(PassiveKind kind) => Table[kind];
	}
}
