using System;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Cria e transforma runas: sorteio de uma runa nova, melhora de nível e refazer subatributo. Não
	/// cobra nada: quem paga o Pó de Sigilo é o inventário do jogador (Core/Player/RuneInventory).
	/// </summary>
	public static class RuneForge
	{
		private static readonly RuneStat[] AllStats = Enum.GetValues<RuneStat>();
		private static readonly Glyph[] AllSets = Enum.GetValues<Glyph>();

		public static Rune Generate(Random random, int id, int grade)
		{
			var slot = random.Next(1, RuneRules.Slots + 1);
			var options = RuneRules.MainOptions(slot);
			var rune = new Rune
			{
				Id = id,
				Set = AllSets[random.Next(AllSets.Length)],
				Slot = slot,
				Grade = Math.Clamp(grade, 1, RuneRules.MaxGrade),
				Main = options[random.Next(options.Count)],
			};

			for (var i = 0; i < RuneRules.SubstatCount; i++)
			{
				var stat = FreeStat(random, rune);
				rune.Substats.Add(new RuneSubstat { Stat = stat, Value = RuneRules.RollSubstat(random, stat, rune.Grade) });
			}

			return rune;
		}

		/// <summary>+1 de melhora. Em +3, +6 e +9, um subatributo ao acaso ganha mais um sorteio.</summary>
		public static void RaiseLevel(Random random, Rune rune)
		{
			if (rune.Level >= RuneRules.MaxLevel)
				return;

			rune.Level++;
			if (!RuneRules.IsMilestone(rune.Level))
				return;

			var substat = rune.Substats[random.Next(rune.Substats.Count)];
			substat.Value += RuneRules.RollSubstat(random, substat.Stat, rune.Grade);
		}

		/// <summary>Troca um subatributo por outro sorteado. As melhoras que ele tinha se perdem.</summary>
		public static void Reroll(Random random, Rune rune, int index)
		{
			var stat = FreeStat(random, rune);
			rune.Substats[index] = new RuneSubstat { Stat = stat, Value = RuneRules.RollSubstat(random, stat, rune.Grade) };
		}

		/// <summary>Um atributo que a runa ainda não tem, nem como principal nem como subatributo.</summary>
		private static RuneStat FreeStat(Random random, Rune rune)
		{
			var free = AllStats.Where(s => s != rune.Main && rune.Substats.All(sub => sub.Stat != s)).ToList();
			return free[random.Next(free.Count)];
		}
	}
}
