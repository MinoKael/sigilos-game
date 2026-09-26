using System;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// As Masmorras: à esquerda cada uma com o chefe e o que solta; à direita os andares da escolhida,
	/// com a recompensa de cada um e os botões Lutar e Resolver. Cada vitória custa a Mana do andar.
	/// </summary>
	public partial class DungeonScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly VBoxContainer _list = new();
		private readonly VBoxContainer _detail = new();
		private readonly Label _message = new();
		private DungeonDefinition _selected;

		public DungeonScreen(GameDatabase database, PlayerState player, string? selected)
		{
			_database = database;
			_player = player;
			_selected = database.Dungeons.FirstOrDefault(d => d.Id == selected) ?? database.Dungeons[0];
		}

		public event Action<DungeonDefinition, int>? FightRequested;
		public event Action<DungeonDefinition, int>? ResolveRequested;

		/// <summary>A Batalha automática do andar.</summary>
		public event Action<DungeonDefinition, int>? RepeatRequested;
		public event Action<DungeonDefinition>? TeamRequested;
		public event Action<DungeonDefinition>? ShopRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("dungeons.title"), _currencies, T("common.back_to_hub"), () => BackRequested?.Invoke()));
			page.AddChild(new Label { Text = T("dungeons.subtitle"), ThemeTypeVariation = GameTheme.Faded });

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 16);
			page.AddChild(body);

			var listScroll = new ScrollContainer { CustomMinimumSize = new Vector2(420, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_list.AddThemeConstantOverride("separation", 8);
			_list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			listScroll.AddChild(_list);
			body.AddChild(listScroll);

			var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var detailScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_detail.AddThemeConstantOverride("separation", 8);
			_detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			detailScroll.AddChild(_detail);
			panel.AddChild(detailScroll);
			body.AddChild(panel);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			_message.HorizontalAlignment = HorizontalAlignment.Center;
			page.AddChild(_message);

			Refresh();
		}

		public void ShowMessage(string text) => _message.Text = text;

		public void Refresh()
		{
			_currencies.Refresh(_player);
			RefreshList();
			RefreshDetail();
		}

		private void RefreshList()
		{
			Layout.Clear(_list);
			foreach (var dungeon in _database.Dungeons)
			{
				var open = Dungeons.IsUnlocked(_player, dungeon);
				var card = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
				card.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, dungeon == _selected ? Palette.Text : Palette.GoldDark, dungeon == _selected ? 3 : 2, 8, 8));
				card.Modulate = open ? Colors.White : new Color(1, 1, 1, 0.55f);

				var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
				row.AddThemeConstantOverride("separation", 10);
				row.AddChild(Doodle.Icon(Art.Creature(dungeon.Image), 64, Palette.Gold));
				var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
				text.AddChild(new Label { Text = dungeon.Name, ThemeTypeVariation = GameTheme.Heading, MouseFilter = MouseFilterEnum.Ignore });
				text.AddChild(new Label
				{
					Text = open
						? T("dungeons.progress", Texts.Name(dungeon.Kind), Dungeons.Cleared(_player, dungeon), dungeon.Floors.Count)
						: T("dungeons.locked", dungeon.UnlockStage),
					ThemeTypeVariation = GameTheme.Faded,
					MouseFilter = MouseFilterEnum.Ignore,
				});
				var glyphs = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
				foreach (var set in dungeon.Sets)
				{
					var icon = Doodle.Icon(Art.Glyph(RuneSets.For(set).Glyph), 20, Palette.Gold);
					icon.MouseFilter = MouseFilterEnum.Ignore;
					glyphs.AddChild(icon);
				}

				if (dungeon.Kind == DungeonKind.Tools)
				{
					glyphs.AddChild(Doodle.Icon(Art.Icon("grindstone"), 20, Palette.Gold));
					glyphs.AddChild(Doodle.Icon(Art.Icon("gem"), 20, Palette.Gold));
				}

				text.AddChild(glyphs);
				row.AddChild(text);
				card.AddChild(row);

				var captured = dungeon;
				card.GuiInput += input =>
				{
					if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
					{
						_selected = captured;
						_message.Text = "";
						Refresh();
					}
				};
				_list.AddChild(card);
			}
		}

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			var dungeon = _selected;
			_detail.AddChild(new Label { Text = dungeon.Name, ThemeTypeVariation = GameTheme.Title });

			if (dungeon.Kind == DungeonKind.Runes)
			{
				_detail.AddChild(Layout.Text(T("dungeons.drops_runes"), GameTheme.Faded));
				foreach (var set in dungeon.Sets)
					_detail.AddChild(RichText.Label($"{Texts.Term(set)} · {Texts.Describe(RuneSets.For(set))}"));
			}
			else
			{
				_detail.AddChild(Layout.Text(T("dungeons.drops_tools"), GameTheme.Faded));
			}

			var team = Teams.Of(_player, dungeon.Id).Count;
			var teamRow = new HBoxContainer();
			teamRow.AddThemeConstantOverride("separation", 12);
			teamRow.AddChild(new Label { Text = T("dungeons.team", team, PlayerState.TeamSize), SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var editTeam = new Button { Text = T("common.team") };
			editTeam.Pressed += () => TeamRequested?.Invoke(dungeon);
			teamRow.AddChild(editTeam);
			var shop = new Button { Text = T("common.shop"), TooltipText = T("common.shop_tip") };
			shop.Pressed += () => ShopRequested?.Invoke(dungeon);
			teamRow.AddChild(shop);
			_detail.AddChild(teamRow);

			if (!Dungeons.IsUnlocked(_player, dungeon))
			{
				_detail.AddChild(new Label { Text = T("dungeons.locked", dungeon.UnlockStage), ThemeTypeVariation = GameTheme.Heading });
				return;
			}

			for (var number = 1; number <= dungeon.Floors.Count; number++)
				_detail.AddChild(FloorRow(dungeon, number, team == 0));
		}

		private Control FloorRow(DungeonDefinition dungeon, int number, bool noTeam)
		{
			var floor = dungeon.Floor(number);
			var cleared = number <= Dungeons.Cleared(_player, dungeon);
			var open = Dungeons.IsFloorUnlocked(_player, dungeon, number);

			var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel };
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 12);
			panel.AddChild(row);

			var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var title = new Label { Text = T(cleared ? "dungeons.floor_cleared" : "dungeons.floor", number, floor.Level), ThemeTypeVariation = GameTheme.Heading };
			text.AddChild(title);
			var drop = dungeon.Kind == DungeonKind.Runes
				? T("dungeons.drop_rune", floor.MinGrade == floor.MaxGrade ? Texts.Stars(floor.MinGrade) : $"{Texts.Stars(floor.MinGrade)}–{Texts.Stars(floor.MaxGrade)}", Texts.Name(floor.MinRarity))
				: T("dungeons.drop_tool", floor.ToolCount, Texts.Name((RuneRarity)floor.ToolGrade));
			text.AddChild(new Label { Text = drop });
			var reward = T("dungeons.reward", floor.Essence, floor.Experience);
			if (!cleared)
				reward += T("dungeons.first_gold", floor.FirstClearGold);
			text.AddChild(new Label { Text = reward, ThemeTypeVariation = GameTheme.Faded });

			var problem = Dungeons.Check(_player, dungeon, number);
			if (problem is EntryProblem.NoMana or EntryProblem.RunesFull)
			{
				var refusal = Layout.Text(Texts.Refusal(problem, floor.Mana), width: 360);
				refusal.AddThemeColorOverride("font_color", Palette.Negative);
				refusal.AddThemeFontSizeOverride("font_size", 13);
				text.AddChild(refusal);
			}

			row.AddChild(text);

			var disabled = noTeam || problem != EntryProblem.None;
			var fight = new Button { Text = T("common.fight_mana", floor.Mana), Disabled = disabled, CustomMinimumSize = new Vector2(130, 44) };
			fight.Pressed += () => FightRequested?.Invoke(dungeon, number);
			row.AddChild(fight);
			if (cleared)
			{
				var resolve = new Button { Text = T("common.resolve_mana", floor.Mana), Disabled = disabled, TooltipText = T("common.resolve_tip"), CustomMinimumSize = new Vector2(130, 44) };
				resolve.Pressed += () => ResolveRequested?.Invoke(dungeon, number);
				row.AddChild(resolve);
				var repeat = new Button { Text = T("common.auto_battle", AutoBattle.RepeatRuns), Disabled = disabled, TooltipText = T("common.auto_battle_tip", AutoBattle.RepeatRuns), CustomMinimumSize = new Vector2(110, 44) };
				repeat.Pressed += () => RepeatRequested?.Invoke(dungeon, number);
				row.AddChild(repeat);
			}

			panel.Modulate = open ? Colors.White : new Color(1, 1, 1, 0.5f);
			return panel;
		}
	}
}
