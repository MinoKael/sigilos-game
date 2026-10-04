namespace Sigilos.Core.Progression
{
	/// <summary>O que uma carta do correio pode trazer: as moedas do jogo. No servidor, o nome em camelCase (<c>reappraisalGems</c>).</summary>
	public enum MailItem
	{
		Gold,
		Mana,
		Scrolls,
		Essence,
		Fragments,
		ReappraisalGems,
		LightDarkScrolls,
		LegendaryScrolls,

		/// <summary>Núcleos de Infusão: chegam à coleção (ou ao Baú) como monstros.</summary>
		InfusionCores,
	}
}
