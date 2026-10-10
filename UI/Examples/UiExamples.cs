using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Animations;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Examples
{
	/// <summary>
	/// Os exemplos de docs/ui, compilados com o jogo e nunca chamados. Cada bloco <c>csharp</c> das páginas
	/// de docs/ui está aqui letra por letra (o teste <c>UiDocTests</c> confere, sem contar espaços no começo
	/// das linhas nem linhas vazias). Um exemplo que deixa de bater com a API quebra o build, em vez de
	/// envelhecer na documentação.
	///
	/// Cada método é uma página; os parâmetros são o que o trecho recebe de quem o chama.
	/// </summary>
	internal static partial class UiExamples
	{
		// docs/ui/componentes/touch-button.md
		/// <summary>Um botão novo sobre a base: só diz as caixas, o conteúdo, o tamanho e como o chamado aparece.</summary>
		private partial class Seal : TouchButton
		{
			private readonly Label _label = new() { Name = "Label", MouseFilter = MouseFilterEnum.Ignore };
			private readonly StyleBoxFlat _box = GameTheme.Box(Palette.Inset, Palette.GoldDark, 2, Radius.Button, 0);

			public Seal(string text) : base(0.93f)
			{
				_label.Text = text;
				AddThemeStyleboxOverride("normal", _box);
				AddThemeStyleboxOverride("hover", _box);
				AddThemeStyleboxOverride("pressed", _box);
				Pad(Space.Large, Space.Small, Space.Small);
				Content.AddChild(_label);
			}

			protected override Vector2? MinimumFor(Vector2 content) => new Vector2(content.X, Mathf.Max(content.Y, GameTheme.Touch));

			protected override void OnHighlightChanged() => _box.BorderColor = Palette.GoldDark;

			protected override void OnPulse(float phase) => _box.BorderColor = Palette.GoldDark.Lerp(Palette.Spirit, phase);
		}

		// docs/ui/componentes/touch-button.md
		private static void TouchButtonUse(Control parent)
		{
			var seal = new Seal(T("common.continue")) { Name = "Seal", Highlight = true };
			seal.Pressed += () => seal.Highlight = false;
			parent.AddChild(seal);
		}

		// docs/ui/componentes/surface-button.md
		private static void SurfaceButtonRow(Control list, Action choose)
		{
			var rest = GameTheme.Box(Palette.Inset, Palette.GoldDark, 1, Radius.Medium, 0);
			var hover = GameTheme.Box(States.HoverFill, Palette.Gold, 1, Radius.Medium, 0);
			var row = new SurfaceButton { Name = "Option1", CustomMinimumSize = new Vector2(0, 52) }
				.Boxes(rest, hover)
				.Padded(Space.Large, Space.Large);
			row.Body.AddChild(Layout.Text(T("filter.sort")));
			row.Pressed += choose;
			list.AddChild(row);
		}

		// docs/ui/componentes/surface-button.md
		private static void SurfaceButtonCapsule(Control header, Action explain)
		{
			var pill = Pills.Carved(18, Space.Small, Space.Large);
			var capsule = new SurfaceButton { Name = "Mana", Hug = true, Floor = new Vector2(0, 36) }
				.Boxes(pill, Pills.Lit(pill));
			capsule.Body.AddChild(Layout.Text("120", GameTheme.Number));
			capsule.Pressed += explain;
			header.AddChild(capsule);
		}

		// docs/ui/componentes/surface-button.md
		private static void SurfaceButtonBare(Control header, Label clock, Action openCalendar)
		{
			var time = new SurfaceButton { Name = "Time", Hug = true }.Boxes(null);
			time.Body.AddChild(clock);
			time.Pressed += openCalendar;
			header.AddChild(time);
		}

		// docs/ui/componentes/game-button.md
		private static void GameButtonKinds(Control actions, Action startBattle, Action openReset, bool canRelease, Action releaseSelected)
		{
			var fight = GameButton.Of(T("common.fight"), startBattle, ButtonKind.Primary, "fight")
				.WithCost("mana", "5")
				.Named("Fight");

			var release = new GameButton(T("monsters.release")) { Name = "Release", Kind = ButtonKind.Danger, Disabled = !canRelease };
			release.Pressed += releaseSelected;

			var forgot = GameButton.Of(T("account.forgot"), openReset, ButtonKind.Text, height: GameButton.TextHeight).Named("Forgot");

			actions.AddChild(fight);
			actions.AddChild(release);
			actions.AddChild(forgot);
		}

		// docs/ui/componentes/game-button.md
		private static void GameButtonPair(Control page, Action cancel, Action next)
		{
			var actions = Layout.Grid(2).Named("Actions");
			actions.AddChild(GameButton.Of(T("common.cancel"), cancel).Wide(200).Named("Cancel"));
			actions.AddChild(GameButton.Of(T("common.continue"), next, ButtonKind.Primary, "confirm").Wide(200).Named("Continue"));
			page.AddChild(actions);
		}

		// docs/ui/componentes/game-button.md
		private static void GameButtonChange(GameButton fight)
		{
			fight.Text = T("common.continue");
			fight.Kind = ButtonKind.Secondary;
			fight.IconName = null;
			fight.WithCost("mana", "");
		}

		// docs/ui/componentes/sigil-button.md
		private static void SigilButtons(Control bar, Dialog dialog, Action openPause, Action goBack)
		{
			var close = SigilButton.Of("cancel", dialog.Close, 48).Named("Close");

			var pause = SigilButton.Of("pause", openPause, 56, SigilShape.Square).Named("Pause");
			pause.Highlight = true;

			var speed = new SigilButton(null, 56, SigilShape.Square) { Name = "Speed", Letters = "2×" };
			speed.Badge = "3";
			speed.Accent = Palette.Spirit;

			var back = new BackButton(goBack) { Name = "Back" };

			bar.AddChild(close);
			bar.AddChild(pause);
			bar.AddChild(speed);
			bar.AddChild(back);
		}

		// docs/ui/componentes/tile-button.md
		private static void TileButtons(Control hub, Control nav, Action openBattle, Action openSummon)
		{
			var battle = new TileButton(T("hub.battle"), T("hub.battle_detail"), Art.Icon("fight"), new Vector2(380, 170), ButtonKind.Secondary, horizontal: true) { Name = "Battle" };
			battle.Pressed += openBattle;
			battle.Highlight = true;
			hub.AddChild(battle);

			var summon = TileButton.Nav(T("destination.Summon"), "summon").Named("Summon");
			summon.Pressed += openSummon;
			nav.AddChild(summon);
		}

		// docs/ui/componentes/choice-button.md
		private static void ChoiceButtons(Control filters, Action<int> sortBy)
		{
			var options = new List<(Choice Choice, int Value)>
			{
				(new Choice(T("filter.all")), 0),
				(new Choice(T("filter.element"), Ink: Palette.Arcane), 1),
			};
			var sort = new ChoiceButton(T("filter.sort"), options, 0) { Name = "Sort" };
			sort.Changed += sortBy;
			filters.AddChild(sort);
		}

		// docs/ui/componentes/choice-button.md
		private static void ChoicesOpen(GameButton field, IReadOnlyList<Choice> options, int current, Action<int> chosen)
		{
			field.Pressed += () => Choices.Open(field, T("filter.sort"), options, current, chosen);
		}

		// docs/ui/componentes/text-tabs.md
		private static void TextTabsUse(Control page, Action<int> show)
		{
			var tabs = new TextTabs(height: 48, compact: true) { Name = "Tabs" };
			tabs.Add(T("destination.Campaign"), "12/20", "fight").Name = "Campaign";
			tabs.Add(T("destination.Dungeons"), enabled: false).Name = "Dungeons";
			tabs.Changed += show;
			tabs.Select(0);
			page.AddChild(tabs);
		}

		// docs/ui/componentes/dialog.md
		private static void DialogOpen(Control from, Action save)
		{
			var dialog = Dialog.Open(from, T("auto.title"), name: "AutoDialog");
			dialog.Body.AddChild(Layout.Text(T("auto.setup_note"), GameTheme.Faded, 480));
			dialog.AddAction(T("common.cancel"), null);
			dialog.AddAction(T("auto.start"), save, ButtonKind.Primary, icon: "confirm");
			dialog.Closed += () => GD.Print("fechou");
		}

		// docs/ui/componentes/dialog.md
		private static void DialogShortcuts(Control from, Control capsule, Action stop)
		{
			Dialog.Info(capsule, T("auto.victories"), T("auto.monsters_hint"));
			Dialog.Confirm(from, T("auto.stop_title"), T("auto.stop_confirm"), T("auto.stop"), stop, ButtonKind.Danger);
		}

		// docs/ui/componentes/dialog.md
		private static void DialogRequired(Control from, Action keep)
		{
			var dialog = Dialog.Open(from, T("account.conflict_title"));
			dialog.Dismissable = false;
			dialog.AddAction(T("common.continue"), keep, ButtonKind.Primary);
		}

		// docs/ui/componentes/layout.md
		private static void LayoutPage(Control screen, CurrencyBar currencies, Action back)
		{
			var page = Layout.Page(screen);
			var (header, extra) = Layout.Header(T("destination.Campaign"), currencies, back);
			extra.AddChild(Layout.Labeled("fight", "3/5", T("battle.wave_tip")).Named("Wave"));
			page.AddChild(header);

			var list = new VBoxContainer { Name = "List" };
			list.AddThemeConstantOverride("separation", Space.Medium);
			page.AddChild(Layout.Scroll(list).Named("Scroll"));

			var (panel, content) = Layout.Section(T("auto.rewards"));
			content.AddChild(Layout.Row(Space.Tight).Named("Chips"));
			list.AddChild(panel);
		}

		// docs/ui/componentes/layout.md
		private static void LayoutRebuild(VBoxContainer list, ScrollContainer scroll, Control chosen)
		{
			Layout.Clear(list);
			list.AddChild(chosen);
			Layout.Reveal(scroll, chosen);
		}

		// docs/ui/componentes/press.md
		/// <summary>Um cartão que não é botão: toque escolhe, segurar abre o resumo.</summary>
		private partial class Card : PanelContainer
		{
			private readonly Press _press = new();

			public Card(Action choose, Action summary)
			{
				MouseFilter = MouseFilterEnum.Pass;
				_press.Tapped += choose;
				_press.Held += summary;
			}

			public override void _GuiInput(InputEvent @event) => _press.Feed(this, @event);
		}

		// docs/ui/componentes/press.md
		private static void PressUse(Control portrait, Button skill, Action select, Action describe)
		{
			Press.On(portrait, select, describe);
			Press.OnButton(skill, select, describe);
		}

		// docs/ui/estilo-e-animacoes/tokens.md
		private static void TokensUse(VBoxContainer column, Label caption)
		{
			column.AddThemeConstantOverride("separation", Space.Large);
			caption.AddThemeFontSizeOverride("font_size", FontSize.Detail);
			var box = GameTheme.Box(Palette.Inset, Palette.GoldDark, 1, Radius.Medium, Space.Medium);
			column.AddThemeStyleboxOverride("panel", box);
			caption.Modulate = new Color(1, 1, 1, Fade.Disabled);
		}

		// docs/ui/estilo-e-animacoes/cores.md
		private static void PaletteUse(Label name, Label bonus, Core.Content.Element element, int stars)
		{
			name.AddThemeColorOverride("font_color", Palette.Of(element));
			bonus.AddThemeColorOverride("font_color", Palette.Positive);
			var frame = GameTheme.Box(Palette.Inset, Palette.Frame(stars), 2, Radius.Tile, 0);
			name.AddThemeStyleboxOverride("normal", frame);
		}

		// docs/ui/estilo-e-animacoes/tons.md
		private static void TonesUse(Label label, ButtonKind kind)
		{
			var (fill, border) = Tones.Of(kind);
			label.AddThemeStyleboxOverride("normal", GameTheme.Box(fill, border, 2, Radius.Button, 0));
			label.AddThemeColorOverride("font_color", Tones.Ink(kind));
			label.AddThemeColorOverride("font_outline_color", Tones.Outline(kind));
		}

		// docs/ui/estilo-e-animacoes/tipografia.md
		private static void TypographyUse(Control panel)
		{
			panel.AddChild(Layout.Text(T("auto.title"), GameTheme.Heading));
			panel.AddChild(Layout.Text(T("auto.setup_note"), GameTheme.Faded, 420));
			panel.AddChild(Layout.Text("1.250", GameTheme.Number));

			var name = new Label { Name = "Name", Text = T("hub.battle") };
			name.AddThemeFontOverride("font", GameTheme.Serif);
			name.AddThemeFontSizeOverride("font_size", FontSize.Strong);
			panel.AddChild(name);
		}

		// docs/ui/estilo-e-animacoes/estados.md
		private static StyleBoxFlat StatesUse(StyleBoxFlat box, Color rarity, bool selected, bool marked, bool hover)
		{
			var selection = new SelectionFrame(box, rarity, 2) { Hover = (new Color(rarity, 0.3f), 5, Vector2.Zero) };
			selection.Apply(selected, marked, hover);

			var option = GameTheme.Box(selected ? States.LitFill : Palette.Inset, selected ? States.Selected : Palette.GoldDark, 1, Radius.Medium, 0);
			return option;
		}

		// docs/ui/estilo-e-animacoes/estados.md
		private static void PillsUse(Control badge, Control capsule)
		{
			badge.AddThemeStyleboxOverride("panel", Pills.Floating(Palette.Arcane, 44));
			badge.Modulate = new Color(1, 1, 1, Fade.Floating);

			var rest = Pills.Carved(16, 5, Space.Large);
			capsule.AddThemeStyleboxOverride("normal", rest);
			capsule.AddThemeStyleboxOverride("hover", Pills.Lit(rest));
		}

		// docs/ui/estilo-e-animacoes/animacoes.md
		private static Tween AnimationsUse(Button button, Control banner, Tween? previous)
		{
			Juice.Attach(button, 0.93f);
			var tween = Motion.SpringTo(banner, previous, "scale", Vector2.One * 1.1f);
			return tween;
		}

		// docs/ui/estilo-e-animacoes/animacoes.md
		/// <summary>Um desenho próprio que respira com o mesmo ritmo do chamado dos botões.</summary>
		private partial class Beacon : Control
		{
			private readonly Pulse _pulse = new();

			public override void _Process(double delta)
			{
				_pulse.Advance(delta);
				Modulate = Colors.White.Lerp(Palette.Spirit, _pulse.Phase);
			}
		}
	}
}
