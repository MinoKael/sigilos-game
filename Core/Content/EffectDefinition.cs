namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma peça de habilidade: "o que faz" e "em quem". Cada campo só vale para alguns
	/// tipos; os outros ficam no padrão.
	///
	/// - Damage: <see cref="Power"/> é o multiplicador sobre o Ataque (0,8 = 80%), em <see cref="Hits"/>
	///   golpes. <see cref="IgnoreDefense"/> e <see cref="Drain"/> são frações.
	/// - Heal: <see cref="Power"/> é a fração da Vida máxima do alvo.
	/// - Shield: <see cref="Power"/> é a fração da Vida máxima de quem lança, por <see cref="Turns"/>.
	/// - Status: aplica <see cref="Status"/> com <see cref="Chance"/>, por <see cref="Turns"/>.
	/// - Impeto: soma <see cref="Power"/> pontos de Ímpeto (negativo atrasa).
	/// - Cleanse: remove um efeito negativo de cada alvo.
	/// - StealBuff: com <see cref="Chance"/>, rouba de cada alvo um efeito positivo (o mais novo; só
	///   <see cref="OnlyStatus"/>, se houver) e o põe em quem lança; sem nenhum e com <see cref="Fallback"/>,
	///   rouba <see cref="Power"/> pontos de Ímpeto (0 = todo o Ímpeto do alvo).
	/// - BonusPerStatus: conta os efeitos de <see cref="Scope"/> (ou só <see cref="OnlyStatus"/>) nas unidades
	///   de <see cref="From"/> e dá <see cref="Bonus"/>, <see cref="Power"/> por efeito: dano ou cura nos
	///   efeitos seguintes da habilidade, ou Ímpeto na hora para os alvos.
	/// - ChangeDuration: com <see cref="Chance"/>, soma <see cref="Turns"/> (negativo encurta) aos efeitos de
	///   <see cref="Scope"/> (ou só <see cref="OnlyStatus"/>) de cada alvo; o que chega a 0 sai.
	/// - EqualizeHealth: os alvos ficam todos com a média da fração de Vida deles.
	/// - HealTeam: cura <see cref="Power"/> da Vida máxima dos <see cref="Count"/> alvos mais feridos (0 = todos).
	/// - JointAttack: <see cref="Count"/> aliados do alvo do efeito (0 = todos), sorteados, atacam com a
	///   básica o alvo da habilidade.
	/// - ExtraTurnOnKill: se a habilidade derrubou alguém até aqui, turno extra para quem lança e
	///   <see cref="Turns"/> turnos a menos na recarga dela.
	///
	/// <see cref="By"/> diz o que LowestAlly e HighestAlly comparam (em qualquer tipo, e no <see cref="From"/>).
	///
	/// <see cref="OnKill"/> faz o efeito só acontecer se o dano anterior da mesma habilidade derrubou o alvo.
	/// </summary>
	public sealed record EffectDefinition
	{
		public EffectKind Kind { get; init; }
		public TargetKind Target { get; init; } = TargetKind.Target;
		public double Power { get; init; }
		public int Hits { get; init; } = 1;
		public double IgnoreDefense { get; init; }
		public double Drain { get; init; }
		public StatusKind Status { get; init; }
		public double Chance { get; init; } = 1;
		public int Turns { get; init; } = 1;
		public bool OnKill { get; init; }

		/// <summary>StealBuff: sem efeito positivo para roubar, rouba Ímpeto.</summary>
		public bool Fallback { get; init; }

		/// <summary>BonusPerStatus e ChangeDuration: que efeitos contam.</summary>
		public StatusScope Scope { get; init; }

		/// <summary>StealBuff, BonusPerStatus e ChangeDuration: só este efeito conta (nulo = todos do <see cref="Scope"/>).</summary>
		public StatusKind? OnlyStatus { get; init; }

		/// <summary>BonusPerStatus: em quem os efeitos são contados.</summary>
		public TargetKind From { get; init; } = TargetKind.Target;

		/// <summary>BonusPerStatus: o que cada efeito contado dá.</summary>
		public BonusKind Bonus { get; init; }

		/// <summary>HealTeam e JointAttack: quantos alvos (0 = todos).</summary>
		public int Count { get; init; }

		/// <summary>LowestAlly e HighestAlly: o valor comparado (o padrão é a fração de Vida).</summary>
		public TargetRank By { get; init; }
	}
}
