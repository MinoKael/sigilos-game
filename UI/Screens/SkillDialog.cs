using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A janela de uma habilidade, colada no ícone que o jogador tocou (com a seta): o nome no título; o
	/// que ela é (passiva ou recarga, nível, "liberada no Despertar"); a frase de ambientação
	/// (<see cref="SkillDefinition.Flavor"/>), só quando existe; a descrição técnica; e a lista de
	/// aprimoramentos, em letra menor e apagada, com os já recebidos mais claros.
	/// </summary>
	public static class SkillDialog
	{
		private const float Width = 460;
		private const float TextWidth = Width - 40;

		/// <param name="level">O nível de agora (1 é a habilidade sem aprimoramento).</param>
		public static Dialog Open(Control anchor, SkillDefinition skill, int level, bool awakened, bool locked)
		{
			var dialog = Dialog.Open(anchor, skill.IsPassive ? T("skill.passive_name", skill.Name) : skill.Name, Width, anchor, "SkillDialog");
			var body = dialog.Body;

			body.AddChild(Facts(skill, level, awakened, locked));

			if (skill.Flavor.Length > 0)
			{
				var flavor = new Label { Name = "Flavor", Text = skill.Flavor, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(TextWidth, 0) };
				flavor.AddThemeFontOverride("font", GameTheme.Serif);
				flavor.AddThemeFontSizeOverride("font_size", FontSize.Note);
				flavor.AddThemeColorOverride("font_color", Palette.GoldDark.Lightened(0.45f));
				body.AddChild(flavor);
			}

			body.AddChild(RichText.Label(Texts.Describe(skill, awakened), TextWidth).Named("Description"));

			if (skill.Levels.Count > 0)
				body.AddChild(Upgrades(skill, level));
			return dialog;
		}

		/// <summary>A linha do que a habilidade é: recarga ou passiva, o nível e o aviso de travada.</summary>
		private static Control Facts(SkillDefinition skill, int level, bool awakened, bool locked)
		{
			var facts = Layout.Flow(Space.Wide).Named("Facts");
			var kind = skill.IsPassive ? T("skill.passive_only")
				: skill.Cooldown > 0 ? T("skill.cooldown_short", skill.At(level, awakened).Cooldown)
				: "";
			if (kind.Length > 0)
				facts.AddChild(Fact(kind, Palette.TextFaded).Named("Kind"));
			if (skill.MaxLevel > 1)
				facts.AddChild(Fact(T("skill.level", level, skill.MaxLevel), level >= skill.MaxLevel ? Palette.Gold : Palette.Text).Named("Level"));
			if (locked)
				facts.AddChild(Fact(T("monsters.skill_locked"), Palette.Awakened).Named("Locked"));
			return facts;
		}

		private static Label Fact(string text, Color color)
		{
			var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
			label.AddThemeFontSizeOverride("font_size", FontSize.Detail);
			label.AddThemeColorOverride("font_color", color);
			return label;
		}

		/// <summary>
		/// Um aprimoramento por linha, "+1  Dano +10%": o +N é o nível N+1. Os já recebidos (até o nível de
		/// agora) ficam em claro; os que faltam, mais apagados.
		/// </summary>
		private static Control Upgrades(SkillDefinition skill, int level)
		{
			var box = new VBoxContainer { Name = "Upgrades" };
			box.AddThemeConstantOverride("separation", Space.Hair);

			var title = new Label { Name = "Title", Text = T("skill.upgrades") };
			title.AddThemeFontSizeOverride("font_size", FontSize.Small);
			title.AddThemeColorOverride("font_color", Palette.TextFaded.Darkened(0.2f));
			box.AddChild(title);

			for (var i = 0; i < skill.Levels.Count; i++)
			{
				var acquired = level >= i + 2;
				var line = new Label
				{
					Name = $"Upgrade{i + 1}",
					Text = T("skill.upgrade_line", $"+{i + 1}", Texts.LevelUp(skill.Levels[i])),
					AutowrapMode = TextServer.AutowrapMode.WordSmart,
					CustomMinimumSize = new Vector2(TextWidth, 0),
				};
				line.AddThemeFontSizeOverride("font_size", FontSize.Small);
				line.AddThemeColorOverride("font_color", acquired ? Palette.Text.Lerp(Colors.White, 0.4f) : Palette.TextFaded.Darkened(0.35f));
				box.AddChild(line);
			}

			return box;
		}
	}
}
