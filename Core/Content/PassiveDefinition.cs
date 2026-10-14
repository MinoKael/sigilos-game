using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// A regra de uma habilidade passiva. <see cref="Value"/> é o número dela (0,15 = 15%);
	/// <see cref="AwakenedValue"/>, quando existe, é o número depois do Despertar. Nas genéricas que
	/// disparam efeitos, o número é a chance de disparar (0 = sempre).
	/// </summary>
	public sealed record PassiveDefinition
	{
		public PassiveKind Kind { get; init; }
		public double Value { get; init; }
		public double AwakenedValue { get; init; }
		public double ValueFor(bool awakened) => awakened && AwakenedValue > 0 ? AwakenedValue : Value;

		/// <summary>
		/// Genéricas: os efeitos que a Passiva faz ao disparar. Vêm da habilidade passiva ("effects" e
		/// "awakenedEffects" em Data/summons, com os bônus de nível): <see cref="SkillDefinition.At"/> põe aqui.
		/// </summary>
		public IReadOnlyList<EffectDefinition> Effects { get; init; } = new List<EffectDefinition>();

		/// <summary>Genéricas que olham efeitos de status: só estes contam (vazio: todos do <see cref="Scope"/>).</summary>
		public IReadOnlyList<StatusKind> Statuses { get; init; } = new List<StatusKind>();

		/// <summary>Genéricas que olham efeitos de status: os positivos, os negativos ou todos.</summary>
		public StatusScope Scope { get; init; }

		/// <summary>Passivas que dependem de um parceiro: o id da variante (ex.: "crow_light") que precisa estar viva no time.</summary>
		public string Partner { get; init; } = "";

		/// <summary>Com o <see cref="Partner"/> vivo: turnos a mais nos efeitos negativos que o dono põe.</summary>
		public int PartnerTurns { get; init; }

		/// <summary>
		/// Com o <see cref="Partner"/> vivo: efeitos a mais que disparam junto com os da Passiva, no mesmo momento e
		/// com o mesmo sorteio (as genéricas que disparam efeitos). Não ganham os bônus de nível da habilidade.
		/// </summary>
		public IReadOnlyList<EffectDefinition> PartnerEffects { get; init; } = new List<EffectDefinition>();

		/// <summary>Com o <see cref="Partner"/> vivo, os golpes do dono não acordam quem dorme (Sono).</summary>
		public bool PartnerKeepsAsleep { get; init; }

		/// <summary>A Passiva dispara os efeitos da habilidade (as genéricas, menos o bônus de dano).</summary>
		public bool UsesEffects => Kind is PassiveKind.StatusOrEffectOnHit or PassiveKind.StatusOrEffectOnAttacker
			or PassiveKind.StatusOrEffectEachTurn or PassiveKind.StatusOrEffectOnWaveStart or PassiveKind.StatusOrEffectOnDeath
			or PassiveKind.StatusOrEffectWhenLowest or PassiveKind.ForEachStatusOrEffectAppliedDo
			or PassiveKind.ForEachStatusOrEffectReceivedDo or PassiveKind.StatusOrEffectOnTargetEachTurn
			or PassiveKind.Undying;
	}
}
