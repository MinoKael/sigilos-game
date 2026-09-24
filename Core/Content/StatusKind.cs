namespace Sigilos.Core.Content
{
	/// <summary>Efeitos que ficam na unidade por alguns turnos. Os números de cada um estão em
	/// Battle/BattleRules.</summary>
	public enum StatusKind
	{
		/// <summary>Absorve dano até o valor guardado.</summary>
		Shield,

		/// <summary>Perde uma fração da Vida no começo de cada turno. Acumula.</summary>
		Burn,

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
		Ward,

		/// <summary>O próximo golpe causado é crítico.</summary>
		Foresight,

		/// <summary>Nenhum efeito negativo pega (conjunto Vontade).</summary>
		Immunity,

		AttackUp,
		AttackDown,
		DefenseUp,
		SpeedUp,
	}
}
