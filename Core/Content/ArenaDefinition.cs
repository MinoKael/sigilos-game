using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// A Arena Astral (Data/arena.json): duelos por pontos contra defesas de outros alquimistas, com Graus,
	/// sequência de vitórias que dá bônus, Rivais com espera e a Loja Cósmica (que cobra Astrarium).
	/// Os adversários sorteados da lista são defesas montadas pelo Grau (<see cref="ArenaGrade.Ghost"/>); quando
	/// houver servidor, o mesmo formato guarda a defesa de um jogador de verdade.
	/// </summary>
	public sealed record ArenaDefinition
	{
		/// <summary>Convites que a conta guarda no máximo.</summary>
		public int MaxInvites { get; init; } = 10;

		/// <summary>Minutos para recarregar um Convite.</summary>
		public int InviteMinutes { get; init; } = 30;

		/// <summary>Convites que vencer a lista inteira devolve.</summary>
		public int ListBonusInvites { get; init; } = 2;

		/// <summary>
		/// Os pontos de cada adversário da lista, em relação ao jogador: o tamanho da lista é o número de
		/// valores. Os primeiros ficam abaixo, os do meio perto e os últimos acima.
		/// </summary>
		public IReadOnlyList<int> ListOffsets { get; init; } = new List<int> { -150, -100, -50, -25, 0, 25, 50, 75, 125, 175 };

		/// <summary>Pontos de uma luta contra alguém de pontos iguais, e quanto cada <see cref="PointsSlope"/> de diferença muda.</summary>
		public int PointsBase { get; init; } = 20;

		public int PointsSlope { get; init; } = 10;
		public int PointsMin { get; init; } = 8;
		public int PointsMax { get; init; } = 40;

		/// <summary>Minutos que a sequência vale depois da última vitória.</summary>
		public int StreakMinutes { get; init; } = 60;

		/// <summary>Os bônus da sequência, empilhados: cada degrau vale de <see cref="StreakStep.Wins"/> vitórias em diante.</summary>
		public IReadOnlyList<StreakStep> Streak { get; init; } = new List<StreakStep>();

		/// <summary>Do menor Grau ao maior.</summary>
		public IReadOnlyList<ArenaGrade> Grades { get; init; } = new List<ArenaGrade>();

		public IReadOnlyList<ArenaRival> Rivals { get; init; } = new List<ArenaRival>();

		/// <summary>As ofertas da Loja Cósmica, em Astrarium.</summary>
		public IReadOnlyList<ArenaOffer> Shop { get; init; } = new List<ArenaOffer>();

		/// <summary>As ofertas da Loja do Véu (Arena do Véu), em Ecos do Véu.</summary>
		public IReadOnlyList<ArenaOffer> VeilShop { get; init; } = new List<ArenaOffer>();

		/// <summary>Nomes dos alquimistas de defesa sorteados na lista.</summary>
		public IReadOnlyList<string> Names { get; init; } = new List<string>();

		/// <summary>O Grau de quem tem <paramref name="points"/> pontos.</summary>
		public ArenaGrade GradeOf(int points)
		{
			var grade = Grades[0];
			foreach (var candidate in Grades)
			{
				if (points >= candidate.Points)
					grade = candidate;
			}

			return grade;
		}

		public int IndexOf(ArenaGrade grade)
		{
			for (var i = 0; i < Grades.Count; i++)
			{
				if (Grades[i].Id == grade.Id)
					return i;
			}

			return 0;
		}
	}

	/// <summary>Um degrau da sequência: com <see cref="Wins"/> vitórias seguidas, soma <see cref="Health"/>% de Vida e <see cref="Attack"/>% de Ataque.</summary>
	public sealed record StreakStep(int Wins, int Health, int Attack);

	/// <summary>Um Grau da Arena: o piso de pontos, a Glória por vitória (em Astrarium) e o prêmio semanal.</summary>
	public sealed record ArenaGrade
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";

		/// <summary>Pontos mínimos do Grau.</summary>
		public int Points { get; init; }

		/// <summary>Astrarium por vitória contra a lista ou um Rival.</summary>
		public int Astrarium { get; init; }

		/// <summary>Ecos do Véu por vitória na Arena do Véu.</summary>
		public int Ecos { get; init; }

		/// <summary>Ouro do prêmio do fim da semana.</summary>
		public int Gold { get; init; }

		/// <summary>Pergaminhos Lendários do prêmio do fim da semana.</summary>
		public int LegendaryScrolls { get; init; }

		/// <summary>A defesa montada para este Grau.</summary>
		public ArenaTeamSpec Ghost { get; init; } = new();
	}

	/// <summary>Como montar uma equipe de quatro: estrelas, nível, e o grau e o nível das runas.</summary>
	public sealed record ArenaTeamSpec
	{
		public int Stars { get; init; } = 5;
		public int Level { get; init; } = 25;
		public int RuneGrade { get; init; } = 5;
		public int RuneLevel { get; init; }
		public bool Awakened { get; init; }

		/// <summary>Quanto das habilidades está melhorado, de 0 (nível 1) a 1 (todas no máximo).</summary>
		public double Skills { get; init; }
	}

	/// <summary>Um Rival: uma equipe fixa, com espera depois de vencida e Ouro garantido.</summary>
	public sealed record ArenaRival
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";

		/// <summary>Horas até poder lutar com ele de novo.</summary>
		public int CooldownHours { get; init; } = 4;

		public int Gold { get; init; }

		/// <summary>Os quatro integrantes, pelo id da invocação; a Líder é a primeira.</summary>
		public IReadOnlyList<string> Members { get; init; } = new List<string>();

		public ArenaTeamSpec Team { get; init; } = new();
	}

	public enum ArenaItem
	{
		Scrolls,
		LightDarkScrolls,
		InfusionCores,
		ReappraisalGems,
		Mana,
	}

	/// <summary>Quando o limite de uma oferta da Loja Cósmica volta.</summary>
	public enum ArenaPeriod
	{
		None,
		Day,
		Week,
	}

	public sealed record ArenaOffer
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";
		public ArenaItem Item { get; init; }
		public int Amount { get; init; } = 1;

		/// <summary>Em Astrarium (Loja Cósmica) ou Ecos do Véu (Loja do Véu).</summary>
		public int Price { get; init; }

		/// <summary>Quantas vezes por <see cref="Period"/>; 0 = sem limite.</summary>
		public int Limit { get; init; }

		public ArenaPeriod Period { get; init; }
	}
}
