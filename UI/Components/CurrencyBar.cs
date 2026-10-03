using Godot;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O cabeçalho de recursos, igual em toda tela: uma cápsula de pedra por moeda, símbolo e número (a
	/// Mana como atual/máxima). Tocar numa cápsula abre, colada nela, o que a moeda é, de onde vem e para
	/// que serve. Só mostra; quem muda é o GameRoot.
	/// </summary>
	public partial class CurrencyBar : HBoxContainer
	{
		private readonly Label _mana = new();
		private readonly Label _essence = new();
		private readonly Label _gold = new();
		private readonly Label _scrolls = new();
		private readonly Label _fragments = new();
		private int _maxMana;

		/// <summary>A altura de cada cápsula.</summary>
		public const float Height = 44;

		public CurrencyBar()
		{
			Name = "Currencies";
			AddThemeConstantOverride("separation", 6);
			Add("Mana", "mana", _mana, () => T("currency_info.mana", Mana.PerHour, _maxMana), T("currency.mana"));
			Add("Essence", "essence", _essence, () => T("currency_info.essence", Leveling.ExperiencePerEssence), T("currency.essence"));
			Add("Gold", "gold", _gold, () => T("currency_info.gold", Account.LevelUpGold), T("currency.gold"));
			Add("Scrolls", "scroll", _scrolls, () => T("currency_info.scrolls"), T("currency.scrolls_name"));
			Add("Fragments", "fragments", _fragments, () => T("currency_info.fragments"), T("currency.fragments"));
		}

		public override void _EnterTree()
		{
			UiSession.Changed += RefreshFromSession;
			// O aviso da Batalha automática fica à esquerda das moedas.
			AddToGroup(AutoBattleBadge.CornerGroup);
		}

		public override void _ExitTree() => UiSession.Changed -= RefreshFromSession;

		public void Refresh(PlayerState player)
		{
			_maxMana = Mana.Max(player);
			_mana.Text = T("currency.mana_of", player.Mana, _maxMana);
			_essence.Text = Texts.Number(player.Essence);
			_gold.Text = Texts.Number(player.Gold);
			_scrolls.Text = Texts.Number(player.Scrolls);
			_fragments.Text = Texts.Number(player.Fragments);
		}

		private void RefreshFromSession()
		{
			if (UiSession.Player is { } player)
				Refresh(player);
		}

		/// <summary>
		/// A cápsula de uma moeda: <paramref name="name"/> é o nome do nó (<c>Mana</c>), com <c>Row</c>,
		/// <c>Icon</c> e <c>Value</c> dentro. O toque abre a explicação (<paramref name="explain"/>, lida na
		/// hora, com os números de agora).
		/// </summary>
		private void Add(string name, string icon, Label label, System.Func<string> explain, string title)
		{
			var capsule = Capsule(name);

			var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore };
			row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			row.OffsetLeft = 6;
			row.OffsetRight = -12;
			row.AddThemeConstantOverride("separation", 6);
			row.AddChild(Doodle.Icon(Art.Icon(icon), 26, Palette.Gold).Named("Icon"));
			label.Name = "Value";
			label.MouseFilter = MouseFilterEnum.Ignore;
			label.ThemeTypeVariation = GameTheme.Number;
			label.VerticalAlignment = VerticalAlignment.Center;
			label.SizeFlagsVertical = SizeFlags.ExpandFill;
			label.AddThemeFontSizeOverride("font_size", 17);
			row.AddChild(label);
			capsule.AddChild(row);

			// O botão não mede os filhos: a largura acompanha o número.
			void Fit() => capsule.CustomMinimumSize = new Vector2(row.GetCombinedMinimumSize().X + 18, Height);
			label.Resized += Fit;
			row.MinimumSizeChanged += Fit;
			capsule.Pressed += () => Dialog.Info(capsule, title, explain());
			AddChild(capsule);
		}

		/// <summary>A cápsula vazia, de pedra entalhada, com a borda de ouro no toque; a altura é <see cref="Height"/>.</summary>
		public static Button Capsule(string name)
		{
			var capsule = new Button { Name = name, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
			var box = GameTheme.Carved(Palette.Inset, 4);
			box.SetCornerRadiusAll(18);
			box.ContentMarginLeft = 6;
			box.ContentMarginRight = 12;
			capsule.AddThemeStyleboxOverride("normal", box);
			var lit = (StyleBoxFlat)box.Duplicate();
			lit.BorderColor = Palette.Gold;
			lit.SetBorderWidthAll(1);
			capsule.AddThemeStyleboxOverride("hover", lit);
			capsule.AddThemeStyleboxOverride("pressed", lit);
			capsule.AddThemeStyleboxOverride("hover_pressed", lit);
			capsule.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			capsule.CustomMinimumSize = new Vector2(0, Height);
			return capsule;
		}
	}
}
