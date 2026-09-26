using System.Collections.Generic;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;
using Side = Sigilos.Core.Battle.Side;

namespace Sigilos.UI.Components
{
	/// <summary>Os próximos a agir, da esquerda para a direita, depois do símbolo de velocidade. Moldura verde: aliado; vermelha: inimigo.</summary>
	public partial class TurnOrderBar : HBoxContainer
	{
		public TurnOrderBar()
		{
			AddThemeConstantOverride("separation", 6);
		}

		public void Show(IReadOnlyList<BattleUnit> order)
		{
			Layout.Clear(this);
			var icon = Doodle.Icon(Art.Icon("speed"), 28, Palette.GoldDark.Lightened(0.3f));
			icon.TooltipText = T("battle.next_up");
			icon.MouseFilter = MouseFilterEnum.Stop;
			AddChild(icon);

			foreach (var unit in order)
			{
				var frame = new PanelContainer { TooltipText = unit.Name, MouseFilter = MouseFilterEnum.Stop };
				frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, unit.Side == Side.Allies ? Palette.Health : Palette.HealthLow, 2, 18, 2));
				frame.AddChild(Layout.Medal(Art.Creature(unit.Image), Palette.Of(unit.Element), 34));
				AddChild(frame);
			}
		}
	}
}
