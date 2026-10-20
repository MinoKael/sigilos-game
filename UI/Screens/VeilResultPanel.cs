using System;
using Godot;
using Sigilos.Core.Rta;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// O fim de uma partida da Arena do Véu, por cima do campo: Vitória, Derrota ou Empate grande, quanto Áster
	/// mudou (e o Grau, se mudou), os Ecos do Véu ganhos e o Continuar. Se a conexão caiu antes do fim, avisa que o
	/// servidor termina a luta sozinho e o resultado fica para a próxima vez que a Arena abrir.
	/// </summary>
	public partial class VeilResultPanel : ColorRect
	{
		public VeilResultPanel(RtaMessage end, RtaSeat seat, string gradeBefore, string gradeAfter, Action close)
		{
			Name = nameof(VeilResultPanel);
			Color = new Color(0, 0, 0, 0.6f);
			MouseFilter = MouseFilterEnum.Stop;
			ZIndex = 90;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(center);

			var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(460, 0) };
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 26));
			center.AddChild(panel);

			var column = new VBoxContainer { Name = "Content" };
			column.AddThemeConstantOverride("separation", Space.Loose);
			panel.AddChild(column);

			if (end.Type != "end")
			{
				column.AddChild(new Label { Name = "Title", Text = T("veil.lost_title"), ThemeTypeVariation = GameTheme.Title, HorizontalAlignment = HorizontalAlignment.Center });
				column.AddChild(Layout.Text(T("veil.lost_text"), GameTheme.Faded, 400).Named("Note"));
				column.AddChild(GameButton.Of(T("battle.continue"), close, ButtonKind.Primary, "play", 64).Named("Continue"));
				return;
			}

			var draw = end.Winner == "Draw";
			var won = end.Winner == seat.ToString();
			var title = new Label { Name = "Title", Text = T(draw ? "veil.draw" : won ? "battle.victory" : "battle.defeat"), ThemeTypeVariation = GameTheme.Title, HorizontalAlignment = HorizontalAlignment.Center };
			title.AddThemeColorOverride("font_color", draw ? Palette.Text : won ? Palette.Gold : Palette.Negative);
			column.AddChild(title);
			if (end.Forfeit)
				column.AddChild(Layout.Text(T(won ? "veil.rival_left" : "veil.you_left"), GameTheme.Faded, 400).Named("Forfeit"));

			if (!end.Ranked)
			{
				column.AddChild(Layout.Text(T("veil.training_result"), GameTheme.Faded, 400).Named("Training"));
			}
			else
			{
				var sign = end.Delta > 0 ? "+" : end.Delta < 0 ? "−" : "";
				column.AddChild(Layout.Labeled("target", $"{sign}{Math.Abs(end.Delta)}", T("veil.aster_now", Texts.Number(end.NewAster)), end.Delta >= 0 ? Palette.Gold : Palette.Negative).Named("Aster"));
				if (end.Ecos > 0)
					column.AddChild(Layout.Labeled("polar_star", $"+{end.Ecos}", T("veil.ecos"), Palette.Spirit).Named("Ecos"));
				if (gradeBefore != gradeAfter && gradeAfter.Length > 0)
				{
					var grade = Layout.Text(T(end.Delta > 0 ? "arena.grade_up" : "arena.grade_down") + ": " + gradeAfter, null, 400).Named("Grade");
					grade.AddThemeColorOverride("font_color", Palette.Gold);
					column.AddChild(grade);
				}
			}

			column.AddChild(GameButton.Of(T("battle.continue"), close, ButtonKind.Primary, "play", 64).Named("Continue"));
		}
	}
}
