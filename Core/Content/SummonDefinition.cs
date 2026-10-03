using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma variante de invocação: uma entrada de "variations" no arquivo da família (Data/summons).
	/// "Uma variante nova é um item no arquivo, sem desenho novo" (GDD, seção 13).
	///
	/// Os atributos vêm prontos do arquivo, no 6★ nível 40: <see cref="Stats"/> e, depois do Despertar,
	/// <see cref="AwakenedStats"/>. Quem os calcula é o construtor de famílias, pelo
	/// <see cref="StatModel"/>; as estrelas e o nível de cada monstro só escalam esses números
	/// (Core/Progression/Growth.cs).
	///
	/// As habilidades seguem a ordem do arquivo: a primeira é a básica (sem recarga), as outras são
	/// ativas com recarga ou uma passiva. O Despertar pode acrescentar mais uma
	/// (<see cref="AwakeningDefinition.Skill"/>), que entra no fim da lista.
	/// </summary>
	public sealed record SummonDefinition
	{
		[JsonPropertyName("variation_id")]
		public string Id { get; init; } = "";

		public string Name { get; init; } = "";

		public Element Element { get; init; }

		/// <summary>O papel: como o orçamento de atributos da variante é repartido.</summary>
		public Role Role { get; init; }

		/// <summary>Atributos no 6★ nível 40, sem Despertar e sem runas. Completados pelo <see cref="GameDatabase"/>.</summary>
		[JsonInclude]
		public StatBlock Stats { get; internal set; } = new();

		/// <summary>Os mesmos, depois do Despertar (sem o bônus de atributo da variante).</summary>
		[JsonInclude]
		[JsonPropertyName("awakened_stats")]
		public StatBlock AwakenedStats { get; internal set; } = new();

		/// <summary>
		/// Estrelas naturais desta variante, quando são outras que as da família ("star_grade" na variante);
		/// nulo = as da família. Com estrelas próprias, o viés da família não vale para ela: o orçamento é o
		/// das estrelas dela e só o ajuste fino (<see cref="Bias"/>) e o elemento o repartem.
		/// </summary>
		[JsonPropertyName("star_grade")]
		public int? StarGrade { get; init; }

		/// <summary>A variante tem estrelas naturais próprias, diferentes das da família.</summary>
		[JsonIgnore]
		public bool OwnStars => StarGrade is { } stars && stars != Family.Rarity;

		/// <summary>O ajuste fino desta variante na distribuição dos atributos; sem ele, neutro. Só o construtor usa.</summary>
		public StatWeights? Bias { get; init; }

		/// <summary>Velocidade que o Despertar soma nesta variante; sem ela, a do modelo. Só o construtor usa.</summary>
		[JsonPropertyName("awakening_spd")]
		public double? AwakeningSpeed { get; init; }

		public IReadOnlyList<SkillDefinition> Skills { get; init; } = new List<SkillDefinition>();

		/// <summary>Só algumas variantes têm.</summary>
		public LeaderDefinition? Leader { get; init; }

		/// <summary>Nome próprio e bônus depois do Despertar.</summary>
		public AwakeningDefinition Awakening { get; init; } = new();

		/// <summary>Ligada pelo <see cref="GameDatabase"/> depois da leitura.</summary>
		[JsonIgnore]
		public FamilyDefinition Family { get; internal set; } = new();

		[JsonIgnore]
		public string FamilyId => Family.Id;

		/// <summary>Estrelas naturais: com quantas a invocação nasce (as da variante, se ela tem; senão, as da família). Todas podem evoluir até 6.</summary>
		[JsonIgnore]
		public int Rarity => StarGrade ?? Family.Rarity;

		/// <summary>
		/// Todas as habilidades que a variante pode ter, com a do Despertar no fim. Os níveis de
		/// habilidade de um monstro seguem esta ordem.
		/// </summary>
		[JsonIgnore]
		public IReadOnlyList<SkillDefinition> AllSkills =>
			Awakening.Skill is { } extra ? Skills.Append(extra).ToList() : Skills;

		/// <summary>As habilidades que o monstro tem agora: a do Despertar só depois de despertar.</summary>
		public IReadOnlyList<SkillDefinition> SkillsFor(bool awakened) => awakened ? AllSkills : Skills;

		/// <summary>Os atributos no 6★ nível 40 da forma pedida.</summary>
		public StatBlock StatsFor(bool awakened) => awakened ? AwakenedStats : Stats;

		public string NameFor(bool awakened) => awakened ? Awakening.Name : Name;

		/// <summary>O desenho da família. Desperto é o mesmo desenho, com a aura do elemento (a tela é quem desenha).</summary>
		public string Image => Family.Image;
	}
}
