using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Botão grande de destino: o símbolo em cima (ou à esquerda, deitado), o nome escrito e uma linha
	/// de detalhe ("Fase 12 de 20"). É o jeito de entrar numa parte do jogo: a barra de baixo do
	/// Santuário (<see cref="Nav"/>), os cartões de Batalha e Invocar, os portais da escolha de batalha.
	/// Pulsa em verde com <see cref="Highlight"/> para guiar quem está começando.
	/// </summary>
	public partial class TileButton : TouchButton
	{
		private readonly Label _title;
		private readonly Label? _detail;
		private readonly StyleBoxFlat _normal;
		private readonly Color _border;

		/// <param name="texture">O desenho (ícone, criatura).</param>
		/// <param name="horizontal">Símbolo à esquerda e texto à direita; senão, símbolo em cima.</param>
		public TileButton(string title, string detail, Texture2D? texture, Vector2 size, ButtonKind kind = ButtonKind.Secondary, bool horizontal = false, float iconSize = 0)
			: base(0.97f)
		{
			CustomMinimumSize = size;

			// Os tons do botão de texto, um pouco mais escuros: o cartão é grande e fica atrás do que está escrito nele.
			var (fill, border) = Tones.Of(kind);
			fill = kind switch
			{
				ButtonKind.Primary => fill.Darkened(0.12f),
				ButtonKind.Secondary => Palette.Panel,
				_ => fill,
			};
			_border = border;
			_normal = Box(fill, border);
			AddThemeStyleboxOverride("normal", _normal);
			AddThemeStyleboxOverride("hover", Box(fill.Lightened(0.08f), Palette.Gold));
			AddThemeStyleboxOverride("pressed", Box(fill.Darkened(0.15f), border));
			AddThemeStyleboxOverride("hover_pressed", Box(fill.Darkened(0.15f), border));
			AddThemeStyleboxOverride("disabled", Box(Palette.Inset, Palette.Disabled));
			Pad(12, 8, 8);

			BoxContainer box = horizontal ? new HBoxContainer() : new VBoxContainer();
			box.Name = "Box";
			box.MouseFilter = MouseFilterEnum.Ignore;
			box.Alignment = BoxContainer.AlignmentMode.Center;
			box.AddThemeConstantOverride("separation", horizontal ? 16 : 4);
			Content.AddChild(box);

			var ink = Tones.Ink(kind);
			var iconSide = iconSize > 0 ? iconSize : horizontal ? size.Y * 0.62f : size.Y * 0.46f;
			var icon = Doodle.Icon(texture, (int)iconSide, ink);
			icon.SizeFlagsHorizontal = horizontal ? SizeFlags.ShrinkBegin : SizeFlags.ShrinkCenter;
			icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			box.AddChild(icon);

			var lines = new VBoxContainer { Name = "Text", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
			lines.AddThemeConstantOverride("separation", 0);
			lines.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			_title = new Label { Name = "Title", Text = title, HorizontalAlignment = horizontal ? HorizontalAlignment.Left : HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
			_title.AddThemeFontOverride("font", GameTheme.Serif);
			_title.AddThemeFontSizeOverride("font_size", horizontal ? (size.Y >= 120 ? 30 : 23) : size.Y >= 100 ? 19 : 16);
			_title.AddThemeColorOverride("font_color", ink);
			_title.AddThemeColorOverride("font_outline_color", Tones.WoodOutline);
			_title.AddThemeConstantOverride("outline_size", 5);
			lines.AddChild(_title);
			if (detail.Length > 0)
			{
				_detail = new Label { Name = "Detail", Text = detail, HorizontalAlignment = _title.HorizontalAlignment, MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
				_detail.AddThemeFontSizeOverride("font_size", horizontal ? 17 : 14);
				_detail.AddThemeColorOverride("font_color", kind == ButtonKind.Secondary ? Palette.TextFaded : Palette.Text);
				lines.AddChild(_detail);
			}

			if (horizontal)
				lines.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			box.AddChild(lines);
		}

		/// <summary>O botão da barra de baixo do Santuário: símbolo em cima e o nome embaixo.</summary>
		public static TileButton Nav(string title, string icon, float width = 128) => new(title, "", Art.Icon(icon), new Vector2(width, 86)) { Name = Layout.NodeName(icon) };

		/// <summary>O chamado apagou (ou acendeu): a moldura volta ao repouso, e o pulso a pinta de novo a cada quadro.</summary>
		protected override void OnHighlightChanged()
		{
			_normal.BorderColor = _border;
			_normal.ShadowColor = new Color(0, 0, 0, 0.4f);
			_normal.ShadowSize = 4;
		}

		/// <summary>A moldura e a sombra respiram em verde.</summary>
		protected override void OnPulse(float phase)
		{
			_normal.BorderColor = _border.Lerp(Palette.Spirit, phase);
			_normal.ShadowColor = new Color(Palette.Spirit, 0.25f + 0.3f * phase);
			_normal.ShadowSize = 10;
		}

		public override void _Ready()
		{
			base._Ready();

			// Botão de ligar (a Masmorra escolhida na lista): o ligado fica com a moldura azul arcana.
			if (ToggleMode)
			{
				var lit = (StyleBoxFlat)_normal.Duplicate();
				lit.BorderColor = Palette.Arcane;
				lit.BgColor = lit.BgColor.Lightened(0.06f);
				lit.ShadowColor = new Color(Palette.Arcane, 0.35f);
				lit.ShadowSize = 8;
				lit.ShadowOffset = Vector2.Zero;
				AddThemeStyleboxOverride("pressed", lit);
				AddThemeStyleboxOverride("hover_pressed", lit);
			}
		}

		private static StyleBoxFlat Box(Color fill, Color border)
		{
			var box = GameTheme.Box(fill, border, 2, 14, 0);
			box.BorderWidthBottom = 5;
			box.ShadowColor = new Color(0, 0, 0, 0.4f);
			box.ShadowSize = 4;
			box.ShadowOffset = new Vector2(0, 2);
			return box;
		}
	}
}
