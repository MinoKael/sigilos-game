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
		/// <summary>Limos: recebe menos dano de todo golpe.</summary>
		DamageReduction,
		/// <summary>Goblins: mais dano em quem está com efeito negativo.</summary>
		BonusVsDebuffed,
		/// <summary>Lobos: mais dano em quem está abaixo da metade da Vida máxima.</summary>
		BonusVsWounded,
		/// <summary>Bandidos: começa cada onda com Ímpeto.</summary>
		ImpetoAtWaveStart,
		/// <summary>Trolls: recupera Vida no começo de cada turno dele.</summary>
		RegenEachTurn,
		/// <summary>Dragões: chance de Queimadura em cada alvo atingido, uma vez por habilidade.</summary>
		BurnOnHit,
	}
}
