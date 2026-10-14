namespace Sigilos.Core.Content
{
	/// <summary>Como a condição de uma Passiva compara: maior, menor ou igual.</summary>
	public enum Comparison
	{
		Greater,
		Less,
		Equal,
	}

	/// <summary>De quem a condição olha o atributo ou os efeitos: do dono da Passiva ou do outro lado do momento.</summary>
	public enum ConditionSide
	{
		Self,
		Opponent,
	}

	/// <summary>Com o que a condição compara: o mesmo atributo do outro lado, ou um número.</summary>
	public enum ConditionAgainst
	{
		Opponent,
		Value,
	}

	/// <summary>
	/// O que uma Passiva faz por si só quando a condição vale (<see cref="PassiveDefinition.Actions"/>) ou, com o
	/// parceiro vivo, a mais (<see cref="PassiveDefinition.PartnerActions"/>).
	/// </summary>
	public enum PassiveAction
	{
		/// <summary>Os inimigos para quem a condição vale só podem escolher o dono como alvo único.</summary>
		Force,

		/// <summary>Quando uma habilidade inimiga atinge o dono, ele revida com a básica. No parceiro: o parceiro revida junto.</summary>
		Counter,

		/// <summary>Os golpes do dono não acordam quem está com Sono.</summary>
		KeepAsleep,
	}

	/// <summary>
	/// A condição de uma Passiva, no formato "atributo ou efeito X, maior/menor/igual, a Y". O "outro lado" é o
	/// alvo de quem a Passiva olha no momento: quem escolhe o alvo (Force), quem atingiu o dono (Counter), quem
	/// dorme (KeepAsleep), o alvo do golpe ou do efeito. Em momentos sem outro lado (começo de turno), o que
	/// depende dele não vale.
	///
	/// O assunto é um atributo (<see cref="Stat"/>), a quantidade de um efeito (<see cref="Status"/>) ou de
	/// efeitos positivos/negativos (<see cref="Scope"/>). Atributo contra o outro lado compara o valor de agora dos
	/// dois; contra um número, Vida é a fração da máxima (0,5 = metade) e os demais são o valor.
	/// </summary>
	public sealed record PassiveCondition
	{
		/// <summary>O atributo comparado (Defense, Health...).</summary>
		public Stat? Stat { get; init; }

		/// <summary>A quantidade deste efeito de status (0 = sem ele).</summary>
		public StatusKind? Status { get; init; }

		/// <summary>A quantidade de efeitos positivos, negativos ou todos.</summary>
		public StatusScope? Scope { get; init; }

		public Comparison Compare { get; init; }

		/// <summary>De quem é o atributo ou os efeitos: do dono (padrão) ou do outro lado.</summary>
		public ConditionSide Of { get; init; }

		/// <summary>Só para atributo: compara com o outro lado (padrão) ou com <see cref="Value"/>.</summary>
		public ConditionAgainst Against { get; init; }

		public double Value { get; init; }
	}
}
