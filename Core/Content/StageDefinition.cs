using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>Uma fase da campanha (Data/stages.json): até 3 ondas de até 5 inimigos.</summary>
	public sealed record StageDefinition
	{
		public int Number { get; init; }
		public string Name { get; init; } = "";

		/// <summary>Nível de todos os inimigos da fase.</summary>
		public int Level { get; init; }

		public IReadOnlyList<IReadOnlyList<StageEnemy>> Waves { get; init; } = new List<IReadOnlyList<StageEnemy>>();

		/// <summary>Só na primeira vitória.</summary>
		public int FirstClearScrolls { get; init; }

		public int FirstClearEssence { get; init; }

		/// <summary>Toda vitória, inclusive a primeira.</summary>
		public int Essence { get; init; }

		/// <summary>Até 3 falas antes da luta: "a história é tempero" (GDD, seção 4).</summary>
		public IReadOnlyList<string> Lines { get; init; } = new List<string>();
	}
}
