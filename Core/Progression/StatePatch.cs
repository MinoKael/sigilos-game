using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// A diferença entre dois estados da mesma conta, em JSON. O servidor calcula (<see cref="Diff"/>) o que uma ação mudou e o
	/// jogo aplica (<see cref="Apply"/>) na própria cópia, sem trocar o objeto inteiro: as telas guardam as runas e os monstros,
	/// então um item com <c>Id</c> que já existe é atualizado no lugar. Serve a qualquer ação do servidor, sem uma classe de
	/// resultado por ação.
	/// </summary>
	public static class StatePatch
	{
		private const string Ids = "$ids";
		private const string Changed = "changed";
		private const string Removed = "removed";

		public static JsonObject Diff(PlayerState before, PlayerState after) => Diff(Node(before), after);

		public static JsonObject Diff(JsonObject old, PlayerState after)
		{
			var now = Node(after);
			var patch = new JsonObject();
			foreach (var (name, value) in now)
			{
				old.TryGetPropertyValue(name, out var previous);
				if (JsonNode.DeepEquals(previous, value))
					continue;

				if (value is JsonArray list && IsIdList(list) && (previous is null or JsonArray))
				{
					var was = previous as JsonArray ?? new JsonArray();
					var changed = new JsonArray();
					foreach (var item in list)
					{
						var id = IdOf(item);
						if (!was.Any(o => IdOf(o) == id && JsonNode.DeepEquals(o, item)))
							changed.Add(item!.DeepClone());
					}

					var removed = new JsonArray();
					foreach (var item in was)
					{
						var id = IdOf(item);
						if (!list.Any(n => IdOf(n) == id))
							removed.Add(id);
					}

					patch[name] = new JsonObject { [Ids] = true, [Changed] = changed, [Removed] = removed };
				}
				else
					patch[name] = value?.DeepClone();
			}

			return patch;
		}

		/// <summary>O estado de agora em JSON: a base de <see cref="Diff(JsonObject, PlayerState)"/> quando a ação muda o próprio objeto.</summary>
		public static JsonObject Snapshot(PlayerState player) => Node(player);

		/// <summary>
		/// Aplica o que o servidor mudou. A cópia do aparelho só muda por aqui, então cada valor do patch é o do servidor e
		/// substitui o de antes, moedas inclusive.
		/// </summary>
		public static void Apply(PlayerState player, JsonObject patch)
		{
			foreach (var (name, value) in patch)
			{
				var property = typeof(PlayerState).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
				if (property is not { CanWrite: true })
					continue;

				if (value is JsonObject marked && marked.ContainsKey(Ids))
					ApplyList(property, player, marked);
				else
					property.SetValue(player, value.Deserialize(property.PropertyType, GameDatabase.JsonOptions));
			}
		}

		private static void ApplyList(PropertyInfo property, PlayerState player, JsonObject marked)
		{
			var list = (IList)property.GetValue(player)!;
			var type = property.PropertyType.GetGenericArguments()[0];
			var id = type.GetProperty("Id")!;
			var removed = marked[Removed]!.AsArray().Select(n => n!.GetValue<int>()).ToHashSet();
			for (var i = list.Count - 1; i >= 0; i--)
			{
				if (removed.Contains((int)id.GetValue(list[i])!))
					list.RemoveAt(i);
			}

			foreach (var item in marked[Changed]!.AsArray())
			{
				var next = item.Deserialize(type, GameDatabase.JsonOptions)!;
				var key = (int)id.GetValue(next)!;
				var own = list.Cast<object>().FirstOrDefault(o => (int)id.GetValue(o)! == key);
				if (own == null)
					list.Add(next);
				else
					foreach (var field in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
						field.SetValue(own, field.GetValue(next));
			}
		}

		private static JsonObject Node(PlayerState player) =>
			JsonSerializer.SerializeToNode(player, GameDatabase.JsonOptions)!.AsObject();

		private static bool IsIdList(JsonArray list) =>
			list.Count > 0 && list.All(i => i is JsonObject o && o.Any(p => string.Equals(p.Key, "id", StringComparison.OrdinalIgnoreCase)));

		private static int IdOf(JsonNode? node) =>
			node is JsonObject o ? o.First(p => string.Equals(p.Key, "id", StringComparison.OrdinalIgnoreCase)).Value!.GetValue<int>() : -1;
	}
}
