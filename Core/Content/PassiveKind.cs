namespace Sigilos.Core.Content
{
	/// <summary>As Passivas que o MVP implementa. Cada uma nasce do conceito de uma família.</summary>
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
		AfflictionOnHit,

		/// <summary>Magos: chance de encurtar as próprias recargas no começo de cada turno.</summary>
		CooldownEachTurn,

		/// <summary>Paladinos: curam o aliado mais ferido no começo de cada turno.</summary>
		HealAllyEachTurn,

		/// <summary>Druidas: quem os atinge recebe de volta parte do dano que causou.</summary>
		Thorns,

		/// <summary>Gárgulas: chance de atordoar quem as atinge, uma vez por habilidade.</summary>
		StunAttacker,

		/// <summary>Vampiros: drenam parte de todo dano que causam.</summary>
		Lifesteal,

		/// <summary>Corvos: chance de Maldição em cada alvo atingido, uma vez por habilidade.</summary>
		CurseOnHit,

		/// <summary>Pássaros: chance de esquivar de cada golpe.</summary>
		Dodge,

		/// <summary>Pixies: chance de tirar um efeito negativo de um aliado no começo de cada turno.</summary>
		CleanseAllyEachTurn,
	}
}
