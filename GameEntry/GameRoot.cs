using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Summoning;
using Sigilos.UI;
using Sigilos.UI.Screens;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// Nó raiz (Scenes/GameRoot.tscn) e único lugar que junta as peças: carrega os dados, os textos e o
	/// save, decide qual tela está no ar e aplica as regras do Core quando uma tela pede.
	///
	/// <b>As telas não o conhecem.</b> Cada uma recebe o que mostra e avisa por evento o que o jogador
	/// escolheu; quem muda o <see cref="PlayerState"/> e salva é esta classe.
	///
	/// Argumentos de desenvolvimento (depois de <c>--</c>): <c>--save=nome</c> usa outro arquivo de
	/// save; <c>--idioma=nome</c> usa Data/texts/nome.json;
	/// <c>--tela=campanha|masmorras|invocar|loja|monstros|equipes|runas|compendio|grimorio|batalha</c> abre essa tela direto.
	/// </summary>
	public partial class GameRoot : Node
	{
		private const string DefaultSlot = "sigilos";

		private readonly Random _random = new();
		private Control _ui = null!;
		private Control? _screen;
		private GameDatabase _database = null!;
		private SaveStore _store = null!;
		private PlayerState _player = null!;

		public override void _Ready()
		{
			ContentLoader.LoadTexts(Argument("--idioma=") ?? "pt-BR");
			_database = ContentLoader.Load();
			_store = new SaveStore(Argument("--save=") ?? DefaultSlot);
			_player = _store.Load() ?? NewGame.Create(DateTime.Now, _random);

			_ui = new Control { Theme = GameTheme.Build() };
			_ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			AddChild(_ui);

			switch (Argument("--tela="))
			{
				case "campanha":
					ShowCampaign();
					break;
				case "masmorras":
					ShowDungeons(null);
					break;
				case "invocar":
					ShowSummon();
					break;
				case "loja":
					ShowShop(ShowHub);
					break;
				case "monstros":
					ShowStorage(null);
					break;
				case "equipes":
					ShowTeams(Teams.Campaign, ShowHub);
					break;
				case "runas":
					ShowRunes(Teams.Of(_player, Teams.Campaign).FirstOrDefault(), ShowHub);
					break;
				case "compendio":
					ShowCompendium();
					break;
				case "grimorio":
					ShowGrimoire();
					break;
				case "batalha":
					FightStage(_database.Stage(Math.Min(_player.HighestStage + 1, _database.Stages.Count)));
					break;
				default:
					ShowHub();
					break;
			}
		}

		public override void _Notification(int what)
		{
			if (what == NotificationWMCloseRequest)
				Save();
		}

		// Telas -------------------------------------------------------------------------------------

		private void ShowHub()
		{
			var hub = new HubScreen(_database, _player);
			hub.CampaignRequested += ShowCampaign;
			hub.DungeonsRequested += () => ShowDungeons(null);
			hub.SummonRequested += ShowSummon;
			hub.StorageRequested += () => ShowStorage(null);
			hub.TeamsRequested += () => ShowTeams(Teams.Campaign, ShowHub);
			hub.RunesRequested += () => ShowRunes(null, ShowHub);
			hub.CompendiumRequested += ShowCompendium;
			hub.GrimoireRequested += ShowGrimoire;
			hub.ShopRequested += () => ShowShop(ShowHub);
			hub.CollectRequested += () => Change(() => Idle.Collect(_player, DateTime.Now), () => hub.Refresh(DateTime.Now));
			hub.QuickChannelRequested += () => Change(() => Idle.QuickChannel(_player, DateTime.Now), () => hub.Refresh(DateTime.Now));
			Swap(hub);
		}

		private void ShowCampaign() => ShowCampaign(null, null);

		/// <summary>A Campanha na fase <paramref name="selected"/> (nula: a próxima a vencer), com um aviso embaixo.</summary>
		private void ShowCampaign(int? selected, string? message)
		{
			var campaign = new CampaignScreen(_database, _player, selected);
			campaign.BackRequested += ShowHub;
			campaign.FightRequested += FightStage;
			campaign.TeamRequested += () => ShowTeams(Teams.Campaign, () => ShowCampaign(campaign.Selected, null));
			campaign.ShopRequested += () => ShowShop(() => ShowCampaign(campaign.Selected, null));
			campaign.ResolveRequested += stage =>
			{
				campaign.ShowMessage(ResolveStage(stage));
				campaign.Refresh();
			};
			Swap(campaign);
			if (message != null)
				campaign.ShowMessage(message);
		}

		private void ShowDungeons(string? selected, string? message = null)
		{
			var dungeons = new DungeonScreen(_database, _player, selected);
			dungeons.BackRequested += ShowHub;
			dungeons.FightRequested += FightFloor;
			dungeons.TeamRequested += dungeon => ShowTeams(dungeon.Id, () => ShowDungeons(dungeon.Id));
			dungeons.ShopRequested += dungeon => ShowShop(() => ShowDungeons(dungeon.Id));
			dungeons.ResolveRequested += (dungeon, floor) =>
			{
				dungeons.ShowMessage(ResolveFloor(dungeon, floor));
				dungeons.Refresh();
			};
			Swap(dungeons);
			if (message != null)
				dungeons.ShowMessage(message);
		}

		private void ShowSummon()
		{
			var summon = new SummonScreen(_database, _player);
			summon.BackRequested += ShowHub;
			summon.ShopRequested += () => ShowShop(ShowSummon);
			summon.SummonRequested += count =>
			{
				var results = SummonRitual.Perform(_random, _database, _player, count);
				if (results.Count == 0)
					return;

				Teams.FillCampaign(_player, results.Select(r => r.Monster).OrderByDescending(m => _database.Summon(m.SummonId).Rarity));
				Save();
				summon.ShowResults(results);
			};
			Swap(summon);
		}

		private void ShowStorage(int? selected)
		{
			var storage = new StorageScreen(_database, _player, selected);
			storage.BackRequested += ShowHub;
			storage.RunesRequested += id => ShowRunes(id, () => ShowStorage(id));
			storage.InfuseRequested += (id, toMax) => Change(() =>
			{
				var monster = _player.Monster(id)!;
				Leveling.Infuse(_player, monster, toMax ? int.MaxValue : Leveling.Missing(monster));
			}, storage.Refresh);
			storage.AwakenRequested += id => Change(() => Awakening.Awaken(_player, _player.Monster(id)!, _database.Summon(_player.Monster(id)!.SummonId)), storage.Refresh);
			storage.StoreRequested += id => Change(() => Roster.Store(_player, id), storage.Refresh);
			storage.RetrieveRequested += id => Change(() => Roster.Retrieve(_player, id), storage.Refresh);
			storage.FuseRequested += (target, materials) => Change(() => Fusion.FuseMany(_player, target, materials), storage.Refresh);
			storage.ReleaseRequested += ids => Change(() => Fusion.ReleaseMany(_player, _database, ids), storage.Refresh);
			Swap(storage);
		}

		private void ShowShop(Action back)
		{
			var shop = new ShopScreen(_database, _player);
			shop.BackRequested += back;
			shop.BuyRequested += offer => Change(() =>
			{
				if (Shop.Buy(_player, offer))
					shop.ShowMessage(T("loja.comprado", Texts.Amount(offer.Item, offer.Amount)));
			}, shop.Refresh);
			Swap(shop);
		}

		private void ShowTeams(string content, Action back)
		{
			var teams = new TeamScreen(_database, _player, content);
			teams.BackRequested += back;
			teams.ToggleRequested += (key, id) => Change(() => Teams.Toggle(_player, key, id), teams.Refresh);
			teams.LeaderRequested += (key, id) => Change(() => Teams.MakeLeader(_player, key, id), teams.Refresh);
			Swap(teams);
		}

		private void ShowRunes(int? monsterId, Action back)
		{
			var runes = new RuneScreen(_database, _player, monsterId);
			runes.BackRequested += _ => back();
			runes.EquipRequested += (id, monster) => Change(() => RuneInventory.Equip(_player, Rune(id), monster), runes.Refresh);
			runes.UnequipRequested += id => Change(() => RuneInventory.Unequip(_player, Rune(id)), runes.Refresh);
			runes.UpgradeRequested += (id, target) => Change(() => RuneInventory.Upgrade(_random, _player, Rune(id), target), runes.Refresh);
			runes.GrindRequested += (id, index, tool) => Change(() => RuneInventory.Grind(_random, _player, Rune(id), index, tool), runes.Refresh);
			runes.EnchantRequested += (id, index, tool) => Change(() => RuneInventory.Enchant(_random, _player, Rune(id), index, tool), runes.Refresh);
			runes.SellRequested += id => Change(() => RuneInventory.Sell(_player, Rune(id)), runes.Refresh);
			runes.SellManyRequested += ids => Change(() => RuneInventory.SellAll(_player, _player.Runes.Where(r => ids.Contains(r.Id))), runes.Refresh);
			Swap(runes);
		}

		private void ShowCompendium()
		{
			var compendium = new CompendiumScreen();
			compendium.BackRequested += ShowHub;
			Swap(compendium);
		}

		private void ShowGrimoire()
		{
			var grimoire = new GrimoireScreen(_database, _player);
			grimoire.BackRequested += ShowHub;
			Swap(grimoire);
		}

		// Lutas -------------------------------------------------------------------------------------

		private void FightStage(StageDefinition stage)
		{
			// Fase nova vencida: a Campanha volta já na próxima. Fase repetida: volta nela, para farmar.
			var repeat = Campaign.IsCleared(_player, stage.Number);
			void Back() => ShowCampaign(repeat ? stage.Number : null, null);
			if (NeedsTeam(Teams.Campaign, Back))
				return;

			var problem = Campaign.Check(_player, stage);
			if (problem != EntryProblem.None)
			{
				ShowCampaign(stage.Number, Texts.Refusal(problem, stage.Mana));
				return;
			}

			Fight(T("batalha.titulo_fase", stage.Number, stage.Name), stage.Encounter, Teams.Campaign, () => Campaign.ApplyVictory(_random, _player, stage), Back);
		}

		private void FightFloor(DungeonDefinition dungeon, int floor)
		{
			void Back() => ShowDungeons(dungeon.Id);
			if (NeedsTeam(dungeon.Id, Back))
				return;

			var problem = Dungeons.Check(_player, dungeon, floor);
			if (problem != EntryProblem.None)
			{
				ShowDungeons(dungeon.Id, Texts.Refusal(problem, dungeon.Floor(floor).Mana));
				return;
			}

			Fight(T("batalha.titulo_andar", dungeon.Name, floor), dungeon.Floor(floor).Encounter, dungeon.Id, () => Dungeons.ApplyVictory(_random, _player, dungeon, floor), Back);
		}

		/// <summary>Sem ninguém na equipe do conteúdo, abre a tela de Equipes em vez da luta.</summary>
		private bool NeedsTeam(string content, Action back)
		{
			if (Teams.Of(_player, content).Any(id => _player.Monster(id) is { Stored: false }))
				return false;

			ShowTeams(content, back);
			return true;
		}

		/// <summary>A luta na tela: a vitória cobra a Mana e entrega a recompensa; volta para <paramref name="back"/>.</summary>
		private void Fight(string title, Encounter encounter, string content, Func<VictoryReward> victoryReward, Action back)
		{
			Save();
			var team = PlayerTeam.Build(_player, _database, content);
			var session = BattleFactory.Create(_database, team, encounter, _random.Next());
			var battle = new BattleScreen(session, title, _player.AutoBattle);
			battle.Finished += victory =>
			{
				var reward = victory ? victoryReward() : null;
				Save();
				battle.ShowResult(victory, reward, reward == null ? Array.Empty<string>() : Names(reward.LevelUps), _player.AccountLevel);
			};
			battle.Closed += auto =>
			{
				_player.AutoBattle = auto;
				Save();
				back();
			};
			Swap(battle);
		}

		/// <summary>O botão Resolver: a luta inteira no automático, sem tela. Como na luta, só a vitória cobra Mana.</summary>
		private string ResolveStage(StageDefinition stage)
		{
			var problem = Campaign.Check(_player, stage);
			return problem != EntryProblem.None
				? Texts.Refusal(problem, stage.Mana)
				: Resolve(stage.Encounter, Teams.Campaign, T("geral.fase", stage.Number), () => Campaign.ApplyVictory(_random, _player, stage));
		}

		private string ResolveFloor(DungeonDefinition dungeon, int floor)
		{
			var problem = Dungeons.Check(_player, dungeon, floor);
			return problem != EntryProblem.None
				? Texts.Refusal(problem, dungeon.Floor(floor).Mana)
				: Resolve(dungeon.Floor(floor).Encounter, dungeon.Id, T("geral.andar", dungeon.Name, floor), () => Dungeons.ApplyVictory(_random, _player, dungeon, floor));
		}

		private string Resolve(Encounter encounter, string content, string where, Func<VictoryReward> victoryReward)
		{
			var session = BattleFactory.Create(_database, PlayerTeam.Build(_player, _database, content), encounter, _random.Next());
			if (!AutoBattle.Run(session))
			{
				Save();
				return T("geral.resolver_derrota", where, Math.Min(session.Round, BattleRules.RoundLimit));
			}

			var reward = victoryReward();
			Save();
			var drops = new List<string>();
			if (reward.Gold > 0)
				drops.Add(T("geral.resolver_ouro", reward.Gold));
			if (reward.Rune is { } rune)
				drops.Add(T("geral.resolver_runa", Texts.Name(rune.Set), Texts.Stars(rune.Grade)));
			drops.AddRange(reward.Tools.Select(Texts.Name));
			if (reward.AccountLevels > 0)
				drops.Add(T("geral.resolver_conta", _player.AccountLevel, reward.AccountLevels * Account.LevelUpGold));
			return T("geral.resolver_vitoria", where, reward.Mana, reward.Essence, reward.Experience, drops.Count == 0 ? "" : ", " + string.Join(", ", drops));
		}

		// Infraestrutura ----------------------------------------------------------------------------

		/// <summary>Aplica uma regra, salva e atualiza a tela.</summary>
		private void Change(Action change, Action refresh)
		{
			change();
			Save();
			refresh();
		}

		private IReadOnlyList<string> Names(IEnumerable<int> monsterIds) => monsterIds
			.Select(_player.Monster)
			.OfType<OwnedSummon>()
			.Select(m => _database.Summon(m.SummonId).NameFor(m.Awakened))
			.ToList();

		private Core.Runes.Rune Rune(int id) => _player.Runes.First(r => r.Id == id);

		private void Save() => _store.Save(_player);

		private void Swap(Control screen)
		{
			_screen?.QueueFree();
			_screen = screen;
			_ui.AddChild(screen);
		}

		private static string? Argument(string prefix) => OS.GetCmdlineUserArgs()
			.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
	}
}
