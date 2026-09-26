using System.Collections.Generic;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;
using Side = Sigilos.Core.Battle.Side;

namespace Sigilos.UI.Components
{
	/// <summary>Os próximos a agir, da esquerda para a direita. Borda verde: aliado; vermelha: inimigo.</summary>
	public partial class TurnOrderBar : HBoxContainer
	{
		public TurnOrderBar()
		{
			AddThemeConstantOverride("separation", 6);
		}

		public void Show(IReadOnlyList<BattleUnit> order)
		{
			Layout.Clear(this);
			AddChild(new Label { Text = T("battle.next_up") });

			foreach (var unit in order)
			{
				var frame = new PanelContainer { TooltipText = unit.Name, MouseFilter = MouseFilterEnum.Stop };
				frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, unit.Side == Side.Allies ? Palette.Health : Palette.HealthLow, 2, 4, 2));
				frame.AddChild(Doodle.Icon(Art.Creature(unit.Image), 34, Palette.Of(unit.Element)));
				AddChild(frame);
			}
		}
	}
}
