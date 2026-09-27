using System;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A escolha da Batalha automática, por cima da tela: quantas lutas seguidas, numa barra de energia
	/// entalhada com a gema (de 1 a <see cref="AutoBattle.MaxRuns"/>, começando em
	/// <see cref="AutoBattle.RepeatRuns"/>), os sigilos de uma a menos e uma a mais, a Mana que todas as
	/// vitórias custariam, e ✓ para começar. Clicar fora ou Esc desiste.
	/// </summary>
	public partial class RunsPicker : ColorRect
	{
		private readonly HSlider _slider = new() { Name = "Slider", MinValue = 1, MaxValue = AutoBattle.MaxRuns, Step = 1, CustomMinimumSize = new Vector2(320, 26), SizeFlagsVertical = SizeFlags.ShrinkCenter };
		private readonly Label _count = new() { Name = "Count", ThemeTypeVariation = GameTheme.Number, HorizontalAlignment = HorizontalAlignment.Center };
		private readonly HBoxContainer _cost = Layout.Row(0, true).Named("Cost");
		private readonly int _mana;
		private readonly Action<int> _chosen;

		private RunsPicker(int mana, Action<int> chosen)
		{
			_mana = mana;
			_chosen = chosen;
			Name = nameof(RunsPicker);
			Color = new Color(0, 0, 0, 0.6f);
			MouseFilter = MouseFilterEnum.Stop;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(center);

			var panel = new PanelContainer { Name = "Panel" };
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 24));
			center.AddChild(panel);

			var column = new VBoxContainer { Name = "Content" };
			column.AddThemeConstantOverride("separation", 18);
			panel.AddChild(column);

			var title = Layout.Row(12, true).Named("Title");
			title.AddChild(Doodle.Icon(Art.Icon("repeat"), 44, Palette.Gold));
			_count.AddThemeFontSizeOverride("font_size", 40);
			_count.AddThemeColorOverride("font_color", Palette.Gold);
			title.AddChild(_count);
			column.AddChild(title);

			var row = Layout.Row(12, true).Named("Runs");
			row.AddChild(Step("−", T("auto.fewer"), -1).Named("Fewer"));
			_slider.Value = AutoBattle.RepeatRuns;
			_slider.TooltipText = T("auto.runs");
			_slider.ValueChanged += _ => Refresh();
			row.AddChild(_slider);
			row.AddChild(Step("+", T("auto.more"), 1).Named("More"));
			column.AddChild(row);

			column.AddChild(_cost);

			var actions = Layout.Row(40, true).Named("Actions");
			actions.AddChild(SigilButton.Of("confirm", T("auto.start"), Confirm, 60).Named("Start"));
			actions.AddChild(SigilButton.Of("cancel", T("common.no"), QueueFree, 60));
			column.AddChild(actions);
			Refresh();
		}

		/// <summary>Abre por cima da tela de <paramref name="from"/>; <paramref name="mana"/> é o custo de uma vitória.</summary>
		public static void Open(Control from, int mana, Action<int> chosen) => Layout.Host(from).AddChild(new RunsPicker(mana, chosen));

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

		private void Refresh()
		{
			var runs = (int)_slider.Value;
			_count.Text = $"×{runs}";
			Layout.Clear(_cost);
			_cost.AddChild(Layout.Chip("mana", (runs * _mana).ToString(), T("auto.mana_needed", runs * _mana)));
		}

		/// <summary>Sigilo de uma luta a menos ou a mais.</summary>
		private SigilButton Step(string letters, string tooltip, int delta)
		{
			var button = new SigilButton(null, tooltip, 44) { Letters = letters };
			button.SetLetterSize(26);
			button.Pressed += () => _slider.Value += delta;
			return button;
		}

		private void Confirm()
		{
			QueueFree();
			_chosen((int)_slider.Value);
		}
	}
}
