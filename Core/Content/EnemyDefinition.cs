namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma criatura Profanada (Data/enemies.json). O elemento não mora aqui: cada fase escolhe o
	/// elemento de cada inimigo, como as famílias de invocação. Inimigos não usam Éter.
	/// </summary>
	public sealed record EnemyDefinition
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";
		public Role Role { get; init; }

		/// <summary>Estrelas, de 1 a 5: escala os atributos como nas invocações.</summary>
		public int Rarity { get; init; }

		/// <summary>Multiplica a Vida. Chefes usam mais de 1.</summary>
		public double HealthScale { get; init; } = 1;

		public string Image { get; init; } = "";
		public SkillDefinition Basic { get; init; } = new();

		/// <summary>Opcional: habilidade forte com recarga.</summary>
		public SkillDefinition? GlyphSkill { get; init; }
	}
}
