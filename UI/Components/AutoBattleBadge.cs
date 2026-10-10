using System;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O aviso flutuante da Batalha automática, por cima de qualquer tela, no alto e no centro: só o
	/// símbolo dela (girando enquanto luta) e a luta de agora, "4/30", meio transparente para não esconder
	/// o que está embaixo. A cor da borda diz o estado: arcano lutando, vermelho parada (dá para retomar),
	/// verde concluída. Fica até o jogador abrir a janela e dispensar. Tocar abre a janela da Batalha
	/// automática (<see cref="Pressed"/>); fechar a janela não para nada.
	/// </summary>
	public partial class AutoBattleBadge : TouchButton
	{
		private const float Height = 44;

		private const int IconSize = 28;

		private readonly Doodle _icon = Doodle.Icon(Art.Icon("repeat"), IconSize, Palette.Arcane);
		private readonly Label _count = new() { Name = "Count", ThemeTypeVariation = GameTheme.Number, MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
		private readonly StyleBoxFlat _box;
		private AutoBattleRun? _run;

		public AutoBattleBadge()
		{
			Name = "AutoBattleBadge";
			Visible = false;
			ZIndex = 70;
			Modulate = new Color(1, 1, 1, Fade.Floating);

			_box = Pills.Floating(Palette.Arcane, Height);
			foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
				AddThemeStyleboxOverride(state, _box);

			var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
			row.AddThemeConstantOverride("separation", Space.Medium);
			_icon.Name = "Icon";
			_icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			_icon.PivotOffset = new Vector2(IconSize / 2f, IconSize / 2f);
			row.AddChild(_icon);
			_count.AddThemeFontSizeOverride("font_size", FontSize.Button);
			_count.SizeFlagsVertical = SizeFlags.ExpandFill;
			row.AddChild(_count);
			Content.AddChild(row);

			// No alto, no centro, crescendo para os dois lados conforme o número.
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

		/// <summary>O processo gira o símbolo (a base só o liga para o pulso de <see cref="TouchButton.Highlight"/>).</summary>
		public override void _Ready()
		{
			base._Ready();
			SetProcess(true);
		}

		public override void _Process(double delta)
		{
			base._Process(delta);
			if (_run is { Running: true })
				_icon.Rotation += (float)delta * 2.4f;
		}

		private void Refresh()
		{
			Visible = _run != null;
			if (_run is not { } run)
				return;

			_count.Text = $"{Math.Min(run.Running ? run.Number : run.Done, run.Runs)}/{run.Runs}";
			var color = run.Running ? Palette.Arcane : run.CanResume ? Palette.Negative : Palette.Spirit;
			_box.BorderColor = color;
			_icon.SetInk(color);
			if (!run.Running)
				_icon.Rotation = 0;

			// O botão não mede os filhos: a largura acompanha o número.
			var width = 14 + IconSize + 8 + _count.GetCombinedMinimumSize().X + 16;
			OffsetLeft = -width / 2;
			OffsetRight = width / 2;
		}
	}
}
