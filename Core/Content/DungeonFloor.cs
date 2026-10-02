using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Um andar de Masmorra (Data/dungeons.json): a luta e o que toda vitória solta. Andares mais
	/// fundos pedem times mais fortes e soltam runas maiores ou pedras melhores.
	/// </summary>
	public sealed record DungeonFloor
	{
		/// <summary>Estrelas e nível de todos os inimigos do andar.</summary>
		public int Stars { get; init; } = 6;

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

		/// <summary>Masmorra de runas: a chance (em %, somando 100) de cada estrela da runa.</summary>
		public IReadOnlyDictionary<int, double> Grades { get; init; } = new Dictionary<int, double>();

		/// <summary>
		/// A chance (em %, somando 100) de cada raridade: da runa, na Masmorra de runas; do grau de cada
		/// pedra, na de pedras.
		/// </summary>
		public IReadOnlyDictionary<RuneRarity, double> Rarities { get; init; } = new Dictionary<RuneRarity, double>();

		/// <summary>Masmorra de pedras: quantas saem.</summary>
		public int ToolCount { get; init; } = 1;

		/// <summary>Multiplica Vida e Ataque de todos os inimigos do andar.</summary>
		public double Scale { get; init; } = 1;

		public Encounter Encounter => new(Stars, Level, Waves, Scale);
	}
}
