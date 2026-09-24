namespace Sigilos.Core.Content
{
	/// <summary>As Assinaturas que o MVP implementa. Cada uma nasce do conceito de uma família.</summary>
	public enum PassiveKind
	{
		/// <summary>Diabretes: mais Velocidade enquanto for o aliado com menos Vida.</summary>
		SpeedWhenLowest,

		/// <summary>Cavaleiros: ao cair, dão escudo a todos os aliados.</summary>
		ShieldOnDeath,

		/// <summary>Fênix: na primeira vez que cai, renasce no turno seguinte dela.</summary>
		RebirthOnce,
	}
}
