using System.Text.Json.Serialization;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma variante de invocação: um arquivo em Data/summons. "Uma variante nova é um arquivo, sem
	/// desenho novo" (GDD, seção 13).
	/// </summary>
	public sealed record SummonDefinition
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";

		/// <summary>Id da família em Data/families.json.</summary>
		[JsonPropertyName("family")]
		public string FamilyId { get; init; } = "";

		public Element Element { get; init; }
		public Role Role { get; init; }
		public SkillDefinition Basic { get; init; } = new();
		public SkillDefinition Special { get; init; } = new();

		/// <summary>Só famílias de 4 e 5 estrelas têm.</summary>
		public LeaderDefinition? Leader { get; init; }

		/// <summary>Nome próprio e bônus depois do Despertar.</summary>
		public AwakeningDefinition Awakening { get; init; } = new();

		/// <summary>Ligada pelo <see cref="GameDatabase"/> depois da leitura.</summary>
		[JsonIgnore]
		public FamilyDefinition Family { get; internal set; } = new();

		public int Rarity => Family.Rarity;

		public string NameFor(bool awakened) => awakened ? Awakening.Name : Name;

		public string ImageFor(bool awakened) => awakened ? Family.AwakenedImage : Family.Image;
	}
}
