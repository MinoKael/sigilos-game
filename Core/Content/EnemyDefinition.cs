using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma criatura única (Data/enemies.json): os chefes, que não existem como invocação. Os outros
	/// inimigos das fases e Masmorras são invocações (<see cref="StageEnemy.Summon"/>). O elemento não
	/// mora aqui: cada fase escolhe.
	/// </summary>
	public sealed record EnemyDefinition
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";

		/// <summary>Com <see cref="Rarity"/>, de onde o modelo tira os <see cref="Stats"/>.</summary>
		public Role Role { get; init; }

		/// <summary>Estrelas naturais: o orçamento de atributos, como nas invocações.</summary>
		public int Rarity { get; init; }

		/// <summary>
		/// Atributos no 6★ nível 40, prontos no arquivo: os do <see cref="StatModel"/> para estas estrelas
		/// e este papel, sem viés. A luta escala pelas estrelas e pelo nível do encontro.
		/// </summary>
		[JsonInclude]
		public StatBlock Stats { get; internal set; } = new();

		/// <summary>Multiplica a Vida. Chefes usam mais.</summary>
		public double HealthScale { get; init; } = 1;

		/// <summary>Multiplica o Ataque. Compensa a raridade baixa: inimigo não tem runa nem aprimoramento.</summary>
		public double AttackScale { get; init; } = 1;

		public string Image { get; init; } = "";

		/// <summary>Como nas invocações: a primeira sem recarga, as outras com recarga ou passivas.</summary>
		public IReadOnlyList<SkillDefinition> Skills { get; init; } = new List<SkillDefinition>();
	}
}
