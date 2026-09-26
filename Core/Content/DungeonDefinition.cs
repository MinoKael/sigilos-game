using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma Masmorra (Data/dungeons.json, GDD seção 11): andares cada vez mais difíceis que soltam o que
	/// a Campanha não solta — runas de 5 e 6 estrelas de conjuntos certos, ou Pedras de Afiar e Gemas.
	/// Cada vitória custa a Mana do andar, e cada Masmorra tem a sua equipe.
	/// </summary>
	public sealed record DungeonDefinition
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";
		public DungeonKind Kind { get; init; }

		/// <summary>Desenho do chefe, em Assets/Creatures.</summary>
		public string Image { get; init; } = "";

		/// <summary>Abre depois de vencer esta fase da Campanha.</summary>
		public int UnlockStage { get; init; }

		/// <summary>Masmorra de runas: os conjuntos que ela solta.</summary>
		public IReadOnlyList<RuneSet> Sets { get; init; } = new List<RuneSet>();

		public IReadOnlyList<DungeonFloor> Floors { get; init; } = new List<DungeonFloor>();

		/// <summary>Andar de 1 em diante.</summary>
		public DungeonFloor Floor(int number) => Floors[number - 1];
	}
}
