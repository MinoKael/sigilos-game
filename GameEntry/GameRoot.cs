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
using Sigilos.UI.Components;
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
	/// A navegação: o Santuário leva à escolha de batalha (Campanha, Masmorras), à Invocação e, pela
	/// barra de baixo, a Monstros, Runas, Equipes, Loja, Grimório, Compêndio e Ajustes; cada tela volta
	/// para onde veio (seta, Esc ou o Voltar do celular). A Batalha automática roda fora das telas
	/// (<see cref="AutoBattleRunner"/>), com o aviso flutuante no alto de todas.
	///
	/// Argumentos de desenvolvimento (depois de <c>--</c>): <c>--save=nome</c> usa outro arquivo de
	/// save; <c>--language=nome</c> usa Data/texts/nome.json (padrão: o da Configuração, ou en);
	/// <c>--screen=map|campaign|dungeons|summon|shop|monsters|teams|runes|compendium|grimoire|battle</c> abre essa tela direto.
	/// </summary>
	public partial class GameRoot : Node
	{
		private const string DefaultSlot = "sigilos";

		private readonly Random _random = new();
		private readonly AutoBattleRunner _runner = new();
		private readonly AutoBattleBadge _badge = new();
		private Control _ui = null!;
		private Control _screens = null!;
		private Control? _screen;
		private GameDatabase _database = null!;
		private SaveStore _store = null!;
		private PlayerState _player = null!;
		private string _language = ContentLoader.BaseLanguage;

		/// <summary>Remonta a tela de agora (depois de uma janela que mudou algo, ou para voltar a ela).</summary>
		private Action _current = () => { };

		// Para onde cada tela de conteúdo volta: vale também depois de uma luta ou da Loja.
		private Action _campaignBack = null!;
		private Action _dungeonsBack = null!;
		private Action _storageBack = null!;
		private Action _summonBack = null!;

		public override void _Ready()
		{
			_campaignBack = _dungeonsBack = ShowMap;
			_storageBack = _summonBack = ShowHub;
			// Os textos vêm antes dos dados: os nomes dos dados saem no idioma deles.
			_store = new SaveStore(Argument("--save=") ?? DefaultSlot);
			var saved = _store.Load();
			_language = Argument("--language=") ?? saved?.Language ?? ContentLoader.BaseLanguage;
			ContentLoader.LoadTexts(_language);
			_database = ContentLoader.Load();
			_player = saved ?? NewGame.Create(DateTime.Now, _random, _database);
			UiSession.Database = _database;
			UiSession.Player = _player;

			// O Voltar do celular vira Esc (ui_cancel): fecha a janela de cima ou volta de tela.
			GetTree().QuitOnGoBack = false;

			_ui = new Control { Name = "UI", Theme = GameTheme.Build() };
			_ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			AddChild(_ui);
			_screens = new Control { Name = "Screens", MouseFilter = Control.MouseFilterEnum.Ignore };
			_screens.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			_ui.AddChild(_screens);
			_ui.AddChild(_badge);
			_badge.Pressed += OpenAutoBattle;
			AddChild(_runner);
			_runner.RunChanged += run => _badge.Show(run);

			switch (Argument("--screen="))
			{
				case "map":
					ShowMap();
					break;
				case "campaign":
					Go(Destination.Campaign, ShowMap);
					break;
				case "dungeons":
					Go(Destination.Dungeons, ShowMap);
					break;
				case "summon":
					Go(Destination.Summon, ShowHub);
					break;
				case "shop":
					Go(Destination.Shop, ShowHub);
					break;
				case "monsters":
					Go(Destination.Monsters, ShowHub);
					break;
				case "teams":
					Go(Destination.Teams, ShowHub);
					break;
				case "runes":
					ShowRunes(Teams.Of(_player, Teams.Campaign).FirstOrDefault(), ShowHub);
					break;
				case "compendium":
					Go(Destination.Compendium, ShowHub);
					break;
				case "grimoire":
					Go(Destination.Grimoire, ShowHub);
					break;
				case "battle":
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
			else if (what == NotificationWMGoBackRequest)
				Input.ParseInputEvent(new InputEventAction { Action = "ui_cancel", Pressed = true });
			else if (what == NotificationApplicationPaused)
				Save();
		}

		// Telas -------------------------------------------------------------------------------------

		private void ShowHub()
		{
			var hub = new HubScreen(_database, _player);
			hub.Requested += destination => Go(destination, ShowHub);
			hub.ConfigRequested += () => ConfigPanel.Open(hub, ContentLoader.Languages(), _language, language =>
			{
				_language = language;
				_player.Language = language;
				Save();
				ContentLoader.LoadTexts(language);
				_database = ContentLoader.Load();
				UiSession.Database = _database;
				_badge.Show(_runner.Run);
				ShowHub();
			});
			hub.CollectRequested += () => Change(() => Idle.Collect(_player, DateTime.Now), () => hub.Refresh(DateTime.Now));
			Swap(hub, ShowHub);
		}

		private void ShowMap()
		{
			var map = new MapScreen(_database, _player);
			map.BackRequested += ShowHub;
			map.Requested += destination => Go(destination, ShowMap);
			Swap(map, ShowMap);
		}

		/// <summary>Abre um destino de navegação; <paramref name="back"/> é para onde ele volta.</summary>
		private void Go(Destination destination, Action back)
		{
			switch (destination)
			{
				case Destination.Campaign:
					_campaignBack = back;
					ShowCampaign(null, null);
					break;
				case Destination.Dungeons:
					_dungeonsBack = back;
					ShowDungeons(null);
					break;
				case Destination.Summon:
					_summonBack = back;
					ShowSummon();
					break;
				case Destination.Monsters:
					_storageBack = back;
					ShowStorage(null);
					break;
				case Destination.Runes:
					ShowRunes(null, back);
					break;
				case Destination.Teams:
					ShowTeams(Teams.Campaign, back);
					break;
				case Destination.Shop:
					ShowShop(back);
					break;
				case Destination.Compendium:
					ShowCompendium(back);
					break;
				case Destination.Grimoire:
					ShowGrimoire(back);
					break;
				case Destination.Map:
					ShowMap();
					break;
			}
		}

		/// <summary>A Campanha na fase <paramref name="selected"/> (nula: a próxima a vencer), com um aviso embaixo.</summary>
		private void ShowCampaign(int? selected, string? message)
		{
			var campaign = new CampaignScreen(_database, _player, selected);
			campaign.BackRequested += () => _campaignBack();
			campaign.FightRequested += FightStage;
			campaign.TeamRequested += () => ShowTeams(Teams.Campaign, () => ShowCampaign(campaign.Selected, null));
			campaign.ShopRequested += () => ShowShop(() => ShowCampaign(campaign.Selected, null));
			campaign.RepeatRequested += stage => StartAutoBattle(
				campaign,
				T("battle.title_stage", stage.Number, stage.Name),
				Teams.Campaign,
				() => Campaign.Check(_player, stage),
				stage.Mana,
				stage.Encounter,
				() => Campaign.ApplyVictory(_random, _player, stage, _database),
				() => ShowCampaign(stage.Number, null));
			Swap(campaign, () => ShowCampaign(campaign.Selected, null));
			if (message != null)
				campaign.ShowMessage(message);
		}

		private void ShowDungeons(string? selected, string? message = null)
		{
			var dungeons = new DungeonScreen(_database, _player, selected);
			dungeons.BackRequested += () => _dungeonsBack();
			dungeons.FightRequested += FightFloor;
			dungeons.TeamRequested += dungeon => ShowTeams(dungeon.Id, () => ShowDungeons(dungeon.Id));
			dungeons.ShopRequested += dungeon => ShowShop(() => ShowDungeons(dungeon.Id));
			dungeons.RepeatRequested += (dungeon, floor) => StartAutoBattle(
				dungeons,
				T("battle.title_floor", dungeon.Name, floor),
				dungeon.Id,
				() => Dungeons.Check(_player, dungeon, floor),
				dungeon.Floor(floor).Mana,
				dungeon.Floor(floor).Encounter,
				() => Dungeons.ApplyVictory(_random, _player, dungeon, floor),
				() => ShowDungeons(dungeon.Id));
			Swap(dungeons, () => ShowDungeons(selected));
			if (message != null)
				dungeons.ShowMessage(message);
		}

		private void ShowSummon()
		{
			var summon = new SummonScreen(_database, _player);
			summon.BackRequested += () => _summonBack();
			summon.ShopRequested += () => ShowShop(ShowSummon);
			summon.MonstersRequested += () =>
			{
				_storageBack = ShowSummon;
				ShowStorage(null);
			};
			summon.SummonRequested += count =>
			{
				var results = SummonRitual.Perform(_random, _database, _player, count);
				if (results.Count == 0)
					return;

				Teams.FillCampaign(_player, results.Select(r => r.Monster).OrderByDescending(m => _database.Summon(m.SummonId).Rarity));
				Save();
				summon.ShowResults(results);
			};
			Swap(summon, ShowSummon);
		}

		private void ShowStorage(int? selected)
		{
			var storage = new StorageScreen(_database, _player, selected);
			storage.BackRequested += () => _storageBack();
			storage.RunesRequested += id => ShowRunes(id, () => ShowStorage(id));
			storage.InfuseRequested += (id, toMax) => Change(() =>
			{
				var monster = _player.Monster(id)!;
				Leveling.Infuse(_player, monster, toMax ? int.MaxValue : Leveling.InfuseCosts(monster).Next);
			}, () => storage.Refresh());
			storage.AwakenRequested += id => Change(() => Awakening.Awaken(_player, _player.Monster(id)!, _database.Summon(_player.Monster(id)!.SummonId)), () => storage.Refresh());
			storage.EvolveRequested += id => Change(() => Evolution.Evolve(_player, _player.Monster(id)!), () => storage.Refresh());
			storage.StoreRequested += id => Change(() => Roster.Store(_player, id), () => storage.Refresh());
			storage.RetrieveRequested += id => Change(() => Roster.Retrieve(_player, id), () => storage.Refresh());
			storage.FuseRequested += (target, materials) => Change(() => Fusion.FuseMany(_random, _player, _database, target, materials), () => storage.Refresh(true));
			storage.ReleaseRequested += ids => Change(() => Fusion.ReleaseMany(_player, _database, ids), () => storage.Refresh());
			storage.LockRequested += id => Change(() => _player.Monster(id)!.Locked = !_player.Monster(id)!.Locked, () => storage.Refresh());
			Swap(storage, () => ShowStorage(selected));
		}

		private void ShowShop(Action back)
		{
			var shop = new ShopScreen(_database, _player);
			shop.BackRequested += back;
			shop.BuyRequested += offer => Change(() =>
			{
				if (Shop.Buy(_player, offer))
					shop.ShowMessage(T("shop.bought", Texts.Amount(offer.Item, offer.Amount)));
			}, shop.Refresh);
			Swap(shop, () => ShowShop(back));
		}

		private void ShowTeams(string content, Action back)
		{
			var teams = new TeamScreen(_database, _player, content);
			teams.BackRequested += back;
			teams.ToggleRequested += (key, id) => Change(() => Teams.Toggle(_player, key, id), teams.Refresh);
			teams.LeaderRequested += (key, id) => Change(() => Teams.MakeLeader(_player, key, id), teams.Refresh);
			Swap(teams, () => ShowTeams(content, back));
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
			runes.LockRequested += id => Change(() => Rune(id).Locked = !Rune(id).Locked, runes.Refresh);
			Swap(runes, () => ShowRunes(monsterId, back));
		}

		private void ShowCompendium(Action back)
		{
			var compendium = new CompendiumScreen();
			compendium.BackRequested += back;
			Swap(compendium, () => ShowCompendium(back));
		}

		private void ShowGrimoire(Action back)
		{
			var grimoire = new GrimoireScreen(_database, _player);
			grimoire.BackRequested += back;
			Swap(grimoire, () => ShowGrimoire(back));
		}

		// Lutas -------------------------------------------------------------------------------------

		private void FightStage(StageDefinition stage)
		{
			// Fase nova vencida: a Campanha volta já na próxima. Fase repetida: volta nela, para farmar.
			var repeat = Campaign.IsCleared(_player, stage.Number);
			void Back() => ShowCampaign(repeat ? stage.Number : null, null);
			if (NeedsTeam(Teams.Campaign, Back) || BusyWithAutoBattle(() => FightStage(stage)))
				return;

			var problem = Campaign.Check(_player, stage);
			if (problem != EntryProblem.None)
			{
				ShowCampaign(stage.Number, Texts.Refusal(problem, stage.Mana));
				return;
			}

			Fight(T("battle.title_stage", stage.Number, stage.Name), stage.Encounter, Teams.Campaign, Records.StageKey(stage.Number), () => Campaign.ApplyVictory(_random, _player, stage, _database), Back, () => FightStage(stage));
		}

		private void FightFloor(DungeonDefinition dungeon, int floor)
		{
			void Back() => ShowDungeons(dungeon.Id);
			if (NeedsTeam(dungeon.Id, Back) || BusyWithAutoBattle(() => FightFloor(dungeon, floor)))
				return;

			var problem = Dungeons.Check(_player, dungeon, floor);
			if (problem != EntryProblem.None)
			{
				ShowDungeons(dungeon.Id, Texts.Refusal(problem, dungeon.Floor(floor).Mana));
				return;
			}

			Fight(T("battle.title_floor", dungeon.Name, floor), dungeon.Floor(floor).Encounter, dungeon.Id, Records.FloorKey(dungeon.Id, floor), () => Dungeons.ApplyVictory(_random, _player, dungeon, floor), Back, () => FightFloor(dungeon, floor));
		}

		/// <summary>Sem ninguém na equipe do conteúdo, abre a tela de Equipes em vez da luta.</summary>
		private bool NeedsTeam(string content, Action back)
		{
			if (Teams.Of(_player, content).Any(id => _player.Monster(id) is { Stored: false }))
				return false;

			ShowTeams(content, back);
			return true;
		}

		/// <summary>
		/// Uma luta na tela não divide a equipe com a Batalha automática: com uma rodando, pergunta se é
		/// para parar e lutar (<paramref name="then"/>). Verdadeiro quando a luta não começa agora.
		/// </summary>
		private bool BusyWithAutoBattle(Action then)
		{
			if (!_runner.Running)
				return false;

			Dialog.Confirm(_ui, T("auto.busy_title"), T("auto.busy_text"), T("auto.busy_confirm"), () =>
			{
				_runner.Dismiss();
				then();
			}, ButtonKind.Danger);
			return true;
		}

		/// <summary>
		/// A luta na tela: a vitória cobra a Mana, entrega a recompensa e grava o tempo no recorde
		/// <paramref name="record"/>; o resultado mostra a experiência de cada monstro subindo do ponto em que
		/// estava. Volta para <paramref name="back"/>; <paramref name="again"/> é a mesma luta de novo, pela
		/// porta de entrada (confere Mana e equipe).
		/// </summary>
		private void Fight(string title, Encounter encounter, string content, string record, Func<VictoryReward> victoryReward, Action back, Action again)
		{
			Save();
			var team = PlayerTeam.Build(_player, _database, content);
			var session = BattleFactory.Create(_database, team, encounter, _random.Next());
			var battle = new BattleScreen(session, title, _player.AutoBattle);
			var finished = false;
			battle.Finished += victory =>
			{
				finished = true;
				var before = Teams.Of(_player, content)
					.Select(_player.Monster)
					.OfType<OwnedSummon>()
					.Where(m => !m.Stored && _database.HasSummon(m.SummonId))
					.Select(m => (Monster: m, m.Level, m.Experience))
					.ToList();
				var reward = victory ? victoryReward() : null;
				var newBest = victory && Records.Submit(_player, record, battle.Elapsed);
				Save();
				var result = before.Select(b => ResultOf(b.Monster, b.Level, b.Experience)).ToList();
				battle.ShowResult(new BattleOutcome(victory, reward, result, _player.AccountLevel, Records.Best(_player, record), newBest));
			};
			battle.RuneSellRequested += rune =>
			{
				RuneInventory.Sell(_player, rune);
				Save();
			};
			battle.RuneLockRequested += rune =>
			{
				rune.Locked = true;
				Save();
			};
			battle.Closed += auto =>
			{
				_player.AutoBattle = auto;
				Save();
				back();
			};
			battle.RestartRequested += auto =>
			{
				// A Mana só sai na vitória: recomeçar no meio é abrir a mesma luta, com outra semente; depois
				// do fim, é entrar de novo pela porta (que confere a Mana).
				_player.AutoBattle = auto;
				if (finished)
					again();
				else
					Fight(title, encounter, content, record, victoryReward, back, again);
			};
			Swap(battle, back);
		}

		// Batalha automática ------------------------------------------------------------------------

		/// <summary>
		/// Abre a escolha de quantas lutas e começa a Batalha automática, que segue sozinha fora das telas;
		/// a janela dela abre em seguida (e pode ser fechada sem parar nada). Já havendo uma rodando,
		/// pergunta antes de trocar.
		/// </summary>
		private void StartAutoBattle(Control from, string title, string content, Func<EntryProblem> check, int mana, Encounter encounter, Func<VictoryReward> victory, Action back)
		{
			if (NeedsTeam(content, back))
				return;

			void Setup() => AutoBattleSetup.Open(from, title, mana, _player.Mana, runs =>
			{
				var run = new AutoBattleRun(title, content, mana, runs);
				_runner.Start(run, check, () =>
				{
					var session = BattleFactory.Create(_database, PlayerTeam.Build(_player, _database, content), encounter, _random.Next());
					var log = new List<BattleEvent>();
					var won = AutoBattle.Run(session, log);
					return (won, BattlePace.Seconds(log, BattlePace.AutoBattleFactor));
				}, victory, () =>
				{
					Save();
					UiSession.NotifyChanged();
				});
				OpenAutoBattle();
			});

			if (_runner.Running)
				Dialog.Confirm(from, T("auto.replace_title"), T("auto.replace_text", _runner.Run!.Title), T("auto.replace_confirm"), () =>
				{
					_runner.Dismiss();
					Setup();
				}, ButtonKind.Danger);
			else
				Setup();
		}

		/// <summary>A janela da Batalha automática, por cima de qualquer tela; fechar não para nada.</summary>
		private void OpenAutoBattle()
		{
			if (_runner.Run is not { } run)
				return;

			AutoBattleDialog.Open(_ui, run, _player, new AutoBattleActions
			{
				Stop = _runner.Stop,
				Resume = _runner.Resume,
				Dismiss = _runner.Dismiss,
				SetRuns = _runner.SetRuns,
				SellRune = rune =>
				{
					if (RuneInventory.Sell(_player, rune) > 0)
						run.Sold.Add(rune.Id);
					Save();
					run.Notify();
					RefreshCurrent();
				},
				LockRune = rune =>
				{
					rune.Locked = !rune.Locked;
					Save();
					run.Notify();
					RefreshCurrent();
				},
				LockMonster = monster =>
				{
					monster.Locked = !monster.Locked;
					Save();
					run.Notify();
					RefreshCurrent();
				},
				UpgradeRune = (rune, target) =>
				{
					RuneInventory.Upgrade(_random, _player, rune, target);
					Save();
					run.Notify();
					RefreshCurrent();
				},
				ManageRunes = () =>
				{
					if (_screen is not RuneScreen)
						ShowRunes(null, _current);
				},
			});
		}

		/// <summary>Remonta a tela de agora quando uma janela por cima mudou algo que ela mostra (moedas, runas).</summary>
		private void RefreshCurrent()
		{
			if (_screen is not BattleScreen)
				_current();
		}

		// Infraestrutura ----------------------------------------------------------------------------

		/// <summary>Aplica uma regra, salva e atualiza a tela.</summary>
		private void Change(Action change, Action refresh)
		{
			change();
			Save();
			refresh();
		}

		private Core.Runes.Rune Rune(int id) => _player.Runes.First(r => r.Id == id);

		/// <summary>Um monstro no resultado da luta: o retrato e a barra de experiência do antes até agora.</summary>
		private ResultMonster ResultOf(OwnedSummon monster, int level, int experience)
		{
			var summon = _database.Summon(monster.SummonId);
			return new ResultMonster(summon.NameFor(monster.Awakened), Art.Creature(summon.ImageFor(monster.Awakened)), Palette.Of(summon.Element), ResultMonster.StepsOf(monster, level, experience))
			{
				MaxLevel = Leveling.IsMaxLevel(monster),
				Summon = summon,
				Monster = monster,
			};
		}

		private void Save() => _store.Save(_player);

		/// <summary>
		/// Troca a tela. A nova leva o nome da classe (<c>RuneScreen</c>): é a raiz do caminho de todo nó
		/// dela. <paramref name="reshow"/> é como remontá-la (para voltar a ela depois de uma janela).
		/// </summary>
		private void Swap(Control screen, Action reshow)
		{
			if (_screen != null)
				Layout.Discard(_screen);
			_screen = screen;
			_current = reshow;
			screen.Name = screen.GetType().Name;
			_screens.AddChild(screen);
		}

		private static string? Argument(string prefix) => OS.GetCmdlineUserArgs()
			.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
	}
}
