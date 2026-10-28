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
		/// <summary>Liches: todo golpe drena o número (0,15 = 15%) do dano como Vida, e a habilidade rouba o mesmo tanto do Ímpeto de cada alvo.</summary>
		SoulDrain,

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

		/// <summary>
		/// Mais dano (o número) para cada efeito que conta no alvo. Com um <see cref="PassiveDefinition.Partner"/>
		/// vivo no time, os efeitos negativos que o dono põe duram <see cref="PassiveDefinition.PartnerTurns"/> a mais.
		/// </summary>
		BonusPerStatusOrEffect,

		// Masmorras ---------------------------------------------------------------------------------

		/// <summary>Golem Rúnico: a Defesa do dono sobe um passo (o número, em fração da base) a cada rodada.</summary>
		RunicCore,

		/// <summary>Santuário: o dono perde o número (0,1 = 10%) da Vida máxima a cada rodada e fica lento.</summary>
		Drowning,

		// Chefes ------------------------------------------------------------------------------------

		/// <summary>
		/// Rei Ossudo: enquanto ele está em campo, ninguém (aliado ou inimigo) ganha nem perde Ímpeto; os
		/// efeitos depois de cada ação dele (o escudo) e, ao cair, volta no turno seguinte com o número da
		/// Passiva em Vida, toda vez. O Esquecimento cala tudo: quem cai esquecido não volta.
		/// </summary>
		Undying,

		/// <summary>Os efeitos de <see cref="PassiveDefinition.Statuses"/> não pegam no dono (a Cabeça Erguida do Cavaleiro: Atordoamento e Sono).</summary>
		StatusImmunity,

		/// <summary>
		/// Condicional: não dispara efeitos; só faz o que <see cref="PassiveDefinition.Actions"/> diz (obrigar a mirar,
		/// revidar, não acordar quem dorme) quando a <see cref="PassiveDefinition.Condition"/> vale, e o que
		/// <see cref="PassiveDefinition.PartnerActions"/> diz com o parceiro vivo.
		/// </summary>
		Conditional,
	}
}
