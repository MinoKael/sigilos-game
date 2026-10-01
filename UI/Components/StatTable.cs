using System;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A ficha de atributos: Glifo e nome, valor de base e, em verde, o que as runas somam. O que cada
	/// atributo faz está no Compêndio.
	/// </summary>
	public partial class StatTable : GridContainer
	{
		public StatTable()
		{
			Columns = 4;
			AddThemeConstantOverride("h_separation", 12);
			AddThemeConstantOverride("v_separation", 0);
		}

		public void Show(StatSheet sheet)
		{
			Layout.Clear(this);
			foreach (var stat in Enum.GetValues<Stat>())
			{
				// Quatro células por atributo, com o nome dele na frente: HpGlyph, HpName, HpBase, HpBonus.
				AddChild(new RuneGlyph(Texts.GlyphOf(stat), 20, Palette.Gold) { Name = $"{stat}Glyph" });
				AddChild(new Label { Name = $"{stat}Name", Text = Texts.Name(stat) });

				var baseValue = new Label { Name = $"{stat}Base", Text = Texts.Value(stat, sheet.Base.Get(stat)), HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(64, 0) };
				AddChild(baseValue);

				var bonus = sheet.Runes.Stats.Get(stat);
				var bonusLabel = new Label { Name = $"{stat}Bonus", Text = bonus > 0.0005 ? $"+{Texts.Value(stat, bonus)}" : "", CustomMinimumSize = new Vector2(64, 0) };
				bonusLabel.AddThemeColorOverride("font_color", Palette.Positive);
				AddChild(bonusLabel);
			}
		}
	}
}
