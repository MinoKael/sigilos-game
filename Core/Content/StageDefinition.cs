using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>Uma fase da campanha (Data/stages.json): até 3 ondas de até 5 inimigos.</summary>
	public sealed record StageDefinition
	{
		public int Number { get; init; }
		public string Name { get; init; } = "";

		/// <summary>Estrelas e nível de todos os inimigos da fase.</summary>
		public int Stars { get; init; } = 3;

		public int Level { get; init; }

		/// <summary>Mana de cada vitória. A derrota não custa nada.</summary>
		public int Mana { get; init; }

		public IReadOnlyList<IReadOnlyList<StageEnemy>> Waves { get; init; } = new List<IReadOnlyList<StageEnemy>>();

		/// <summary>Só na primeira vitória.</summary>
		public int FirstClearScrolls { get; init; }

		public int FirstClearEssence { get; init; }

		/// <summary>Toda vitória, inclusive a primeira.</summary>
		public int Essence { get; init; }

		/// <summary>Experiência de cada monstro da equipe em toda vitória (na escala da tabela de nível).</summary>
		public int Experience { get; init; }

		/// <summary>Estrelas da runa que a vitória solta (1 a 4; as maiores vêm das Masmorras).</summary>
		public int RuneGrade { get; init; } = 1;

		/// <summary>Até 3 falas antes da luta: "a história é tempero" (GDD, seção 4).</summary>
		public IReadOnlyList<string> Lines { get; init; } = new List<string>();

		public Encounter Encounter => new(Stars, Level, Waves);
	}
}
