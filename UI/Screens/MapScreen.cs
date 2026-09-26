using System;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// O Mapa: três portais altos lado a lado — o que ainda não existe (Torre e Provações, fechado), a
	/// Campanha com a próxima fase (região-fase) e as Masmorras com o chefe da primeira aberta. Cada
	/// portal tem uma barra de energia com o progresso.
	/// </summary>
	public partial class MapScreen : Control
	{
		private static readonly Vector2 DoorSize = new(250, 400);

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly CurrencyBar _currencies = new();

		public MapScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
		}

		public event Action<Destination>? Requested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			var row = Layout.Row(36, true);
			AddChild(Layout.Background(row));
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Map"), "map", _currencies, () => BackRequested?.Invoke()).Header);
			_currencies.Refresh(_player);

			var center = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			center.AddChild(row);
			page.AddChild(center);

			row.AddChild(new DoorCard(Art.Icon("tower"), Palette.Gold, T("map.locked"), true, DoorSize));
			row.AddChild(Campaign());
			row.AddChild(Dungeons());
		}

		private Control Campaign()
		{
			var next = Math.Min(_player.HighestStage + 1, _database.Stages.Count);
			var stage = _database.Stage(next);
			var door = new DoorCard(Art.Icon("region"), Palette.Gold, T("map.campaign", stage.Name), false, DoorSize);
			door.Footer.AddChild(Caption(T("map.stage", 1, next)));
			door.Footer.AddChild(Progress(_player.HighestStage, _database.Stages.Count, T("map.campaign_progress", _player.HighestStage, _database.Stages.Count)));
			door.Pressed += () => Requested?.Invoke(Destination.Campaign);
			return door;
		}

		private Control Dungeons()
		{
			var open = _database.Dungeons.Where(d => Core.Progression.Dungeons.IsUnlocked(_player, d)).ToList();
			var shown = open.LastOrDefault() ?? _database.Dungeons[0];
			var cleared = _database.Dungeons.Sum(d => Core.Progression.Dungeons.Cleared(_player, d));
			var total = _database.Dungeons.Sum(d => d.Floors.Count);
			var door = new DoorCard(Art.Creature(shown.Image), Palette.Gold, T("map.dungeons", open.Count, _database.Dungeons.Count), false, DoorSize);
			door.Footer.AddChild(Caption($"{open.Count}/{_database.Dungeons.Count}"));
			door.Footer.AddChild(Progress(cleared, total, T("map.dungeons_progress", cleared, total)));
			door.Pressed += () => Requested?.Invoke(Destination.Dungeons);
			return door;
		}

		private static Label Caption(string text)
		{
			var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = GameTheme.Number, MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", 28);
			label.AddThemeColorOverride("font_color", Palette.Gold);
			return label;
		}

		private static ProgressBar Progress(int value, int max, string tooltip)
		{
			var bar = Layout.Energy(Palette.Arcane, 10);
			bar.MaxValue = Math.Max(1, max);
			bar.Value = value;
			bar.TooltipText = tooltip;
			bar.MouseFilter = MouseFilterEnum.Ignore;
			return bar;
		}
	}
}
