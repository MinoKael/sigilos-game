using System;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma runa em miniatura, quadrada de cantos redondos: as estrelas no alto, o Glifo do conjunto grande
	/// no meio (na fonte das runas, na cor da raridade) e, embaixo, numa linha só, o espaço à esquerda e a
	/// melhora à direita. O espaço é o número dele por cima de uma seta que aponta para onde ele fica no
	/// círculo das runas (o 1 no alto, os outros em volta, no sentido do relógio). Sem runa, só o espaço,
	/// grande e apagado.
	///
	/// Toque curto é <see cref="Pressed"/>; toque longo abre a ficha da runa (<see cref="RuneCard"/>) numa
	/// janela colada nela. Escolhida, fica azul arcano; marcada para vender, ganha o ✓ verde. Na lista, a
	/// runa equipada mostra no lugar da seta o medalhão de quem a usa (apagado se ele está no Baú); a
	/// bloqueada, um cadeado no meio da linha de baixo. O toque passa para cima, então arrastar rola a
	/// lista.
	/// </summary>
	public partial class RuneTile : PanelContainer
	{
		/// <summary>O lado da pedra, em px, na escala 1.</summary>
		public const float Side = 60;

		public static readonly Vector2 TileSize = new(Side, Side);

		/// <summary>A linha de baixo (seta, cadeado e melhora): a altura do centro dela acima da borda, na escala 1.</summary>
		private const float BottomLine = 11;

		/// <summary>A distância da seta e da melhora até a borda do lado, na escala 1.</summary>
		private const float Inset = 5;

		/// <summary>O lado da seta do espaço na linha de baixo, na escala 1.</summary>
		private const float ArrowSide = 18;

		private readonly StyleBoxFlat _box;
		private readonly Color _color;
		private readonly Control _layer = new() { Name = "Layer", MouseFilter = MouseFilterEnum.Ignore };
		private readonly Doodle _check = new(Art.Icon("confirm"), Palette.Spirit, boil: false) { Name = "Check", Visible = false };
		private readonly float _scale;
		private readonly int _stars;
		private readonly Control? _slotMark;
		private readonly Press _press = new();
		private bool _selected;
		private bool _marked;

		public RuneTile(Rune? rune, int slot, float scale = 1)
		{
			Rune = rune;
			Slot = slot;
			_scale = scale;
			_stars = rune?.Grade ?? 0;
			CustomMinimumSize = TileSize * scale;
			MouseFilter = MouseFilterEnum.Pass;
			MouseDefaultCursorShape = CursorShape.PointingHand;
			_press.Tapped += () => Pressed?.Invoke(this);
			_press.Held += () =>
			{
				if (Rune != null)
					RuneDialog.Show(this, Rune);
			};

			_color = rune == null ? Palette.GoldDark : Palette.Of(rune.Rarity);
			_box = GameTheme.Box(Palette.Inset, _color, rune == null ? 1 : 2, (int)(9 * scale), 0);
			AddThemeStyleboxOverride("panel", _box);
			AddChild(_layer);

			if (rune == null)
			{
				var side = 32 * scale;
				var mark = SlotMark(slot, side, new Color(Palette.GoldDark, 0.6f), new Color(Palette.Gold, 0.9f), 17);
				mark.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
				mark.OffsetLeft = mark.OffsetTop = -side / 2;
				mark.OffsetRight = mark.OffsetBottom = side / 2;
				_layer.AddChild(mark);
			}
			else
			{
				// O Glifo ocupa o miolo, um pouco abaixo do centro para deixar a fileira de cima livre.
				var glyph = new RuneGlyph(RuneSets.For(rune.Set).Glyph, (int)(40 * scale), _color, outline: true) { Name = "Glyph" };
				glyph.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				glyph.OffsetTop = 6 * scale;
				_layer.AddChild(glyph);

				// A linha de baixo: cada peça é centrada num ponto fixo dela, então o espaço, o cadeado e a
				// melhora ficam no mesmo lugar em qualquer escala e com qualquer texto.
				var side = ArrowSide * scale;
				_slotMark = SlotMark(slot, side, Palette.GoldDark, Palette.Text, 11);
				_layer.AddChild(OnBottomLine(_slotMark, 0, Inset * scale + side / 2, GrowDirection.Both));
				var level = Small(rune.Level > 0 ? $"+{rune.Level}" : "", Palette.Text, 12, HorizontalAlignment.Right).Named("Level");
				_layer.AddChild(OnBottomLine(level, 1, -Inset * scale, GrowDirection.Begin));
				if (rune.Locked)
					LockBadge();
			}

			_check.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_check.OffsetLeft = _check.OffsetTop = 10 * scale;
			_check.OffsetRight = _check.OffsetBottom = -10 * scale;
			_layer.AddChild(_check);

			MouseEntered += Restyle;
			MouseExited += Restyle;
		}

		public event Action<RuneTile>? Pressed;

		public Rune? Rune { get; }
		public int Slot { get; }

		public void SetSelected(bool selected)
		{
			_selected = selected;
			Restyle();
		}

		/// <summary>Quem usa a runa: o medalhão do monstro no lugar do espaço (apagado se ele está no Baú).</summary>
		public void SetOwner(Texture2D? creature, Color ink, bool stored)
		{
			var size = 18 * _scale;
			var holder = new Control { Name = "Owner", MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(size, size) };
			var medal = Doodle.Masked(creature, stored ? ink.Darkened(0.5f) : ink, MaskShape.Circle, boil: false);
			medal.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			holder.AddChild(medal);
			_layer.AddChild(OnBottomLine(holder, 0, (Inset + ArrowSide / 2) * _scale, GrowDirection.Both));
			if (_slotMark != null)
				_slotMark.Visible = false;
		}

		/// <summary>
		/// Quanto a seta do espaço gira: o desenho aponta para baixo, e o espaço 1 fica no alto do círculo das
		/// runas, os outros a cada 60°, no sentido do relógio (<see cref="SigilRing"/>).
		/// </summary>
		public static float SlotRotation(int slot) => Mathf.DegToRad(-180 + 60 * (slot - 1));

		/// <summary>
		/// Uma faixa escrita por cima da runa ("Vendida"), no centro da pedra, e a runa apagada por baixo.
		/// Só o desenho e a moldura apagam: a faixa fica inteira, para ler.
		/// </summary>
		public void SetStamp(string text)
		{
			_layer.Modulate = new Color(1, 1, 1, 0.35f);
			SelfModulate = new Color(1, 1, 1, 0.55f);
			var center = new CenterContainer { Name = "Stamp", MouseFilter = MouseFilterEnum.Ignore };
			var plate = new PanelContainer { Name = "Plate", MouseFilter = MouseFilterEnum.Ignore };
			var box = GameTheme.Box(Palette.Inset, Palette.Negative, 1, (int)(5 * _scale), 0);
			box.ContentMarginLeft = box.ContentMarginRight = 5 * _scale;
			box.ContentMarginTop = box.ContentMarginBottom = 1 * _scale;
			plate.AddThemeStyleboxOverride("panel", box);
			var label = new Label
			{
				Name = "Text",
				Text = text,
				MouseFilter = MouseFilterEnum.Ignore,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			};
			label.AddThemeFontSizeOverride("font_size", (int)(12 * _scale));
			label.AddThemeColorOverride("font_color", Palette.Negative.Lightened(0.35f));
			plate.AddChild(label);
			center.AddChild(plate);
			AddChild(center);
		}

		/// <summary>O cadeado da runa bloqueada: uma plaquinha no meio da linha de baixo, entre a seta e a melhora.</summary>
		private void LockBadge()
		{
			var size = 16 * _scale;
			var badge = new PanelContainer { Name = "Lock", MouseFilter = MouseFilterEnum.Ignore };
			badge.AddThemeStyleboxOverride("panel", GameTheme.Box(new Color(Palette.Inset, 0.92f), Palette.GoldDark, 1, (int)size, (int)(2 * _scale)));
			badge.AddChild(Doodle.Icon(Art.Icon("lock"), (int)(size - 4 * _scale), Palette.Gold).Named("Icon"));
			_layer.AddChild(OnBottomLine(badge, 0.5f, 0, GrowDirection.Both));
		}

		/// <summary>
		/// O espaço, num quadrado de <paramref name="side"/> px: a seta girada para ele e, por cima, o número
		/// dele (de pé, com contorno escuro), em <paramref name="fontSize"/> na escala 1.
		/// </summary>
		private Control SlotMark(int slot, float side, Color arrowInk, Color numberInk, int fontSize)
		{
			var mark = new Control { Name = "Slot", MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(side, side) };
			var arrow = new Doodle(Art.Icon("arrow"), arrowInk, boil: false)
			{
				Name = "Arrow",
				PivotOffset = new Vector2(side, side) / 2,
				Rotation = SlotRotation(slot),
			};
			arrow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			mark.AddChild(arrow);
			var number = Small(slot.ToString(), numberInk, fontSize).Named("Number");
			number.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			mark.AddChild(number);
			return mark;
		}

		/// <summary>
		/// Prende o controle à linha de baixo pelo centro: na altura <see cref="BottomLine"/> e em
		/// <paramref name="x"/> px da borda que <paramref name="anchor"/> marca (0 esquerda, 0,5 meio,
		/// 1 direita). Ele cresce a partir desse ponto, então o tamanho do texto não o tira do lugar.
		/// </summary>
		private Control OnBottomLine(Control control, float anchor, float x, GrowDirection horizontal)
		{
			control.AnchorLeft = control.AnchorRight = anchor;
			control.AnchorTop = control.AnchorBottom = 1;
			control.OffsetLeft = control.OffsetRight = x;
			control.OffsetTop = control.OffsetBottom = -BottomLine * _scale;
			control.GrowHorizontal = horizontal;
			control.GrowVertical = GrowDirection.Both;
			return control;
		}

		/// <summary>Marca para desfazer em massa.</summary>
		public void SetMarked(bool marked)
		{
			_marked = marked;
			_check.Visible = marked;
			Restyle();
		}

		public override void _GuiInput(InputEvent @event) => _press.Feed(this, @event);

		/// <summary>
		/// As estrelas da runa, em fileira no alto, ao lado do número do espaço: o desenho da estrela (o PNG
		/// renderizado, em branco para tingir) em ouro, sobre a mesma estrela em preto, um pouco maior.
		/// </summary>
		public override void _Draw()
		{
			if (_stars == 0 || Art.IconInk("star") is not { } star)
				return;

			var size = 9 * _scale;
			var step = size * 0.92f;
			var top = 3.5f * _scale;
			var left = 5 * _scale;
			for (var i = 0; i < _stars; i++)
			{
				var rect = new Rect2(left + i * step, top, size, size);
				DrawTextureRect(star, rect.Grow(1.2f), false, new Color(0, 0, 0, 0.85f));
				DrawTextureRect(star, rect, false, Palette.Gold);
			}
		}

		private void Restyle()
		{
			var hover = IsInsideTree() && GetGlobalRect().HasPoint(GetGlobalMousePosition());
			var width = Rune == null ? 1 : 2;
			_box.BorderColor = _selected ? Palette.Arcane : hover ? _color.Lightened(0.3f) : _color;
			_box.SetBorderWidthAll(_selected ? width + 1 : width);
			_box.BgColor = _marked ? Palette.Inset.Lerp(Palette.Spirit, 0.18f) : hover ? Palette.Inset.Lightened(0.06f) : Palette.Inset;
			_box.ShadowColor = _selected ? new Color(Palette.Arcane, 0.4f) : new Color(_color, hover ? 0.3f : 0);
			_box.ShadowSize = _selected || hover ? 5 : 0;
		}

		private Label Small(string text, Color color, int size, HorizontalAlignment? horizontalAlignment = null, VerticalAlignment? verticalAlignment = null)
		{
			var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", (int)(size * _scale));
			label.AddThemeColorOverride("font_color", color);
			label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
			label.AddThemeConstantOverride("outline_size", 2);
			label.AddThemeConstantOverride("line_spacing", -8);
			label.HorizontalAlignment = horizontalAlignment ?? HorizontalAlignment.Center;
			label.VerticalAlignment = verticalAlignment ?? VerticalAlignment.Center;
			return label;
		}
	}
}
