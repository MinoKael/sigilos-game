using System;
using Godot;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A pausa da luta, por cima da tela: a árvore inteira para (<see cref="SceneTree.Paused"/>) e só este
	/// painel segue vivo. Três sigilos: continuar, recomeçar a luta do início e sair dela. Clicar fora ou
	/// Esc continua.
	/// </summary>
	public partial class PauseMenu : ColorRect
	{
		private PauseMenu(Action restart, Action leave)
		{
			Name = nameof(PauseMenu);
			ProcessMode = ProcessModeEnum.Always;
			Color = new Color(0, 0, 0, 0.6f);
			MouseFilter = MouseFilterEnum.Stop;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(center);

			var panel = new PanelContainer { Name = "Panel" };
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 26));
			center.AddChild(panel);

			var column = new VBoxContainer { Name = "Content" };
			column.AddThemeConstantOverride("separation", 22);
			panel.AddChild(column);

			var title = Layout.Row(12, true).Named("Header");
			title.AddChild(Doodle.Icon(Art.Icon("pause"), 40, Palette.Gold));
			title.AddChild(new Label { Name = "Title", Text = T("battle.paused"), ThemeTypeVariation = GameTheme.Title });
			column.AddChild(title);

			var actions = Layout.Row(28, true).Named("Actions");
			actions.AddChild(SigilButton.Of("play", T("battle.continue"), Resume, 68).Named("Continue"));
			actions.AddChild(SigilButton.Of("repeat", T("battle.restart"), () =>
			{
				Resume();
				restart();
			}, 60, SigilShape.Diamond).Named("Restart"));
			actions.AddChild(SigilButton.Of("retreat", T("battle.retreat"), () =>
			{
				Resume();
				leave();
			}, 60, SigilShape.Diamond).Named("Leave"));
			column.AddChild(actions);
		}

		/// <summary>Pausa o jogo e abre o painel por cima da tela de <paramref name="from"/>.</summary>
		public static void Open(Control from, Action restart, Action leave)
		{
			Layout.Host(from).AddChild(new PauseMenu(restart, leave));
			from.GetTree().Paused = true;
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				Resume();
		}

		public override void _UnhandledKeyInput(InputEvent @event)
		{
			if (!@event.IsActionPressed("ui_cancel"))
				return;
			GetViewport().SetInputAsHandled();
			Resume();
		}

		/// <summary>Garante que o jogo não fique pausado se o painel sair por outro caminho.</summary>
		public override void _ExitTree() => GetTree().Paused = false;

		private void Resume()
		{
			GetTree().Paused = false;
			QueueFree();
		}
	}
}
