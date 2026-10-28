using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;
using Sigilos.Core.Social;
using Sigilos.Core.Summoning;

namespace Sigilos.Core.Progression
{
	/// <summary>Uma ação que muda a conta (gasta, rende, sorteia ou só organiza) e que o servidor executa: o jogo só pede.</summary>
	public enum ServerActionKind
	{
		UpgradeRune,
		GrindRune,
		EnchantRune,
		ReappraiseRune,
		Summon,
		ClaimMail,
		CollectIdle,
		BuyShop,
		Fuse,
		Release,
		Evolve,
		Awaken,
		Infuse,
		SellRunes,
		EquipRunes,
		UnequipRunes,
		Shelve,
		Team,
		SetAvatar,
		Toggle,
		ExchangeFragments,
		Preference,
		ArenaOpen,
		ArenaRefresh,
		ArenaBuy,
		VeilBuy,

		/// <summary>Abre um kit de sigilos no conjunto de <see cref="ActionRequest.Text"/> (<see cref="RuneKits"/>).</summary>
		OpenRuneKit,

		/// <summary>Melhora as runas de <see cref="ActionRequest.Ids"/> até <see cref="ActionRequest.Target"/>, todas ou nenhuma (<see cref="RuneInventory.UpgradeMany"/>).</summary>
		UpgradeRunes,

		/// <summary>Conta o dia de hoje no check-in e manda a carta do prêmio (<see cref="DailyCheckIn.Open"/>).</summary>
		CheckIn,
	}

	/// <summary>Os gestos de <see cref="ServerActionKind.Team"/>.</summary>
	public enum TeamOp
	{
		Toggle,
		Replace,
		Leader,
		Seed,
	}

	/// <summary>O que o servidor tem à mão para executar uma ação: o relógio dele e as cartas do correio da conta.</summary>
	public sealed record ActionContext(DateTime Now, Func<string, Mail?> Mail)
	{
		/// <summary>As defesas de outros jogadores perto dos pontos dados (o servidor as guarda).</summary>
		public Func<int, IReadOnlyList<ArenaOnlineOpponent>>? Opponents { get; init; }

		/// <summary>Grava a defesa da conta (a equipe e os pontos) para os outros enfrentarem.</summary>
		public Action<int, BattleTeam>? Defense { get; init; }
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

		/// <summary>O monstro ou a runa a que a ação se refere (alvo da fusão, quem recebe os sigilos).</summary>
		public int Id { get; set; }

		/// <summary>O segundo monstro (Time: quem entra no lugar de <see cref="Id"/>).</summary>
		public int Id2 { get; set; }

		/// <summary>Vários monstros ou runas (materiais da fusão, sigilos a vender ou equipar).</summary>
		public List<int> Ids { get; set; } = new();

		/// <summary>Texto da ação: a carta, a oferta da Loja, o conteúdo da equipe, o retrato, a chave da preferência.</summary>
		public string Text { get; set; } = "";

		/// <summary>Um sim ou não da ação: infusão até o máximo, guardar ou tirar do Baú, retrato desperto, valor da preferência.</summary>
		public bool Flag { get; set; }

		public TeamOp Op { get; set; }

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

		public Rune? Rune { get; set; }

		public List<DeltaMonster> Monsters { get; set; } = new();

		/// <summary>O que a ação mudou na conta, como diferença de JSON (<see cref="StatePatch"/>); o jogo aplica na cópia dele.</summary>
		public JsonObject? Patch { get; set; }

		/// <summary>Quantos monstros, runas ou cartas a ação alcançou.</summary>
		public int Count { get; set; }

		/// <summary>Quanto de Ouro ou Essência a ação rendeu (venda, infusão).</summary>
		public int Gained { get; set; }

		/// <summary>A canalização coletada.</summary>
		public IdleReward? Idle { get; set; }

		/// <summary>Os sigilos que a ação criou (kit de sigilos).</summary>
		public List<Rune> Runes { get; set; } = new();

