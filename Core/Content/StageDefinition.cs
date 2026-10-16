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

		/// <summary>
		/// Multiplicador de Vida e Ataque dos inimigos: acerta a força da fase quando as ondas pesam mais ou
		/// menos que o nível sozinho diz (a calibragem do simulador; GDD, seção 10).
		/// </summary>
		public double Scale { get; init; } = 1;

		/// <summary>Mana de cada vitória. A derrota não custa nada.</summary>
		public int Mana { get; init; }

		public IReadOnlyList<IReadOnlyList<StageEnemy>> Waves { get; init; } = new List<IReadOnlyList<StageEnemy>>();

		/// <summary>Só na primeira vitória.</summary>
		public int FirstClearScrolls { get; init; }

		public int FirstClearEssence { get; init; }

		/// <summary>Marco: Pergaminhos Lendários da primeira vitória (as fases que fecham uma região).</summary>
		public int FirstClearLegendary { get; init; }

		/// <summary>Marco: Pergaminhos de Luz e Trevas da primeira vitória (a última fase).</summary>
		public int FirstClearLightDark { get; init; }

		/// <summary>
		/// Marco: kits de 6 sigilos 6★ Lendários da primeira vitória, de um conjunto à escolha do jogador
		/// (<see cref="Progression.RuneKits"/>).
		/// </summary>
		public int FirstClearKits { get; init; }

		/// <summary>
		/// Sigilos Lendários da primeira vitória, com as estrelas dos inimigos da fase: metade do que antes eram
		/// Pergaminhos (<see cref="FirstClearScrolls"/>) vem assim.
		/// </summary>
		public int FirstClearRunes { get; init; }

		/// <summary>Marco: Núcleos de Infusão da primeira vitória.</summary>
		public int FirstClearCores { get; init; }

		/// <summary>Toda vitória, inclusive a primeira.</summary>
		public int Essence { get; init; }

		/// <summary>Experiência de cada monstro da equipe em toda vitória (na escala da tabela de nível).</summary>
		public int Experience { get; init; }

		/// <summary>Estrelas da runa que a vitória solta (1 a 4; as maiores vêm das Masmorras).</summary>
		public int RuneGrade { get; init; } = 1;

		/// <summary>Até 3 falas antes da luta: "a história é tempero" (GDD, seção 4).</summary>
		public IReadOnlyList<string> Lines { get; init; } = new List<string>();

		/// <summary>
		/// A pressão que cresce com a Campanha, além de <see cref="Scale"/>: Ataque e Velocidade dos inimigos
		/// (1 = os de sempre). A dificuldade do fim do jogo vem daqui e do nível, não do XP.
		/// </summary>
		public double AttackScale { get; init; } = 1;

		public double SpeedScale { get; init; } = 1;

		public Encounter Encounter => new(Stars, Level, Waves, Scale, Attack: AttackScale, Speed: SpeedScale);
	}
}
