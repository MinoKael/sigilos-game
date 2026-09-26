using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Cria e transforma runas: o drop, a melhora, a Pedra de Afiar e a Gema
	/// Encantada. Não cobra nada nem guarda pedras: isso é do inventário do jogador
	/// (Core/Player/RuneInventory). Cada operação impossível devolve falso sem mudar a runa.
	/// </summary>
	public static class RuneForge
	{
		private static readonly RuneStat[] AllStats = Enum.GetValues<RuneStat>();
		private static readonly IReadOnlyList<RuneSet> AllSets = Enum.GetValues<RuneSet>();

		/// <summary>Nível mínimo para usar Gema Encantada.</summary>
		public const int EnchantLevel = 12;

		/// <summary>
		/// Uma runa de drop. Sem <paramref name="slot"/>, o espaço é sorteado; sem <paramref name="sets"/>,
		/// qualquer conjunto. A raridade nunca sai abaixo de <paramref name="minRarity"/> (Masmorras).
		/// </summary>
		public static Rune Generate(
			Random random,
			int id,
			int grade,
			int? slot = null,
			IReadOnlyList<RuneSet>? sets = null,
			RuneRarity minRarity = RuneRarity.Normal)
		{
			var chosenSlot = slot ?? random.Next(1, RuneRules.Slots + 1);
			var options = RuneRules.MainOptions(chosenSlot);
			var pool = sets is { Count: > 0 } ? sets : AllSets;
			var rune = new Rune
			{
				Id = id,
				Set = pool[random.Next(pool.Count)],
				Slot = chosenSlot,
				Grade = Math.Clamp(grade, 1, RuneRules.MaxGrade),
				Main = options[random.Next(options.Count)],
			};

			if (random.NextDouble() < RuneRules.InnateChance)
				rune.Innate = NewSubstat(random, rune);

			var count = (int)RuneRules.RollRarity(random, minRarity);
			for (var i = 0; i < count; i++)
				rune.Substats.Add(NewSubstat(random, rune));

			return rune;
		}

		/// <summary>Uma pedra de drop: metade Pedras de Afiar, metade Gemas, atributo ao acaso.</summary>
		public static RuneTool GenerateTool(Random random, RuneRarity grade)
		{
			if (random.NextDouble() < 0.5)
			{
				var grindable = AllStats.Where(RuneRules.IsGrindable).ToList();
				return new RuneTool(RuneToolKind.Grindstone, grindable[random.Next(grindable.Count)], grade);
			}

			return new RuneTool(RuneToolKind.Gem, AllStats[random.Next(AllStats.Length)], grade);
		}

		/// <summary>
		/// +1 de melhora, que nunca falha. Em +3, +6, +9 e +12 a runa ganha um subatributo novo até ter 4;
		/// com 4, um deles, ao acaso, ganha mais um sorteio.
		/// </summary>
		public static bool RaiseLevel(Random random, Rune rune)
		{
			if (rune.Level >= RuneRules.MaxLevel)
				return false;

			rune.Level++;
			if (!RuneRules.IsMilestone(rune.Level))
				return true;

			if (rune.Substats.Count < RuneRules.MaxSubstats)
			{
				rune.Substats.Add(NewSubstat(random, rune));
			}
			else
			{
				var substat = rune.Substats[random.Next(rune.Substats.Count)];
				substat.Rolls.Add(new RuneRoll(rune.Level, RuneRules.RollSubstat(random, substat.Stat, rune.Grade)));
			}

			return true;
		}

		/// <summary>
		/// A pedra pode ser usada se afia o mesmo atributo e ainda pode passar do bônus atual. O bônus
		/// novo substitui o antigo e pode sair menor.
		/// </summary>
		public static bool CanGrind(Rune rune, int index, RuneTool tool) =>
			tool.Kind == RuneToolKind.Grindstone &&
			index >= 0 && index < rune.Substats.Count &&
			rune.Substats[index].Stat == tool.Stat &&
			RuneRules.GrindRange(tool.Stat, tool.Grade) is { } range &&
			range.Max > rune.Substats[index].Grind + 1e-9;

		public static bool Grind(Random random, Rune rune, int index, RuneTool tool)
		{
			if (!CanGrind(rune, index, tool))
				return false;

			rune.Substats[index].Grind = RuneRules.RollGrind(random, tool.Stat, tool.Grade);
			return true;
		}

		/// <summary>
		/// A gema troca um subatributo de uma runa +12 por outro que a runa ainda não tem. Só um
		/// subatributo por runa pode ser encantado (o mesmo pode ser trocado de novo).
		/// </summary>
		public static bool CanEnchant(Rune rune, int index, RuneTool tool) =>
			tool.Kind == RuneToolKind.Gem &&
			rune.Level >= EnchantLevel &&
			index >= 0 && index < rune.Substats.Count &&
			rune.Substats.Where((s, i) => i != index).All(s => !s.Enchanted && s.Stat != tool.Stat) &&
			rune.Innate?.Stat != tool.Stat &&
			RuneRules.CanBeSubstat(rune.Slot, rune.Main, tool.Stat);

		/// <summary>As melhoras e o bônus de pedra do subatributo trocado se perdem.</summary>
		public static bool Enchant(Random random, Rune rune, int index, RuneTool tool)
		{
			if (!CanEnchant(rune, index, tool))
				return false;

			var enchanted = RuneSubstat.Rolled(tool.Stat, rune.Level, RuneRules.RollGem(random, tool.Stat, tool.Grade));
			enchanted.Enchanted = true;
			rune.Substats[index] = enchanted;
			return true;
		}

		/// <summary>Um atributo que a runa aceita e ainda não tem, nem como nativo nem como subatributo.</summary>
		private static RuneSubstat NewSubstat(Random random, Rune rune)
		{
			var taken = new HashSet<RuneStat>(rune.Substats.Select(s => s.Stat));
			if (rune.Innate != null)
				taken.Add(rune.Innate.Stat);

			var free = AllStats.Where(s => RuneRules.CanBeSubstat(rune.Slot, rune.Main, s) && !taken.Contains(s)).ToList();
			var stat = free[random.Next(free.Count)];
			return RuneSubstat.Rolled(stat, rune.Level, RuneRules.RollSubstat(random, stat, rune.Grade));
		}
	}
}
