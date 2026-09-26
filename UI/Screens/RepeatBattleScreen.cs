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
	/// tela no automático (<see cref="BattlePace"/>). Mostra a luta em andamento, quanto falta e tudo o
	/// que já rendeu.
	///
	/// Quem resolve as lutas e aplica as recompensas é o GameRoot: ele chama <see cref="BeginRun"/> com
	/// a duração, a tela espera e avisa <see cref="RunFinished"/>. Parar ou sair no meio de uma luta
	/// descarta essa luta: nada ganho, nada gasto.
	/// </summary>
	public partial class RepeatBattleScreen : Control
	{
		private readonly PlayerState _player;
		private readonly string _title;
		private readonly int _runs;

		private readonly CurrencyBar _currencies = new();
		private readonly Label _status = new();
		private readonly ProgressBar _progress = new() { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14) };
		private readonly Label _timeLeft = new() { ThemeTypeVariation = GameTheme.Faded };
		private readonly Label _record = new();
		private readonly Label _gains = new();
		private readonly Label _levels = new() { ThemeTypeVariation = GameTheme.Faded };
		private readonly HFlowContainer _runes = new();
		private readonly VBoxContainer _tools = new();
		private readonly Button _stop = new();

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

		public RepeatBattleScreen(PlayerState player, string title, int runs)
		{
			_player = player;
			_title = title;
			_runs = runs;
		}

		/// <summary>A luta em andamento acabou de passar na tela: o GameRoot aplica o resultado e chama a próxima.</summary>
		public event Action? RunFinished;

		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("auto.title", _title), _currencies, T("common.back"), () =>
			{
				_running = false;
				BackRequested?.Invoke();
			}));

			var (runPanel, run) = Layout.Section(T("auto.fight"));
			_status.AddThemeFontOverride("font", GameTheme.Serif);
			_status.AddThemeFontSizeOverride("font_size", 20);
			run.AddChild(_status);
			_progress.AddThemeStyleboxOverride("fill", GameTheme.Box(Palette.Gold, Palette.Gold, 0, 3, 0));
			run.AddChild(_progress);
			run.AddChild(_timeLeft);
			_stop.Text = T("auto.stop");
			_stop.TooltipText = T("auto.stop_tip");
			_stop.CustomMinimumSize = new Vector2(160, 44);
			_stop.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			_stop.Pressed += () => Finish(T("auto.stopped"));
			run.AddChild(_stop);
			page.AddChild(runPanel);

			var (resultPanel, results) = Layout.Section(T("auto.results"));
			resultPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
			results.AddChild(_record);
			results.AddChild(_gains);
			results.AddChild(_levels);
			_runes.AddThemeConstantOverride("h_separation", 6);
			_runes.AddThemeConstantOverride("v_separation", 6);
			var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			list.AddChild(_runes);
			list.AddChild(_tools);
			scroll.AddChild(list);
			results.AddChild(scroll);
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
			_status.Text = T("auto.run", number, _runs);
			_progress.MaxValue = _duration;
			_progress.Value = 0;
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
				_runes.AddChild(new RuneTile(rune, rune.Slot));
			foreach (var tool in reward.Tools)
				_tools.AddChild(new Label { Text = T("battle.tool", Texts.Name(tool), Texts.Range(tool)) });
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
			_timeLeft.Text = "";
			_progress.Value = _progress.MaxValue;
			_stop.Visible = false;
			_currencies.Refresh(_player);
		}

		public override void _Process(double delta)
		{
			if (!_running)
				return;

			_elapsed += delta;
			_progress.Value = _elapsed;
			var average = _durations.Count == 0 ? _duration : _durations.Average();
			var left = Math.Max(0, _duration - _elapsed) + average * (_runs - _number);
			_timeLeft.Text = T("auto.time_left", TimeSpan.FromSeconds(left).ToString(@"m\:ss"));
			if (_elapsed < _duration)
				return;

			_running = false;
			_durations.Add(_duration);
			RunFinished?.Invoke();
		}

		private void RefreshTotals()
		{
			_currencies.Refresh(_player);
			_record.Text = T("auto.record", _victories, _defeats);
			_gains.Text = T("auto.gains", _mana, _essence, _gold, _experience);
			var levels = new List<string>();
			if (_levelUps > 0)
				levels.Add(T("auto.level_ups", _levelUps));
			if (_accountLevels > 0)
				levels.Add(T("auto.account", _accountLevel));
			levels.Add(T("auto.runes", _runes.GetChildCount()));
			_levels.Text = string.Join(" · ", levels);
		}
	}
}
