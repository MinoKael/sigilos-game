using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;
using Sigilos.Core.Summoning;

namespace Sigilos.Core.Progression
{
	/// <summary>Uma ação que sorteia ou gasta e que o servidor da conta executa: o jogo só pede.</summary>
	public enum ServerActionKind
	{
		UpgradeRune,
		GrindRune,
		EnchantRune,
		ReappraiseRune,
		Summon,
	}

	/// <summary>O pedido: só os parâmetros que o jogador escolhe, nunca um resultado.</summary>
	public sealed class ActionRequest
	{
		public ServerActionKind Kind { get; set; }

		public int RuneId { get; set; }

		/// <summary>Melhora: o nível a alcançar.</summary>
		public int Target { get; set; }

		/// <summary>Afiar e Encantar: o subatributo.</summary>
		public int Index { get; set; }

		public RuneTool? Tool { get; set; }

		/// <summary>Invocação: 1 ou 10.</summary>
		public int Count { get; set; } = 1;

		public ScrollKind Scroll { get; set; }

		public string ToJson() => JsonSerializer.Serialize(this, GameDatabase.JsonOptions);

		public static ActionRequest? FromJson(string json)
		{
			try
			{
				return JsonSerializer.Deserialize<ActionRequest>(json, GameDatabase.JsonOptions);
			}
			catch (JsonException)
			{
				return null;
			}
		}
	}

	/// <summary>Um monstro que a invocação entregou.</summary>
	public sealed class DeltaMonster
	{
		public OwnedSummon Monster { get; set; } = new();

		public bool FirstCopy { get; set; }
	}

	/// <summary>
	/// O que a ação mudou na conta, para o jogo aplicar na própria cópia sem trocar o estado inteiro (as telas
	/// guardam as runas e os monstros). Traz os números como ficaram, não as diferenças.
	/// </summary>
	public sealed class ActionDelta
	{
		public ServerActionKind Kind { get; set; }

		public int Essence { get; set; }

		public int Scrolls { get; set; }

		public int LightDarkScrolls { get; set; }

		public int LegendaryScrolls { get; set; }

		public int ReappraisalGems { get; set; }

		public int TotalPulls { get; set; }

		public int PullsSinceFiveStar { get; set; }

		public int NextMonsterId { get; set; }

		public List<RuneTool> Tools { get; set; } = new();

		public Rune? Rune { get; set; }

		public List<DeltaMonster> Monsters { get; set; } = new();

		public string ToJson() => JsonSerializer.Serialize(this, GameDatabase.JsonOptions);

		public static ActionDelta? FromJson(string json)
		{
			try
			{
				return JsonSerializer.Deserialize<ActionDelta>(json, GameDatabase.JsonOptions);
			}
			catch (JsonException)
			{
				return null;
			}
		}
	}

	/// <summary>
	/// As ações que só o servidor pode fazer numa conta com nuvem (o jogo sem conta e o save de teste usam a mesma
	/// conta, no aparelho). <see cref="Apply"/> muda o estado dado e descreve a mudança; <see cref="ApplyDelta"/>
	/// leva essa descrição a outra cópia da mesma conta.
	/// </summary>
	public static class ServerActions
	{
		/// <summary>Faz a ação em <paramref name="player"/>. Nulo, sem mudar nada, se o pedido não vale (sem Essência, runa que não existe...).</summary>
		public static ActionDelta? Apply(Random random, GameDatabase database, PlayerState player, ActionRequest request)
		{
			var delta = new ActionDelta { Kind = request.Kind };
			switch (request.Kind)
			{
				case ServerActionKind.UpgradeRune:
					if (Find(player, request.RuneId) is not { } upgraded || !RuneInventory.Upgrade(random, player, upgraded, request.Target))
						return null;
					delta.Rune = upgraded;
					break;

				case ServerActionKind.GrindRune:
					if (request.Tool == null || Find(player, request.RuneId) is not { } ground || !RuneInventory.Grind(random, player, ground, request.Index, request.Tool))
						return null;
					delta.Rune = ground;
					break;

				case ServerActionKind.EnchantRune:
					if (request.Tool == null || Find(player, request.RuneId) is not { } enchanted || !RuneInventory.Enchant(random, player, enchanted, request.Index, request.Tool))
						return null;
					delta.Rune = enchanted;
					break;

				case ServerActionKind.ReappraiseRune:
					if (Find(player, request.RuneId) is not { } reappraised || !RuneReappraisal.Reappraise(player, reappraised))
						return null;
					delta.Rune = reappraised;
					break;

				case ServerActionKind.Summon:
					if (request.Count is not (1 or 10) || !Enum.IsDefined(request.Scroll))
						return null;
					var results = SummonRitual.Perform(random, database, player, request.Count, request.Scroll);
					if (results.Count == 0)
						return null;
					delta.Monsters = results.Select(r => new DeltaMonster { Monster = r.Monster, FirstCopy = r.FirstCopy }).ToList();
					break;

				default:
					return null;
			}

			delta.Essence = player.Essence;
			delta.Scrolls = player.Scrolls;
			delta.LightDarkScrolls = player.LightDarkScrolls;
			delta.LegendaryScrolls = player.LegendaryScrolls;
			delta.ReappraisalGems = player.ReappraisalGems;
			delta.TotalPulls = player.TotalPulls;
			delta.PullsSinceFiveStar = player.PullsSinceFiveStar;
			delta.NextMonsterId = player.NextMonsterId;
			delta.Tools = player.Tools.ToList();
			return delta;
		}

