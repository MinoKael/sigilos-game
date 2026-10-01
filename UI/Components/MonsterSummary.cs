using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;
using Side = Sigilos.Core.Battle.Side;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O resumo de um monstro, aberto pelo toque longo em qualquer lugar onde ele aparece (cartão, equipe,
	/// unidade na luta, inimigo da fase): retrato, nome, estrelas, elemento e papel, nível, os oito
	/// atributos, os efeitos em cima dele (na luta), as habilidades e a Liderança. É uma janela contextual
	/// colada em quem foi tocado; não pausa nada — a luta e a Batalha automática seguem.
	/// </summary>
	public static class MonsterSummary
	{
		private const float Width = 600;

		/// <summary>Um monstro do jogador (<paramref name="monster"/>) ou, sem ele, a variante do catálogo nas estrelas naturais, nível 1.</summary>
		public static Dialog Open(Control from, SummonDefinition summon, OwnedSummon? monster)
		{
			var awakened = monster?.Awakened ?? false;
			var stars = monster?.Stars ?? summon.Rarity;
			var level = monster?.Level ?? 1;
			var runes = monster != null && UiSession.Player is { } player ? player.RunesOn(monster.Id) : Array.Empty<Rune>();
			var sheet = SummonStats.For(summon, stars, level, awakened, runes);

			var dialog = Dialog.Open(from, summon.NameFor(awakened), Width, from, "MonsterSummary");
			var body = dialog.Body;
			var max = monster != null ? Leveling.MaxLevel(monster) : Growth.MaxLevel(stars);
			body.AddChild(Identity(summon.ImageFor(awakened), summon.Element, summon.Rarity, stars, awakened, Texts.Name(summon.Role), T("summary.level", level, max)));

			if (monster == null)
				body.AddChild(Layout.Text(T("summary.catalog", Texts.Stars(summon.Rarity)), GameTheme.Faded).Named("Catalog"));
			else if (Where(monster) is { Length: > 0 } where)
				body.AddChild(Layout.Text(where, GameTheme.Faded).Named("Where"));

			body.AddChild(Stats(stat => sheet.Total.Get(stat), stat => sheet.Runes.Stats.Get(stat)));
			if (sheet.Runes.ActiveSets.Count > 0)
			{
				var sets = Layout.Flow(6).Named("Sets");
				for (var i = 0; i < sheet.Runes.ActiveSets.Count; i++)
				{
					var set = sheet.Runes.ActiveSets[i];
					sets.AddChild(Layout.Labeled(RuneSets.For(set.Set).Glyph, "", Texts.Name(set.Set)).Named($"{set.Set}{i + 1}"));
				}

				body.AddChild(Section(T("summary.sets"), sets));
			}

			var skills = new VBoxContainer { Name = "Skills" };
			skills.AddThemeConstantOverride("separation", 10);
			var list = summon.SkillsFor(awakened);
			for (var i = 0; i < list.Count; i++)
				skills.AddChild(SkillRow.Build(list[i], monster?.SkillLevel(i) ?? 1, awakened, false, Width - 110).Named($"Skill{i + 1}"));
			body.AddChild(Section(T("summary.skills"), skills));

			if (summon.Leader is { } leader)
				body.AddChild(Leadership(leader));
			return dialog;
		}

		/// <summary>Uma unidade na luta (ou na prévia de uma fase): Vida de agora, atributos com os efeitos, os efeitos e as recargas.</summary>
		public static Dialog Open(Control from, BattleUnit unit)
		{
			var database = UiSession.Database;
			var summon = database != null && database.HasSummon(unit.DefinitionId) ? database.Summon(unit.DefinitionId) : null;
			var enemy = summon == null && database != null ? database.Enemies.FirstOrDefault(e => e.Id == unit.DefinitionId) : null;
			var rarity = summon?.Rarity ?? enemy?.Rarity ?? 0;
			var role = summon?.Role ?? enemy?.Role;

			var dialog = Dialog.Open(from, unit.Name, Width, from, "MonsterSummary");
			var body = dialog.Body;
			var side = unit.Side == Side.Allies ? T("summary.ally") : T("summary.enemy");
			var caption = role is { } r ? $"{Texts.Name(r)} · {side}" : side;
			body.AddChild(Identity(unit.Image, unit.Element, rarity, 0, unit.Awakened, caption, T("summary.level_only", unit.Level)));

			var health = Layout.Energy(unit.HealthFraction < 0.3 ? Palette.HealthLow : Palette.Health, 20).Named("Health");
			health.MaxValue = Math.Max(1, unit.MaxHealth);
			health.Value = unit.Health;
			var healthRow = Layout.Row(10).Named("HealthRow");
			health.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			health.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			healthRow.AddChild(health);
			var shield = unit.Find(StatusKind.Shield)?.Value ?? 0;
			var healthText = unit.IsAlive ? T("summary.health", Math.Round(unit.Health), Math.Round(unit.MaxHealth)) : T("battle.fallen");
			if (shield > 0)
				healthText += " " + T("summary.shield", Math.Round(shield));
			healthRow.AddChild(new Label { Name = "Text", Text = healthText, ThemeTypeVariation = GameTheme.Number });
			body.AddChild(healthRow);

			body.AddChild(Stats(unit.Current, _ => 0));

			var effects = unit.Statuses.Where(s => s.Kind != StatusKind.Shield).ToList();
			if (effects.Count > 0)
			{
				var column = new VBoxContainer { Name = "Effects" };
				column.AddThemeConstantOverride("separation", 6);
				for (var i = 0; i < effects.Count; i++)
				{
					var status = effects[i];
					var ink = BattleRules.IsNegative(status.Kind) ? Palette.Negative : Palette.Positive;
					var row = Layout.Row(8).Named($"{status.Kind}{i + 1}");
					row.AddChild(Doodle.Icon(Art.Effect(status.Kind), 28, ink).Named("Icon"));
					row.AddChild(RichText.Label($"[color=#{ink.ToHtml(false)}]{Texts.Name(status.Kind)}[/color] · {Texts.Turns(status.Turns)} — {Texts.Explain(status.Kind)}", Width - 120, null, GameTheme.SmallSize).Named("Text"));
					column.AddChild(row);
				}

				body.AddChild(Section(T("summary.effects"), column));
			}

			var skills = new VBoxContainer { Name = "Skills" };
			skills.AddThemeConstantOverride("separation", 10);
			for (var i = 0; i < unit.Skills.Count; i++)
			{
				var row = SkillRow.Build(unit.Skills[i], 0, unit.Awakened, false, Width - 110).Named($"Skill{i + 1}");
				if (unit.Cooldown(i) > 0)
					row.AddChild(new Label { Name = "Wait", Text = T("summary.cooldown", unit.Cooldown(i)), ThemeTypeVariation = GameTheme.Faded });
				skills.AddChild(row);
			}

			if (unit.Passive is { } passive)
				skills.AddChild(PassiveRow(passive, unit.Awakened));
			body.AddChild(Section(T("summary.skills"), skills));

			if (summon?.Leader is { } leader && unit.Side == Side.Allies)
				body.AddChild(Leadership(leader));
			return dialog;
		}

		/// <summary>Retrato na moldura da raridade e, ao lado, estrelas, elemento, papel e nível.</summary>
		private static Control Identity(string image, Element element, int rarity, int stars, bool awakened, string role, string level)
		{
			var row = Layout.Row(14).Named("Identity");
			var frame = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(104, 104) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, rarity > 0 ? Palette.Frame(rarity) : Palette.GoldDark, 3, 10, 8));
			frame.AddChild(Doodle.Masked(Art.Creature(image), Palette.Of(element), MaskShape.Rounded, 6));
			row.AddChild(frame);

			var info = new VBoxContainer { Name = "Info", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			info.AddThemeConstantOverride("separation", 4);
			if (stars > 0)
			{
				var starLabel = new Label { Name = "Stars", Text = Texts.Stars(stars) };
				starLabel.AddThemeColorOverride("font_color", Palette.Stars(awakened));
				starLabel.AddThemeFontSizeOverride("font_size", 22);
				info.AddChild(starLabel);
			}

			var line = Layout.Row(8).Named("Element");
			line.AddChild(Doodle.Icon(Art.Element(element), 26, Palette.Of(element)).Named("Icon"));
			var name = new Label { Name = "Name", Text = Texts.Name(element), VerticalAlignment = VerticalAlignment.Center };
			name.AddThemeColorOverride("font_color", Palette.Of(element));
			line.AddChild(name);
			line.AddChild(new Label { Name = "Role", Text = $"· {role}", VerticalAlignment = VerticalAlignment.Center });
			info.AddChild(line);
			info.AddChild(new Label { Name = "Level", Text = level, ThemeTypeVariation = GameTheme.Number });
			if (awakened)
				info.AddChild(new Label { Name = "Awakened", Text = T("summary.awakened"), ThemeTypeVariation = GameTheme.Faded });
			row.AddChild(info);
			return row;
		}

		/// <summary>Os oito atributos em duas colunas: Glifo, nome e valor (com o que as runas somam em verde).</summary>
		private static Control Stats(Func<Stat, double> value, Func<Stat, double> bonus)
		{
			var grid = new GridContainer { Name = "Stats", Columns = 2 };
			grid.AddThemeConstantOverride("h_separation", 24);
			grid.AddThemeConstantOverride("v_separation", 4);
			foreach (var stat in Enum.GetValues<Stat>())
			{
				var row = Layout.Row(8).Named(stat.ToString());
				row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				row.AddChild(new RuneGlyph(Texts.GlyphOf(stat), 20, Palette.Gold) { Name = "Glyph" });
				row.AddChild(new Label { Name = "Name", Text = Texts.Name(stat), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
				row.AddChild(new Label { Name = "Value", Text = Texts.Value(stat, value(stat)), ThemeTypeVariation = GameTheme.Number, HorizontalAlignment = HorizontalAlignment.Right });
				var extra = bonus(stat);
				if (extra > 0.0005)
				{
					var green = new Label { Name = "Bonus", Text = $"+{Texts.Value(stat, extra)}" };
					green.AddThemeColorOverride("font_color", Palette.Positive);
					green.AddThemeFontSizeOverride("font_size", GameTheme.SmallSize);
					row.AddChild(green);
				}

				grid.AddChild(row);
			}

			return grid;
		}

		private static Control PassiveRow(PassiveDefinition passive, bool awakened)
		{
			var row = Layout.Row(10).Named("Passive");
			row.AddChild(Doodle.Icon(Art.Icon("skill"), 40, Palette.Gold).Named("Icon"));
			var column = new VBoxContainer { Name = "Text", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			column.AddChild(new Label { Name = "Name", Text = T("skill.passive_only"), ThemeTypeVariation = GameTheme.Heading });
			column.AddChild(RichText.Label(Texts.Describe(passive, awakened), Width - 110, GameTheme.Faded, GameTheme.SmallSize).Named("Description"));
			row.AddChild(column);
			return row;
		}

		private static Control Leadership(LeaderDefinition leader)
		{
			var row = Layout.Row(10).Named("Leader");
			row.AddChild(Doodle.Icon(Art.Icon("leader"), 32, Palette.Gold).Named("Icon"));
			row.AddChild(RichText.Label(T("monsters.leadership", Texts.Percent(leader.Value), Texts.Name(leader.Stat)), Width - 100).Named("Text"));
			return row;
		}

		/// <summary>Um bloco com título pequeno em ouro e o conteúdo embaixo.</summary>
		private static Control Section(string title, Control content)
		{
			var column = new VBoxContainer { Name = content.Name + "Section" };
			column.AddThemeConstantOverride("separation", 6);
			column.AddChild(new HSeparator { Name = "Line" });
			var heading = new Label { Name = "Heading", Text = title };
			heading.AddThemeColorOverride("font_color", Palette.Gold);
			heading.AddThemeFontSizeOverride("font_size", 19);
			column.AddChild(heading);
			column.AddChild(content);
			return column;
		}

		/// <summary>Onde o monstro está: no Baú, nas equipes.</summary>
		private static string Where(OwnedSummon monster)
		{
			var parts = new List<string>();
			if (monster.Stored)
				parts.Add(T("monsters.in_vault"));
			if (UiSession.Player is { } player && UiSession.Database is { } database)
			{
				var teams = player.Teams.Where(pair => pair.Value.Contains(monster.Id))
					.Select(pair => pair.Key == Teams.Campaign ? T("teams.campaign") : database.Dungeons.FirstOrDefault(d => d.Id == pair.Key)?.Name ?? pair.Key)
					.ToList();
				if (teams.Count > 0)
					parts.Add(T("monsters.teams", string.Join(", ", teams)));
			}

			return string.Join(" · ", parts);
		}
	}
}
