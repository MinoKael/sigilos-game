using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// A Campanha apresenta o jogo aos poucos (GDD, seção 10): cada parte abre numa fase, sem trava
	/// artificial — é a primeira vitória que mostra a parte, e ela nunca mais fecha. Monstros abre na
	/// primeira invocação; as Masmorras, na fase que abre a primeira delas (Data/dungeons.json), e a
	/// Exploração Estelar na fase dela (Data/exploration.json).
	/// A parte que acabou de abrir é "nova" até a próxima fase vencida: o botão dela pulsa, e a vitória
	/// que a abriu avisa.
	/// </summary>
	public static class Features
	{
		/// <summary>A fase cuja primeira vitória abre cada parte.</summary>
		private static readonly IReadOnlyDictionary<Feature, int> Stages = new Dictionary<Feature, int>
		{
			[Feature.Runes] = 1,
			[Feature.Compendium] = 2,
			[Feature.Channel] = 3,
			[Feature.Shop] = 4,
			[Feature.AutoBattle] = 5,
			[Feature.Grimoire] = 8,
		};

		/// <summary>A fase que abre a parte; 0 para Monstros, que abre na primeira invocação.</summary>
		public static int StageOf(GameDatabase database, Feature feature) => feature switch
		{
			Feature.Monsters => 0,
			Feature.Dungeons => database.Dungeons.Select(d => d.UnlockStage).DefaultIfEmpty(0).Min(),
			Feature.Exploration => database.Exploration.Constellations.Count > 0 ? database.Exploration.UnlockStage : 0,
			_ => Stages[feature],
		};

		public static bool IsOpen(PlayerState player, GameDatabase database, Feature feature) => feature switch
		{
			Feature.Monsters => player.TotalPulls > 0 || player.Collection.Any(),
			// Uma Masmorra já vencida continua aberta (Dungeons.IsUnlocked), e a porta junto.
			Feature.Dungeons => database.Dungeons.Any(d => Dungeons.IsUnlocked(player, d)),
			Feature.Exploration => Exploration.IsOpen(player, database.Exploration),
			_ => player.HighestStage >= StageOf(database, feature),
		};

		/// <summary>Aberta na última fase vencida: o botão dela pulsa até a próxima.</summary>
		public static bool IsNew(PlayerState player, GameDatabase database, Feature feature)
		{
			var stage = StageOf(database, feature);
			return stage > 0 && player.HighestStage == stage;
		}

		/// <summary>O que a primeira vitória da fase abre, na ordem da tabela.</summary>
		public static IReadOnlyList<Feature> OpenedBy(GameDatabase database, int stageNumber) =>
			System.Enum.GetValues<Feature>().Where(f => StageOf(database, f) == stageNumber).ToList();
	}
}