		/// <summary>Os feitos que a ação rendeu: o servidor os anuncia no Chat depois de gravar. Não viajam ao jogo.</summary>
		[JsonIgnore]
		public List<Feat> Feats { get; } = new();

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
	/// Toda mudança na conta fora das lutas: o servidor executa, com o relógio dele (o save de teste roda as mesmas
	/// regras no aparelho, sem rede). <see cref="Apply"/> muda o estado dado e descreve a mudança; <see cref="StatePatch"/>
	/// leva essa descrição à cópia do jogo, que só lê.
	/// </summary>
	public static class ServerActions
	{
		/// <summary>Faz a ação em <paramref name="player"/>. Nulo, sem mudar nada, se o pedido não vale (sem Essência, runa que não existe...).</summary>
		public static ActionDelta? Apply(Random random, GameDatabase database, PlayerState player, ActionRequest request, ActionContext? context = null)
		{
			var delta = new ActionDelta { Kind = request.Kind };
			var before = StatePatch.Snapshot(player);
			var now = context?.Now ?? DateTime.UtcNow;
			switch (request.Kind)
			{
				case ServerActionKind.UpgradeRune:
					if (Find(player, request.RuneId) is not { } upgraded)
						return null;
					var levelBefore = upgraded.Level;
					if (!RuneInventory.Upgrade(random, player, upgraded, request.Target))
						return null;
					delta.Rune = upgraded;
					if (Feats.Of(upgraded, levelBefore) is { } runeFeat)
						delta.Feats.Add(runeFeat);
					break;

				case ServerActionKind.UpgradeRunes:
					var raised = RuneInventory.UpgradeMany(random, player, player.Runes.Where(r => request.Ids.Contains(r.Id)), request.Target);
					if ((delta.Count = raised.Count) == 0)
						return null;
					delta.Feats.AddRange(raised.Select(r => Feats.Of(r.Rune, r.LevelBefore)).OfType<Feat>());
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
					Teams.FillCampaign(player, results.Select(r => r.Monster).OrderByDescending(m => database.Summon(m.SummonId).Rarity));
					delta.Monsters = results.Select(r => new DeltaMonster { Monster = r.Monster, FirstCopy = r.FirstCopy }).ToList();
					delta.Feats.AddRange(Feats.Of(results));
					break;

				case ServerActionKind.ClaimMail:
					if (Letters.Find(player, request.Text) is { } letter)
					{
						if (!Letters.Claim(random, database, player, letter, now))
							return null;
					}
					else if (context?.Mail(request.Text) is not { } mail || !Mailbox.Claim(player, mail, database, random))
						return null;
					delta.Count = 1;
					break;

				case ServerActionKind.CollectIdle:
					delta.Idle = Idle.Collect(player, now);
					break;

				case ServerActionKind.BuyShop:
					if (database.Shop.FirstOrDefault(o => o.Id == request.Text) is not { } offer || !Shop.Buy(player, offer))
						return null;
					break;

				case ServerActionKind.Fuse:
					if ((delta.Count = Fusion.FuseMany(random, player, database, request.Id, request.Ids)) == 0)
						return null;
					break;

				case ServerActionKind.Release:
					if ((delta.Count = Fusion.ReleaseMany(player, database, request.Ids)) == 0)
						return null;
					break;

				case ServerActionKind.Evolve:
					if (player.Monster(request.Id) is not { } evolved || !Evolution.Evolve(player, evolved))
						return null;
					SixStar.Reached(player, database, evolved, now);
					break;

				case ServerActionKind.Awaken:
					if (player.Monster(request.Id) is not { } awakened || !database.HasSummon(awakened.SummonId) || !Awakening.Awaken(player, awakened, database.Summon(awakened.SummonId)))
						return null;
					break;

				case ServerActionKind.Infuse:
					if (player.Monster(request.Id) is not { } infused)
						return null;
					var level = infused.Level;
					if ((delta.Gained = Leveling.Infuse(player, infused, request.Flag ? int.MaxValue : Leveling.InfuseCosts(infused).Next)) <= 0)
						return null;
					delta.Count = infused.Level - level;
					break;

				case ServerActionKind.SellRunes:
					if ((delta.Gained = RuneInventory.SellAll(player, player.Runes.Where(r => request.Ids.Contains(r.Id)))) <= 0)
						return null;
					break;

				case ServerActionKind.EquipRunes:
					if (player.Monster(request.Id) == null || (delta.Count = RuneInventory.EquipMany(player, player.Runes.Where(r => request.Ids.Contains(r.Id)), request.Id)) == 0)
						return null;
					break;

				case ServerActionKind.UnequipRunes:
					delta.Count = player.Runes.Where(r => request.Ids.Contains(r.Id)).ToList().Count(r => RuneInventory.Unequip(player, r));
					if (delta.Count == 0)
						return null;
					break;

				case ServerActionKind.Shelve:
					delta.Count = request.Flag ? Roster.StoreMany(player, request.Ids) : Roster.RetrieveMany(player, request.Ids);
					if (delta.Count == 0)
						return null;
					break;

				case ServerActionKind.Team:
					if (!Team(player, request))
						return null;
					break;

				case ServerActionKind.SetAvatar:
					if (!Account.SetAvatar(player, request.Text, request.Flag))
						return null;
					break;

				case ServerActionKind.Toggle:
					if (!Flip(player, request))
						return null;
					break;

				case ServerActionKind.ExchangeFragments:
					if (FragmentExchange.Exchange(player, database, request.Text) is not { } exchanged)
						return null;
					delta.Monsters = new List<DeltaMonster> { new() { Monster = exchanged.Monster, FirstCopy = exchanged.FirstCopy } };
					break;

				case ServerActionKind.ArenaOpen:
					delta.Count = ArenaOpen(random, database, player, context, now) ? 1 : 0;
					break;

				case ServerActionKind.CheckIn:
					if (DailyCheckIn.Open(player, database, now) == null)
						return null;
					break;

				case ServerActionKind.ArenaRefresh:
					Arena.RefreshList(player, random);
					break;

				case ServerActionKind.ArenaBuy:
					if (database.Arena.Shop.FirstOrDefault(o => o.Id == request.Text) is not { } cosmic || !Arena.Buy(player, database.Arena, cosmic, now))
						return null;
					break;

				case ServerActionKind.VeilBuy:
					if (database.Arena.VeilShop.FirstOrDefault(o => o.Id == request.Text) is not { } veil || !Veil.Buy(player, database.Arena, veil, now))
						return null;
					break;

				case ServerActionKind.Preference:
					if (!Preferences.Set(player, database, request.Text, request.Flag))
						return null;
					break;

				case ServerActionKind.OpenRuneKit:
					if (!Enum.TryParse<RuneSet>(request.Text, out var kitSet) || !Enum.IsDefined(kitSet) || (delta.Runes = RuneKits.Open(random, player, kitSet).ToList()).Count == 0)
						return null;
					break;

				default:
					return null;
			}

			delta.Patch = StatePatch.Diff(before, player);
			return delta;
		}

