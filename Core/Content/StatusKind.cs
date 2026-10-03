namespace Sigilos.Core.Content
{
	/// <summary>Efeitos que ficam na unidade por alguns turnos. Os números de cada um estão em
	/// Battle/BattleRules.</summary>
	public enum StatusKind
	{
		/// <summary>Absorve dano até o valor guardado.</summary>
		Shield,

		/// <summary>Aflição: perde uma fração da Vida no começo de cada turno, por cópia. Acumula até o limite de efeitos do monstro.</summary>
		Affliction,

		/// <summary>Perde o próximo turno.</summary>
		Stun,

		/// <summary>Só pode mirar em quem provocou.</summary>
		Taunt,

		/// <summary>Não pode ser alvo de ataques únicos.</summary>
		Hidden,

		/// <summary>Recebe mais dano.</summary>
		Curse,

		/// <summary>Cada golpe tem chance de errar.</summary>
		Blind,

        /// <summary>Anula o próximo golpe recebido.</summary>
        Aegis,

		/// <summary>O próximo golpe causado é crítico.</summary>
		Foresight,

		/// <summary>Nenhum efeito negativo pega (conjunto Tenacity).</summary>
		Immunity,

		AttackUp,
		AttackDown,
		DefenseUp,
		SpeedUp,
        /// <summary>Diminui a defesa em 70%.</summary>
        DefenseBreak,
        /// <summary>Depois de 1 turno, recebe dano ignorando defesa.</summary>
        Bomb,

		/// <summary>Velocidade−: diminui a Velocidade.</summary>
		SpeedDown,

		/// <summary>Crítico+: soma pontos à chance de Crítico.</summary>
		CritUp,

		/// <summary>Resistir Crítico: os golpes recebidos têm menos chance de ser críticos.</summary>
		CritResist,

		/// <summary>Bênção: recupera uma fração da Vida máxima no começo de cada turno.</summary>
		Blessing,

		/// <summary>Contragolpe: revida com a básica toda vez que é atingido.</summary>
		Counter,

		/// <summary>Reviver: ao cair, volta na hora com uma fração da Vida máxima (e o efeito se gasta).</summary>
		Revive,

		/// <summary>Karma: nenhum efeito positivo pega.</summary>
		Karma,

		/// <summary>Sono: perde os turnos até a duração acabar ou até ser atingido.</summary>
		Sleep,

		/// <summary>Ferida: não recebe cura.</summary>
		Unrecoverable,

		/// <summary>Silêncio: só usa habilidades sem recarga.</summary>
		Silence,

		/// <summary>Esquecimento: a Passiva para de funcionar.</summary>
		Oblivion,

	}
}
