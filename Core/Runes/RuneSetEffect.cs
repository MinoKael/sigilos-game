namespace Sigilos.Core.Runes
{
	/// <summary>O que um conjunto faz em combate quando não é só atributo.</summary>
	public enum RuneSetEffect
	{
		None,

		/// <summary>Vampiro: cura uma fração do dano causado.</summary>
		Drain,

		/// <summary>Desespero: chance de atordoar cada alvo atingido. Só a Imunidade barra.</summary>
		Stun,

		/// <summary>Violento: chance de turno extra, que cai a cada turno extra seguido.</summary>
		ExtraTurn,

		/// <summary>Escudo: no começo de cada onda, todos os aliados ganham escudo de uma fração da Vida de base do dono.</summary>
		AllyShield,

		/// <summary>Vontade: Imunidade no começo de cada onda.</summary>
		Immunity,

		/// <summary>Vingança: chance de contra-atacar com o básico quando é atingido.</summary>
		Counter,

		/// <summary>Nêmesis: Ímpeto a cada 7% da Vida máxima perdida num golpe.</summary>
		Nemesis,

		/// <summary>Destruição: o dano causado reduz a Vida máxima do alvo.</summary>
		Destroy,
	}
}
