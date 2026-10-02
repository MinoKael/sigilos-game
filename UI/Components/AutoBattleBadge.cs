using System;
using Godot;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O aviso flutuante da Batalha automática, no alto e no centro da tela, por cima de qualquer tela:
	/// o símbolo girando, "Batalha automática 4/30" e, embaixo, a barra da luta em andamento. Pequeno,
	/// para não tapar nada. Acabada ou parada, diz isso e fica até o jogador abrir a janela e dispensar.
	/// Tocar abre a janela da Batalha automática (<see cref="Pressed"/>); fechar a janela não para nada.
	/// </summary>
	public partial class AutoBattleBadge : Button
	{
		private const float Height = 50;

		private readonly Doodle _icon = Doodle.Icon(Art.Icon("repeat"), 30, Palette.Arcane);
		private readonly Label _text = new() { Name = "Text", MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
		private readonly ProgressBar _bar = Layout.Energy(Palette.Arcane, 5).Named("Progress");
		private readonly StyleBoxFlat _box;
		private AutoBattleRun? _run;

		public AutoBattleBadge()
		{
			Name = "AutoBattleBadge";
			FocusMode = FocusModeEnum.None;
			MouseDefaultCursorShape = CursorShape.PointingHand;
			Visible = false;
			ZIndex = 70;

			_box = GameTheme.Box(new Color(Palette.Inset, 0.94f), Palette.Arcane, 2, 25, 0);
			_box.ShadowColor = new Color(0, 0, 0, 0.5f);
			_box.ShadowSize = 6;
			foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
				AddThemeStyleboxOverride(state, _box);
			AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

			var column = new VBoxContainer { Name = "Column", MouseFilter = MouseFilterEnum.Ignore };
			column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			column.OffsetLeft = 14;
			column.OffsetRight = -18;
			column.OffsetTop = 4;
			column.OffsetBottom = -6;
			column.AddThemeConstantOverride("separation", 2);
			AddChild(column);

			var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ExpandFill };
			row.AddThemeConstantOverride("separation", 10);
			_icon.Name = "Icon";
			_icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			_icon.PivotOffset = new Vector2(15, 15);
			row.AddChild(_icon);
			_text.AddThemeFontOverride("font", GameTheme.Serif);
			_text.AddThemeFontSizeOverride("font_size", 18);
			_text.AddThemeColorOverride("font_color", Palette.Text);
			_text.SizeFlagsVertical = SizeFlags.ExpandFill;
			row.AddChild(_text);
			column.AddChild(row);

			_bar.MaxValue = 1;
			_bar.Step = 0;
			_bar.MouseFilter = MouseFilterEnum.Ignore;
			column.AddChild(_bar);

			// Preso no alto, no centro, crescendo para os dois lados.
			SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
			GrowHorizontal = GrowDirection.Both;
			OffsetTop = 6;
			OffsetBottom = 6 + Height;
		}

		/// <summary>Mostra esta Batalha automática (nula esconde o aviso).</summary>
		public void Show(AutoBattleRun? run)
		{
			if (_run != null)
				_run.Changed -= Refresh;
			_run = run;
			if (run != null)
				run.Changed += Refresh;
			Refresh();
		}

		public override void _Process(double delta)
		{
			if (_run is not { Running: true } run)
				return;
			_icon.Rotation -= (float)delta * 2.4f;
			_bar.Value = run.FightProgress;
		}

		private void Refresh()
		{
			Visible = _run != null;
			if (_run is not { } run)
				return;

			var count = $"{Math.Min(run.Number, run.Runs)}/{run.Runs}";
			_text.Text = run.Running ? T("auto.badge_running", count)
				: run.CanResume ? T("auto.badge_stopped", count)
				: T("auto.badge_done", $"{run.Done}/{run.Runs}");
			var color = run.Running ? Palette.Arcane : run.CanResume ? Palette.Negative : Palette.Spirit;
			_box.BorderColor = color;
			_icon.SetInk(color);
			if (!run.Running)
				_icon.Rotation = 0;
			_bar.Visible = run.Running;
			_bar.Value = run.FightProgress;

			// O botão não mede os filhos: a largura acompanha o texto.
			var width = _text.GetCombinedMinimumSize().X + 30 + 10 + 32;
			OffsetLeft = -width / 2;
			OffsetRight = width / 2;
		}
	}
}
