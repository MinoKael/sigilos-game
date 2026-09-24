using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Summoning;
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
	/// Argumentos de desenvolvimento (depois de <c>--</c>):
	/// <c>--save=nome</c> usa outro arquivo de save; <c>--tela=campanha|invocar|time|batalha</c> abre
	/// essa tela direto.
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
			_player = _store.Load() ?? NewGame.Create(DateTime.Now);

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
				case "time":
					ShowPrepare();
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
			hub.PrepareRequested += ShowPrepare;
			hub.CollectRequested += () => ChangeAndRefresh(hub, () => Idle.Collect(_player, DateTime.Now));
			hub.QuickChannelRequested += () => ChangeAndRefresh(hub, () => Idle.QuickChannel(_player, DateTime.Now));
			hub.RaiseLevelRequested += () => ChangeAndRefresh(hub, RaiseLevel);
			Swap(hub);
		}

		private void ShowCampaign()
		{
			var campaign = new CampaignScreen(_database, _player);
			campaign.BackRequested += ShowHub;
			campaign.FightRequested += StartBattle;
			campaign.ResolveRequested += stage => campaign.ShowMessage(Resolve(stage));
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

		private void ShowPrepare()
		{
			var prepare = new PrepareScreen(_database, _player);
			prepare.BackRequested += ShowHub;
			prepare.Confirmed += (team, pages, posture) =>
			{
				_player.Team = team.ToList();
				_player.Grimoire = pages.ToList();
				_player.Posture = posture;
				Save();
				ShowHub();
			};
			Swap(prepare);
		}

		private void StartBattle(StageDefinition stage)
		{
			var team = PlayerTeam.Build(_player, _database);
			if (team.Members.Count == 0)
			{
				ShowPrepare();
				return;
			}

			var session = BattleFactory.Create(_database, team, stage, _random.Next());
			var battle = new BattleScreen(session, stage, _player.Posture, _player.AutoBattle);
			battle.Finished += victory =>
			{
				var reward = victory ? Campaign.ApplyVictory(_player, stage) : null;
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
			if (!AutoBattle.Run(session, _player.Posture))
				return $"Derrota na fase {stage.Number} (rodada {Math.Min(session.Round, BattleRules.RoundLimit)}).";

			var reward = Campaign.ApplyVictory(_player, stage);
			Save();
			return $"Vitória na fase {stage.Number}: +{reward.Essence} Essência.";
		}

		private void RaiseLevel()
		{
			if (!SharedLevel.CanRaise(_player.Level, _player.Essence))
				return;

			_player.Essence -= SharedLevel.CostToRaise(_player.Level);
			_player.Level++;
		}

		/// <summary>Invocação nova entra no time se ainda há vaga: a primeira luta não espera o jogador achar a tela de time.</summary>
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

		private void ChangeAndRefresh(HubScreen hub, Action change)
		{
			change();
			Save();
			hub.Refresh(DateTime.Now);
		}

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
