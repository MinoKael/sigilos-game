using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma habilidade: o Glifo dela num sigilo (losango na Passiva), o nome com a recarga e os níveis em
	/// losangos cheios e vazios, e o que ela faz. Travada (a do Despertar, antes de despertar), fica
	/// apagada com o olho do Despertar ao lado.
	/// </summary>
	public static class SkillRow
	{
		public static Control Build(SkillDefinition skill, int level, bool awakened, bool locked, float width = 400)
		{
			var row = Layout.Row(12).Named("Skill");
			var icon = new SigilButton(null, "", 52, skill.IsPassive ? SigilShape.Diamond : SigilShape.Circle)
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
			var title = Layout.Row(8).Named("Title");
			var name = new Label { Name = "Name", Text = skill.IsPassive ? T("skill.passive_name", skill.Name) : skill.Name };
			name.AddThemeFontOverride("font", GameTheme.Serif);
			name.AddThemeFontSizeOverride("font_size", 17);
			name.AddThemeColorOverride("font_color", locked ? Palette.TextFaded : Palette.Gold);
			title.AddChild(name);
			if (!skill.IsPassive && skill.Cooldown > 0)
				title.AddChild(new Label { Name = "Cooldown", Text = $"⟳{skill.At(level, awakened).Cooldown}", ThemeTypeVariation = GameTheme.Faded, TooltipText = T("skill.cooldown_tip"), MouseFilter = Control.MouseFilterEnum.Stop });
			if (skill.MaxLevel > 1)
			{
				var pips = new Label
				{
					Name = "Level",
					Text = new string('◆', level) + new string('◇', skill.MaxLevel - level),
					TooltipText = Texts.Plain(Texts.LevelUps(skill, 1)),
					MouseFilter = Control.MouseFilterEnum.Stop,
				};
				pips.AddThemeColorOverride("font_color", Palette.Gold);
				pips.AddThemeFontSizeOverride("font_size", 13);
				title.AddChild(pips);
			}

			if (locked)
			{
				var eye = Doodle.Icon(Art.Icon("awaken"), 18, Palette.Awakened).Named("Locked");
				eye.TooltipText = T("monsters.skill_locked");
				eye.MouseFilter = Control.MouseFilterEnum.Stop;
				title.AddChild(eye);
			}

			column.AddChild(title);
			column.AddChild(RichText.Label(Texts.Describe(skill, awakened), width, GameTheme.Faded, 14).Named("Description"));
			row.AddChild(column);
			return row;
		}
	}
}
