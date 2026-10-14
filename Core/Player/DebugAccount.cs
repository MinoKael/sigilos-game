using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// A conta de desenvolvimento, "Debugger": a que se usa sempre que se valida alguma coisa à mão. Tem tudo
	/// feito: todos os homúnculos em jogo (6★, nível máximo, habilidades no máximo, despertos quando podem),
	/// a Campanha, as Masmorras e a Exploração Estelar vencidas, a conta no nível 100 e 700 runas Lendárias 6★
	/// +15 com os números todos no máximo (principal, nativo, subatributos, melhoras e Pedra de Afiar).
	///
	/// É a mesma conta a cada abertura (o sorteio é fixo): o GameEntry a refaz do zero toda vez que o jogo abre
	/// com <c>-- --debugger</c>, num save próprio (<see cref="Slot"/>), longe do save de verdade.
	/// </summary>
	public static class DebugAccount
	{
		public const string Name = "Debugger";

		/// <summary>O nome dos arquivos em user://: nunca o do save de verdade.</summary>
		public const string Slot = "debugger";

		public const int RuneCount = 700;

		private const int Seed = 700;

		/// <summary>Quantas de cada Pedra de Afiar e de cada Gema Lendária a conta tem.</summary>
		private const int ToolsEach = 5;

		/// <summary>Os níveis de melhora que trazem sorteio: um por subatributo, na ordem.</summary>
		private static readonly int[] UpgradeLevels = [3, 6, 9, 12];

		public static PlayerState Create(DateTime now, GameDatabase database)
		{
			var player = new PlayerState
			{
				Version = PlayerState.CurrentVersion,
				Started = now,
				LastIdleCollect = now,
				TutorialDone = true,
				AccountLevel = Progression.Account.MaxLevel,
				CollectionCapacity = Progression.Account.MaxCollectionCapacity,
				Scrolls = 9_999,
				LightDarkScrolls = 999,
				LegendaryScrolls = 999,
				Essence = 50_000_000,
				Gold = 999_999,
				Fragments = 99_999,
				ReappraisalGems = 99,
			};
			player.Mana = Progression.Mana.Max(player);

			var monsters = AddSummons(player, database);
			FillTeams(player, monsters, database);
			CompleteContent(player, database, now);
			AddRunes(player, new Random(Seed));
			AddTools(player);
			player.SeenMonster = player.NextMonsterId - 1;
			return player;
		}

		/// <summary>Uma cópia de cada variante em jogo, no máximo de tudo.</summary>
		private static List<OwnedSummon> AddSummons(PlayerState player, GameDatabase database)
		{
			var monsters = new List<OwnedSummon>();
			foreach (var summon in database.Summons)
			{
				var monster = Roster.Add(player, summon);
				monster.Stars = Progression.Growth.MaxStars;
				monster.Level = Progression.Leveling.MaxLevel(monster);
				monster.Awakened = Progression.Awakening.CanBeAwakened(summon);
				monster.SkillLevels = summon.AllSkills.Select(skill => skill.MaxLevel).ToList();
				monsters.Add(monster);
			}

			return monsters;
		}

		/// <summary>As cinco de mais estrelas naturais (na ordem dos arquivos) vão para a Campanha e a Exploração.</summary>
		private static void FillTeams(PlayerState player, List<OwnedSummon> monsters, GameDatabase database)
		{
			var strongest = monsters
				.OrderByDescending(m => database.Summon(m.SummonId).Rarity)
				.Take(PlayerState.TeamSize)
				.ToList();
			Teams.FillCampaign(player, strongest);
			foreach (var monster in strongest)
				Teams.Toggle(player, Teams.Exploration, monster.Id);
		}

		private static void CompleteContent(PlayerState player, GameDatabase database, DateTime now)
		{
			player.HighestStage = database.Stages.Count == 0 ? 0 : database.Stages.Max(stage => stage.Number);
			foreach (var dungeon in database.Dungeons)
				player.DungeonFloors[dungeon.Id] = dungeon.Floors.Count;

			var constellations = database.Exploration.Constellations.Count;
			player.ExplorationMonth = Progression.Exploration.MonthKey(now);
			player.ExplorationCleared = constellations;
			player.ExplorationBest = constellations;
		}

		/// <summary>
		/// <see cref="RuneCount"/> runas soltas, repartidas em rodízio pelos conjuntos em jogo, pelos espaços e
		/// pelos principais de cada espaço; só os subatributos saem do sorteio fixo.
		/// </summary>
		private static void AddRunes(PlayerState player, Random random)
		{
			var sets = RuneSets.ActiveSets;
			for (var i = 0; i < RuneCount; i++)
			{
				var slot = i / sets.Count % RuneRules.Slots + 1;
				var options = RuneRules.MainOptions(slot);
				var main = options[i / (sets.Count * RuneRules.Slots) % options.Count];
				player.Runes.Add(PerfectRune(random, player.NextRuneId++, sets[i % sets.Count], slot, main));
			}
		}

		/// <summary>
		/// Lendária 6★ +15: o nativo e os quatro subatributos com o sorteio de origem no máximo, uma melhora no
		/// máximo em cada subatributo (+3, +6, +9 e +12) e a Pedra de Afiar Lendária no máximo onde ela serve.
		/// </summary>
		private static Rune PerfectRune(Random random, int id, RuneSet set, int slot, RuneStat main)
		{
			var rune = new Rune { Id = id, Set = set, Slot = slot, Grade = RuneRules.MaxGrade, Level = RuneRules.MaxLevel, Main = main };
			var taken = new HashSet<RuneStat> { main };
			rune.Innate = Perfect(random, rune, taken);
			for (var i = 0; i < RuneRules.MaxSubstats; i++)
				rune.Substats.Add(Perfect(random, rune, taken));

			for (var i = 0; i < rune.Substats.Count; i++)
			{
				var substat = rune.Substats[i];
				var max = RuneRules.SubstatRange(substat.Stat, rune.Grade).Max;
				substat.Rolls.Add(new RuneRoll(UpgradeLevels[i], max));
				if (RuneRules.GrindRange(substat.Stat, RuneRarity.Legendary) is { } grind)
					substat.Grind = grind.Max;
			}

			return rune;
		}

		private static RuneSubstat Perfect(Random random, Rune rune, HashSet<RuneStat> taken)
		{
			var free = Enum.GetValues<RuneStat>().Where(stat => RuneRules.CanBeSubstat(rune.Slot, rune.Main, stat) && !taken.Contains(stat)).ToList();
			var stat = free[random.Next(free.Count)];
			taken.Add(stat);
			return RuneSubstat.Rolled(stat, 0, RuneRules.SubstatRange(stat, rune.Grade).Max);
		}

		/// <summary>Pedras de Afiar (onde servem) e Gemas Encantadas Lendárias de cada atributo.</summary>
		private static void AddTools(PlayerState player)
		{
			foreach (var stat in Enum.GetValues<RuneStat>())
			{
				for (var i = 0; i < ToolsEach; i++)
				{
					if (RuneRules.IsGrindable(stat))
						player.Tools.Add(new RuneTool(RuneToolKind.Grindstone, stat, RuneRarity.Legendary));
					player.Tools.Add(new RuneTool(RuneToolKind.Gem, stat, RuneRarity.Legendary));
				}
			}
		}
	}
}
