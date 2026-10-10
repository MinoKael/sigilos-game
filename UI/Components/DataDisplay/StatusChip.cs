using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Um efeito em campo, num quadradinho: vermelho com o símbolo branco quando é negativo, verde com o
	/// símbolo preto quando é positivo, e os turnos que faltam num selo escuro no canto de cima à direita,
	/// meio para fora (sem selo, o efeito não tem prazo). Fica em cima do cartão da unidade e na barra do
	/// chefe.
	/// </summary>
	public partial class StatusChip : Control
	{
		/// <summary>O lado do quadradinho no cartão comum; o do chefe é maior.</summary>
		public const int Side = 22;

		private static readonly Color Dark = new(0.07f, 0.06f, 0.05f);

		public StatusChip(StatusKind kind, int turns, int side = Side)
		{
			Name = kind.ToString();
			MouseFilter = MouseFilterEnum.Ignore;
			CustomMinimumSize = new Vector2(side, side);

			var negative = BattleRules.IsNegative(kind);
			var fill = negative ? Palette.Negative : Palette.Positive;
			var box = new StyleBoxFlat { BgColor = fill, BorderColor = fill.Darkened(0.5f), AntiAliasing = true };
			box.SetBorderWidthAll(1);
			box.SetCornerRadiusAll(4);
			var plate = new Panel { Name = "Plate", MouseFilter = MouseFilterEnum.Ignore };
			plate.AddThemeStyleboxOverride("panel", box);
			plate.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(plate);

			var icon = Doodle.Icon(Art.Effect(kind), side - 4, negative ? Colors.White : Dark).Named("Icon");
			icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			icon.OffsetLeft = icon.OffsetTop = 2;
			icon.OffsetRight = icon.OffsetBottom = -2;
			AddChild(icon);

			if (turns <= 0)
				return;

			// O selo dos turnos: um círculo escuro com o número branco, preso no canto e meio para fora.
			var badgeSide = side >= 28 ? 18 : 16;
			var badgeBox = new StyleBoxFlat { BgColor = Dark, BorderColor = fill.Lightened(0.35f), AntiAliasing = true };
			badgeBox.SetBorderWidthAll(1);
			badgeBox.SetCornerRadiusAll(badgeSide);
			var badge = new PanelContainer { Name = "Turns", MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(badgeSide, badgeSide) };
			badge.AddThemeStyleboxOverride("panel", badgeBox);
			var count = new Label
			{
				Name = "Count",
				Text = turns.ToString(),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			count.AddThemeFontOverride("font", GameTheme.Serif);
			count.AddThemeFontSizeOverride("font_size", side >= 28 ? 14 : 11);
			count.AddThemeColorOverride("font_color", Colors.White);
			count.AddThemeConstantOverride("line_spacing", 0);
			badge.AddChild(count);
			badge.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
			badge.GrowHorizontal = GrowDirection.Begin;
			badge.OffsetLeft = badge.OffsetRight = badgeSide * 0.45f;
			badge.OffsetTop = badge.OffsetBottom = -badgeSide * 0.45f;
			AddChild(badge);
		}
	}
}
