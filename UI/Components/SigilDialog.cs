using System;
using Godot;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Pergunta de confirmação dentro do jogo, no lugar da janela do sistema: a tela escurece, um
	/// painel de couro mostra o texto e dois sigilos, ✓ e ✕. Clicar fora ou apertar Esc é ✕.
	/// </summary>
	public partial class SigilDialog : ColorRect
	{
		private readonly Action _onConfirmed;

		private SigilDialog(string text, Action onConfirmed)
		{
			_onConfirmed = onConfirmed;
			Color = new Color(0, 0, 0, 0.6f);
			MouseFilter = MouseFilterEnum.Stop;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(center);

			var panel = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 22));
			center.AddChild(panel);

			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 18);
			panel.AddChild(column);
			var label = RichText.Label(text, 380);
			column.AddChild(label);

			var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			row.AddThemeConstantOverride("separation", 40);
			row.AddChild(SigilButton.Of("confirm", T("common.yes"), Confirm, 60));
			row.AddChild(SigilButton.Of("cancel", T("common.no"), QueueFree, 60));
			column.AddChild(row);
		}

		/// <summary>Abre a pergunta por cima da tela de <paramref name="from"/>.</summary>
		public static void Ask(Control from, string text, Action onConfirmed) => Layout.Host(from).AddChild(new SigilDialog(text, onConfirmed));

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

		private void Confirm()
		{
			QueueFree();
			_onConfirmed();
		}
	}
}
