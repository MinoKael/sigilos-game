using Godot;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>Faixa com a Mana (atual / máxima) e as moedas. Só mostra; quem muda é o GameRoot.</summary>
	public partial class CurrencyBar : PanelContainer
	{
		private readonly Label _mana = new();
		private readonly Label _essence = new();
		private readonly Label _gold = new();
		private readonly Label _scrolls = new();
		private readonly Label _fragments = new();

		public CurrencyBar()
		{
			ThemeTypeVariation = GameTheme.InsetPanel;
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 20);
			AddChild(row);

			Add(row, "mana", _mana, T("moeda.dica.mana", Mana.PerHour, Mana.BaseMax, Mana.BaseMax + Mana.MaxFromLevels, Account.MaxLevel));
			Add(row, "essence", _essence, T("moeda.dica.essencia"));
			Add(row, "gold", _gold, T("moeda.dica.ouro"));
			Add(row, "scroll", _scrolls, T("moeda.dica.pergaminhos"));
			Add(row, "fragments", _fragments, T("moeda.dica.fragmentos"));
		}

		public void Refresh(PlayerState player)
		{
			_mana.Text = T("moeda.mana_de", player.Mana, Mana.Max(player));
			_essence.Text = player.Essence.ToString();
			_gold.Text = player.Gold.ToString();
			_scrolls.Text = player.Scrolls.ToString();
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
