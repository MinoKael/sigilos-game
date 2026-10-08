using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Audio;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Abas escritas: cada uma diz o nome (e, se quiser, um detalhe embaixo, como "5/5" ou "19"), com um
	/// símbolo opcional na frente. A aberta fica clara, com moldura de ouro; as outras, na pedra escura.
	/// Deitadas (no alto de um painel) ou em pé (a régua da ficha de Monstros).
	/// </summary>
	public partial class TextTabs : BoxContainer
	{
		private readonly List<TabButton> _tabs = new();
		private readonly ButtonGroup _group = new();
		private readonly float _height;
		private readonly bool _compact;

		/// <param name="compact">Letra menor e menos folga: para abas que dividem pouco espaço.</param>
		public TextTabs(bool vertical = false, float height = 52, bool compact = false)
		{
			Vertical = vertical;
			_height = height;
			_compact = compact;
			AddThemeConstantOverride("separation", vertical ? 8 : 6);
		}

		/// <summary>A aba aberta mudou.</summary>
		public event Action<int>? Changed;

		public int Selected { get; private set; }

		/// <summary>Uma aba nova. O nó se chama <c>Tab0</c>, <c>Tab1</c>...; quem cria pode dar o nome do conteúdo.</summary>
		public Button Add(string text, string detail = "", string? icon = null, bool enabled = true)
		{
			var index = _tabs.Count;
			var tab = new TabButton(text, detail, icon, _height, _compact)
			{
				Name = $"Tab{index}",
				ButtonGroup = _group,
				ButtonPressed = index == Selected,
				Disabled = !enabled,
			};
			if (Vertical)
				tab.SizeFlagsHorizontal = SizeFlags.Fill;
			tab.Pressed += () =>
			{
				if (Selected == index)
					return;
				Selected = index;
				Sfx.Fallback("ui.tab_switch", 1);
				Changed?.Invoke(index);
			};
			_tabs.Add(tab);
			AddChild(tab);
			return tab;
		}

		/// <summary>Abre a aba sem avisar (quando a tela já sabe).</summary>
		public void Select(int index)
		{
			Selected = index;
			for (var i = 0; i < _tabs.Count; i++)
				_tabs[i].SetPressedNoSignal(i == index);
		}

		/// <summary>Uma aba: botão de ligar com o símbolo, o nome e o detalhe, que mede o próprio conteúdo.</summary>
		private sealed partial class TabButton : Button
		{
			private readonly MarginContainer _content = new() { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
			private readonly Label _label;
			private readonly Label? _detail;
			private readonly Doodle? _icon;
			private readonly float _height;

			public TabButton(string text, string detail, string? icon, float height, bool compact)
			{
				_height = height;
				ToggleMode = true;
				FocusMode = FocusModeEnum.None;
				MouseDefaultCursorShape = CursorShape.PointingHand;
				AddThemeStyleboxOverride("normal", Box(Palette.Inset, Palette.GoldDark, 1));
				AddThemeStyleboxOverride("hover", Box(Palette.Inset.Lightened(0.05f), Palette.Gold, 1));
				AddThemeStyleboxOverride("pressed", Box(Palette.PanelLight, Palette.Gold, 2));
				AddThemeStyleboxOverride("hover_pressed", Box(Palette.PanelLight, Palette.Gold, 2));
				AddThemeStyleboxOverride("disabled", Box(Palette.Inset.Darkened(0.2f), Palette.Disabled, 1));
				AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

				_content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				_content.AddThemeConstantOverride("margin_left", compact ? 10 : 16);
				_content.AddThemeConstantOverride("margin_right", compact ? 10 : 16);
				_content.AddThemeConstantOverride("margin_top", 4);
				_content.AddThemeConstantOverride("margin_bottom", 4);
				AddChild(_content);

				var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
				row.AddThemeConstantOverride("separation", 8);
				_content.AddChild(row);
				if (icon != null)
				{
					_icon = Doodle.Icon(Art.Icon(icon), 26, Palette.Gold);
					_icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
					row.AddChild(_icon);
				}

				var lines = new VBoxContainer { Name = "Text", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
				lines.AddThemeConstantOverride("separation", -4);
				_label = new Label { Name = "Label", Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
				_label.AddThemeFontOverride("font", GameTheme.Serif);
				_label.AddThemeFontSizeOverride("font_size", compact ? 16 : 18);
				lines.AddChild(_label);
				if (detail.Length > 0)
				{
					_detail = new Label { Name = "Detail", Text = detail, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
					_detail.AddThemeFontSizeOverride("font_size", 14);
					lines.AddChild(_detail);
				}

				row.AddChild(lines);
				Toggled += _ => Recolor();
				_content.MinimumSizeChanged += Fit;
			}

			public override void _Ready()
			{
				Recolor();
				Fit();
			}

			/// <summary>O <c>Button</c> não mede filhos: o tamanho mínimo acompanha o conteúdo.</summary>
			private void Fit()
			{
				var content = _content.GetCombinedMinimumSize();
				CustomMinimumSize = new Vector2(content.X, Mathf.Max(content.Y, _height));
			}

			public override void _Draw() => _content.Modulate = Disabled ? new Color(1, 1, 1, 0.4f) : Colors.White;

			private void Recolor()
			{
				var on = ButtonPressed;
				_label.AddThemeColorOverride("font_color", on ? Palette.Gold.Lightened(0.15f) : Palette.TextFaded);
				_detail?.AddThemeColorOverride("font_color", on ? Palette.Text : Palette.TextFaded.Darkened(0.15f));
				_icon?.SetInk(on ? Palette.Gold : Palette.GoldDark.Lightened(0.2f));
			}

			private static StyleBoxFlat Box(Color fill, Color border, int width)
			{
				var box = GameTheme.Box(fill, border, width, 8, 0);
				box.BorderWidthBottom = width + 1;
				return box;
			}
		}
	}
}
