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
	/// A região 1: as 20 fases, o detalhe da escolhida e os botões Lutar e Resolver, cada um com o custo
	/// em Mana. Resolver só aparece em fase já vencida (GDD, seção 7) e simula na hora, sem tela de
	/// batalha. Sem Mana, o motivo aparece embaixo, ao lado da Loja.
	/// </summary>
	public partial class CampaignScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly GridContainer _grid = new() { Columns = 5 };
		private readonly VBoxContainer _detail = new();
		private readonly Label _message = new();
		private StageDefinition _selected;

		public CampaignScreen(GameDatabase database, PlayerState player, int? selected = null)
		{
			_database = database;
			_player = player;
			_selected = database.Stage(selected ?? Math.Min(player.HighestStage + 1, database.Stages.Count));
		}

		public event Action<StageDefinition>? FightRequested;
		public event Action<StageDefinition>? ResolveRequested;

		/// <summary>A Batalha automática: várias lutas seguidas da fase, cada uma no tempo que levaria na tela.</summary>
		public event Action<StageDefinition>? RepeatRequested;
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

			page.AddChild(Layout.Header(T("campaign.title"), _currencies, T("common.back_to_hub"), () => BackRequested?.Invoke()));
			page.AddChild(new Label { Text = T("campaign.subtitle"), ThemeTypeVariation = GameTheme.Faded });

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 20);
			page.AddChild(body);

			_grid.AddThemeConstantOverride("h_separation", 10);
			_grid.AddThemeConstantOverride("v_separation", 10);
			body.AddChild(_grid);

			_detail.AddThemeConstantOverride("separation", 8);
			var (panel, content) = Layout.Section(T("campaign.stage"));
			panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			content.AddChild(_detail);
			body.AddChild(panel);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			_message.HorizontalAlignment = HorizontalAlignment.Center;
			page.AddChild(_message);

			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			RefreshGrid();
			RefreshDetail();
		}

		/// <summary>Resultado do Resolver, na faixa de baixo.</summary>
		public void ShowMessage(string text) => _message.Text = text;

		private void RefreshGrid()
		{
			Layout.Clear(_grid);

			foreach (var stage in _database.Stages)
			{
				var cleared = Campaign.IsCleared(_player, stage.Number);
				var button = new Button
				{
					Text = cleared ? T("campaign.number_cleared", stage.Number) : stage.Number.ToString(),
					CustomMinimumSize = new Vector2(92, 64),
					Disabled = !Campaign.IsUnlocked(_player, stage.Number),
					TooltipText = stage.Name,
				};
				if (stage == _selected)
					button.AddThemeStyleboxOverride("normal", GameTheme.Box(Palette.Gold, Palette.Text, 3, 5, 8));
				var captured = stage;
				button.Pressed += () =>
				{
					_selected = captured;
					_message.Text = "";
					Refresh();
				};
				_grid.AddChild(button);
			}
		}

		private void RefreshDetail()
		{
			Layout.Clear(_detail);

			var stage = _selected;
			var cleared = Campaign.IsCleared(_player, stage.Number);
			_detail.AddChild(new Label { Text = T("campaign.stage_title", stage.Number, stage.Name), ThemeTypeVariation = GameTheme.Heading });
			var levels = Teams.Of(_player, Teams.Campaign).Select(_player.Monster).OfType<OwnedSummon>().Select(m => T("common.stars_level", Texts.Stars(m.Stars), m.Level)).ToList();
			_detail.AddChild(new Label { Text = T("campaign.levels", T("common.stars_level", Texts.Stars(stage.Stars), stage.Level), levels.Count == 0 ? "—" : string.Join(", ", levels)) });

			for (var i = 0; i < stage.Waves.Count; i++)
			{
				var row = new HBoxContainer();
				row.AddChild(new Label { Text = T("campaign.wave", i + 1), CustomMinimumSize = new Vector2(70, 0) });
				foreach (var slot in stage.Waves[i])
				{
					var (name, image, element) = _database.Foe(slot);
					var icon = Doodle.Icon(Art.Creature(image), 40, Palette.Of(element));
					icon.TooltipText = T("campaign.enemy_tip", name, Texts.Name(element));
					icon.MouseFilter = MouseFilterEnum.Stop;
					row.AddChild(icon);
				}

				_detail.AddChild(row);
			}

			var reward = cleared
				? T("campaign.reward", stage.Essence, stage.Experience, Texts.Percent(Campaign.RepeatRuneChance), Texts.Stars(stage.RuneGrade))
				: T("campaign.reward_first", Texts.Scrolls(stage.FirstClearScrolls), stage.Essence + stage.FirstClearEssence, stage.Experience, Texts.Stars(stage.RuneGrade));
			_detail.AddChild(Layout.Text(reward, width: 400));

			foreach (var line in stage.Lines)
				_detail.AddChild(Layout.Text(T("campaign.line", line), GameTheme.Faded, 400));

			var buttons = new HFlowContainer();
			buttons.AddThemeConstantOverride("h_separation", 12);
			buttons.AddThemeConstantOverride("v_separation", 8);
			var problem = Campaign.Check(_player, stage);
			var blocked = Teams.Of(_player, Teams.Campaign).Count == 0 || problem != EntryProblem.None;
			var fight = new Button { Text = T("common.fight_mana", stage.Mana), CustomMinimumSize = new Vector2(150, 52), Disabled = blocked };
			fight.Pressed += () => FightRequested?.Invoke(stage);
			buttons.AddChild(fight);
			if (cleared)
			{
				var resolve = new Button { Text = T("common.resolve_mana", stage.Mana), CustomMinimumSize = new Vector2(150, 52), TooltipText = T("common.resolve_tip"), Disabled = blocked };
				resolve.Pressed += () => ResolveRequested?.Invoke(stage);
				buttons.AddChild(resolve);
				var repeat = new Button { Text = T("common.auto_battle", AutoBattle.RepeatRuns), CustomMinimumSize = new Vector2(150, 52), TooltipText = T("common.auto_battle_tip", AutoBattle.RepeatRuns), Disabled = blocked };
				repeat.Pressed += () => RepeatRequested?.Invoke(stage);
				buttons.AddChild(repeat);
			}

			var team = new Button { Text = T("common.team"), CustomMinimumSize = new Vector2(120, 52) };
			team.Pressed += () => TeamRequested?.Invoke();
			buttons.AddChild(team);
			var shop = new Button { Text = T("common.shop"), CustomMinimumSize = new Vector2(120, 52), TooltipText = T("common.shop_tip") };
			shop.Pressed += () => ShopRequested?.Invoke();
			buttons.AddChild(shop);
			_detail.AddChild(buttons);

			if (problem is EntryProblem.NoMana or EntryProblem.RunesFull)
			{
				var refusal = Layout.Text(Texts.Refusal(problem, stage.Mana), width: 400);
				refusal.AddThemeColorOverride("font_color", Palette.Negative);
				_detail.AddChild(refusal);
			}

			var size = Teams.Of(_player, Teams.Campaign).Count;
			if (size < PlayerState.TeamSize)
				_detail.AddChild(new Label { Text = T("common.team_incomplete", size, PlayerState.TeamSize), ThemeTypeVariation = GameTheme.Faded });
		}
	}
}
