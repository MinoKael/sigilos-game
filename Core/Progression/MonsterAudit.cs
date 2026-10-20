using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>O que o servidor sabe do envio além dos dois saves: o conteúdo do jogo, o tempo desde o save guardado e o que o Correio pode ter dado.</summary>
	public sealed record AuditContext(GameDatabase Database, TimeSpan Since, long Gift);

	/// <summary>
	/// A conferência do crescimento dos monstros de um save enviado. Só olha o que mudou desde o save guardado: um
	/// monstro igual ao de antes nunca é recusado, mesmo que as tabelas tenham mudado. O resto é a conta de quem pagou:
	/// a experiência vem da Essência gasta (<see cref="Leveling.ExperiencePerEssence"/>) ou das vitórias que o tempo
	/// permite, cada estrela nova custa Fragmentos, e cada habilidade nova pede um monstro fundido.
	/// </summary>
	public static class MonsterAudit
	{
		/// <summary>
		/// A vitória mais curta que o jogo mostra: um golpe só (<c>BattlePace.Approach + Hit + Return</c>, 1,85 s) na
		/// velocidade da Batalha automática (<c>BattlePace.AutoBattleFactor</c>, 3×) dá 0,62 s. Uma luta nunca é mais rápida.
		/// </summary>
		public const double MinWinSeconds = 0.6;

		/// <summary>A vitória que terminou entre dois envios seguidos, e a da Exploração (que não custa Mana).</summary>
		public const int WinsBurst = 1;

		/// <summary>O que a conta ganha de graça por minuto, como o <c>SaveValidator</c> já deixa para cada moeda.</summary>
		private const long CurrencySlack = 20_000;

		private const long CurrencyPerMinute = 2_000;

		/// <summary>Fragmentos de prêmios de marco, que chegam de uma vez.</summary>
		private const long FragmentSlack = 300;

		/// <summary>
		/// Nulo se a estrutura vale: estrelas, nível, habilidades e despertar que o jogo permite. Um monstro novo passa pela
		/// conferência inteira; um que já existia, só pelo que subiu desde o save guardado (as tabelas podem ter mudado).
		/// </summary>
		public static string? Structure(PlayerState next, PlayerState? previous, GameDatabase database)
		{
			var before = Index(previous);
			foreach (var monster in next.Monsters)
			{
				if (monster.IsInfusionCore || !database.HasSummon(monster.SummonId))
					continue;

				var known = before.TryGetValue((monster.Id, monster.SummonId), out var old);
				if (known && Same(monster, old!))
					continue;

				var summon = database.Summon(monster.SummonId);
				if (monster.Level < 1 || monster.Experience < 0 || monster.Stars > Growth.MaxStars)
					return "monster_structure";

				if ((!known || monster.Stars != old!.Stars) && monster.Stars < summon.Rarity)
					return "monster_structure";

				if ((!known || monster.Level > old!.Level || monster.Stars != old.Stars) && monster.Level > Leveling.MaxLevel(monster))
					return "monster_structure";

				var skills = summon.AllSkills;
				if (monster.SkillLevels.Count > skills.Count && (!known || monster.SkillLevels.Count > old!.SkillLevels.Count))
					return "monster_structure";

				for (var i = 0; i < Math.Min(skills.Count, monster.SkillLevels.Count); i++)
				{
					var level = monster.SkillLevels[i];
					var raised = !known || level > old!.SkillLevel(i);
					if (level < 1 || raised && level > Math.Max(1, skills[i].MaxLevel))
						return "monster_structure";
				}

				if (monster.Awakened && !(known && old!.Awakened) && !Awakening.CanBeAwakened(summon))
					return "monster_structure";
			}

			return null;
		}

		/// <summary>Nulo se o crescimento desde <paramref name="previous"/> (nulo: uma conta nova) cabe no que a conta pagou.</summary>
		public static string? Spending(PlayerState next, PlayerState? previous, AuditContext context)
		{
			var database = context.Database;
			var before = Index(previous);
			var kept = new HashSet<(int, string)>();
			long experience = 0, fragments = 0, awakening = 0;
			var skillUps = 0;

			foreach (var monster in next.Monsters)
			{
				if (monster.IsInfusionCore || !database.HasSummon(monster.SummonId))
					continue;

				var summon = database.Summon(monster.SummonId);
				var key = (monster.Id, monster.SummonId);
				var known = before.TryGetValue(key, out var old);
				if (known)
					kept.Add(key);
				if (known && Same(monster, old!))
					continue;

				var baseStars = known ? old!.Stars : summon.Rarity;
				experience += Math.Max(0, Worth(summon.Rarity, monster) - (known ? Worth(summon.Rarity, old!) : 0));
				for (var stars = baseStars; stars < monster.Stars; stars++)
					fragments += Evolution.Cost(stars);
				skillUps += Math.Max(0, Ups(monster) - (known ? Ups(old!) : 0));
				if (monster.Awakened && !(known && old!.Awakened))
					awakening += Awakening.Cost(summon.Rarity);
			}

			var materials = 0;
			long released = 0;
			foreach (var (key, gone) in before)
			{
				if (kept.Contains(key))
					continue;

				materials++;
				released += gone.IsInfusionCore || !database.HasSummon(gone.SummonId) ? Fusion.FragmentsFor(5) : Fusion.FragmentsFor(database.Summon(gone.SummonId).Rarity);
			}

			var minutes = Math.Max(0.03, context.Since.TotalMinutes);
			var essence = Math.Max(0, (previous?.Essence ?? next.Essence) - next.Essence) + CurrencySlack + (long)(minutes * CurrencyPerMinute) + context.Gift;

			// Fragmentos só chegam soltando monstros, pelo Correio e por Núcleos (um por vitória, no máximo).
			var cores = (long)RewardDrops.FragmentCoreValue * (WinsPossible(next, previous, context));
			var stock = Math.Max(0, (previous?.Fragments ?? next.Fragments) - next.Fragments) + released + FragmentSlack + (long)cores + context.Gift;

			var free = FreeExperience(database) * (WinsPossible(next, previous, context));
			var paid = Math.Max(0, experience - free);
			if ((paid + Leveling.ExperiencePerEssence - 1) / Leveling.ExperiencePerEssence + awakening > essence)
				return "monster_growth";

			if (fragments > stock)
				return "monster_stars";

			return skillUps > materials + 3 ? "monster_skills" : null;
		}

		/// <summary>Os pontos da Arena Astral ganhos desde o save guardado cabem nas lutas que os Convites pagaram. Nulo se valem.</summary>
		public static string? Points(PlayerState next, PlayerState previous, GameDatabase database)
		{
			var arena = database.Arena;
			if (next.Arena.Cycle != previous.Arena.Cycle)
				return null;

			var fights = Math.Max(0, previous.Arena.Invites - next.Arena.Invites) + 3 + arena.ListBonusInvites * 2;
			return next.Arena.Points - previous.Arena.Points > (long)arena.PointsMax * fights ? "arena_points" : null;
		}

		/// <summary>
		/// Quantas vitórias cabem entre o save guardado e este: as que o tempo permite (<see cref="MinWinSeconds"/>), nunca
		/// mais que a Mana que a conta pode ter gasto (cada vitória de Campanha ou Masmorra a cobra: o dobro do máximo, mais o
		/// que a canalização, o nível da conta e a Loja, pelo Ouro gasto, repõem).
		/// </summary>
		public static long WinsPossible(PlayerState next, PlayerState? previous, AuditContext context)
		{
			var database = context.Database;
			var byTime = WinsBurst + (long)Math.Floor(Math.Max(0, context.Since.TotalSeconds) / MinWinSeconds);
			var cheapest = Math.Max(1, database.Stages.Select(s => s.Mana).Concat(database.AllDungeons.SelectMany(d => d.Floors).Select(f => f.Mana)).Where(cost => cost > 0).DefaultIfEmpty(1).Min());
			var ratio = database.Shop.Where(o => o.Item == ShopItem.Mana && o.Price > 0).Select(o => (double)o.Amount / o.Price).DefaultIfEmpty(0).Max();
			var goldSpent = Math.Max(0, (previous?.Gold ?? next.Gold) - next.Gold) + 100;
			var levelRefill = previous != null && next.AccountLevel > previous.AccountLevel ? Mana.Max(next) : 0;
			var regen = (long)Math.Ceiling(Mana.PerHour * Math.Max(0, context.Since.TotalHours));
			var mana = 2L * Mana.Max(next) + (long)Math.Ceiling(goldSpent * ratio) + levelRefill + regen;
			return Math.Min(byTime, mana / cheapest);
		}

		/// <summary>
		/// A experiência total que uma equipe pode receber de uma vitória: a maior recompensa do jogo vezes o tamanho da
		/// equipe (a divisão entre os presentes nunca passa disso).
		/// </summary>
		public static long FreeExperience(GameDatabase database)
		{
			var best = Math.Max(
				database.Stages.Select(s => s.Experience).DefaultIfEmpty(0).Max(),
				Math.Max(
					database.AllDungeons.SelectMany(d => d.Floors).Select(f => f.Experience).DefaultIfEmpty(0).Max(),
					database.Exploration.Constellations.Select(c => c.Reward.Experience).DefaultIfEmpty(0).Max()));
			var size = database.AllDungeons.Select(d => Teams.SizeOf(d.Id)).Append(Teams.SizeOf(Teams.Campaign)).Append(Teams.SizeOf(Teams.Exploration)).Max();
			return (long)best * size;
		}

		/// <summary>
		/// A experiência acumulada de um monstro, a mesma por qualquer caminho de evolução: cada nível conta na coluna da
		/// menor estrela que o deixa subir (<see cref="Growth.MaxLevel"/>), a partir das estrelas naturais.
		/// </summary>
		public static long Worth(int natural, OwnedSummon monster)
		{
			long total = Math.Max(0, monster.Experience);
			for (var level = 1; level < monster.Level; level++)
			{
				var stars = natural;
				while (stars < Growth.MaxStars && Growth.MaxLevel(stars) <= level)
					stars++;
				total += Leveling.ExperienceToNext(stars, level);
			}

			return total;
		}

		private static int Ups(OwnedSummon monster) => monster.SkillLevels.Sum(level => Math.Max(1, level) - 1);

		private static bool Same(OwnedSummon a, OwnedSummon b) =>
			a.Stars == b.Stars && a.Level == b.Level && a.Experience == b.Experience && a.Awakened == b.Awakened && a.SkillLevels.SequenceEqual(b.SkillLevels);

		private static Dictionary<(int, string), OwnedSummon> Index(PlayerState? player) =>
			player?.Monsters.GroupBy(m => (m.Id, m.SummonId)).ToDictionary(g => g.Key, g => g.First()) ?? new Dictionary<(int, string), OwnedSummon>();
	}
}
