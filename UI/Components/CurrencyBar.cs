using Godot;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>Faixa com as moedas e o nível compartilhado. Só mostra; quem muda é o GameRoot.</summary>
	public partial class CurrencyBar : PanelContainer
	{
		private readonly Label _scrolls = new();
		private readonly Label _essence = new();
		private readonly Label _fragments = new();
		private readonly Label _level = new();

		public CurrencyBar()
		{
			ThemeTypeVariation = GameTheme.DarkPanel;
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 22);
			AddChild(row);

			Add(row, "scroll", _scrolls, "Pergaminhos Místicos: invocam criaturas.");
			Add(row, "essence", _essence, "Essência: eleva o nível compartilhado.");
			Add(row, "fragments", _fragments, "Fragmentos: vêm de duplicatas além dos 5 Ecos.");
			_level.ThemeTypeVariation = GameTheme.OnStone;
			_level.TooltipText = "Nível compartilhado por todas as invocações e pelo Conjurador.";
			_level.MouseFilter = MouseFilterEnum.Stop;
			row.AddChild(_level);
		}

		public void Refresh(PlayerState player)
		{
			_scrolls.Text = player.Scrolls.ToString();
			_essence.Text = player.Essence.ToString();
			_fragments.Text = player.Fragments.ToString();
			_level.Text = $"Nível {player.Level}/{SharedLevel.RegionOneCap}";
		}

		private static void Add(HBoxContainer row, string icon, Label label, string tooltip)
		{
			var box = new HBoxContainer { TooltipText = tooltip, MouseFilter = MouseFilterEnum.Stop };
			box.AddChild(Doodle.Icon(Art.Icon(icon), 24, Palette.Bone));
			label.ThemeTypeVariation = GameTheme.OnStone;
			label.MouseFilter = MouseFilterEnum.Ignore;
			box.AddChild(label);
			row.AddChild(box);
		}
	}
}
