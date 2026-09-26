namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma família de invocações (Data/families.json). A família decide as estrelas naturais e o
	/// desenho; cada variante (Data/summons/*.json) decide elemento, papel, habilidades e Passiva.
	/// Um desenho por família, recolorido nos 5 elementos, e um segundo para o Despertar (GDD, seção 5).
	/// </summary>
	public sealed record FamilyDefinition
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";

		/// <summary>Estrelas naturais, de 1 a 5.</summary>
		public int Rarity { get; init; }

		/// <summary>Nome do arquivo em Assets/Creatures, sem extensão.</summary>
		public string Image { get; init; } = "";

		/// <summary>O segundo desenho, depois do Despertar (GDD, seção 5).</summary>
		public string AwakenedImage { get; init; } = "";
	}
}
