using System;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A escolha de batalha: três portais grandes, cada um com o nome e o progresso escritos — a
	/// Campanha (a próxima fase), as Masmorras (quantas abertas, andares vencidos) e a Exploração
	/// Estelar (a Exploração do mês e as constelações vencidas nele; fechada, a fase que abre).
	/// </summary>
	public partial class MapScreen : Control
	{
		private static readonly Vector2 DoorSize = new(300, 360);

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
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Map"), _currencies, () => BackRequested?.Invoke()).Header);
			_currencies.Refresh(_player);

			var center = new CenterContainer { Name = "Center", SizeFlagsVertical = SizeFlags.ExpandFill };
			var row = Layout.Row(32, true).Named("Doors"); // Fora da escala: as portas grandes do mapa.
			center.AddChild(row);
			page.AddChild(center);

			var next = Math.Min(_player.HighestStage + 1, _database.Stages.Count);
			var stage = _database.Stage(next);
			var campaign = new TileButton(T("destination.Campaign"), T("map.campaign_detail", next, stage.Name, _player.HighestStage, _database.Stages.Count), Art.Icon("region"), DoorSize, ButtonKind.Secondary, iconSize: 150) { Name = "Campaign" };
			campaign.Highlight = _player.HighestStage == 0;
			campaign.Pressed += () => Requested?.Invoke(Destination.Campaign);
			row.AddChild(campaign);

			var open = _database.Dungeons.Count(d => Core.Progression.Dungeons.IsUnlocked(_player, d));
			var cleared = _database.Dungeons.Sum(d => Core.Progression.Dungeons.Cleared(_player, d));
			var floors = _database.Dungeons.Sum(d => d.Floors.Count);
			// As Masmorras aparecem na fase que abre a primeira (Features); antes, a porta diz qual é.
			var dungeonsOpen = Core.Progression.Features.IsOpen(_player, _database, Core.Progression.Feature.Dungeons);
			var detail = dungeonsOpen
				? T("map.dungeons_detail", open, _database.Dungeons.Count, cleared, floors)
				: T("map.dungeons_opens", Core.Progression.Features.StageOf(_database, Core.Progression.Feature.Dungeons));
			var dungeons = new TileButton(T("destination.Dungeons"), detail, Art.Creature("crowned_skull"), DoorSize, ButtonKind.Secondary, iconSize: 150) { Name = "Dungeons", Disabled = !dungeonsOpen };
			dungeons.Highlight = Core.Progression.Features.IsNew(_player, _database, Core.Progression.Feature.Dungeons);
			dungeons.Pressed += () => Requested?.Invoke(Destination.Dungeons);
			row.AddChild(dungeons);

			var exploration = _database.Exploration;
			var explorationOpen = Core.Progression.Features.IsOpen(_player, _database, Core.Progression.Feature.Exploration) && exploration.Constellations.Count > 0;
			var now = DateTime.Now;
			var month = exploration.Explorations.Count > 0 ? exploration.Explorations[Core.Progression.Exploration.VariationOf(now) % exploration.Explorations.Count].Name : "";
			var starDetail = explorationOpen
				? T("map.exploration_detail", month, Core.Progression.Exploration.Cleared(_player, now), exploration.Constellations.Count)
				: T("map.exploration_opens", exploration.UnlockStage);
			var stars = new TileButton(T("map.exploration"), starDetail, Art.Icon("star_exploration"), DoorSize, ButtonKind.Secondary, iconSize: 150) { Name = "Exploration", Disabled = !explorationOpen };
			stars.Highlight = Core.Progression.Features.IsNew(_player, _database, Core.Progression.Feature.Exploration);
			stars.Pressed += () => Requested?.Invoke(Destination.Exploration);
			row.AddChild(stars);
		}
	}
}
