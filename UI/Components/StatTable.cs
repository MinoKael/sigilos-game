using System;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A ficha de atributos: Glifo e nome, valor de base e, em verde, o que as runas somam. Passar o
	/// mouse num atributo explica o que ele faz.
	/// </summary>
	public partial class StatTable : GridContainer
	{
		public StatTable()
		{
			Columns = 4;
			AddThemeConstantOverride("h_separation", 12);
			AddThemeConstantOverride("v_separation", 2);
		}

		public void Show(StatSheet sheet)
		{
			Layout.Clear(this);
			foreach (var stat in Enum.GetValues<Stat>())
			{
				var icon = Doodle.Icon(Art.Glyph(Texts.GlyphOf(stat)), 18, Palette.Gold);
				icon.TooltipText = Texts.Name(Texts.GlyphOf(stat));
				icon.MouseFilter = MouseFilterEnum.Stop;
				AddChild(icon);
				AddChild(new Label { Text = Texts.Name(stat), TooltipText = Texts.Plain(Texts.Explain(stat)), MouseFilter = MouseFilterEnum.Stop });

				var baseValue = new Label { Text = Texts.Value(stat, sheet.Base.Get(stat)), HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(64, 0) };
				AddChild(baseValue);

				var bonus = sheet.Runes.Stats.Get(stat);
				var bonusLabel = new Label { Text = bonus > 0.0005 ? $"+{Texts.Value(stat, bonus)}" : "", CustomMinimumSize = new Vector2(64, 0) };
				bonusLabel.AddThemeColorOverride("font_color", Palette.Positive);
				AddChild(bonusLabel);
			}
		}
	}
}
