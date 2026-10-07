using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;
using Sigilos.Core.Summoning;

namespace Sigilos.Core.Social
{
	/// <summary>
	/// Um feito que o Chat global anuncia sozinho (<see cref="Feats"/>). Leva só o que mostra o feito, nada
	/// da conta além do nome de quem o fez (que o servidor põe).
	/// </summary>
	public abstract record Feat;

	/// <summary>Invocou um monstro de 5★: a variante (o id do catálogo).</summary>
	public sealed record SummonFeat(string SummonId) : Feat;

	/// <summary>Levou uma runa a +15: o conjunto, o espaço, as estrelas, o atributo principal e os subatributos (sem o inato).</summary>
	public sealed record RuneFeat(RuneSet Set, int Slot, int Grade, RuneStat Main, List<RuneSubstat> Substats) : Feat
	{
		/// <summary>Uma runa de mostruário com o que o feito conta, em +15.</summary>
		public Rune ToRune() => new() { Set = Set, Slot = Slot, Grade = Grade, Main = Main, Level = RuneRules.MaxLevel, Substats = Substats };
	}

	/// <summary>Quando uma ação do jogador vira feito.</summary>
	public static class Feats
	{
		/// <summary>As estrelas de uma invocação que vira feito.</summary>
		public const int SummonStars = 5;

		/// <summary>As invocações do ritual que viram feito (uma por monstro de 5★).</summary>
		public static IEnumerable<Feat> Of(IEnumerable<SummonResult> results) =>
			results.Where(r => r.Summon.Rarity >= SummonStars).Select(r => new SummonFeat(r.Summon.Id));

		/// <summary>A runa que estava abaixo de +15 em <paramref name="levelBefore"/> e chegou lá; nulo nos outros casos.</summary>
		public static Feat? Of(Rune rune, int levelBefore) =>
			levelBefore < RuneRules.MaxLevel && rune.Level >= RuneRules.MaxLevel ? new RuneFeat(rune.Set, rune.Slot, rune.Grade, rune.Main, rune.Substats) : null;
	}
}
