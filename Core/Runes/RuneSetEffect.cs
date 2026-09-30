namespace Sigilos.Core.Runes
{
	/// <summary>O que um conjunto faz em combate quando não é só atributo. A estratégia de cada um está em Core/Battle/Sets.</summary>
	public enum RuneSetEffect
	{
		None,

		/// <summary>Sifão: cura uma fração do dano causado.</summary>
		Drain,

		/// <summary>Tormento: chance de atordoar cada alvo atingido. Só a Imunidade barra.</summary>
		Stun,

		/// <summary>Frenesi: chance de turno extra depois de agir; o turno extra não dá outro.</summary>
		ExtraTurn,

		/// <summary>Baluarte: no começo de cada onda, todos os aliados ganham escudo de uma fração da Vida de base do dono.</summary>
		AllyShield,

		/// <summary>Tenacidade: Imunidade no começo de cada onda.</summary>
		Immunity,

		/// <summary>Contragolpe: chance de contra-atacar com o básico quando é atingido.</summary>
		Counter,

		/// <summary>Perdição: Ímpeto a cada 7% da Vida máxima perdida num golpe.</summary>
		Bane,

		/// <summary>Oblívio: o dano causado reduz a Vida máxima do alvo.</summary>
		Oblivion,
	}
}
