using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A ficha de uma runa: o jeito de mostrar runa em todo o jogo (a tela de Runas, a vitória, a
	/// Batalha automática e o toque longo em toda runa em miniatura).
	///
	/// No alto, o título (conjunto e espaço) na cor da raridade e a plaquinha da raridade; ao lado da runa,
	/// o atributo principal grande e o inato em ouro; embaixo, os subatributos com o Glifo; por fim, o
	/// bônus do conjunto em verde.
	///
	/// Com <c>opened</c> (o nível da runa quando a tela abriu), o que ela ganhou desde então fica em verde
	/// ao lado do valor, e o subatributo novo inteiro em verde, com "new". <c>tools</c> põe sigilos no fim
	/// de cada linha de subatributo (as pedras da janela Afiar e Encantar, na tela de Runas); <c>note</c>
	/// é uma linha apagada no pé (quem usa a runa). Sem <c>title</c>, o título fica de fora: a
	/// <see cref="RuneDialog"/> o põe no cabeçalho da janela.
	/// </summary>
	public partial class RuneCard : VBoxContainer
	{
		public RuneCard(Rune rune, float width = 320, int? opened = null, Func<int, IEnumerable<Control>>? tools = null, string? note = null, bool title = true)
		{
			Name = "RuneCard";
			CustomMinimumSize = new Vector2(width, 0);
			MouseFilter = MouseFilterEnum.Pass;
			AddThemeConstantOverride("separation", Space.Medium);
			var color = Palette.Of(rune.Rarity);

			if (title)
			{
				var header = Layout.Row(Space.Regular).Named("Header");
				var name = new Label { Name = "Title", Text = Texts.Title(rune), HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
				name.AddThemeColorOverride("font_color", color);
				header.AddChild(name);
				AddChild(header);
			}

            var body = Layout.Row(Space.Wide).Named("Body");
            body.AddChild(new RuneTile(rune, rune.Slot, 1.35f) { Name = "Tile", MouseFilter = MouseFilterEnum.Ignore });
            var stats = new VBoxContainer { Name = "Stats", Alignment = AlignmentMode.Begin, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            stats.AddThemeConstantOverride("separation", Space.Tight);

            var main = new VBoxContainer { Name = "Main", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            main.SizeFlagsHorizontal = SizeFlags.ExpandFill;

			var tagBox = new VBoxContainer { Name = "TagBox", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var tag = RarityTag(rune.Rarity);
            tag.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; // Empurra a tag para a direita
            tag.SizeFlagsVertical = SizeFlags.ShrinkCenter; // Centraliza a tag verticalmente
            tagBox.AddChild(tag);
            main.AddChild(tagBox);

            var valuesBox = new HBoxContainer { Name = "ValuesBox", SizeFlagsHorizontal = SizeFlags.ExpandFill};

            valuesBox.AddThemeConstantOverride("separation", Space.Tight);

            var mainValue = new Label
            {
                Name = "Value",
                Text = Texts.Format(rune.Main, rune.MainValue),
                //SizeFlagsHorizontal = SizeFlags.ExpandFill,
                //SizeFlagsStretchRatio = 1
            };

            mainValue.AddThemeFontOverride("font", GameTheme.Serif);
            mainValue.AddThemeFontSizeOverride("font_size", 22);
            mainValue.HorizontalAlignment = HorizontalAlignment.Left;

            valuesBox.AddChild(mainValue);

            if (opened is { } level && rune.Level > level)
            {
                valuesBox.AddChild(Gain("Gain", Texts.Amount(rune.Main, rune.MainValue - RuneRules.MainValue(rune.Main, rune.Grade, level)), 16));
            }

            main.AddChild(valuesBox);

            stats.AddChild(main);

			if (rune.Innate is { } innate)
			{
				var label = new Label { Name = "Innate", Text = Texts.Format(innate) };
				label.AddThemeColorOverride("font_color", Palette.Gold);
				stats.AddChild(label);
			}

			body.AddChild(stats);
            AddChild(body);

            if (rune.Substats.Count > 0)
			{
				AddChild(new HSeparator { Name = "SubstatsLine" });
				for (var i = 0; i < rune.Substats.Count; i++)
					AddChild(Substat(rune, i, opened, tools).Named($"Substat{i + 1}"));
			}

			var bonus = RichText.Label(Texts.Describe(RuneSets.For(rune.Set)), width, null, 14).Named("SetBonus");
			bonus.AddThemeColorOverride("default_color", Palette.TextFaded);
			AddChild(bonus);

			if (note != null)
				AddChild(new Label { Name = "Note", Text = note, ThemeTypeVariation = GameTheme.Faded, AutowrapMode = TextServer.AutowrapMode.WordSmart });
		}

		/// <summary>
		/// Um subatributo: o nome em ouro, o valor (com a Pedra de Afiar) em branco e, entre parênteses e
		/// apagado, o quanto veio das melhoras, o da pedra e o "encantado": "Vida +28% (+22%)". O que mudou
		/// desde <paramref name="opened"/> fica em verde: a linha inteira com "new" se ele nasceu depois, ou
		/// o quanto subiu ao lado do valor.
		/// </summary>
		private static HBoxContainer Substat(Rune rune, int index, int? opened, Func<int, IEnumerable<Control>>? tools)
		{
			var substat = rune.Substats[index];
			var row = Layout.Row(Space.Small);
			var isNew = opened is { } level && substat.Rolls.Count > 0 && substat.Rolls[0].Level > level;
			var gained = opened is { } since ? substat.Rolls.Where(r => r.Level > since).Sum(r => r.Amount) : 0;
			row.AddChild(new RuneGlyph(Texts.GlyphOf(substat.Stat), 18, isNew ? Palette.Positive : Palette.GoldDark.Lightened(0.3f)) { Name = "Glyph" });
			row.AddChild(Part("Name", Texts.Name(substat.Stat), isNew ? Palette.Positive : Palette.Gold));
			row.AddChild(Part("Value", Texts.Amount(substat.Stat, substat.Total), isNew ? Palette.Positive : Palette.Text));
			if (substat.Upgraded > 0)
				row.AddChild(Part("Upgraded", $"({Texts.Amount(substat.Stat, substat.Upgraded)})", Palette.TextFaded));
			if (substat.Grind > 0)
				row.AddChild(Part("Ground", T("rune.ground", Texts.Amount(substat.Stat, substat.Grind)), Palette.TextFaded));
			if (substat.Enchanted)
				row.AddChild(Part("Enchanted", T("runes.enchanted_line"), Palette.TextFaded));

			if (isNew)
			{
				row.AddChild(Gain("New", T("runes.new")));
			}
			else if (gained > 0)
			{
				row.AddChild(Gain("Gain", Texts.Amount(substat.Stat, gained)));
			}

			row.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
			foreach (var tool in tools?.Invoke(index) ?? Array.Empty<Control>())
				row.AddChild(tool);
			return row;
		}

		private static Label Part(string name, string text, Color color)
		{
			var label = new Label { Name = name, Text = text, VerticalAlignment = VerticalAlignment.Center };
			label.AddThemeColorOverride("font_color", color);
			return label;
		}

		/// <summary>O que a runa ganhou, em verde, ao lado do valor.</summary>
		private static Label Gain(string name, string text, int size = 0)
		{
			var label = new Label { Name = name, Text = text, VerticalAlignment = VerticalAlignment.Center };
			label.AddThemeColorOverride("font_color", Palette.Positive);
			if (size > 0)
			{
				label.AddThemeFontOverride("font", GameTheme.Serif);
				label.AddThemeFontSizeOverride("font_size", size);
			}

			return label;
		}

		/// <summary>A plaquinha da raridade, na cor dela ("Legend").</summary>
		private static PanelContainer RarityTag(Core.Content.RuneRarity rarity)
		{
			var color = Palette.Of(rarity);
			var tag = new PanelContainer { Name = "Rarity", SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore };
			var box = GameTheme.Box(color.Darkened(0.55f), color, 1, 6, 0);
			box.ContentMarginLeft = box.ContentMarginRight = 8;
			box.ContentMarginTop = box.ContentMarginBottom = 2;
			tag.AddThemeStyleboxOverride("panel", box);
			var label = new Label { Name = "Label", Text = Texts.Name(rarity), MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", 13);
			label.AddThemeColorOverride("font_color", color.Lightened(0.35f));
			tag.AddChild(label);
			return tag;
		}
	}
}
