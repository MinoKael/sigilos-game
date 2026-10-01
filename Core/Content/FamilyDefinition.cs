using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma família de invocações: um arquivo em Data/summons (<c>dragon_family.json</c>), feito em
	/// docs/summon_family_builder.html. A família decide as estrelas naturais e o desenho — um por
	/// família, recolorido em cada elemento, e um segundo para o Despertar (GDD, seção 5) — e traz as
	/// variantes dela, até uma por elemento (<see cref="Variations"/>), cada uma com papel, atributos,
	/// habilidades e Passiva.
	///
	/// Os nomes dos campos no arquivo são os do contrato do modelo de atributos
	/// (docs/summon_family_stat_formula_spec.md): "family_id", "star_grade", "variations"...
	/// </summary>
	public sealed record FamilyDefinition
	{
		[JsonPropertyName("family_id")]
		public string Id { get; init; } = "";

		/// <summary>O nome no singular, de que saem os nomes das variantes ("Dragão" em "Dragão de Fogo"); é o que o Grimório mostra.</summary>
		[JsonPropertyName("base_name")]
		public string BaseName { get; init; } = "";

		/// <summary>O plural ("Dragões"), para a lista do construtor de famílias.</summary>
		public string Name { get; init; } = "";

		/// <summary>Estrelas naturais: escolhem o orçamento de atributos (<see cref="StatModel"/>).</summary>
		[JsonPropertyName("star_grade")]
		public int Rarity { get; init; }

		/// <summary>Nome do arquivo em Assets/Creatures, sem extensão.</summary>
		public string Image { get; init; } = "";

		/// <summary>O segundo desenho, depois do Despertar (GDD, seção 5).</summary>
		[JsonPropertyName("awakened_image")]
		public string AwakenedImage { get; init; } = "";

		/// <summary>A personalidade da família na distribuição dos atributos; sem ela, neutra.</summary>
		[JsonPropertyName("family_bias")]
		public StatWeights? Bias { get; init; }

		/// <summary>As variantes, pela chave do elemento em minúsculas ("fire", "water"...).</summary>
		public IReadOnlyDictionary<string, SummonDefinition> Variations { get; init; } = new Dictionary<string, SummonDefinition>();
	}
}
