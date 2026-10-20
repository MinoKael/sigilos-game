using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// O que mudou entre dois saves, para enviar só isso. Vale para qualquer JSON de save, sem conhecer o jogo: cada
	/// chave de primeiro nível que mudou vai inteira, menos as listas de objetos com <c>Id</c> (monstros, runas...), em que
	/// só vão os elementos novos ou alterados, com a ordem. O cliente e o servidor usam este mesmo código, e quem aplica
	/// confere o hash do resultado: o que não bate volta como save inteiro.
	/// </summary>
	public static class SaveDelta
	{
		private const string Id = "Id";

		/// <summary>
		/// O delta que leva <paramref name="baseline"/> a <paramref name="next"/>; nulo se não dá para descrever (JSON que não é
		/// objeto, ou sem mudança nenhuma não: sem mudança sai um delta vazio).
		/// </summary>
		public static string? Diff(string baseline, string next)
		{
			try
			{
				if (JsonNode.Parse(baseline) is not JsonObject before || JsonNode.Parse(next) is not JsonObject after)
					return null;

				var set = new JsonObject();
				var lists = new JsonObject();
				var removed = new JsonArray();

				foreach (var (name, value) in after)
				{
					if (!before.TryGetPropertyValue(name, out var old))
					{
						set[name] = value?.DeepClone();
						continue;
					}

					if (JsonNode.DeepEquals(old, value))
						continue;

					if (Keys(old) is { } oldItems && Keys(value) is { } newItems)
						lists[name] = ListDelta(oldItems, newItems);
					else
						set[name] = value?.DeepClone();
				}

				foreach (var (name, _) in before)
				{
					if (!after.ContainsKey(name))
						removed.Add(name);
				}

				var delta = new JsonObject();
				if (set.Count > 0)
					delta["set"] = set;
				if (removed.Count > 0)
					delta["del"] = removed;
				if (lists.Count > 0)
					delta["lists"] = lists;
				return delta.ToJsonString();
			}
			catch (JsonException)
			{
				return null;
			}
		}

		/// <summary>O save que sai de aplicar <paramref name="delta"/> sobre <paramref name="baseline"/>; nulo se o delta não serve àquele save.</summary>
		public static string? Apply(string baseline, string delta)
		{
			try
			{
				if (JsonNode.Parse(baseline) is not JsonObject target || JsonNode.Parse(delta) is not JsonObject change)
					return null;

				if (change["del"] is JsonArray removed)
				{
					foreach (var name in removed)
					{
						if (name?.GetValue<string>() is not { } key || !target.Remove(key))
							return null;
					}
				}

				if (change["set"] is JsonObject set)
				{
					foreach (var (name, value) in set.ToList())
						target[name] = value?.DeepClone();
				}

				if (change["lists"] is JsonObject lists)
				{
					foreach (var (name, spec) in lists)
					{
						if (spec is not JsonObject list || Keys(target[name]) is not { } items || Merge(items, list) is not { } merged)
							return null;
						target[name] = merged;
					}
				}

				return target.ToJsonString();
			}
			catch (System.Exception exception) when (exception is JsonException or System.InvalidOperationException or System.FormatException)
			{
				return null;
			}
		}

		private sealed record Keyed(List<long> Ids, Dictionary<long, JsonNode> Items);

		private static JsonObject ListDelta(Keyed before, Keyed after)
		{
			var put = new JsonArray();
			foreach (var id in after.Ids)
			{
				var item = after.Items[id];
				if (!before.Items.TryGetValue(id, out var old) || !JsonNode.DeepEquals(old, item))
					put.Add(item.DeepClone());
			}

			var list = new JsonObject();
			if (!before.Ids.SequenceEqual(after.Ids))
			{
				var order = new JsonArray();
				foreach (var id in after.Ids)
					order.Add(id);
				list["order"] = order;
			}

			list["put"] = put;
			return list;
		}

		private static JsonArray? Merge(Keyed items, JsonObject list)
		{
			var put = new Dictionary<long, JsonNode>();
			foreach (var item in list["put"] as JsonArray ?? new JsonArray())
			{
				if (item is not JsonObject entry || IdOf(entry) is not { } id || !put.TryAdd(id, entry.DeepClone()))
					return null;
			}

			var order = list["order"] is JsonArray ids ? ids.Select(node => node!.GetValue<long>()).ToList() : items.Ids;
			var result = new JsonArray();
			var seen = new HashSet<long>();
			foreach (var id in order)
			{
				if (!seen.Add(id))
					return null;

				if (put.TryGetValue(id, out var changed))
					result.Add(changed);
				else if (items.Items.TryGetValue(id, out var kept))
					result.Add(kept.DeepClone());
				else
					return null;
			}

			return put.Keys.All(seen.Contains) ? result : null;
		}

		/// <summary>Os elementos de uma lista de objetos com <c>Id</c> inteiro e único, na ordem; nulo se não é essa lista.</summary>
		private static Keyed? Keys(JsonNode? node)
		{
			if (node is not JsonArray array)
				return null;

			var items = new Dictionary<long, JsonNode>();
			var ids = new List<long>();
			foreach (var item in array)
			{
				if (item is not JsonObject entry || IdOf(entry) is not { } id || !items.TryAdd(id, entry))
					return null;
				ids.Add(id);
			}

			return new Keyed(ids, items);
		}

		private static long? IdOf(JsonObject entry) =>
			entry[Id] is JsonValue value && value.TryGetValue<long>(out var id) ? id : null;
	}
}
