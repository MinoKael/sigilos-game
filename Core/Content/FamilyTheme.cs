namespace Sigilos.Core.Content
{
	/// <summary>
	/// A temática da família, para o Grimório agrupar o elenco (Data/summons, campo <c>"theme"</c>). Vem dos
	/// dados e nunca do nome: família sem o campo fica sem temática, e o nome de cada grupo é texto
	/// (<c>theme.Nome</c> em Data/texts), então trocar o nome provisório não mexe no código.
	/// </summary>
	public enum FamilyTheme
	{
		/// <summary>Sem temática atribuída (o padrão).</summary>
		None,

		/// <summary>Constelações.</summary>
		Constellations,

		/// <summary>Adversários das Constelações (nome provisório).</summary>
		Adversaries,

		/// <summary>Místicos.</summary>
		Mystics,
	}
}
