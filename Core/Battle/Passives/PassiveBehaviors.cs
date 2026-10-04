using System;
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
	/// ("passive.Nome"). As genéricas (<see cref="EffectPassive"/>) nascem uma por unidade, com a definição
	/// (os efeitos e o filtro); as outras são uma só para todas.
	/// </summary>
	internal static class PassiveBehaviors
	{
		private static readonly Dictionary<PassiveKind, UnitBehavior> Table = new()
		{
			[PassiveKind.SpeedWhenLowest] = new SpeedWhenLowestPassive(),
			[PassiveKind.RebirthOnce] = new RebirthOncePassive(),
			[PassiveKind.DamageReduction] = new DamageReductionPassive(),
			[PassiveKind.BonusVsWounded] = new BonusVsWoundedPassive(),
			[PassiveKind.ImpetoAtWaveStart] = new ImpetoAtWaveStartPassive(),
			[PassiveKind.CooldownEachTurn] = new CooldownEachTurnPassive(),
			[PassiveKind.Thorns] = new ThornsPassive(),
			[PassiveKind.Lifesteal] = new LifestealPassive(),
			[PassiveKind.Dodge] = new DodgePassive(),
			[PassiveKind.CleanseAllyEachTurn] = new CleanseAllyEachTurnPassive(),
		};

		private static readonly Dictionary<PassiveKind, Func<PassiveDefinition, UnitBehavior>> Generic = new()
		{
			[PassiveKind.BonusVsStatusOrEffect] = p => new BonusVsStatusOrEffectPassive(p),
			[PassiveKind.StatusOrEffectOnHit] = p => new StatusOrEffectOnHitPassive(p),
			[PassiveKind.StatusOrEffectOnAttacker] = p => new StatusOrEffectOnAttackerPassive(p),
			[PassiveKind.StatusOrEffectEachTurn] = p => new StatusOrEffectEachTurnPassive(p),
			[PassiveKind.StatusOrEffectOnWaveStart] = p => new StatusOrEffectOnWaveStartPassive(p),
			[PassiveKind.StatusOrEffectOnDeath] = p => new StatusOrEffectOnDeathPassive(p),
			[PassiveKind.StatusOrEffectWhenLowest] = p => new StatusOrEffectWhenLowestPassive(p),
			[PassiveKind.ForEachStatusOrEffectAppliedDo] = p => new ForEachStatusOrEffectAppliedDoPassive(p),
			[PassiveKind.ForEachStatusOrEffectReceivedDo] = p => new ForEachStatusOrEffectReceivedDoPassive(p),
			[PassiveKind.StatusOrEffectOnTargetEachTurn] = p => new StatusOrEffectOnTargetEachTurnPassive(p),
		};

		public static UnitBehavior Of(PassiveDefinition passive) =>
			Generic.TryGetValue(passive.Kind, out var create) ? create(passive) : Table[passive.Kind];
	}
}