		/// <summary>Os números da conta agora, para <see cref="ApplyDelta"/> saber o que o servidor mudou desde então.</summary>
		public static ActionDelta Snapshot(PlayerState player) => new()
		{
			Essence = player.Essence,
			Scrolls = player.Scrolls,
			LightDarkScrolls = player.LightDarkScrolls,
			LegendaryScrolls = player.LegendaryScrolls,
			ReappraisalGems = player.ReappraisalGems,
			TotalPulls = player.TotalPulls,
			PullsSinceFiveStar = player.PullsSinceFiveStar,
			NextMonsterId = player.NextMonsterId,
			Tools = player.Tools.ToList(),
		};

		/// <summary>
		/// Leva o que o servidor fez à cópia da conta no jogo. <paramref name="baseline"/> é <see cref="Snapshot"/> de quando o
		/// pedido saiu: o que o servidor gastou ou ganhou entra como diferença (o que o jogo ganhou enquanto esperava não se
		/// perde), a runa muda no lugar, e os monstros novos pegam os próximos ids do jogo.
		/// </summary>
		public static void ApplyDelta(PlayerState player, ActionDelta delta, ActionDelta baseline)
		{
			player.Essence += delta.Essence - baseline.Essence;
			player.Scrolls += delta.Scrolls - baseline.Scrolls;
			player.LightDarkScrolls += delta.LightDarkScrolls - baseline.LightDarkScrolls;
			player.LegendaryScrolls += delta.LegendaryScrolls - baseline.LegendaryScrolls;
			player.ReappraisalGems += delta.ReappraisalGems - baseline.ReappraisalGems;
			player.TotalPulls = delta.TotalPulls;
			player.PullsSinceFiveStar = delta.PullsSinceFiveStar;

			var left = delta.Tools.ToList();
			foreach (var tool in baseline.Tools)
			{
				if (!left.Remove(tool))
					player.Tools.Remove(tool);
			}

			if (delta.Rune is { } rune && Find(player, rune.Id) is { } own)
			{
				own.Level = rune.Level;
				own.Innate = rune.Innate;
				own.Substats = rune.Substats;
			}

			foreach (var entry in delta.Monsters)
			{
				entry.Monster.Id = player.NextMonsterId++;
				entry.Monster.Stored = Roster.IsFull(player);
				player.Monsters.Add(entry.Monster);
			}
		}

		/// <summary>Os monstros da invocação como resultados, com a definição de cada um.</summary>
		public static IReadOnlyList<SummonResult> Results(GameDatabase database, ActionDelta delta) =>
			delta.Monsters.Select(entry => new SummonResult(database.Summon(entry.Monster.SummonId), entry.Monster, entry.FirstCopy)).ToList();

		private static Rune? Find(PlayerState player, int id) => player.Runes.FirstOrDefault(r => r.Id == id);
	}
}
