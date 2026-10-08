using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sigilos.Sounds.Recipes;

namespace Sigilos.Sounds.Library
{
	/// <summary>
	/// Todos os efeitos da biblioteca, na ordem das pastas. Efeito novo: uma linha no grupo dele em
	/// <c>Recipes/</c>; grupo novo: a pasta em <see cref="Categories"/> e o <c>All()</c> dele aqui.
	/// </summary>
	public static class SoundCatalog
	{
		public static readonly IReadOnlyList<string> Categories = new[]
		{
			"ui", "grimoire", "constellation", "summon", "rewards", "combat", "combat/damage", "combat/elements", "combat/status", "combat/boss", "progression",
		};

		private static readonly Regex SnakeCase = new("^[a-z][a-z0-9]*(_[a-z0-9]+)*$");

		public static IReadOnlyList<SoundDef> All()
		{
			var all = new List<SoundDef>();
			all.AddRange(UiSounds.All());
			all.AddRange(GrimoireSounds.All());
			all.AddRange(ConstellationSounds.All());
			all.AddRange(SummonSounds.All());
			all.AddRange(RewardSounds.All());
			all.AddRange(CombatSounds.All());
			all.AddRange(DamageSounds.All());
			all.AddRange(ElementSounds.All());
			all.AddRange(StatusSounds.All());
			all.AddRange(ProgressionSounds.All());
			all.AddRange(BossSounds.All());
			Validate(all);
			return all;
		}

		/// <summary>
		/// Pasta conhecida, nome em snake_case, sem repetir, e nenhum nome que pareça variação de outro da
		/// mesma pasta (<c>gold</c> e <c>gold_02</c>): a limpeza dos arquivos velhos apagaria um pelo outro.
		/// </summary>
		private static void Validate(IReadOnlyList<SoundDef> all)
		{
			var ids = new HashSet<string>();
			foreach (var def in all)
			{
				if (!Categories.Contains(def.Category))
					throw new InvalidOperationException($"{def.Id}: a pasta {def.Category} não está em SoundCatalog.Categories.");
				if (!SnakeCase.IsMatch(def.Name))
					throw new InvalidOperationException($"{def.Id}: o nome precisa ser snake_case (minúsculas, números e _).");
				if (!ids.Add(def.Id))
					throw new InvalidOperationException($"{def.Id} aparece duas vezes.");
			}
			foreach (var def in all)
			{
				var variation = new Regex($"^{Regex.Escape(def.Name)}_\\d+$");
				var clash = all.FirstOrDefault(other => other.Category == def.Category && variation.IsMatch(other.Name));
				if (clash != null)
					throw new InvalidOperationException($"{clash.Id} parece uma variação de {def.Id}: troque um dos nomes.");
			}
		}
	}
}
