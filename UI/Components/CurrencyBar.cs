using Godot;
using Sigilos.Core.Player;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>Faixa com as quatro moedas. Só mostra; quem muda é o GameRoot.</summary>
	public partial class CurrencyBar : PanelContainer
	{
		private readonly Label _scrolls = new();
		private readonly Label _essence = new();
		private readonly Label _dust = new();
		private readonly Label _fragments = new();

		public CurrencyBar()
		{
			ThemeTypeVariation = GameTheme.InsetPanel;
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 22);
			AddChild(row);

			Add(row, "scroll", _scrolls, "Pergaminhos Místicos: invocam criaturas.");
			Add(row, "essence", _essence, "Essência: sobe o nível das invocações e paga o Despertar.");
			Add(row, "dust", _dust, "Pó de Sigilo: melhora e tira runas. Escasso: a melhora nunca falha.");
			Add(row, "fragments", _fragments, "Fragmentos: vêm de duplicatas além dos 5 Ecos.");
		}

		public void Refresh(PlayerState player)
		{
			_scrolls.Text = player.Scrolls.ToString();
			_essence.Text = player.Essence.ToString();
			_dust.Text = player.Dust.ToString();
			_fragments.Text = player.Fragments.ToString();
		}

		private static void Add(HBoxContainer row, string icon, Label label, string tooltip)
		{
			var box = new HBoxContainer { TooltipText = tooltip, MouseFilter = MouseFilterEnum.Stop };
			box.AddChild(Doodle.Icon(Art.Icon(icon), 24, Palette.Gold));
			label.MouseFilter = MouseFilterEnum.Ignore;
			box.AddChild(label);
			row.AddChild(box);
		}
	}
}
