using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// A forma desperta de uma habilidade, derivada da lista de efeitos que o arquivo traz: cada efeito pode ter
	/// <see cref="EffectDefinition.Awakened"/> (os campos que mudam, o resto fica), <see cref="EffectDefinition.LostAfterAwaken"/>
	/// (sai no Despertar) ou <see cref="EffectDefinition.OnlyAwakened"/> (só existe desperto). A forma normal é a lista sem
	/// os que só existem despertos; a desperta é a lista sem os que saem, cada um com os campos trocados.
	/// </summary>
	internal static class AwakenedForm
	{
		/// <summary>Campos que o Despertar não troca: o tipo do efeito e as próprias marcas.</summary>
		private static readonly HashSet<string> Locked = new(StringComparer.OrdinalIgnoreCase) { "kind", "awakened", "lostAfterAwaken", "onlyAwakened" };

		private static readonly HashSet<string> Fields = typeof(EffectDefinition).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

		/// <summary>Algum efeito da lista muda no Despertar.</summary>
		public static bool Declares(IReadOnlyList<EffectDefinition> raw) =>
			raw.Any(e => e.Awakened is { Count: > 0 } || e.LostAfterAwaken || e.OnlyAwakened);

		/// <summary>A lista da forma normal, sem as marcas do Despertar.</summary>
		public static IReadOnlyList<EffectDefinition> Normal(IReadOnlyList<EffectDefinition> raw) =>
			!Declares(raw) ? raw : raw.Where(e => !e.OnlyAwakened).Select(Plain).ToList();

		/// <summary>A lista da forma desperta.</summary>
		public static IReadOnlyList<EffectDefinition> Awakened(IReadOnlyList<EffectDefinition> raw) =>
			raw.Where(e => !e.LostAfterAwaken).Select(Awaken).ToList();

		/// <summary>O que está errado nas marcas do Despertar da lista (vazio: nada).</summary>
		public static IEnumerable<string> Problems(IReadOnlyList<EffectDefinition> raw)
		{
			foreach (var effect in raw)
			{
				if (effect.LostAfterAwaken && effect.OnlyAwakened)
					yield return "efeito com lostAfterAwaken e onlyAwakened ao mesmo tempo.";
				if (effect.OnlyAwakened && effect.Awakened is { Count: > 0 })
					yield return "efeito onlyAwakened com awakened: ele já é a forma desperta.";
				if (effect.LostAfterAwaken && effect.Awakened is { Count: > 0 })
					yield return "efeito lostAfterAwaken com awakened: ele some desperto.";
				foreach (var key in effect.Awakened?.Keys ?? Enumerable.Empty<string>())
				{
					if (Locked.Contains(key))
						yield return $"awakened não troca \"{key}\".";
					else if (!Fields.Contains(key))
						yield return $"awakened tem um campo desconhecido: \"{key}\".";
				}
			}

			if (Declares(raw) && raw.All(e => e.LostAfterAwaken))
				yield return "todos os efeitos saem no Despertar.";
		}

		private static EffectDefinition Plain(EffectDefinition effect) =>
			effect with { Awakened = null, LostAfterAwaken = false, OnlyAwakened = false };

		private static EffectDefinition Awaken(EffectDefinition effect)
		{
			if (effect.Awakened is not { Count: > 0 } changes)
				return Plain(effect);

			var node = JsonSerializer.SerializeToNode(Plain(effect), GameDatabase.JsonOptions)!.AsObject();
			foreach (var (key, value) in changes)
			{
				if (!Fields.Contains(key) || Locked.Contains(key))
					continue;

				var existing = node.Select(pair => pair.Key).FirstOrDefault(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase));
				node[existing ?? key] = JsonNode.Parse(value.GetRawText());
			}

			return JsonSerializer.Deserialize<EffectDefinition>(node, GameDatabase.JsonOptions)!;
		}
	}
}
