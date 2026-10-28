using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Summoning;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O que um marco ou um drop especial dá: Pergaminhos Lendários, Pergaminhos de Luz e Trevas, Núcleos de
	/// Infusão e Núcleos de Fragmento (cada um vale <see cref="RewardDrops.FragmentCoreValue"/> Fragmentos).
	/// </summary>
	public sealed record Prize(int LegendaryScrolls = 0, int LightDarkScrolls = 0, int InfusionCores = 0, int FragmentCores = 0, int RuneKits = 0)
	{
		public static readonly Prize None = new();

		public bool IsEmpty => LegendaryScrolls == 0 && LightDarkScrolls == 0 && InfusionCores == 0 && FragmentCores == 0 && RuneKits == 0;

		public static Prize operator +(Prize a, Prize b) =>
			new(a.LegendaryScrolls + b.LegendaryScrolls, a.LightDarkScrolls + b.LightDarkScrolls, a.InfusionCores + b.InfusionCores, a.FragmentCores + b.FragmentCores, a.RuneKits + b.RuneKits);
	}

	/// <summary>
	/// Os marcos do jogo (GDD, seção 12): o que é especial vem de chegar lá, não da sorte do dia a dia. A
	/// primeira vitória das fases que fecham uma região e dos andares de Masmorra pagam o que cada uma traz
	/// nos dados (Data/stages.json, Data/dungeons.json); os níveis da conta e os 6★ (<see cref="SixStar"/>) pagam pelas
	/// tabelas daqui, por carta (<see cref="Letters"/>). A Torre (100 andares, ainda por vir) já tem a parte dela reservada no GDD: estes
	/// números não podem crescer sem tirar dela.
	/// </summary>
	public static class Milestones
	{
		/// <summary>Níveis da conta que dão um Pergaminho de Luz e Trevas.</summary>
		public static readonly int[] LightDarkLevels = { 20, 30, 40, 50, 60, 70, 80, 90, 100 };

		/// <summary>A cada tantos níveis da conta, um Núcleo de Infusão.</summary>
		public const int CoreLevelStep = 5;

		/// <summary>O prêmio de chegar a este nível da conta.</summary>
		public static Prize ForAccountLevel(int level) =>
			new(0, LightDarkLevels.Contains(level) ? 1 : 0, level % CoreLevelStep == 0 ? 1 : 0);

		/// <summary>
		/// O primeiro 6★ de cada elemento (<see cref="SixStar"/>): um Núcleo de Infusão, 200 de Mana, 1.000 de Essência e um sigilo 6★
		/// Lendário sorteado (a chave <c>rune:6:Legendary</c> do correio, sem conjunto: sai de um dos de jogo).
		/// </summary>
		public static readonly IReadOnlyDictionary<string, int> FirstSixStar = new Dictionary<string, int>
		{
			["infusionCores"] = 1,
			["mana"] = 200,
			["essence"] = 1000,
			["rune:6:Legendary"] = 1,
		};

		/// <summary>Cada 6★ depois do primeiro do elemento: 300 de Mana e 1.000 de Essência.</summary>
		public static readonly IReadOnlyDictionary<string, int> NextSixStar = new Dictionary<string, int>
		{
			["mana"] = 300,
			["essence"] = 1000,
		};

		/// <summary>A primeira vitória da fase.</summary>
		public static Prize ForFirstClear(StageDefinition stage) =>
			new(stage.FirstClearLegendary, stage.FirstClearLightDark, stage.FirstClearCores, 0, stage.FirstClearKits);

		/// <summary>A primeira vitória do andar de Masmorra.</summary>
		public static Prize ForFirstClear(DungeonFloor floor) => new(floor.FirstClearLegendary, floor.FirstClearLightDark, floor.FirstClearCores);

		/// <summary>Entrega o prêmio: os pergaminhos na conta e os Núcleos na coleção (ou no Baú, se ela está cheia).</summary>
		public static void Grant(PlayerState player, Prize prize)
		{
			SummonRitual.AddScrolls(player, ScrollKind.Legendary, prize.LegendaryScrolls);
			SummonRitual.AddScrolls(player, ScrollKind.LightDark, prize.LightDarkScrolls);
			for (var i = 0; i < prize.InfusionCores; i++)
				Roster.Add(player, InfusionCore.Summon);
			player.Fragments += prize.FragmentCores * RewardDrops.FragmentCoreValue;
			player.PendingRuneKits += prize.RuneKits;
		}
	}
}
