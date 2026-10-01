using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma habilidade: o símbolo dela num sigilo (losango na Passiva), o nome, a recarga e o nível
	/// escritos ("Recarga 4", "Nv 2/5"), e o que ela faz. Com <c>levels</c>, uma linha a mais diz o que
	/// cada nível que falta dá. Travada (a do Despertar, antes de despertar), fica apagada, com "Liberada
	/// no Despertar" ao lado do nome.
	/// </summary>
	public static class SkillRow
	{
		/// <param name="level">O nível de agora; 0 esconde o nível (na luta, a habilidade já vem no dela).</param>
		public static Control Build(SkillDefinition skill, int level, bool awakened, bool locked, float width = 400, bool levels = false)
		{
			var row = Layout.Row(10).Named("Skill");
			var icon = new SigilButton(null, 52, skill.IsPassive ? SigilShape.Diamond : SigilShape.Circle)
			{
				Name = "Symbol",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Disabled = locked,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			};
			icon.SetSymbol(Art.Skill(skill));
			row.AddChild(icon);

			var column = new VBoxContainer { Name = "Text", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			column.AddThemeConstantOverride("separation", 2);
			// Em fileira que quebra: nome, recarga, nível e o "ao despertar" não cabem sempre numa linha, e o
			// que não cabe alargava a ficha inteira.
			var title = Layout.Flow(8).Named("Title");
			title.AddThemeConstantOverride("v_separation", 0);
			var name = new Label { Name = "Name", Text = skill.IsPassive ? T("skill.passive_name", skill.Name) : skill.Name };
			name.AddThemeFontOverride("font", GameTheme.Serif);
			name.AddThemeFontSizeOverride("font_size", 17);
			name.AddThemeColorOverride("font_color", locked ? Palette.TextFaded : Palette.Gold);
			title.AddChild(name);
			if (!skill.IsPassive && skill.Cooldown > 0)
				title.AddChild(new Label { Name = "Cooldown", Text = T("skill.cooldown_short", level > 0 ? skill.At(level, awakened).Cooldown : skill.Cooldown), ThemeTypeVariation = GameTheme.Faded, VerticalAlignment = VerticalAlignment.Center });
			if (level > 0 && skill.MaxLevel > 1)
			{
				var tag = new Label { Name = "Level", Text = T("skill.level", level, skill.MaxLevel), VerticalAlignment = VerticalAlignment.Center };
				tag.AddThemeColorOverride("font_color", level >= skill.MaxLevel ? Palette.Gold : Palette.Text);
				tag.AddThemeFontSizeOverride("font_size", GameTheme.SmallSize);
				title.AddChild(tag);
			}

			if (locked)
			{
				var note = new Label { Name = "Locked", Text = T("monsters.skill_locked"), VerticalAlignment = VerticalAlignment.Center };
				note.AddThemeColorOverride("font_color", Palette.Awakened);
				note.AddThemeFontSizeOverride("font_size", GameTheme.SmallSize);
				title.AddChild(note);
			}

			column.AddChild(title);
			column.AddChild(RichText.Label(Texts.Describe(skill, awakened), width, GameTheme.Faded, GameTheme.SmallSize).Named("Description"));
			if (levels && level > 0 && level < skill.MaxLevel)
			{
				var next = new Label { Name = "Levels", Text = Texts.LevelUps(skill, level), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(width, 0) };
				next.AddThemeFontSizeOverride("font_size", 14);
				next.AddThemeColorOverride("font_color", Palette.GoldDark.Lightened(0.35f));
				column.AddChild(next);
			}
			row.AddChild(column);
			return row;
		}
	}
}
