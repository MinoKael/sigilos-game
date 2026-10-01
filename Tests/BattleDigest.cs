using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>
	/// A impressão digital do combate: roda centenas de lutas com sementes fixas e resume tudo o que
	/// aconteceu em cada uma numa linha. Serve para mexer no código do combate sem mudar a regra: guarde a
	/// saída antes, mexa, rode de novo e compare. Linha igual, luta igual, golpe a golpe.
	///
	///   dotnet run --project Tests -- --digest > antes.txt
	///   (mexe no combate)
	///   dotnet run --project Tests -- --digest > depois.txt
	///   dotnet run --project Tests -- --digest="stage3 typical #1"    os eventos de uma luta, um por linha
	///
	/// Cada linha traz o resultado, a duração, a Vida que sobrou e duas somas dos eventos: <c>seq</c>
	/// muda se qualquer evento mudar ou trocar de lugar; <c>set</c> só muda se um evento mudar. Se só o
	/// <c>seq</c> mudou, a luta é a mesma e os eventos saíram em outra ordem.
	///
	/// Mudar número de balanceamento ou dado de Data/ muda a impressão, e é para mudar: aí ela não
	/// compara nada.
	/// </summary>
	internal static class BattleDigest
	{
		private const int Seeds = 3;

		/// <summary>O rótulo da luta cujos eventos são mostrados; nulo no resumo de todas.</summary>
		private static string? _only;

		/// <summary>Os conjuntos de cada monstro dos times com runas: todo efeito de conjunto aparece em algum.</summary>
		private static readonly RuneSet[][] Loadouts =
		{
			new[] { RuneSet.Frenzy, RuneSet.Frenzy, RuneSet.Frenzy, RuneSet.Frenzy, RuneSet.Counter, RuneSet.Counter },
			new[] { RuneSet.Siphon, RuneSet.Siphon, RuneSet.Siphon, RuneSet.Siphon, RuneSet.Bane, RuneSet.Bane },
			new[] { RuneSet.Torment, RuneSet.Torment, RuneSet.Torment, RuneSet.Torment, RuneSet.Oblivion, RuneSet.Oblivion },
			new[] { RuneSet.Bulwark, RuneSet.Bulwark, RuneSet.Tenacity, RuneSet.Tenacity, RuneSet.Counter, RuneSet.Counter },
			new[] { RuneSet.Lethal, RuneSet.Lethal, RuneSet.Lethal, RuneSet.Lethal, RuneSet.Bulwark, RuneSet.Bulwark },
		};

		/// <param name="only">Com um rótulo de luta ("stage3 typical #1"), mostra os eventos dela em vez do resumo de todas.</param>
		public static void Print(GameDatabase database, string? only = null)
		{
			_only = only;
			CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
			var encounters = database.Stages.Select(s => ($"stage{s.Number}", s.Encounter, s.Stars, s.Level))
				.Concat(database.Dungeons.SelectMany(d => d.Floors.Select((f, i) => ($"{d.Id}{i + 1}", f.Encounter, f.Stars, f.Level))))
				.ToList();

			ulong total = 0;
			var fights = 0;
			foreach (var (label, encounter, stars, level) in encounters)
			{
				// O time típico, no nível dos inimigos, sem Despertar e sem runas.
				total ^= Run(database, $"{label} typical", Team(database, TestData.TypicalTeam, stars, level, false, false), encounter, ref fights);

				// Cada família inteira, desperta, no máximo e com runas de conjunto.
				foreach (var family in database.Families)
				{
					var ids = database.Summons.Where(s => s.FamilyId == family.Id).OrderBy(s => s.Element).Select(s => s.Id).ToArray();
					total ^= Run(database, $"{label} {family.Id}", Team(database, ids, 6, 40, true, true), encounter, ref fights);
				}
			}

			if (only == null)
				Console.WriteLine($"total {fights} fights {total:x16}");
		}

		private static BattleTeam Team(GameDatabase database, IReadOnlyList<string> ids, int stars, int level, bool awakened, bool runed)
		{
			var random = new Random(11);
			return new BattleTeam(ids.Select((id, index) =>
			{
				var summon = database.Summon(id);
				var levels = awakened ? summon.AllSkills.Select(s => s.MaxLevel).ToList() : new List<int>();
				var runes = runed ? Runes(random, Loadouts[index % Loadouts.Length]) : new List<Rune>();
				return new TeamMember(summon, stars, level, awakened, levels, runes);
			}).ToList());
		}

		private static List<Rune> Runes(Random random, IReadOnlyList<RuneSet> sets) => sets.Select((set, index) =>
		{
			var rune = RuneForge.Generate(random, index + 1, 6, index + 1, new[] { set });
			while (rune.Level < 12)
				RuneForge.RaiseLevel(random, rune);
			return rune;
		}).ToList();

		private static ulong Run(GameDatabase database, string label, BattleTeam team, Encounter encounter, ref int fights)
		{
			ulong all = 0;
			for (var seed = 1; seed <= Seeds; seed++)
			{
				fights++;
				string line;
				try
				{
					var session = BattleFactory.Create(database, team, encounter, seed);
					var log = new List<BattleEvent>();
					AutoBattle.Run(session, log);

					ulong sequence = Offset, set = 0;
					foreach (var text in log.Select(Text))
					{
						if (_only == $"{label} #{seed}")
							Console.WriteLine(text);

						var hash = Hash(Offset, text);
						sequence = Hash(sequence, text);
						set += hash;
					}

					var health = session.Allies.Sum(u => u.Health);
					line = $"{(session.Victory == true ? "win " : "loss")} t={session.Time:R} hp={health:R} n={log.Count} seq={sequence:x16} set={set:x16}";
				}
				catch (Exception exception)
				{
					line = $"CRASH {exception.GetType().Name}";
				}

				if (_only == null || _only == $"{label} #{seed}")
					Console.WriteLine($"{label} #{seed}: {line}");
				all ^= Hash(Offset, $"{label}#{seed}{line}");
			}

			return all;
		}

		private static string Text(BattleEvent battleEvent) => battleEvent switch
		{
			WaveStarted e => $"wave {e.Wave}/{e.WaveCount} {e.Enemies.Count}",
			TurnStarted e => $"turn {Id(e.Actor)} {e.Round}",
			SkillUsed e => $"skill {Id(e.Actor)} {SkillIndex(e.Actor, e.Skill)}",
			Damaged e => $"damage {Id(e.Target)} {e.Amount} {e.Absorbed} {e.Crit} {e.ElementMultiplier:R}",
			Missed e => $"miss {Id(e.Target)}",
			Protected e => $"protected {Id(e.Target)}",
			Healed e => $"heal {Id(e.Target)} {e.Amount}",
			StatusApplied e => $"status {Id(e.Target)} {e.Status} {e.Turns}",
			Resisted e => $"resist {Id(e.Target)}",
			Immune e => $"immune {Id(e.Target)}",
			StatusRemoved e => $"removed {Id(e.Target)} {e.Status}",
			ImpetoChanged e => $"impeto {Id(e.Target)} {e.Amount:R}",
			TurnSkipped e => $"skip {Id(e.Unit)}",
			ExtraTurn e => $"extra {Id(e.Unit)}",
			Counterattack e => $"counter {Id(e.Unit)}",
			MaxHealthReduced e => $"maxhealth {Id(e.Target)} {e.Amount}",
			Died e => $"died {Id(e.Unit)}",
			Revived e => $"revived {Id(e.Unit)}",
			BattleEnded e => $"end {e.Victory}",
			_ => battleEvent.GetType().Name,
		};

		/// <summary>A posição da habilidade na lista da unidade: o nome muda com o idioma dos dados, a posição não.</summary>
		private static string SkillIndex(BattleUnit unit, SkillDefinition skill)
		{
			for (var i = 0; i < unit.Skills.Count; i++)
			{
				if (ReferenceEquals(unit.Skills[i], skill))
					return i.ToString();
			}

			return skill.Name;
		}

		/// <summary>Lado, lugar no time e quem é: o bastante para não confundir duas unidades.</summary>
		private static string Id(BattleUnit unit)
		{
			var index = 0;
			while (index < unit.Team.Count && !ReferenceEquals(unit.Team[index], unit))
				index++;
			return $"{(unit.Side == Side.Allies ? 'A' : 'E')}{index}:{unit.DefinitionId}";
		}

		// FNV-1a de 64 bits: não depende da versão do .NET, ao contrário do GetHashCode de string.
		private const ulong Offset = 14695981039346656037;

		private static ulong Hash(ulong seed, string text)
		{
			var hash = seed;
			foreach (var letter in text)
			{
				hash ^= letter;
				hash *= 1099511628211;
			}

			return hash;
		}
	}
}
