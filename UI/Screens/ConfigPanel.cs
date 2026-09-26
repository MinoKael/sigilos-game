using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A Configuração, por cima do Santuário: um sigilo por idioma (o aceso é o atual) e o de fechar.
	/// Trocar de idioma avisa o GameRoot, que recarrega os textos e remonta a tela.
	/// </summary>
	public partial class ConfigPanel : ColorRect
	{
		public ConfigPanel(IReadOnlyList<string> languages, string current)
		{
			Color = new Color(0, 0, 0, 0.55f);
			MouseFilter = MouseFilterEnum.Stop;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(center);

			var panel = new PanelContainer();
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 24));
			center.AddChild(panel);

			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 22);
			panel.AddChild(column);

			var title = Layout.Row(12);
			title.AddChild(Doodle.Icon(Art.Icon("config"), 40, Palette.Gold));
			title.AddChild(new Label { Text = T("destination.Config"), ThemeTypeVariation = GameTheme.Title });
			column.AddChild(title);

			var row = Layout.Row(16, true);
			foreach (var language in languages)
			{
				var code = language;
				var sigil = new SigilButton(null, code, 64) { ToggleMode = true, ButtonPressed = code == current, Letters = code.Split('-')[0].ToUpperInvariant() };
				sigil.Pressed += () =>
				{
					if (code != current)
						LanguageChosen?.Invoke(code);
					else
						sigil.ButtonPressed = true;
				};
				row.AddChild(sigil);
			}

			column.AddChild(row);
			var close = Layout.Row(0, true);
			close.AddChild(SigilButton.Of("cancel", T("common.close"), QueueFree, 52));
			column.AddChild(close);
		}

		public event Action<string>? LanguageChosen;

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				QueueFree();
		}

		public override void _UnhandledKeyInput(InputEvent @event)
		{
			if (@event.IsActionPressed("ui_cancel"))
				QueueFree();
		}
	}
}
