using System;
using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>O contorno do sigilo: círculo (navegação), losango (ação) ou pedra quadrada (aba, atalho).</summary>
	public enum SigilShape
	{
		Circle,
		Diamond,
		Square,
	}

	/// <summary>
	/// O botão do jogo: só um símbolo dentro de um sigilo, sem texto. O nome vem na dica (passar o
	/// mouse); o único texto é a <see cref="Badge"/>, um número pequeno (custo, quantidade).
	///
	/// Os estados se leem pela luz, não pela cor de fundo: sob o mouse a moldura vira ouro e ganha a
	/// aura arcana, e o botão cresce um pouco; apertado, afunda; ligado (<see cref="BaseButton.ToggleMode"/>)
	/// fica aceso em azul — é o substituto da caixinha de marcar —; desligado, apaga. <see cref="Highlight"/>
	/// pulsa em verde para chamar o jogador (o que coletar, por onde começar).
	/// </summary>
	public partial class SigilButton : Button
	{
		private readonly Doodle _icon;
		private readonly Label _letters = new()
		{
			Name = "Letters",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
		};

		private readonly Label _badge = new()
		{
			Name = "Badge",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			ThemeTypeVariation = GameTheme.Number,
		};

		/// <summary>A plaquinha escura com borda de ouro onde o número fica: destaca do fundo e do sigilo.</summary>
		private readonly PanelContainer _plaque = new() { Name = "Plaque", MouseFilter = MouseFilterEnum.Ignore, Visible = false };

		private bool _highlight;
		private Glyph? _rune;
		private float _time;
		private Color _ink = Palette.Gold;
		private Color? _accent;

		public SigilButton(Texture2D? icon, string tooltip, float size = 56, SigilShape shape = SigilShape.Circle)
		{
			Shape = shape;
			Flat = true;
			FocusMode = FocusModeEnum.None;
			TooltipText = tooltip;
			CustomMinimumSize = new Vector2(size, size);
			MouseDefaultCursorShape = CursorShape.PointingHand;

			// Símbolo e letras ficam dentro da máscara da forma: recortados no contorno interno do sigilo.
			var mask = new ArtMask(shape switch
			{
				SigilShape.Circle => MaskShape.Circle,
				SigilShape.Diamond => MaskShape.Diamond,
				_ => MaskShape.Rounded,
			}, Mathf.Max(6, size * 0.18f)) { Name = "Mask" };
			mask.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			mask.OffsetLeft = mask.OffsetTop = 6;
			mask.OffsetRight = mask.OffsetBottom = -6;
			AddChild(mask);

			_icon = new Doodle(icon, _ink, boil: false) { Name = "Icon" };
			_icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			var inset = size * (shape == SigilShape.Diamond ? 0.26f : 0.2f) - 6;
			_icon.OffsetLeft = _icon.OffsetTop = inset;
			_icon.OffsetRight = _icon.OffsetBottom = -inset;
			mask.AddChild(_icon);

			_letters.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_letters.AddThemeFontOverride("font", GameTheme.Serif);
			_letters.AddThemeFontSizeOverride("font_size", (int)(size * 0.34f));
			_letters.AddThemeColorOverride("font_color", _ink);
			mask.AddChild(_letters);

			// A plaquinha do número fica sobre a borda de baixo, como uma inscrição no sigilo.
			var plate = GameTheme.Box(new Color(Palette.Inset, 0.95f), Palette.GoldDark, 1, 8, 0);
			plate.ContentMarginLeft = plate.ContentMarginRight = 5;
			_plaque.AddThemeStyleboxOverride("panel", plate);
			_plaque.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
			_plaque.GrowHorizontal = GrowDirection.Both;
			_plaque.GrowVertical = GrowDirection.Both;
			_plaque.OffsetTop = _plaque.OffsetBottom = -2;
			_badge.AddThemeFontSizeOverride("font_size", Math.Clamp((int)(size * 0.24f), 11, 16));
			_badge.AddThemeColorOverride("font_color", Palette.Text);
			_badge.AddThemeColorOverride("font_outline_color", Colors.Black);
			_badge.AddThemeConstantOverride("outline_size", 3);
			_plaque.AddChild(_badge);
			AddChild(_plaque);

			Juice.Attach(this, 1.08f, 0.93f);
			Toggled += _ => RefreshInk();
			MouseEntered += RefreshInk;
			MouseExited += RefreshInk;
		}

		public SigilShape Shape { get; }

		/// <summary>Cor do símbolo em repouso (ouro por padrão; a cor do elemento num monstro).</summary>
		public Color Ink
		{
			get => _ink;
			set
			{
				_ink = value;
				RefreshInk();
			}
		}

		/// <summary>Cor da moldura em repouso, quando não é a de ouro (raridade, elemento).</summary>
		public Color? Accent
		{
			get => _accent;
			set
			{
				_accent = value;
				QueueRedraw();
			}
		}

		/// <summary>O número na borda de baixo; vazio esconde.</summary>
		public string Badge
		{
			get => _badge.Text;
			set
			{
				_badge.Text = value;
				_plaque.Visible = value.Length > 0;
			}
		}

		/// <summary>Letras no lugar do símbolo, para o que não tem ícone (1×, EN, +3).</summary>
		public string Letters
		{
			get => _letters.Text;
			set
			{
				_letters.AddThemeFontOverride("font", GameTheme.Serif);
				_letters.AddThemeFontSizeOverride("font_size", (int)(CustomMinimumSize.Y * 0.34f));
				_letters.Text = value;
				_letters.Visible = value.Length > 0;
				_icon.Visible = value.Length == 0;
			}
		}

		/// <summary>Tamanho das letras, quando o padrão (um terço do sigilo) fica pequeno.</summary>
		public void SetLetterSize(int pixels) => _letters.AddThemeFontSizeOverride("font_size", pixels);

		/// <summary>Um Glifo no lugar do símbolo, escrito na fonte das runas.</summary>
		public Glyph? Rune
		{
			get => _rune;
			set
			{
				_rune = value;
				if (value is not { } glyph)
				{
					Letters = "";
					return;
				}

				_letters.AddThemeFontOverride("font", GameTheme.Runes);
				_letters.AddThemeFontSizeOverride("font_size", (int)(CustomMinimumSize.Y * 0.46f));
				_letters.Text = Texts.Rune(glyph);
				_letters.Visible = true;
				_icon.Visible = false;
			}
		}

		/// <summary>O símbolo de uma coisa do jogo: um desenho ou um Glifo.</summary>
		public void SetSymbol(Symbol symbol)
		{
			if (symbol.Rune is { } glyph)
			{
				Rune = glyph;
				return;
			}

			_rune = null;
			Letters = "";
			SetIcon(symbol.Icon);
		}

		/// <summary>Pulsa em verde espiritual até o jogador tocar.</summary>
		public bool Highlight
		{
			get => _highlight;
			set
			{
				_highlight = value;
				SetProcess(value);
				QueueRedraw();
			}
		}

		public void SetIcon(Texture2D? icon) => _icon.SetArt(icon);

		/// <summary>Botão pronto: símbolo de Assets/Icons, dica e ação. O nó leva o nome do símbolo (<c>level_max</c> → <c>LevelMax</c>).</summary>
		public static SigilButton Of(string icon, string tooltip, Action onPressed, float size = 56, SigilShape shape = SigilShape.Circle)
		{
			var button = new SigilButton(Art.Icon(icon), tooltip, size, shape) { Name = Layout.NodeName(icon) };
			button.Pressed += onPressed;
			return button;
		}

		public override void _Ready()
		{
			SetProcess(_highlight);
			RefreshInk();
		}

		public override void _Process(double delta)
		{
			_time += (float)delta;
			QueueRedraw();
		}

		public override void _Notification(int what)
		{
			if (what == NotificationEnabled || what == NotificationDisabled)
				RefreshInk();
		}

		public override void _Draw()
		{
			var center = Size / 2;
			var radius = Mathf.Min(Size.X, Size.Y) / 2 - 4;
			var mode = GetDrawMode();
			var hover = mode is DrawMode.Hover or DrawMode.HoverPressed;
			var down = mode is DrawMode.Pressed or DrawMode.HoverPressed;
			var lit = ToggleMode && ButtonPressed;
			var pressing = down && !ToggleMode;

			var ring = Disabled ? Palette.Disabled.Lightened(0.15f)
				: lit ? Palette.Arcane
				: hover ? Palette.Gold
				: _accent ?? Palette.GoldDark;
			var fill = Disabled ? Palette.Inset
				: pressing ? Palette.Inset
				: lit ? Palette.Inset.Lerp(Palette.Arcane, 0.14f)
				: hover ? Palette.PanelLight
				: Palette.Panel;

			// A aura: anéis cada vez mais apagados em volta do contorno.
			var pulse = 0.5f + 0.5f * Mathf.Sin(_time * 3.2f);
			var glow = !Disabled && (hover || lit) ? Palette.Arcane
				: _highlight && !Disabled ? Palette.Spirit
				: (Color?)null;
			if (glow is { } aura)
			{
				var strength = hover || lit ? 0.5f : 0.25f + 0.35f * pulse;
				for (var i = 1; i <= 4; i++)
					Outline(center, radius + i * 1.6f, new Color(aura, strength * (1 - i / 5f)), 2);
			}

			Body(center, radius, fill);
			Outline(center, radius, ring, 2);
			Outline(center, radius - 4, new Color(ring, 0.35f), 1);
		}

		private void Body(Vector2 center, float radius, Color fill)
		{
			switch (Shape)
			{
				case SigilShape.Circle:
					DrawCircle(center, radius, fill);
					break;
				case SigilShape.Diamond:
					DrawColoredPolygon(Diamond(center, radius), fill);
					break;
				default:
					DrawStyleBox(Rounded(fill, new Color(0, 0, 0, 0), 0), Square(center, radius));
					break;
			}
		}

		private void Outline(Vector2 center, float radius, Color color, float width)
		{
			switch (Shape)
			{
				case SigilShape.Circle:
					DrawArc(center, radius, 0, Mathf.Tau, 64, color, width, true);
					break;
				case SigilShape.Diamond:
					var points = Diamond(center, radius);
					DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, color, width, true);
					break;
				default:
					DrawStyleBox(Rounded(new Color(0, 0, 0, 0), color, width), Square(center, radius));
					break;
			}
		}

		/// <summary>A pedra de cantos redondos: o raio acompanha o tamanho do sigilo.</summary>
		private StyleBoxFlat Rounded(Color fill, Color border, float width)
		{
			var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, AntiAliasing = true, DrawCenter = fill.A > 0 };
			box.SetBorderWidthAll((int)Mathf.Ceil(width));
			box.SetCornerRadiusAll((int)Mathf.Max(8, Mathf.Min(Size.X, Size.Y) * 0.22f));
			return box;
		}

		private static Vector2[] Diamond(Vector2 center, float radius) => new[]
		{
			center + new Vector2(0, -radius),
			center + new Vector2(radius, 0),
			center + new Vector2(0, radius),
			center + new Vector2(-radius, 0),
		};

		private Rect2 Square(Vector2 center, float radius)
		{
			var half = new Vector2(Size.X / 2 - 4, radius);
			return new Rect2(center - half, half * 2);
		}

		private void RefreshInk()
		{
			var lit = ToggleMode && ButtonPressed;
			var ink = Disabled ? Palette.TextFaded.Darkened(0.35f)
				: lit ? Palette.Arcane.Lerp(Colors.White, 0.25f)
				: IsHovered() ? _ink.Lightened(0.25f)
				: _ink;
			_icon.SetInk(ink);
			_letters.AddThemeColorOverride("font_color", ink);
			QueueRedraw();
		}
	}
}
