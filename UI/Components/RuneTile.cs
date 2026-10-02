using System;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma runa em miniatura, quadrada de cantos redondos: as estrelas no alto, o Glifo do conjunto grande
	/// no meio (na fonte das runas, na cor da raridade) e, embaixo, numa linha só, o número do espaço à
	/// esquerda e a melhora à direita. Atrás do Glifo, o selo do espaço: um hexágono entalhado em que cada
	/// lado é um lugar do círculo das runas (o de cima é o 1, os outros seguem no sentido do relógio), e o
	/// lado do espaço da runa fica aceso na cor da raridade (<see cref="DrawSeal"/>). Sem runa, só o selo,
	/// maior e apagado, com o número no meio.
	///
	/// Toque curto é <see cref="Pressed"/>; toque longo abre a ficha da runa (<see cref="RuneCard"/>) numa
	/// janela colada nela. Escolhida, fica azul arcano; marcada para vender, ganha o ✓ verde. Na lista, a
	/// runa equipada mostra no lugar do número o medalhão de quem a usa (apagado se ele está no Baú); a
	/// bloqueada, um cadeado no meio da linha de baixo. O toque passa para cima, então arrastar rola a
	/// lista.
	/// </summary>
	public partial class RuneTile : PanelContainer
	{
		/// <summary>O lado da pedra, em px, na escala 1.</summary>
		public const float Side = 60;

		public static readonly Vector2 TileSize = new(Side, Side);

		/// <summary>A linha de baixo (espaço, cadeado e melhora): a altura do centro dela acima da borda, na escala 1.</summary>
		private const float BottomLine = 11;

		/// <summary>A distância do número do espaço e da melhora até a borda do lado, na escala 1.</summary>
		private const float Inset = 5;

		/// <summary>A largura reservada ao número do espaço (ao medalhão de quem usa a runa) e, espelhada, à melhora, na escala 1.</summary>
		private const float SlotWidth = 18;

		/// <summary>O raio do selo do espaço (até o vértice), na escala 1: atrás do Glifo, e maior sem runa.</summary>
		private const float SealRadius = 20;

		private const float EmptySealRadius = 22;

		private readonly StyleBoxFlat _box;
		private readonly Color _color;
		private readonly Control _layer = new() { Name = "Layer", MouseFilter = MouseFilterEnum.Ignore };
		private readonly Doodle _check = new(Art.Icon("confirm"), Palette.Spirit, boil: false) { Name = "Check", Visible = false };
		private readonly float _scale;
		private readonly int _stars;
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
				var number = Small(slot.ToString(), new Color(Palette.Gold, 0.85f), 18).Named("Slot");
				number.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				_layer.AddChild(number);
			}
			else
			{
				// O Glifo ocupa o miolo, um pouco abaixo do centro para deixar a fileira de cima livre.
				var glyph = new RuneGlyph(RuneSets.For(rune.Set).Glyph, (int)(33 * scale), _color, outline: true) { Name = "Glyph" };
				glyph.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				glyph.OffsetTop = 6 * scale;
				_layer.AddChild(glyph);

				// A linha de baixo: cada peça é centrada num ponto fixo dela, então o espaço, o cadeado e a
				// melhora ficam no mesmo lugar em qualquer escala e com qualquer texto.
				var level = Small(rune.Level > 0 ? $"+{rune.Level}" : "", Palette.Text, 12).Named("Level");
				_layer.AddChild(OnBottomLine(level, 1, -(Inset + SlotWidth / 2) * scale, GrowDirection.Both));
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

		/// <summary>Quem usa a runa: o medalhão do monstro no lugar do número do espaço (apagado se ele está no Baú).</summary>
		public void SetOwner(Texture2D? creature, Color ink, bool stored)
		{
			var size = 18 * _scale;
			var holder = new Control { Name = "Owner", MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(size, size) };
			var medal = Doodle.Masked(creature, stored ? ink.Darkened(0.5f) : ink, MaskShape.Circle, boil: false);
			medal.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			holder.AddChild(medal);
			_layer.AddChild(OnBottomLine(holder, 0, (Inset + SlotWidth / 2) * _scale, GrowDirection.Both));
		}

		/// <summary>
		/// Para onde o lado do espaço olha, em graus: o 1 para cima (como no círculo das runas, o
		/// <see cref="SigilRing"/>), os outros a cada 60° no sentido do relógio.
		/// </summary>
		public static float SlotAngle(int slot) => -90 + 60 * (slot - 1);

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
			DrawSeal();
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

		/// <summary>
		/// O selo do espaço, atrás do Glifo: um hexágono de lados chatos em cima e embaixo, um pouco mais claro
		/// que a pedra, com o contorno entalhado em ouro escuro. O lado que olha para o espaço da runa
		/// (<see cref="SlotAngle"/>) vira uma barra acesa na cor da raridade.
		/// </summary>
		private void DrawSeal()
		{
			var empty = Rune == null;
			var radius = (empty ? EmptySealRadius : SealRadius) * _scale;
			// Com runa, o centro do Glifo: a pedra menos a fileira das estrelas, um pouco acima para não
			// encostar na linha de baixo.
			var center = new Vector2(Size.X / 2, Size.Y / 2 + (empty ? 0 : 2 * _scale));
			var corners = new Vector2[7];
			for (var i = 0; i < 6; i++)
				corners[i] = center + Vector2.FromAngle(Mathf.DegToRad(60 * i)) * radius;
			corners[6] = corners[0];

			var lit = empty ? new Color(Palette.Gold, 0.55f) : _color;
			DrawColoredPolygon(corners[..6], empty ? new Color(Palette.Inset.Lightened(0.04f), 0.9f) : Palette.Inset.Lightened(0.07f));

			var facing = SlotAngle(Slot);
			var from = center + Vector2.FromAngle(Mathf.DegToRad(facing - 30)) * radius;
			var to = center + Vector2.FromAngle(Mathf.DegToRad(facing + 30)) * radius;
			DrawPolyline(corners, new Color(Palette.GoldDark, empty ? 0.5f : 0.7f), 1.2f * _scale, true);
			DrawLine(from, to, new Color(lit, 0.35f), 7 * _scale, true);
			DrawLine(from, to, lit, 3 * _scale, true);
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
