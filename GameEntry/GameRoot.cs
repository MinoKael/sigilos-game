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

namespace Sigilos.GameEntry
{
	/// <summary>
	/// Nó raiz (Scenes/GameRoot.tscn) e único lugar que junta as peças: carrega os dados e o save,
	/// decide qual tela está no ar e aplica as regras do Core quando uma tela pede.
	///
	/// <b>As telas não o conhecem.</b> Cada uma recebe o que mostra e avisa por evento o que o jogador
	/// escolheu; quem muda o <see cref="PlayerState"/> e salva é esta classe.
	///
	/// Argumentos de desenvolvimento (depois de <c>--</c>): <c>--save=nome</c> usa outro arquivo de
	/// save; <c>--tela=campanha|invocar|monstros|runas|compendio|batalha</c> abre essa tela direto.
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
				case "invocar":
					ShowSummon();
					break;
				case "monstros":
					ShowStorage(null);
					break;
				case "runas":
					ShowRunes(_player.Team.First());
					break;
				case "compendio":
					ShowCompendium();
					break;
				case "batalha":
					StartBattle(_database.Stage(Math.Min(_player.HighestStage + 1, _database.Stages.Count)));
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
			hub.SummonRequested += ShowSummon;
			hub.StorageRequested += () => ShowStorage(null);
			hub.CompendiumRequested += ShowCompendium;
			hub.CollectRequested += () => Change(() => Idle.Collect(_player, DateTime.Now), () => hub.Refresh(DateTime.Now));
			hub.QuickChannelRequested += () => Change(() => Idle.QuickChannel(_player, DateTime.Now), () => hub.Refresh(DateTime.Now));
			Swap(hub);
		}

		private void ShowCampaign()
		{
			var campaign = new CampaignScreen(_database, _player);
			campaign.BackRequested += ShowHub;
			campaign.FightRequested += StartBattle;
			campaign.ResolveRequested += stage =>
			{
				campaign.ShowMessage(Resolve(stage));
				campaign.Refresh();
			};
			Swap(campaign);
		}

		private void ShowSummon()
		{
			var summon = new SummonScreen(_database, _player);
			summon.BackRequested += ShowHub;
			summon.SummonRequested += (count, glyphs) =>
			{
				var results = SummonRitual.Perform(_random, _database, _player, count, glyphs);
				if (results.Count == 0)
					return;

				AddToTeamIfRoom(results.Select(r => r.Summon));
				Save();
				summon.ShowResults(results);
			};
			Swap(summon);
		}

		private void ShowStorage(string? selected)
		{
			var storage = new StorageScreen(_database, _player, selected);
			storage.BackRequested += ShowHub;
			storage.RunesRequested += ShowRunes;
			storage.ToggleTeamRequested += id => Change(() => ToggleTeam(id), storage.Refresh);
			storage.MakeLeaderRequested += id => Change(() => MakeLeader(id), storage.Refresh);
			storage.InfuseRequested += (id, toMax) => Change(
				() => Leveling.Infuse(_player, id, toMax ? int.MaxValue : Leveling.Missing(_player.Summon(id))),
				storage.Refresh);
			storage.AwakenRequested += id => Change(() => Awakening.Awaken(_player, _database.Summon(id)), storage.Refresh);
			Swap(storage);
		}

		private void ShowRunes(string summonId)
		{
			var runes = new RuneScreen(_database, _player, summonId);
			runes.BackRequested += () => ShowStorage(summonId);
			runes.EquipRequested += id => Change(() => RuneInventory.Equip(_player, Rune(id), summonId), runes.Refresh);
			runes.UnequipRequested += id => Change(() => RuneInventory.Unequip(_player, Rune(id)), runes.Refresh);
			runes.UpgradeRequested += (id, target) => Change(() => RuneInventory.Upgrade(_random, _player, Rune(id), target), runes.Refresh);
			runes.GrindRequested += (id, index, tool) => Change(() => RuneInventory.Grind(_random, _player, Rune(id), index, tool), runes.Refresh);
			runes.EnchantRequested += (id, index, tool) => Change(() => RuneInventory.Enchant(_random, _player, Rune(id), index, tool), runes.Refresh);
			runes.SellRequested += id => Change(() => RuneInventory.Sell(_player, Rune(id)), runes.Refresh);
			Swap(runes);
		}

		private void ShowCompendium()
		{
			var compendium = new CompendiumScreen(_database);
			compendium.BackRequested += ShowHub;
			Swap(compendium);
		}

		private void StartBattle(StageDefinition stage)
		{
			var team = PlayerTeam.Build(_player, _database);
			if (team.Members.Count == 0)
			{
				ShowStorage(null);
				return;
			}

			var session = BattleFactory.Create(_database, team, stage, _random.Next());
			var battle = new BattleScreen(session, stage, _player.AutoBattle);
			battle.Finished += victory =>
			{
				var reward = victory ? Campaign.ApplyVictory(_random, _player, stage) : null;
				Save();
				battle.ShowResult(victory, reward);
			};
			battle.Closed += auto =>
			{
				_player.AutoBattle = auto;
				Save();
				ShowCampaign();
			};
			Swap(battle);
		}

		// Regras que as telas pedem -----------------------------------------------------------------

		/// <summary>O botão Resolver: a luta inteira no automático, sem tela.</summary>
		private string Resolve(StageDefinition stage)
		{
			var session = BattleFactory.Create(_database, PlayerTeam.Build(_player, _database), stage, _random.Next());
			if (!AutoBattle.Run(session))
				return $"Derrota na fase {stage.Number} (rodada {Math.Min(session.Round, BattleRules.RoundLimit)}).";

			var reward = Campaign.ApplyVictory(_random, _player, stage);
			Save();
			var rune = reward.Rune == null ? "" : $", runa {Texts.Name(reward.Rune.Set)} {Texts.Stars(reward.Rune.Grade)}";
			var tool = reward.Tool == null ? "" : $", {Texts.Name(reward.Tool)}";
			return $"Vitória na fase {stage.Number}: +{reward.Essence} Essência, +{reward.Dust} Pó, +{reward.Experience} de experiência{rune}{tool}.";
		}

		private void ToggleTeam(string id)
		{
			if (_player.Team.Remove(id))
				return;
			if (_player.Team.Count < PlayerState.TeamSize)
				_player.Team.Add(id);
		}

		private void MakeLeader(string id)
		{
			if (!_player.Team.Remove(id))
				return;
			_player.Team.Insert(0, id);
		}

		/// <summary>Invocação nova entra no time se ainda há vaga: a primeira luta não espera o jogador achar a tela de Monstros.</summary>
		private void AddToTeamIfRoom(IEnumerable<SummonDefinition> summons)
		{
			foreach (var summon in summons.OrderByDescending(s => s.Rarity))
			{
				if (_player.Team.Count >= PlayerState.TeamSize)
					return;
				if (!_player.Team.Contains(summon.Id))
					_player.Team.Add(summon.Id);
			}
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
