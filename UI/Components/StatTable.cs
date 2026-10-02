using System;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A ficha de atributos: Glifo e nome, o total (base mais runas e conjuntos) em branco e, em verde e um
	/// pouco menor, quanto disso as runas somaram. O que cada atributo faz está no Compêndio.
	/// </summary>
	public partial class StatTable : GridContainer
	{
		/// <summary>O bônus fica um pouco menor que o total: o total é o número que importa.</summary>
		private const int BonusSize = GameTheme.BodySize - 3;

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
				// Quatro células por atributo, com o nome dele na frente: HpGlyph, HpName, HpTotal, HpBonus.
				AddChild(new RuneGlyph(Texts.GlyphOf(stat), 20, Palette.Gold) { Name = $"{stat}Glyph" });
				AddChild(new Label { Name = $"{stat}Name", Text = Texts.Name(stat) });

				var total = new Label { Name = $"{stat}Total", Text = Texts.Value(stat, sheet.Total.Get(stat)), HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(64, 0) };
				AddChild(total);

				var bonus = sheet.Runes.Stats.Get(stat);
				var bonusLabel = new Label { Name = $"{stat}Bonus", Text = bonus > 0.0005 ? $"+{Texts.Value(stat, bonus)}" : "", CustomMinimumSize = new Vector2(64, 0), VerticalAlignment = VerticalAlignment.Center };
				bonusLabel.AddThemeColorOverride("font_color", Palette.Positive);
				bonusLabel.AddThemeFontSizeOverride("font_size", BonusSize);
				AddChild(bonusLabel);
			}
		}
	}
}
