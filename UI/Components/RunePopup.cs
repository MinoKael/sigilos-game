using System.Collections.Generic;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A <see cref="RuneCard"/> por cima da tela, num painel de couro com o ✕ no canto: a runa que caiu
	/// na vitória (com Vender e Pegar) ou a que o jogador tocou na Batalha automática (só para ver).
	/// Clicar fora ou Esc fecha, a menos que <c>modal</c> peça uma escolha entre os sigilos.
	/// </summary>
	public partial class RunePopup : ColorRect
	{
		private readonly bool _modal;

		private RunePopup(Rune rune, IReadOnlyList<SigilButton> actions, bool modal)
		{
			_modal = modal;
			Name = nameof(RunePopup);
			Color = new Color(0, 0, 0, 0.55f);
			MouseFilter = MouseFilterEnum.Stop;
			ZIndex = 50;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(center);

			var panel = new PanelContainer { Name = "Panel" };
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 22));
			center.AddChild(panel);

			var column = new VBoxContainer { Name = "Content" };
			column.AddThemeConstantOverride("separation", 16);
			panel.AddChild(column);

			var top = Layout.Row(0).Named("Top");
			top.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
			top.AddChild(SigilButton.Of("cancel", T("common.close"), QueueFree, 36).Named("Close"));
			column.AddChild(top);
			column.AddChild(new RuneCard(rune, 380));

			if (actions.Count > 0)
			{
				var row = Layout.Row(36, true).Named("Actions");
				foreach (var action in actions)
				{
					action.Pressed += QueueFree;
					row.AddChild(action);
				}

				column.AddChild(row);
			}
		}

		/// <summary>
		/// Abre a ficha de <paramref name="rune"/> dentro de <paramref name="parent"/>. Cada sigilo de
		/// <paramref name="actions"/> fecha o painel depois de fazer o seu. Com <paramref name="modal"/>, só
		/// o ✕ e os sigilos fecham.
		/// </summary>
		public static RunePopup Open(Control parent, Rune rune, IReadOnlyList<SigilButton>? actions = null, bool modal = false)
		{
			var popup = new RunePopup(rune, actions ?? new List<SigilButton>(), modal);
			parent.AddChild(popup);
			return popup;
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (!_modal && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				QueueFree();
		}

		public override void _UnhandledKeyInput(InputEvent @event)
		{
			if (_modal || !@event.IsActionPressed("ui_cancel"))
				return;
			GetViewport().SetInputAsHandled();
			QueueFree();
		}
	}
}
