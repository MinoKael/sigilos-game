using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma criatura única (Data/enemies.json): os chefes, que não existem como invocação. Os outros
	/// inimigos das fases e Masmorras são invocações (<see cref="StageEnemy.Summon"/>). O elemento não
	/// mora aqui: cada fase escolhe. Inimigos não usam Éter.
	/// </summary>
	public sealed record EnemyDefinition
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";
		public Role Role { get; init; }

		/// <summary>Estrelas naturais: escalam os atributos como nas invocações.</summary>
		public int Rarity { get; init; }

		/// <summary>Multiplica a Vida. Chefes usam mais.</summary>
		public double HealthScale { get; init; } = 1;

		/// <summary>Multiplica o Ataque. Compensa a raridade baixa: inimigo não tem runa nem aprimoramento.</summary>
		public double AttackScale { get; init; } = 1;

		public string Image { get; init; } = "";

		/// <summary>Como nas invocações: a primeira sem recarga, as outras com recarga ou passivas.</summary>
		public IReadOnlyList<SkillDefinition> Skills { get; init; } = new List<SkillDefinition>();
	}
}
