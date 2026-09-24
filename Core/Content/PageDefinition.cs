namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma página pronta do Grimório (Data/pages.json): Glifo, Forma e Círculo. O que ela faz não
	/// está nos dados: sai da fórmula em Battle/PageFormula, como pede o GDD (seção 14) — assim as 72
	/// combinações possíveis seguem as mesmas regras.
	/// </summary>
	public sealed record PageDefinition
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";
		public Glyph Glyph { get; init; }
		public Form Form { get; init; }

		/// <summary>1, 2 ou 3.</summary>
		public int Circle { get; init; }
	}
}
