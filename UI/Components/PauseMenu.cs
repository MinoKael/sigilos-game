using System;
using Godot;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A pausa da luta, por cima da tela: a árvore inteira para (<see cref="SceneTree.Paused"/>) e só este
	/// painel segue vivo. Três botões escritos: Continuar, Recomeçar a luta (do início, sem custo) e Sair
	/// da luta (sem recompensa). Tocar fora, Esc e Voltar continuam.
	/// </summary>
	public partial class PauseMenu : ColorRect
	{
		private PauseMenu(Action restart, Action leave)
		{
			Name = nameof(PauseMenu);
			ProcessMode = ProcessModeEnum.Always;
			Color = new Color(0, 0, 0, 0.6f);
			MouseFilter = MouseFilterEnum.Stop;
			ZIndex = 99;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(center);

			var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(420, 0) };
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 26));
			center.AddChild(panel);

			var column = new VBoxContainer { Name = "Content" };
			column.AddThemeConstantOverride("separation", 16);
			panel.AddChild(column);
			column.AddChild(new Label { Name = "Title", Text = T("battle.paused"), ThemeTypeVariation = GameTheme.Title, HorizontalAlignment = HorizontalAlignment.Center });
			column.AddChild(GameButton.Of(T("battle.continue"), Resume, ButtonKind.Primary, "play", 64).Named("Continue"));
			column.AddChild(GameButton.Of(T("battle.restart"), () =>
			{
				Resume();
				restart();
			}, ButtonKind.Secondary, "repeat").Named("Restart"));
			column.AddChild(GameButton.Of(T("battle.retreat"), () =>
			{
				Resume();
				leave();
			}, ButtonKind.Danger, "retreat").Named("Leave"));
			column.AddChild(Layout.Text(T("battle.retreat_tip"), GameTheme.Faded, 360).Named("Note"));
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

		public override void _UnhandledInput(InputEvent @event)
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
