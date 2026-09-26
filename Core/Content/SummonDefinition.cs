using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma variante de invocação: um arquivo em Data/summons. "Uma variante nova é um arquivo, sem
	/// desenho novo" (GDD, seção 13).
	///
	/// As habilidades seguem a ordem do arquivo: a primeira é a básica (sem recarga), as outras são
	/// ativas com recarga ou uma passiva. O Despertar pode acrescentar mais uma
	/// (<see cref="AwakeningDefinition.Skill"/>), que entra no fim da lista.
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

		public IReadOnlyList<SkillDefinition> Skills { get; init; } = new List<SkillDefinition>();

		/// <summary>Só algumas variantes têm.</summary>
		public LeaderDefinition? Leader { get; init; }

		/// <summary>Nome próprio e bônus depois do Despertar.</summary>
		public AwakeningDefinition Awakening { get; init; } = new();

		/// <summary>Ligada pelo <see cref="GameDatabase"/> depois da leitura.</summary>
		[JsonIgnore]
		public FamilyDefinition Family { get; internal set; } = new();

		/// <summary>Estrelas naturais: com quantas a invocação nasce. Todas podem evoluir até 6.</summary>
		public int Rarity => Family.Rarity;

		/// <summary>
		/// Todas as habilidades que a variante pode ter, com a do Despertar no fim. Os níveis de
		/// habilidade de um monstro seguem esta ordem.
		/// </summary>
		public IReadOnlyList<SkillDefinition> AllSkills =>
			Awakening.Skill is { } extra ? Skills.Append(extra).ToList() : Skills;

		/// <summary>As habilidades que o monstro tem agora: a do Despertar só depois de despertar.</summary>
		public IReadOnlyList<SkillDefinition> SkillsFor(bool awakened) => awakened ? AllSkills : Skills;

		public string NameFor(bool awakened) => awakened ? Awakening.Name : Name;

		public string ImageFor(bool awakened) => awakened ? Family.AwakenedImage : Family.Image;
	}
}
