using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O retrato da conta, no canto do Santuário: a Líder da Campanha num medalhão, a experiência da
	/// conta como um anel de energia em volta e o nível numa pedrinha no canto de baixo.
	/// </summary>
	public partial class AccountSigil : Control
	{
		private const float Diameter = 104;
		private const float BadgeSize = 38;

		private readonly float _progress;
		private readonly Label _level = new()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			ThemeTypeVariation = GameTheme.Number,
			MouseFilter = MouseFilterEnum.Ignore,
		};

		/// <param name="progress">Experiência dentro do nível, de 0 a 1 (1 no nível máximo).</param>
		public AccountSigil(Texture2D? portrait, Color ink, int level, float progress, string tooltip)
		{
			_progress = Mathf.Clamp(progress, 0, 1);
			CustomMinimumSize = new Vector2(Diameter + 14, Diameter + 14);
			TooltipText = tooltip;
			MouseFilter = MouseFilterEnum.Stop;

			// O retrato recortado no círculo interno do medalhão.
			var holder = new Control { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(16, 16), Size = new Vector2(Diameter - 18, Diameter - 18) };
			holder.AddChild(Doodle.Masked(portrait, ink, MaskShape.Circle, inset: 0));
			AddChild(holder);

			_level.Text = level.ToString();
			_level.AddThemeFontSizeOverride("font_size", 17);
			_level.Position = new Vector2(Diameter + 14 - BadgeSize, Diameter + 14 - BadgeSize);
			_level.Size = new Vector2(BadgeSize, BadgeSize);
			AddChild(_level);
		}

		public override void _Draw()
		{
			var center = new Vector2(7 + Diameter / 2, 7 + Diameter / 2);
			var radius = Diameter / 2;

			// O anel de experiência: sulco escuro e a energia por cima, a partir do alto.
			DrawArc(center, radius + 3, 0, Mathf.Tau, 64, new Color(0, 0, 0, 0.55f), 6, true);
			if (_progress > 0)
				DrawArc(center, radius + 3, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * _progress, 64, Palette.Arcane, 4, true);

			DrawCircle(center, radius - 2, Palette.Panel);
			DrawArc(center, radius - 2, 0, Mathf.Tau, 64, Palette.Gold, 2, true);
			DrawArc(center, radius - 7, 0, Mathf.Tau, 64, new Color(Palette.Gold, 0.3f), 1, true);

			var badge = new Vector2(Diameter + 14 - BadgeSize / 2, Diameter + 14 - BadgeSize / 2);
			DrawCircle(badge, BadgeSize / 2, Palette.Inset);
			DrawArc(badge, BadgeSize / 2, 0, Mathf.Tau, 32, Palette.Gold, 2, true);
		}
	}
}
