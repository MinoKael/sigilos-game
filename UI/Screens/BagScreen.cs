using System;
using System.Linq;
using Godot;
using Sigilos.Core.Player;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A Bolsa: um círculo de conjuração com o que o jogador carrega em volta — runas no alto, monstros,
	/// equipes, Grimório e Compêndio — e o portal de invocar no centro.
	/// </summary>
	public partial class BagScreen : Control
	{
		private static readonly Destination[] Around =
		{
			Destination.Runes, Destination.Monsters, Destination.Teams, Destination.Grimoire, Destination.Compendium,
		};

		private readonly PlayerState _player;
		private readonly CurrencyBar _currencies = new();

		public BagScreen(PlayerState player)
		{
			_player = player;
		}

		public event Action<Destination>? Requested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			var ring = new SigilRing(540) { Name = "Ring", Spread = 0.74f };
			AddChild(Layout.Background(ring));
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Bag"), "bag", _currencies, () => BackRequested?.Invoke()).Header);
			_currencies.Refresh(_player);

			var center = new CenterContainer { Name = "Center", SizeFlagsVertical = SizeFlags.ExpandFill };
			center.AddChild(ring);
			page.AddChild(center);

			var portal = Sigil(Destination.Summon, 124, SigilShape.Circle);
			portal.Highlight = _player.TotalPulls == 0;
			ring.Set(portal, Around.Select(d => (Control)Sigil(d, 92, SigilShape.Square)).ToList());
		}

		private SigilButton Sigil(Destination destination, float size, SigilShape shape) =>
			SigilButton.Of(Destinations.Icon(destination), Destinations.Name(destination), () => Requested?.Invoke(destination), size, shape).Named(destination.ToString());
	}
}
