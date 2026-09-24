using System.Collections.Generic;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.UI.Style;
using Side = Sigilos.Core.Battle.Side;

namespace Sigilos.UI.Components
{
	/// <summary>Os próximos a agir, da esquerda para a direita, na cor de cada um.</summary>
	public partial class TurnOrderBar : HBoxContainer
	{
		private readonly string _conjurerImage;

		public TurnOrderBar(string conjurerImage)
		{
			_conjurerImage = conjurerImage;
			AddThemeConstantOverride("separation", 6);
		}

		public void Show(IReadOnlyList<ITurnTaker> order)
		{
			Layout.Clear(this);

			var label = new Label { Text = "Próximos:", ThemeTypeVariation = GameTheme.OnStone };
			AddChild(label);

			foreach (var taker in order)
			{
				var (image, ink, border) = taker switch
				{
					BattleUnit unit => (unit.Image, Palette.Of(unit.Element), unit.Side == Side.Allies ? Palette.Health : Palette.HealthLow),
					_ => (_conjurerImage, Palette.Gold, Palette.Gold),
				};

				var frame = new PanelContainer { TooltipText = taker.Name, MouseFilter = MouseFilterEnum.Stop };
				frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Parchment, border, 2, 4, 2));
				frame.AddChild(Doodle.Icon(Art.Creature(image), 34, ink));
				AddChild(frame);
			}
		}
	}
}