		/// <summary>Os monstros da invocação como resultados, com a definição de cada um.</summary>
		public static IReadOnlyList<SummonResult> Results(GameDatabase database, ActionDelta delta) =>
			delta.Monsters.Select(entry => new SummonResult(database.Summon(entry.Monster.SummonId), entry.Monster, entry.FirstCopy)).ToList();

		/// <summary>Põe a Arena em dia (luta pendente vira derrota, Convites, semana) e completa a lista com as defesas de outros jogadores. Devolve se uma luta deixada pela metade foi perdida.</summary>
		private static bool ArenaOpen(Random random, GameDatabase database, PlayerState player, ActionContext? context, DateTime now)
		{
			var abandoned = player.Arena.Pending != null;
			Teams.SeedFromCampaign(player, Teams.ArenaDefense);
			Teams.SeedFromCampaign(player, Teams.Arena);
			Arena.Renew(player, database.Arena, now, random);
			var defense = PlayerTeam.Build(player, database, Teams.ArenaDefense);
			if (defense.Members.Count > 0)
				context?.Defense?.Invoke(player.Arena.Points, defense);

			if (Arena.NeedsOnline(player) && context?.Opponents != null)
				Arena.SetOnline(player, database.Arena, context.Opponents(player.Arena.Points));

			return abandoned;
		}

		private static bool Team(PlayerState player, ActionRequest request) => request.Op switch
		{
			TeamOp.Toggle => Teams.Toggle(player, request.Text, request.Id),
			TeamOp.Replace => Teams.Replace(player, request.Text, request.Id, request.Id2),
			TeamOp.Leader => Teams.MakeLeader(player, request.Text, request.Id),
			TeamOp.Seed => Teams.SeedFromCampaign(player, request.Text),
			_ => false,
		};

		/// <summary>O cadeado de uma runa (<see cref="ActionRequest.Flag"/> falso) ou o cadeado ou favorito de um monstro (<see cref="ActionRequest.Index"/> 0 ou 1).</summary>
		private static bool Flip(PlayerState player, ActionRequest request)
		{
			if (!request.Flag)
			{
				if (Find(player, request.Id) is not { } rune)
					return false;
				rune.Locked = !rune.Locked;
				return true;
			}

			if (player.Monster(request.Id) is not { } monster)
				return false;
			if (request.Index == 1)
				monster.Favorite = !monster.Favorite;
			else
				monster.Locked = !monster.Locked;
			return true;
		}

		private static Rune? Find(PlayerState player, int id) => player.Runes.FirstOrDefault(r => r.Id == id);
	}
}
