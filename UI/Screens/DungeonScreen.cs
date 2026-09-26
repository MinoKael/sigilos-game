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
	/// As Masmorras: à esquerda um portal por Masmorra (o chefe, os Glifos do que solta e a barra dos
	/// andares vencidos; fechada, com o cadeado); à direita os andares da escolhida — estrelas e nível,
	/// o que solta, o Ouro da primeira vitória e os sigilos de Lutar, Resolver e Batalha automática.
	/// Cada vitória custa a Mana do andar.
	/// </summary>
	public partial class DungeonScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly VBoxContainer _list = new();
		private readonly VBoxContainer _detail = new();
		private readonly Label _message = new() { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
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
			page.AddChild(Layout.Header(T("destination.Dungeons"), "dungeon", _currencies, () => BackRequested?.Invoke()).Header);

			var body = Layout.Row(16);
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			_list.AddThemeConstantOverride("separation", 10);
			var listScroll = Layout.Scroll(_list);
			listScroll.CustomMinimumSize = new Vector2(300, 0);
			listScroll.SizeFlagsHorizontal = SizeFlags.Fill;
			body.AddChild(listScroll);

			var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_detail.AddThemeConstantOverride("separation", 10);
			panel.AddChild(Layout.Scroll(_detail));
			body.AddChild(panel);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
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
				var card = new Button
				{
					Flat = true,
					ToggleMode = true,
					ButtonPressed = dungeon == _selected,
					FocusMode = FocusModeEnum.None,
					CustomMinimumSize = new Vector2(280, 92),
					TooltipText = open ? dungeon.Name : T("dungeons.locked", dungeon.Name, dungeon.UnlockStage),
					MouseDefaultCursorShape = CursorShape.PointingHand,
				};
				var selected = dungeon == _selected;
				card.AddThemeStyleboxOverride("normal", Ornament.Panel(Palette.Panel, selected ? Palette.Arcane : Palette.GoldDark, 8));
				card.AddThemeStyleboxOverride("hover", Ornament.Panel(Palette.PanelLight, selected ? Palette.Arcane : Palette.Gold, 8));
				card.AddThemeStyleboxOverride("pressed", Ornament.Panel(Palette.PanelLight, Palette.Arcane, 8));
				card.AddThemeStyleboxOverride("hover_pressed", Ornament.Panel(Palette.PanelLight, Palette.Arcane, 8));
				Juice.Attach(card, 1.02f, 0.98f);

				var row = Layout.Row(10);
				row.MouseFilter = MouseFilterEnum.Ignore;
				row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				row.OffsetLeft = row.OffsetTop = 10;
				row.OffsetRight = row.OffsetBottom = -10;
				row.AddChild(Doodle.Icon(Art.Creature(dungeon.Image), 68, open ? Palette.Gold : Palette.TextFaded.Darkened(0.3f)));

				var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
				column.AddThemeConstantOverride("separation", 8);
				var glyphs = Layout.Row(4);
				glyphs.MouseFilter = MouseFilterEnum.Ignore;
				if (dungeon.Kind == DungeonKind.Runes)
				{
					foreach (var set in dungeon.Sets)
						glyphs.AddChild(Doodle.Icon(Art.Glyph(RuneSets.For(set).Glyph), 22, Palette.Gold));
				}
				else
				{
					glyphs.AddChild(Doodle.Icon(Art.Icon("grindstone"), 22, Palette.Gold));
					glyphs.AddChild(Doodle.Icon(Art.Icon("gem"), 22, Palette.Gold));
				}

				column.AddChild(glyphs);
				var bar = Layout.Energy(Palette.Arcane, 8);
				bar.MaxValue = dungeon.Floors.Count;
				bar.Value = Dungeons.Cleared(_player, dungeon);
				bar.MouseFilter = MouseFilterEnum.Ignore;
				column.AddChild(bar);
				row.AddChild(column);
				if (!open)
					row.AddChild(Doodle.Icon(Art.Icon("lock"), 28, Palette.Gold));
				card.AddChild(row);

				var captured = dungeon;
				card.Pressed += () =>
				{
					_selected = captured;
					_message.Text = "";
					Callable.From(Refresh).CallDeferred();
				};
				_list.AddChild(card);
			}
		}

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			var dungeon = _selected;
			var title = Layout.Row(12);
			title.AddChild(Doodle.Icon(Art.Creature(dungeon.Image), 48, Palette.Gold));
			title.AddChild(new Label { Text = dungeon.Name, ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
			title.AddChild(SigilButton.Of("shop", T("destination.Shop"), () => ShopRequested?.Invoke(dungeon), 48, SigilShape.Square));
			_detail.AddChild(title);

			var drops = Layout.Flow(8);
			if (dungeon.Kind == DungeonKind.Runes)
			{
				foreach (var set in dungeon.Sets)
					drops.AddChild(Layout.Chip(Art.Glyph(RuneSets.For(set).Glyph), Texts.Name(set), Texts.Plain($"{Texts.Name(set)} · {Texts.Describe(RuneSets.For(set))}")));
			}
			else
			{
				drops.AddChild(Layout.Chip("grindstone", "", T("dungeons.grindstones")));
				drops.AddChild(Layout.Chip("gem", "", T("dungeons.gems")));
			}

			_detail.AddChild(drops);
			_detail.AddChild(new TeamStrip(_database, _player, dungeon.Id, () => TeamRequested?.Invoke(dungeon)));
			_detail.AddChild(new HSeparator());

			if (!Dungeons.IsUnlocked(_player, dungeon))
			{
				var locked = Layout.Row(10, true);
				locked.AddChild(Doodle.Icon(Art.Icon("lock"), 48, Palette.Gold));
				locked.AddChild(Layout.Chip("region", dungeon.UnlockStage.ToString(), T("dungeons.locked", dungeon.Name, dungeon.UnlockStage)));
				_detail.AddChild(locked);
				return;
			}

			var noTeam = Teams.Of(_player, dungeon.Id).Count == 0;
			for (var number = 1; number <= dungeon.Floors.Count; number++)
				_detail.AddChild(FloorRow(dungeon, number, noTeam));
		}

		private Control FloorRow(DungeonDefinition dungeon, int number, bool noTeam)
		{
			var floor = dungeon.Floor(number);
			var cleared = number <= Dungeons.Cleared(_player, dungeon);
			var open = Dungeons.IsFloorUnlocked(_player, dungeon, number);

			var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel, Modulate = open ? Colors.White : new Color(1, 1, 1, 0.5f) };
			var row = Layout.Row(10);
			panel.AddChild(row);

			var badge = new Label { Text = number.ToString(), ThemeTypeVariation = GameTheme.Number, CustomMinimumSize = new Vector2(34, 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TooltipText = T("dungeons.floor", number), MouseFilter = MouseFilterEnum.Stop };
			badge.AddThemeFontSizeOverride("font_size", 26);
			badge.AddThemeColorOverride("font_color", cleared ? Palette.Spirit : Palette.Gold);
			row.AddChild(badge);

			var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			info.AddThemeConstantOverride("separation", 6);
			var chips = Layout.Flow(6);
			chips.AddChild(Layout.Chip("fight", T("common.stars_level", Texts.Stars(floor.Stars), floor.Level), T("campaign.foes")));
			if (dungeon.Kind == DungeonKind.Runes)
			{
				var grades = floor.MinGrade == floor.MaxGrade ? Texts.Stars(floor.MinGrade) : $"{Texts.Stars(floor.MinGrade)}–{Texts.Stars(floor.MaxGrade)}";
				chips.AddChild(Layout.Chip("rune", grades, T("dungeons.drop_rune", grades, Texts.Name(floor.MinRarity)), Palette.Of(floor.MinRarity)));
			}
			else
			{
				chips.AddChild(Layout.Chip("grindstone", $"×{floor.ToolCount}", T("dungeons.drop_tool", floor.ToolCount, Texts.Name((RuneRarity)floor.ToolGrade)), Palette.Of((RuneRarity)floor.ToolGrade)));
			}

			chips.AddChild(Layout.Chip("essence", floor.Essence.ToString(), T("currency.essence")));
			chips.AddChild(Layout.Chip("level_max", floor.Experience.ToString(), T("reward.experience")));
			if (!cleared)
				chips.AddChild(Layout.Chip("gold", floor.FirstClearGold.ToString(), T("dungeons.first_gold"), Palette.Spirit));
			info.AddChild(chips);

			var problem = Dungeons.Check(_player, dungeon, number);
			if (open && problem is EntryProblem.NoMana or EntryProblem.RunesFull)
			{
				var refusal = Layout.Text(Texts.Refusal(problem, floor.Mana), width: 300);
				refusal.AddThemeColorOverride("font_color", Palette.Negative);
				refusal.AddThemeFontSizeOverride("font_size", 13);
				info.AddChild(refusal);
			}

			row.AddChild(info);

			var disabled = noTeam || problem != EntryProblem.None;
			var fight = SigilButton.Of("fight", T("common.fight", floor.Mana), () => FightRequested?.Invoke(dungeon, number), 62);
			fight.Badge = floor.Mana.ToString();
			fight.Disabled = disabled;
			row.AddChild(fight);
			if (cleared)
			{
				var resolve = SigilButton.Of("resolve", T("common.resolve", floor.Mana), () => ResolveRequested?.Invoke(dungeon, number), 56);
				resolve.Badge = floor.Mana.ToString();
				resolve.Disabled = disabled;
				row.AddChild(resolve);
				var repeat = SigilButton.Of("repeat", T("common.auto_battle", AutoBattle.RepeatRuns), () => RepeatRequested?.Invoke(dungeon, number), 56);
				repeat.Badge = $"×{AutoBattle.RepeatRuns}";
				repeat.Disabled = disabled;
				row.AddChild(repeat);
			}

			return panel;
		}
	}
}
