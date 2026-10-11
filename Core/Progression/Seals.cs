using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Os selos do Grimório do Invocador: marcos da conta lidos do save, sem guardar nada e sem prêmio. O
	/// livro só mostra o que o jogador já fez e quanto falta para o resto — os da jornada (Campanha,
	/// Masmorras, o céu da Exploração) e os da coleção (invocações, famílias, Despertar, 6★, a conta).
	/// </summary>
	public static class Seals
	{
		public const int ManyPulls = 100;

		/// <summary>Campanha, Masmorras e a Exploração Estelar (o mais longe que já chegou no céu).</summary>
		public static IReadOnlyList<Seal> Journey(PlayerState player, GameDatabase database)
		{
			var deep = database.Dungeons.Count(d => Dungeons.Cleared(player, d) >= d.Floors.Count);
			var boreal = database.Exploration.Range(Hemisphere.Boreal).Last;
			return
			[
				new("first_victory", Math.Min(player.HighestStage, 1), 1),
				new("campaign", player.HighestStage, database.Stages.Count),
				new("deep_dungeon", Math.Min(deep, 1), 1),
				new("all_dungeons", deep, database.Dungeons.Count),
				new("northern_sky", Math.Min(player.ExplorationBest, boreal), Math.Max(boreal, 1)),
				new("whole_sky", player.ExplorationBest, database.Exploration.Constellations.Count),
			];
		}

		/// <summary>Invocações feitas, famílias na conta, o primeiro desperto, a melhor estrela, uma lenda e o nível da conta.</summary>
		public static IReadOnlyList<Seal> Collection(PlayerState player, GameDatabase database)
		{
			var monsters = Monsters(player).ToList();
			var legends = monsters.Count(m => database.HasSummon(m.SummonId) && database.Summon(m.SummonId).Rarity >= 5);
			return
			[
				new("pulls", player.TotalPulls, ManyPulls),
				new("families", Families(player, database), Math.Max(1, database.Families.Count / 2)),
				new("awakened", Math.Min(monsters.Count(m => m.Awakened), 1), 1),
				new("six_stars", monsters.Count == 0 ? 0 : monsters.Max(m => m.Stars), Growth.MaxStars),
				new("legend", Math.Min(legends, 1), 1),
				new("account", player.AccountLevel, Account.MaxLevel),
			];
		}

		/// <summary>Os monstros de verdade da conta, guardados ou não (sem os Núcleos de Infusão).</summary>
		public static IEnumerable<OwnedSummon> Monsters(PlayerState player) => player.Monsters.Where(m => !m.IsInfusionCore);

		/// <summary>Quantas famílias têm pelo menos um monstro na conta.</summary>
		public static int Families(PlayerState player, GameDatabase database) => Monsters(player)
			.Where(m => database.HasSummon(m.SummonId))
			.Select(m => database.Summon(m.SummonId).FamilyId)
			.Distinct()
			.Count();
	}
}
