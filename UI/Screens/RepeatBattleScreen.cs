using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A Batalha automática: o Resolver repetido, luta após luta, mas cada luta demora o que levaria na
	/// tela no automático (<see cref="BattlePace"/>). Um anel de energia enche com a luta em andamento
	/// (o número dela no meio); ao lado, o tempo que falta e o sigilo de parar; embaixo, em cápsulas,
	/// tudo o que já rendeu e as runas que caíram.
	///
	/// Quem resolve as lutas e aplica as recompensas é o GameRoot: ele chama <see cref="BeginRun"/> com
	/// a duração, a tela espera e avisa <see cref="RunFinished"/>. Parar ou sair no meio de uma luta
	/// descarta essa luta: nada ganho, nada gasto.
	/// </summary>
	public partial class RepeatBattleScreen : Control
	{
		private const float RingSize = 170;

		private readonly PlayerState _player;
		private readonly string _title;
		private readonly int _runs;

		private readonly CurrencyBar _currencies = new();
		private readonly EnergyRing _ring = new(Palette.Arcane, 8) { CustomMinimumSize = new Vector2(RingSize, RingSize) };
		private readonly Label _count = new() { ThemeTypeVariation = GameTheme.Number, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360, 0) };
		private readonly HBoxContainer _time = Layout.Row(8);
		private readonly HFlowContainer _totals = Layout.Flow(8);
		private readonly HFlowContainer _runes = Layout.Flow(8);
		private readonly SigilButton _stop;

		private bool _running;
		private double _elapsed;
		private double _duration;
		private int _number;
		private readonly List<double> _durations = new();

		private int _victories;
		private int _defeats;
		private int _mana;
		private int _essence;
		private int _gold;
		private int _experience;
		private int _levelUps;
		private int _accountLevels;
		private int _accountLevel;
		private readonly List<Core.Runes.RuneTool> _tools = new();

		public RepeatBattleScreen(PlayerState player, string title, int runs)
		{
			_player = player;
			_title = title;
			_runs = runs;
			_stop = SigilButton.Of("cancel", T("auto.stop"), () => Finish(T("auto.stopped")), 60);
		}

		/// <summary>A luta em andamento acabou de passar na tela: o GameRoot aplica o resultado e chama a próxima.</summary>
		public event Action? RunFinished;

		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("auto.title", _title), "repeat", _currencies, () =>
			{
				_running = false;
				BackRequested?.Invoke();
			}).Header);

			var runPanel = new PanelContainer();
			var run = Layout.Row(28);
			runPanel.AddChild(run);
			var ring = new Control { CustomMinimumSize = new Vector2(RingSize, RingSize) };
			_ring.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			ring.AddChild(_ring);
			_count.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_count.AddThemeFontSizeOverride("font_size", 30);
			ring.AddChild(_count);
			run.AddChild(ring);

			var side = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			side.AddThemeConstantOverride("separation", 14);
			side.AddChild(_time);
			_status.AddThemeFontOverride("font", GameTheme.Serif);
			_status.AddThemeFontSizeOverride("font_size", 18);
			side.AddChild(_status);
			side.AddChild(_stop);
			run.AddChild(side);
			page.AddChild(runPanel);

			var resultPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			var results = new VBoxContainer();
			results.AddThemeConstantOverride("separation", 12);
			results.AddChild(_totals);
			results.AddChild(Layout.Scroll(_runes));
			resultPanel.AddChild(results);
			page.AddChild(resultPanel);

			_accountLevel = _player.AccountLevel;
			RefreshTotals();
		}

		/// <summary>Começa a luta número <paramref name="number"/>, que leva <paramref name="seconds"/> na tela.</summary>
		public void BeginRun(int number, double seconds)
		{
			_number = number;
			_duration = Math.Max(0.1, seconds);
			_elapsed = 0;
			_running = true;
			_count.Text = $"{number}/{_runs}";
			_ring.Progress = 0;
			_currencies.Refresh(_player);
		}

		public void AddVictory(VictoryReward reward, int accountLevel)
		{
			_victories++;
			_mana += reward.Mana;
			_essence += reward.Essence;
			_gold += reward.Gold + reward.AccountLevels * Account.LevelUpGold;
			_experience += reward.Experience;
			_levelUps += reward.LevelUps.Count;
			_accountLevels += reward.AccountLevels;
			_accountLevel = accountLevel;
			if (reward.Rune is { } rune)
				_runes.AddChild(new RuneTile(rune, rune.Slot, 1.3f) { MouseFilter = MouseFilterEnum.Pass });
			_tools.AddRange(reward.Tools);
			RefreshTotals();
		}

		public void AddDefeat()
		{
			_defeats++;
			RefreshTotals();
		}

		/// <summary>Acabou (fim das lutas, falta de Mana ou parada): mostra o motivo e para o relógio.</summary>
		public void Finish(string reason)
		{
			_running = false;
			_status.Text = reason;
			Layout.Clear(_time);
			_ring.Progress = 1;
			_stop.Visible = false;
			_currencies.Refresh(_player);
		}

		public override void _Process(double delta)
		{
			if (!_running)
				return;

			_elapsed += delta;
			_ring.Progress = (float)(_elapsed / _duration);
			var average = _durations.Count == 0 ? _duration : _durations.Average();
			var left = Math.Max(0, _duration - _elapsed) + average * (_runs - _number);
			Layout.Clear(_time);
			_time.AddChild(Layout.Chip("resolve", TimeSpan.FromSeconds(left).ToString(@"m\:ss"), T("auto.time_left")));
			if (_elapsed < _duration)
				return;

			_running = false;
			_durations.Add(_duration);
			RunFinished?.Invoke();
		}

		private void RefreshTotals()
		{
			_currencies.Refresh(_player);
			Layout.Clear(_totals);
			_totals.AddChild(Layout.Chip("confirm", _victories.ToString(), T("auto.victories"), Palette.Spirit));
			_totals.AddChild(Layout.Chip("cancel", _defeats.ToString(), T("auto.defeats"), Palette.Negative));
			_totals.AddChild(Layout.Chip("mana", $"−{_mana}", T("currency.mana")));
			_totals.AddChild(Layout.Chip("essence", $"+{_essence}", T("currency.essence")));
			if (_gold > 0)
				_totals.AddChild(Layout.Chip("gold", $"+{_gold}", T("currency.gold")));
			_totals.AddChild(Layout.Chip("level_max", $"+{_experience}", T("reward.experience")));
			if (_levelUps > 0)
				_totals.AddChild(Layout.Chip("stats", _levelUps.ToString(), T("auto.level_ups")));
			if (_accountLevels > 0)
				_totals.AddChild(Layout.Chip("avatar", _accountLevel.ToString(), T("auto.account"), Palette.Arcane));
			_totals.AddChild(Layout.Chip("rune", _runes.GetChildCount().ToString(), T("auto.runes")));
			foreach (var group in _tools.GroupBy(t => t))
				_totals.AddChild(Layout.Chip(group.Key.Kind == Core.Runes.RuneToolKind.Grindstone ? "grindstone" : "gem", $"×{group.Count()}", $"{Texts.Name(group.Key)} ({Texts.Range(group.Key)})", Palette.Of(group.Key.Grade)));
		}
	}
}
