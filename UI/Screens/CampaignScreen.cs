using System;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A região 1: à esquerda a trilha das 20 fases (<see cref="StagePath"/>); à direita a ficha da
	/// escolhida — estrelas e nível dos inimigos, as ondas, o que ela rende, a equipe e os sigilos de
	/// Lutar e Batalha automática, com o custo em Mana na plaquinha. A Batalha automática (quantas lutas
	/// seguidas o jogador escolher, <see cref="RunsPicker"/>) só aparece em fase já vencida (GDD, seção 7).
	/// </summary>
	public partial class CampaignScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly CenterContainer _path = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		private readonly VBoxContainer _detail = new();
		private readonly Label _message = new() { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private StageDefinition _selected;

		public CampaignScreen(GameDatabase database, PlayerState player, int? selected = null)
		{
			_database = database;
			_player = player;
			_selected = database.Stage(selected ?? Math.Min(player.HighestStage + 1, database.Stages.Count));
		}

		public event Action<StageDefinition>? FightRequested;
		/// <summary>A Batalha automática: quantas lutas seguidas da fase, cada uma no tempo que levaria na tela.</summary>
		public event Action<StageDefinition, int>? RepeatRequested;
		public event Action? TeamRequested;
		public event Action? ShopRequested;
		public event Action? BackRequested;

		/// <summary>A fase escolhida agora: a tela volta nela depois da luta ou da Loja.</summary>
		public int Selected => _selected.Number;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Campaign"), "region", _currencies, () => BackRequested?.Invoke()).Header);

			var body = Layout.Row(20);
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);
			body.AddChild(_path);

			var panel = new PanelContainer { CustomMinimumSize = new Vector2(500, 0) };
			_detail.AddThemeConstantOverride("separation", 12);
			panel.AddChild(Layout.Scroll(_detail));
			body.AddChild(panel);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			page.AddChild(_message);

			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			RefreshPath();
			RefreshDetail();
		}

		/// <summary>Um aviso na faixa de baixo (o motivo de não poder lutar).</summary>
		public void ShowMessage(string text) => _message.Text = text;

		private void RefreshPath()
		{
			Layout.Clear(_path);
			var unlocked = _database.Stages.Where(s => Campaign.IsUnlocked(_player, s.Number)).Select(s => s.Number).DefaultIfEmpty(1).Max();
			var path = new StagePath(_database.Stages.Count, _player.HighestStage, unlocked, _selected.Number, number => $"{number} · {_database.Stage(number).Name}");
			path.Chosen += number =>
			{
				_selected = _database.Stage(number);
				_message.Text = "";
				Callable.From(Refresh).CallDeferred();
			};
			_path.AddChild(path);
		}

		private void RefreshDetail()
		{
			Layout.Clear(_detail);

			var stage = _selected;
			var cleared = Campaign.IsCleared(_player, stage.Number);
			var title = Layout.Row(10);
			title.AddChild(new Label { Text = T("campaign.stage_title", stage.Number, stage.Name), ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			if (cleared)
			{
				var done = Doodle.Icon(Art.Icon("confirm"), 24, Palette.Spirit);
				done.TooltipText = T("campaign.cleared");
				done.MouseFilter = MouseFilterEnum.Stop;
				title.AddChild(done);
			}

			_detail.AddChild(title);
			_detail.AddChild(Foes(stage.Stars, stage.Level));

			for (var i = 0; i < stage.Waves.Count; i++)
			{
				var row = Layout.Row(8);
				var wave = new Label { Text = Texts.Roman(i + 1), ThemeTypeVariation = GameTheme.Number, CustomMinimumSize = new Vector2(34, 0), TooltipText = T("campaign.wave", i + 1), MouseFilter = MouseFilterEnum.Stop };
				wave.AddThemeColorOverride("font_color", Palette.GoldDark.Lightened(0.3f));
				row.AddChild(wave);
				foreach (var slot in stage.Waves[i])
				{
					var (name, image, element) = _database.Foe(slot);
					var icon = Doodle.Icon(Art.Creature(image), 44, Palette.Of(element));
					icon.TooltipText = T("campaign.enemy_tip", name, Texts.Name(element));
					icon.MouseFilter = MouseFilterEnum.Stop;
					row.AddChild(icon);
				}

				_detail.AddChild(row);
			}

			_detail.AddChild(new HSeparator());
			var rewards = Layout.Flow(8);
			if (!cleared)
			{
				var first = Doodle.Icon(Art.Icon("collect"), 26, Palette.Spirit);
				first.TooltipText = T("campaign.first_clear");
				first.MouseFilter = MouseFilterEnum.Stop;
				rewards.AddChild(first);
				rewards.AddChild(Layout.Chip("scroll", stage.FirstClearScrolls.ToString(), T("currency.scrolls_name")));
				rewards.AddChild(Layout.Chip("essence", (stage.Essence + stage.FirstClearEssence).ToString(), T("currency.essence")));
				rewards.AddChild(Layout.Chip("rune", Texts.Stars(stage.RuneGrade), T("campaign.rune_first", Texts.Stars(stage.RuneGrade))));
			}
			else
			{
				rewards.AddChild(Layout.Chip("essence", stage.Essence.ToString(), T("currency.essence")));
				rewards.AddChild(Layout.Chip("rune", $"{Texts.Stars(stage.RuneGrade)} {Texts.Percent(Campaign.RepeatRuneChance)}", T("campaign.rune_chance", Texts.Percent(Campaign.RepeatRuneChance), Texts.Stars(stage.RuneGrade))));
			}

			rewards.AddChild(Layout.Chip("level_max", stage.Experience.ToString(), T("reward.experience")));
			_detail.AddChild(rewards);

			_detail.AddChild(new HSeparator());
			_detail.AddChild(new TeamStrip(_database, _player, Teams.Campaign, () => TeamRequested?.Invoke()));

			var problem = Campaign.Check(_player, stage);
			var blocked = Teams.Of(_player, Teams.Campaign).Count == 0 || problem != EntryProblem.None;
			var actions = Layout.Row(14);
			var fight = SigilButton.Of("fight", T("common.fight", stage.Mana), () => FightRequested?.Invoke(stage), 84);
			fight.Badge = stage.Mana.ToString();
			fight.Disabled = blocked;
			fight.Highlight = !blocked && !cleared;
			actions.AddChild(fight);
			if (cleared)
			{
				var repeat = SigilButton.Of("repeat", T("common.auto_battle"), () => RunsPicker.Open(this, stage.Mana, runs => RepeatRequested?.Invoke(stage, runs)), 68);
				repeat.Disabled = blocked;
				actions.AddChild(repeat);
			}

			actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
			actions.AddChild(SigilButton.Of("shop", T("destination.Shop"), () => ShopRequested?.Invoke(), 52, SigilShape.Square));
			_detail.AddChild(actions);

			if (problem is EntryProblem.NoMana or EntryProblem.RunesFull)
			{
				var refusal = Layout.Text(Texts.Refusal(problem, stage.Mana), width: 440);
				refusal.AddThemeColorOverride("font_color", Palette.Negative);
				_detail.AddChild(refusal);
			}
		}

		/// <summary>Estrelas e nível dos inimigos, numa cápsula.</summary>
		private static Control Foes(int stars, int level)
		{
			var row = Layout.Row(8);
			row.AddChild(Layout.Chip("fight", T("common.stars_level", Texts.Stars(stars), level), T("campaign.foes")));
			return row;
		}
	}
}
