namespace Sigilos.Core.Content
{
	/// <summary>
	/// As Passivas. As genéricas (do <see cref="BonusVsStatusOrEffect"/> em diante) recebem os efeitos da
	/// habilidade passiva (<see cref="PassiveDefinition.Effects"/>) e, as que olham efeitos de status, o filtro
	/// (<see cref="PassiveDefinition.Statuses"/>, <see cref="PassiveDefinition.Scope"/>); as outras são regras
	/// próprias de uma família.
	/// </summary>
	public enum PassiveKind
	{
		/// <summary>Diabretes: mais Velocidade enquanto for o aliado com menos Vida.</summary>
		SpeedWhenLowest,

		/// <summary>Fênix: na primeira vez que cai, renasce no turno seguinte dela.</summary>
		RebirthOnce,
		/// <summary>Limos: recebe menos dano de todo golpe.</summary>
		DamageReduction,
		/// <summary>Lobos: mais dano em quem está abaixo da metade da Vida máxima.</summary>
		BonusVsWounded,
		/// <summary>Bandidos: começa cada onda com Ímpeto.</summary>
		ImpetoAtWaveStart,

		/// <summary>Magos: chance de encurtar as próprias recargas no começo de cada turno.</summary>
		CooldownEachTurn,

		/// <summary>Druidas: quem os atinge recebe de volta parte do dano que causou.</summary>
		Thorns,

		/// <summary>Vampiros: drenam parte de todo dano que causam.</summary>
		Lifesteal,

		/// <summary>Pássaros: chance de esquivar de cada golpe.</summary>
		Dodge,

		/// <summary>Pixies: chance de tirar um efeito negativo de um aliado no começo de cada turno.</summary>
		CleanseAllyEachTurn,

		// Genéricas ---------------------------------------------------------------------------------

		/// <summary>Mais dano (o número) em quem tem algum efeito que conta. Não usa efeitos.</summary>
		BonusVsStatusOrEffect,

		/// <summary>Os efeitos em cada alvo atingido que ficou de pé, uma vez por habilidade.</summary>
		StatusOrEffectOnHit,

		/// <summary>Os efeitos em quem atinge o dono, uma vez por habilidade.</summary>
		StatusOrEffectOnAttacker,

		/// <summary>Os efeitos no começo de cada turno do dono.</summary>
		StatusOrEffectEachTurn,

		/// <summary>Os efeitos no começo de cada onda.</summary>
		StatusOrEffectOnWaveStart,

		/// <summary>Os efeitos quando o dono cai.</summary>
		StatusOrEffectOnDeath,

		/// <summary>Os efeitos no começo do turno do dono, se ele é o aliado com menos Vida.</summary>
		StatusOrEffectWhenLowest,

		/// <summary>Os efeitos para cada efeito que conta que o dono põe.</summary>
		ForEachStatusOrEffectAppliedDo,

		/// <summary>Os efeitos para cada efeito que conta que o dono recebe.</summary>
		ForEachStatusOrEffectReceivedDo,

		/// <summary>Os efeitos no alvo da habilidade, depois de cada turno do dono.</summary>
		StatusOrEffectOnTargetEachTurn,

		// Chefes ------------------------------------------------------------------------------------

		/// <summary>
		/// Rei Ossudo: não recebe Ímpeto (nem ganha, nem perde), os efeitos depois de cada ação dele (o
		/// escudo) e, ao cair, volta no turno seguinte com o número da Passiva em Vida, toda vez. O
		/// Esquecimento cala tudo: quem cai esquecido não volta.
		/// </summary>
		Undying,
	}
}
