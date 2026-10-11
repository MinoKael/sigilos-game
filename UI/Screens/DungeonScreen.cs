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
	/// As Masmorras: à esquerda uma por Masmorra, com o nome, o chefe e os andares vencidos (fechada,
	/// diz a fase que abre); à direita a escolhida — o que ela solta, escrito, a última equipe usada nela
	/// e os andares, cada um com o nível dos inimigos, o que rende e os botões Lutar, que abre a
	/// preparação da luta (<see cref="PrepScreen"/>), e Batalha automática (em andar já vencido). Cada
	/// vitória custa a Mana do andar.
	/// </summary>
	public partial class DungeonScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly VBoxContainer _list = new() { Name = "Dungeons" };
		private readonly VBoxContainer _detail = new() { Name = "Detail" };
		private readonly Label _message = new() { Name = "Message", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private DungeonDefinition _selected;

		public DungeonScreen(GameDatabase database, PlayerState player, string? selected)
		{
			_database = database;
			_player = player;
			_selected = database.Dungeons.FirstOrDefault(d => d.Id == selected) ?? database.Dungeons[0];
		}

		public event Action<DungeonDefinition, int>? FightRequested;

		/// <summary>A Batalha automática do andar: Masmorra e andar; o GameRoot abre a escolha de quantas lutas.</summary>
		public event Action<DungeonDefinition, int>? RepeatRequested;

		/// <summary>Falta Mana: o botão que leva à Loja.</summary>
		public event Action<DungeonDefinition>? ShopRequested;

		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Dungeons"), _currencies, () => BackRequested?.Invoke()).Header);

			var body = Layout.Row(Space.Loose).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			_list.AddThemeConstantOverride("separation", Space.Regular);
			var listScroll = Layout.Scroll(_list).Named("List");
			listScroll.CustomMinimumSize = new Vector2(320, 0);
			listScroll.SizeFlagsHorizontal = SizeFlags.Fill;
			body.AddChild(listScroll);

			var panel = new PanelContainer { Name = "Dungeon", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_detail.AddThemeConstantOverride("separation", Space.Regular);
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
				var detail = open
					? T("dungeons.progress", Dungeons.Cleared(_player, dungeon), dungeon.Floors.Count)
					: T("dungeons.opens_at", dungeon.UnlockStage);
				var card = new TileButton(dungeon.Name, detail, Art.Creature(dungeon.Image), new Vector2(310, 96), ButtonKind.Secondary, horizontal: true, iconSize: 64)
				{
					Name = Layout.NodeName(dungeon.Id),
					ToggleMode = true,
					ButtonPressed = dungeon == _selected,
				};
				if (!open)
					card.Modulate = new Color(1, 1, 1, 0.6f);
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
			var title = Layout.Row(Space.Large).Named("Header");
			title.AddChild(Doodle.Icon(Art.Creature(dungeon.Image), 56, Palette.Gold).Named("Art"));
			title.AddChild(new Label { Name = "Title", Text = dungeon.Name, ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
			_detail.AddChild(title);

			var drops = Layout.Flow(Space.Medium).Named("Drops");
			if (dungeon.Kind == DungeonKind.Runes)
			{
				drops.AddChild(new Label { Name = "Title", Text = T("dungeons.drops_sets"), VerticalAlignment = VerticalAlignment.Center });
				foreach (var set in dungeon.Sets)
				{
					var chip = Layout.Labeled(RuneSets.For(set).Glyph, "", Texts.Name(set)).Named(set.ToString());
					chip.MouseFilter = MouseFilterEnum.Stop;
					var captured = set;
					Press.On(chip, () => Dialog.Info(chip, Texts.Name(captured), Texts.Describe(RuneSets.For(captured))), () => Dialog.Info(chip, Texts.Name(captured), Texts.Describe(RuneSets.For(captured))));
					drops.AddChild(chip);
				}
			}
			else
			{
				drops.AddChild(new Label { Name = "Title", Text = T("dungeons.drops_tools"), VerticalAlignment = VerticalAlignment.Center });
				drops.AddChild(Layout.Labeled("grindstone", "", T("dungeons.grindstones")));
				drops.AddChild(Layout.Labeled("gem", "", T("dungeons.gems")));
			}

			_detail.AddChild(drops);
			if (dungeon.Kind == DungeonKind.Runes)
				_detail.AddChild(Layout.Text(T("dungeons.sets_hint"), GameTheme.Faded).Named("SetsHint"));
			_detail.AddChild(new TeamStrip(_database, _player, dungeon.Id));
			_detail.AddChild(new HSeparator { Name = "FloorsLine" });

			if (!Dungeons.IsUnlocked(_player, dungeon))
			{
				var locked = Layout.Row(Space.Regular).Named("Locked");
				locked.AddChild(Doodle.Icon(Art.Icon("lock"), 40, Palette.Gold));
				// O texto quebra linha: numa fileira, ele precisa da largura que sobra, senão sai uma letra por linha.
				var text = Layout.Text(T("dungeons.locked", dungeon.Name, dungeon.UnlockStage)).Named("Text");
				text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				text.VerticalAlignment = VerticalAlignment.Center;
				locked.AddChild(text);
				_detail.AddChild(locked);
				return;
			}

			for (var number = 1; number <= dungeon.Floors.Count; number++)
				_detail.AddChild(FloorRow(dungeon, number).Named($"Floor{number}"));
		}

		private Control FloorRow(DungeonDefinition dungeon, int number)
		{
			var floor = dungeon.Floor(number);
			var cleared = number <= Dungeons.Cleared(_player, dungeon);
			var open = Dungeons.IsFloorUnlocked(_player, dungeon, number);

			var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel, Modulate = open ? Colors.White : new Color(1, 1, 1, 0.5f) };
			var row = Layout.Row(Space.Large).Named("Row");
			panel.AddChild(row);

			var info = new VBoxContainer { Name = "Info", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			info.AddThemeConstantOverride("separation", Space.Small);
			var head = Layout.Row(Space.Regular).Named("Head");
			var name = new Label { Name = "Floor", Text = T("dungeons.floor", number), ThemeTypeVariation = GameTheme.Heading };
			name.AddThemeColorOverride("font_color", cleared ? Palette.Spirit : Palette.Gold);
			head.AddChild(name);
			head.AddChild(new Label { Name = "Foes", Text = T("common.stars_level", Texts.Stars(floor.Stars), floor.Level), VerticalAlignment = VerticalAlignment.Center });
			if (cleared)
				head.AddChild(new Label { Name = "Cleared", Text = T("campaign.cleared"), ThemeTypeVariation = GameTheme.Faded, VerticalAlignment = VerticalAlignment.Center });
			info.AddChild(head);

			// O drop do andar com as chances, uma linha para as estrelas da runa (ou quantas pedras) e outra
			// para a raridade: a porcentagem em destaque, o que ela sorteia escrito ao lado.
			var grades = Layout.Flow(Space.Small).Named("Grades");
			if (dungeon.Kind == DungeonKind.Runes)
			{
				foreach (var (grade, chance) in floor.Grades.Where(g => g.Value > 0).OrderBy(g => g.Key))
					grades.AddChild(Layout.Labeled("rune", Texts.Percent(chance / 100), Texts.Stars(grade)).Named($"Grade{grade}"));
			}
			else
			{
				grades.AddChild(Layout.Labeled("grindstone", $"×{floor.ToolCount}", T("dungeons.kind.Tools")).Named("Tools"));
			}

			info.AddChild(grades);
			var rarities = Layout.Flow(Space.Small).Named("Rarities");
			foreach (var (rarity, chance) in floor.Rarities.Where(r => r.Value > 0).OrderBy(r => r.Key))
				rarities.AddChild(Layout.Labeled("gem", Texts.Percent(chance / 100), Texts.Name(rarity), Palette.Of(rarity)).Named(rarity.ToString()));
			info.AddChild(rarities);

			var chips = Layout.Flow(Space.Small).Named("Rewards");
			chips.AddChild(Layout.Labeled("essence", Texts.Number(floor.Essence), T("currency.essence")));
			chips.AddChild(Layout.Labeled("level_max", floor.Experience.ToString(), T("reward.experience")).Named("Experience"));
			if (floor.ScrollChance > 0)
				chips.AddChild(Layout.Labeled("scroll", Texts.Percent(floor.ScrollChance / 100), T("dungeons.scroll_chance")).Named("Scroll"));
			if (floor.CoreChance > 0)
				chips.AddChild(Layout.Labeled("monster", Texts.Percent(floor.CoreChance / 100), T("dungeons.core_chance"), Palette.Arcane).Named("Core"));
			if (!cleared)
			{
				chips.AddChild(Layout.Labeled("gold", floor.FirstClearGold.ToString(), T("dungeons.first_gold"), Palette.Spirit).Named("FirstClearGold"));
				BattleResultPanel.AddPrize(chips, Milestones.ForFirstClear(floor));
			}

			info.AddChild(chips);

			var problem = Dungeons.Check(_player, dungeon, number);
			if (open && problem is EntryProblem.NoMana or EntryProblem.RunesFull)
			{
				var refusal = Layout.Text(Texts.Refusal(problem, floor.Mana)).Named("Refusal");
				refusal.AddThemeColorOverride("font_color", Palette.Negative);
				refusal.AddThemeFontSizeOverride("font_size", FontSize.Note);
				info.AddChild(refusal);
				if (problem == EntryProblem.NoMana)
					info.AddChild(GameButton.Of(T("common.buy_mana"), () => ShopRequested?.Invoke(dungeon), ButtonKind.Secondary, "shop", 44).Named("Shop"));
			}

			row.AddChild(info);

			var buttons = new VBoxContainer { Name = "Buttons", Alignment = BoxContainer.AlignmentMode.Center };
			buttons.AddThemeConstantOverride("separation", Space.Medium);
			var disabled = problem != EntryProblem.None;
			var fight = GameButton.Of(T("common.fight"), () => FightRequested?.Invoke(dungeon, number), ButtonKind.Primary, "fight").Named("Fight");
			fight.Disabled = disabled;
			buttons.AddChild(fight.Wide(190));
			if (cleared)
			{
				var repeat = GameButton.Of(T("common.auto_short"), () => RepeatRequested?.Invoke(dungeon, number), ButtonKind.Secondary, "repeat", 48).Named("AutoBattle");
				repeat.Disabled = disabled;
				buttons.AddChild(repeat.Wide(190));
			}

			row.AddChild(buttons);
			return panel;
		}
	}
}
