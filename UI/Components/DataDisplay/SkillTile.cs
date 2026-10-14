using System;
using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O ícone de uma habilidade: uma pedra quadrada na ativa e um losango na passiva, com o desenho próprio
	/// dela (<see cref="Art.SkillImage"/>) ou, sem ele, o símbolo de <see cref="Art.Skill"/>. Com nível,
	/// uma pastilha redonda no canto de cima diz quanto a habilidade já subiu ("+3"); acesa em ouro quando
	/// chegou ao máximo. Travada (a do Despertar, antes de despertar), fica apagada.
	/// </summary>
	public static class SkillTile
	{
		private const float BadgeSize = 24;
		private const float CrownSize = 26;

		/// <summary>O que a pastilha escreve: os aprimoramentos já recebidos, que são o nível menos o primeiro.</summary>
		public static string Upgrades(int level) => $"+{Math.Max(0, level - 1)}";

		/// <param name="level">O nível de agora; 0 (ou a habilidade travada) esconde a pastilha.</param>
		/// <param name="shape">Nulo: quadrada na ativa, losango na passiva.</param>
		/// <param name="onPressed">Nulo deixa o ícone só de desenho, sem toque.</param>
		public static Control Build(SkillDefinition skill, int level, bool locked, float size = 64, SigilShape? shape = null, Action? onPressed = null)
		{
			var root = new Control { Name = "Skill", CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
			var button = new SigilButton(null, size, shape ?? (skill.IsPassive ? SigilShape.Diamond : SigilShape.Square)) { Name = "Symbol" };
			button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			button.SetSkill(skill);
			if (onPressed != null)
				button.Pressed += onPressed;
			else
				button.MouseFilter = Control.MouseFilterEnum.Ignore;
			if (locked)
			{
				button.Ink = Palette.TextFaded.Darkened(0.35f);
				button.Modulate = new Color(1, 1, 1, 0.55f);
			}

			root.AddChild(button);

			if (!locked && level > 0 && skill.MaxLevel > 1)
				root.AddChild(Badge(level, level >= skill.MaxLevel));
			return root;
		}

		/// <summary>
		/// A Liderança como ícone: um pentágono regular com o planeta do atributo que ela dá (ataque, defesa...)
		/// e a coroa embaixo, centrada, sobre a borda. Toque abre a janela dela.
		/// </summary>
		public static Control Leader(Stat stat, float size = 64, Action? onPressed = null)
		{
			var root = new Control { Name = "Leader", CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
			var button = new SigilButton(null, size, SigilShape.Pentagon) { Name = "Symbol" };
			button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			button.SetSymbol(Symbol.Of(Texts.PlanetOf(stat)));
			if (onPressed != null)
				button.Pressed += onPressed;
			else
				button.MouseFilter = Control.MouseFilterEnum.Ignore;
			root.AddChild(button);

			var plate = new PanelContainer { Name = "Crown", MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(CrownSize, CrownSize) };
			var box = GameTheme.Box(new Color(Palette.Inset, 0.96f), Palette.Gold, 2, (int)(CrownSize / 2), 3);
			plate.AddThemeStyleboxOverride("panel", box);
			plate.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
			plate.GrowHorizontal = Control.GrowDirection.Both;
			plate.GrowVertical = Control.GrowDirection.Begin;
			plate.OffsetBottom = 6;
			var crown = Doodle.Icon(Art.Icon("leader"), (int)(CrownSize - 8), Palette.Gold).Named("Icon");
			crown.MouseFilter = Control.MouseFilterEnum.Ignore;
			plate.AddChild(crown);
			root.AddChild(plate);
			return root;
		}

		private static Control Badge(int level, bool maxed)
		{
			var plate = new PanelContainer { Name = "Level", MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(BadgeSize, BadgeSize) };
			var box = GameTheme.Box(new Color(Palette.Inset, 0.96f), maxed ? Palette.Gold : Palette.GoldDark, 2, (int)(BadgeSize / 2), 0);
			box.ContentMarginLeft = box.ContentMarginRight = 2;
			plate.AddThemeStyleboxOverride("panel", box);
			plate.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
			plate.GrowHorizontal = Control.GrowDirection.Begin;
			plate.OffsetTop = -6;
			plate.OffsetRight = 6;

			var text = new Label
			{
				Name = "Text",
				Text = Upgrades(level),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ThemeTypeVariation = GameTheme.Number,
			};
			text.AddThemeFontSizeOverride("font_size", 12);
			text.AddThemeColorOverride("font_color", maxed ? Palette.Gold : Palette.Text);
			plate.AddChild(text);
			return plate;
		}
	}
}
