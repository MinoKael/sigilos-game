using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Um andar de Masmorra (Data/dungeons.json): a luta e o que toda vitória solta. Andares mais
	/// fundos pedem times mais fortes e soltam runas maiores ou pedras melhores.
	/// </summary>
	public sealed record DungeonFloor
	{
		/// <summary>Nível de todos os inimigos do andar.</summary>
		public int Level { get; init; }

		public IReadOnlyList<IReadOnlyList<StageEnemy>> Waves { get; init; } = new List<IReadOnlyList<StageEnemy>>();

		/// <summary>Mana de cada vitória. A derrota não custa nada.</summary>
		public int Mana { get; init; }

		/// <summary>Toda vitória.</summary>
		public int Essence { get; init; }

		/// <summary>Só na primeira vitória do andar.</summary>
		public int FirstClearGold { get; init; }

		/// <summary>Experiência de cada monstro da equipe.</summary>
		public int Experience { get; init; }

		/// <summary>Masmorra de runas: estrelas da runa, sorteadas entre o mínimo e o máximo.</summary>
		public int MinGrade { get; init; }

		public int MaxGrade { get; init; }

		/// <summary>Masmorra de runas: a runa nunca sai abaixo desta raridade.</summary>
		public RuneRarity MinRarity { get; init; }

		/// <summary>Masmorra de pedras: grau das pedras (1 Mágica ... 4 Lendária) e quantas saem.</summary>
		public int ToolGrade { get; init; }

		public int ToolCount { get; init; } = 1;

		/// <summary>Multiplica Vida e Ataque de todos os inimigos do andar.</summary>
		public double Scale { get; init; } = 1;

		public Encounter Encounter => new(Level, Waves, Scale);
	}
}
