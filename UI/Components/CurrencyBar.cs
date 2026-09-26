using Godot;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O cabeçalho de recursos, igual em toda tela: uma cápsula de pedra por moeda, só símbolo e número
	/// (a Mana como atual/máxima). O nome vem na dica; a explicação de cada moeda mora no Compêndio.
	/// Só mostra; quem muda é o GameRoot.
	/// </summary>
	public partial class CurrencyBar : HBoxContainer
	{
		private readonly Label _mana = new();
		private readonly Label _essence = new();
		private readonly Label _gold = new();
		private readonly Label _scrolls = new();
		private readonly Label _fragments = new();

		public CurrencyBar()
		{
			AddThemeConstantOverride("separation", 6);
			Add("mana", _mana, T("currency.mana"));
			Add("essence", _essence, T("currency.essence"));
			Add("gold", _gold, T("currency.gold"));
			Add("scroll", _scrolls, T("currency.scrolls"));
			Add("fragments", _fragments, T("currency.fragments"));
		}

		public void Refresh(PlayerState player)
		{
			_mana.Text = T("currency.mana_of", player.Mana, Mana.Max(player));
			_essence.Text = Texts.Short(player.Essence);
			_gold.Text = Texts.Short(player.Gold);
			_scrolls.Text = Texts.Short(player.Scrolls);
			_fragments.Text = Texts.Short(player.Fragments);
		}

		private void Add(string icon, Label label, string tooltip)
		{
			var capsule = new PanelContainer { TooltipText = tooltip, MouseFilter = MouseFilterEnum.Stop };
			var box = GameTheme.Carved(Palette.Inset, 4);
			box.SetCornerRadiusAll(16);
			box.ContentMarginLeft = 6;
			box.ContentMarginRight = 12;
			capsule.AddThemeStyleboxOverride("panel", box);

			var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", 6);
			row.AddChild(Doodle.Icon(Art.Icon(icon), 24, Palette.Gold));
			label.MouseFilter = MouseFilterEnum.Ignore;
			label.ThemeTypeVariation = GameTheme.Number;
			label.AddThemeFontSizeOverride("font_size", 16);
			row.AddChild(label);
			capsule.AddChild(row);
			AddChild(capsule);
		}
	}
}
