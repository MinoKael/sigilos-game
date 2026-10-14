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

		/// <summary>
		/// A condição: quando ela não vale, a Passiva não age (nos momentos que olham o outro lado: golpe, alvo,
		/// quem ataca). Nulo: vale sempre. Vale para <see cref="Actions"/> e para os bônus e efeitos da Passiva;
		/// a camada do parceiro vale sempre que ele está vivo.
		/// </summary>
		public PassiveCondition? Condition { get; init; }

		/// <summary>O que a Passiva faz sozinha, quando a condição vale (obrigar a mirar, revidar, não acordar).</summary>
		public IReadOnlyList<PassiveAction> Actions { get; init; } = new List<PassiveAction>();

		/// <summary>Passivas que dependem de um parceiro: o id da variante (ex.: "crow_light") que precisa estar viva no time.</summary>
		public string Partner { get; init; } = "";

		/// <summary>Com o <see cref="Partner"/> vivo: turnos a mais nos efeitos que o dono põe (<see cref="PartnerScope"/>).</summary>
		public int PartnerTurns { get; init; }

		/// <summary>
		/// Quais efeitos ganham os <see cref="PartnerTurns"/>: os negativos postos em inimigos (padrão), os positivos
		/// postos no dono ou nos aliados, ou os dois.
		/// </summary>
		public StatusScope? PartnerScope { get; init; }

		/// <summary>Com o <see cref="Partner"/> vivo, o que a Passiva faz a mais (revidar junto, não acordar quem dorme).</summary>
		public IReadOnlyList<PassiveAction> PartnerActions { get; init; } = new List<PassiveAction>();

		/// <summary>
		/// Com o <see cref="Partner"/> vivo: efeitos a mais que disparam junto com os da Passiva, no mesmo momento e
		/// com o mesmo sorteio (as genéricas que disparam efeitos). Não ganham os bônus de nível da habilidade.
		/// </summary>
		public IReadOnlyList<EffectDefinition> PartnerEffects { get; init; } = new List<EffectDefinition>();

		/// <summary>A Passiva dispara os efeitos da habilidade (as genéricas, menos o bônus de dano).</summary>
		public bool UsesEffects => Kind is PassiveKind.StatusOrEffectOnHit or PassiveKind.StatusOrEffectOnAttacker
			or PassiveKind.StatusOrEffectEachTurn or PassiveKind.StatusOrEffectOnWaveStart or PassiveKind.StatusOrEffectOnDeath
			or PassiveKind.StatusOrEffectWhenLowest or PassiveKind.ForEachStatusOrEffectAppliedDo
			or PassiveKind.ForEachStatusOrEffectReceivedDo or PassiveKind.StatusOrEffectOnTargetEachTurn
			or PassiveKind.Undying;
	}
}
