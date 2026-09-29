using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>Um trecho da barra de experiência: no nível <see cref="Level"/>, de <see cref="From"/> a <see cref="To"/> (frações da barra).</summary>
	public readonly record struct LevelStep(int Level, float From, float To);

	/// <summary>Um monstro da equipe no resultado: o retrato e a barra de experiência, trecho a trecho até onde a luta levou.</summary>
	public sealed record ResultMonster(string Name, Texture2D? Art, Color Ink, IReadOnlyList<LevelStep> Steps)
	{
		/// <summary>Chegou (ou já estava) no nível máximo das estrelas.</summary>
		public bool MaxLevel { get; init; }

		/// <summary>
		/// Os trechos da barra entre o antes (<paramref name="level"/>, <paramref name="experience"/>) e o
		/// agora de <paramref name="monster"/>: um por nível atravessado. Sem experiência ganha, um trecho parado.
		/// </summary>
		public static IReadOnlyList<LevelStep> StepsOf(OwnedSummon monster, int level, int experience)
		{
			var steps = new List<LevelStep>();
			var max = Leveling.MaxLevel(monster);
			for (var current = level; current <= monster.Level; current++)
			{
				if (current >= max)
				{
					steps.Add(new LevelStep(max, 1, 1));
					break;
				}

				var need = Math.Max(1, Leveling.ExperienceToNext(monster.Stars, current));
				var from = current == level ? experience / (float)need : 0;
				var to = current == monster.Level ? monster.Experience / (float)need : 1;
				steps.Add(new LevelStep(current, Mathf.Clamp(from, 0, 1), Mathf.Clamp(to, 0, 1)));
			}

			return steps;
		}
	}

	/// <summary>O que o resultado recebe do GameRoot além do que a própria luta sabe.</summary>
	public sealed record BattleOutcome(bool Victory, VictoryReward? Reward, IReadOnlyList<ResultMonster> Team, int AccountLevel, double? Best, bool NewBest);

	/// <summary>
	/// O fim da luta, por cima do campo: "Vitória" ou "Derrota" grande no alto; no canto, o tempo da luta
	/// e o melhor tempo dela (aceso quando foi batido); no meio, a faixa com o que a luta rendeu; embaixo,
	/// a equipe, cada monstro com a barra de experiência subindo nível a nível (ou "nível máximo"). A runa
	/// que caiu abre por cima, na <see cref="RuneCard"/>, com Vender e Pegar.
	/// </summary>
	public partial class BattleResultPanel : ColorRect
	{
		/// <summary>Quanto um trecho da barra leva para encher; muitos níveis de uma vez dividem <see cref="RiseSeconds"/>.</summary>
		private const double StepSeconds = 0.55;

		private const double RiseSeconds = 2.5;

		private readonly BattleOutcome _outcome;
		private readonly Action<Rune> _sell;

		/// <summary>As barras de experiência, prontas para subir quando o jogador puder vê-las.</summary>
		private readonly List<Action> _animations = new();
		private Label? _essence;
		private int _essenceShown;

		/// <param name="seconds">O tempo da luta na tela.</param>
		/// <param name="defeat">Na derrota, o motivo e o símbolo dele (o tempo esgotado ou todos caídos).</param>
		public BattleResultPanel(BattleOutcome outcome, double seconds, (string Icon, string Text)? defeat, Action onContinue, Action<Rune> sell)
		{
			_outcome = outcome;
			_sell = sell;
			Name = "Result";
			Color = new Color(0, 0, 0, 0.62f);
			MouseFilter = MouseFilterEnum.Stop;
			ZIndex = 60;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			AddChild(Title(outcome.Victory));
			AddChild(Time(seconds, outcome));
			AddChild(Rewards(outcome, defeat));
			AddChild(Team(outcome.Team));

			var next = SigilButton.Of("confirm", T("common.continue"), onContinue, 64).Named("Continue");
			next.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
			next.GrowHorizontal = GrowDirection.Both;
			next.GrowVertical = GrowDirection.Begin;
			next.OffsetTop = next.OffsetBottom = -Layout.ScreenMargin;
			AddChild(next);
		}

		public override void _Ready()
		{
			if (_outcome.Reward?.Rune is not { } rune)
			{
				Animate();
				return;
			}

			// A runa que caiu, por cima de tudo: o jogador decide antes de ver o resto.
			var value = RuneRules.SellValue(rune);
			var sell = SigilButton.Of("dismantle", T("runes.sell", value), () => Sell(rune, value), 64, SigilShape.Diamond).Named("Sell");
			sell.Badge = value.ToString();
			var keep = SigilButton.Of("confirm", T("battle.keep_rune"), () => { }, 64, SigilShape.Diamond).Named("Keep");
			// As barras só sobem depois que a runa sai da frente, para o jogador ver.
			RunePopup.Open(this, rune, new[] { sell, keep }, modal: true).TreeExiting += Animate;
		}

		private void Animate()
		{
			foreach (var animation in _animations)
				animation();
			_animations.Clear();
		}

		private static Label Title(bool victory)
		{
			var title = new Label { Name = "Title", Text = victory ? T("battle.victory") : T("battle.defeat"), HorizontalAlignment = HorizontalAlignment.Center };
			title.AddThemeFontOverride("font", GameTheme.Serif);
			title.AddThemeFontSizeOverride("font_size", 76);
			title.AddThemeColorOverride("font_color", victory ? Palette.Gold : Palette.Negative);
			title.AddThemeColorOverride("font_outline_color", Palette.Background);
			title.AddThemeConstantOverride("outline_size", 14);
			title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
			title.AddThemeConstantOverride("shadow_offset_y", 5);
			title.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
			title.GrowHorizontal = GrowDirection.Both;
			title.OffsetTop = title.OffsetBottom = 28;
			return title;
		}

		/// <summary>O canto de cima à direita: o tempo desta luta e o melhor dela, que acende quando foi batido.</summary>
		private static VBoxContainer Time(double seconds, BattleOutcome outcome)
		{
			var column = new VBoxContainer { Name = "Time" };
			column.AddThemeConstantOverride("separation", 2);
			column.AddChild(TimeRow("Now", T("battle.time"), seconds, Palette.Text, "resolve"));
			if (outcome.Best is { } best)
			{
				var row = TimeRow("Best", T("battle.best"), best, Palette.Gold, null);
				if (outcome.NewBest)
				{
					var tag = new Label { Name = "NewBest", Text = T("battle.new_best") };
					tag.AddThemeColorOverride("font_color", Palette.Spirit);
					tag.AddThemeColorOverride("font_outline_color", Palette.Background);
					tag.AddThemeConstantOverride("outline_size", 4);
					row.AddChild(tag);
					row.MoveChild(tag, 0);
				}

				column.AddChild(row);
			}

			column.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
			column.GrowHorizontal = GrowDirection.Begin;
			column.OffsetLeft = column.OffsetRight = -Layout.ScreenMargin;
			column.OffsetTop = column.OffsetBottom = Layout.ScreenMargin;
			return column;
		}

		private static HBoxContainer TimeRow(string name, string label, double seconds, Color color, string? icon)
		{
			var row = Layout.Row(10).Named(name);
			row.Alignment = BoxContainer.AlignmentMode.End;
			if (icon != null)
				row.AddChild(Doodle.Icon(Art.Icon(icon), 26, Palette.Gold).Named("Icon"));
			var caption = new Label { Name = "Label", Text = label };
			caption.AddThemeFontOverride("font", GameTheme.Serif);
			caption.AddThemeFontSizeOverride("font_size", 24);
			caption.AddThemeColorOverride("font_color", Palette.Gold);
			row.AddChild(caption);
			var value = new Label { Name = "Value", Text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss"), ThemeTypeVariation = GameTheme.Number };
			value.AddThemeFontSizeOverride("font_size", 24);
			value.AddThemeColorOverride("font_color", color);
			row.AddChild(value);
			return row;
		}

		/// <summary>A faixa do meio: o que a vitória rendeu, ou o motivo da derrota.</summary>
		private PanelContainer Rewards(BattleOutcome outcome, (string Icon, string Text)? defeat)
		{
			var band = new PanelContainer { Name = "Rewards", MouseFilter = MouseFilterEnum.Ignore };
			var box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.45f), BorderColor = Palette.GoldDark };
			box.BorderWidthTop = box.BorderWidthBottom = 1;
			box.ContentMarginTop = box.ContentMarginBottom = 14;
			band.AddThemeStyleboxOverride("panel", box);
			band.AnchorLeft = 0;
			band.AnchorRight = 1;
			band.AnchorTop = band.AnchorBottom = 0.36f;
			band.OffsetTop = -36;
			band.OffsetBottom = 36;

			var chips = Layout.Row(14, true).Named("Chips");
			band.AddChild(chips);
			if (outcome.Reward is { } reward)
			{
				if (reward.Scrolls > 0)
					chips.AddChild(Layout.Chip("scroll", $"+{reward.Scrolls}", T("currency.scrolls_name")));
				if (reward.Gold > 0)
					chips.AddChild(Layout.Chip("gold", $"+{reward.Gold}", T("currency.gold")));
				var essence = Layout.Chip("essence", $"+{reward.Essence}", T("currency.essence"));
				_essence = essence.GetNode<Label>("Row/Value");
				_essenceShown = reward.Essence;
				chips.AddChild(essence);
				chips.AddChild(Layout.Chip("level_max", $"+{reward.Experience}", T("reward.experience")).Named("Experience"));
				if (reward.AccountLevels > 0)
					chips.AddChild(Layout.Chip("avatar", outcome.AccountLevel.ToString(), T("battle.account", outcome.AccountLevel, reward.AccountLevels * Account.LevelUpGold), Palette.Arcane).Named("AccountLevel"));
				for (var i = 0; i < reward.Tools.Count; i++)
				{
					var tool = reward.Tools[i];
					chips.AddChild(Layout.Chip(tool.Kind == RuneToolKind.Grindstone ? "grindstone" : "gem", "", $"{Texts.Name(tool)} ({Texts.Range(tool)})", Palette.Of(tool.Grade)).Named($"Tool{i + 1}"));
				}
			}
			else if (defeat is { } reason)
			{
				chips.AddChild(Layout.Chip(reason.Icon, "", reason.Text, Palette.Negative).Named("Reason"));
				chips.AddChild(new Label { Name = "Text", Text = reason.Text, VerticalAlignment = VerticalAlignment.Center });
			}

			return band;
		}

		/// <summary>A equipe embaixo, cada um com a barra de experiência subindo.</summary>
		private HBoxContainer Team(IReadOnlyList<ResultMonster> team)
		{
			var row = Layout.Row(22, true).Named("Team");
			row.AnchorLeft = 0;
			row.AnchorRight = 1;
			row.AnchorTop = row.AnchorBottom = 0.66f;
			row.OffsetTop = -44;
			row.OffsetBottom = 44;
			for (var i = 0; i < team.Count; i++)
				row.AddChild(Monster(team[i]).Named($"Monster{i + 1}"));
			return row;
		}

		/// <summary>Um monstro: o retrato e, ao lado, o nível e a barra. Não é container: o "subiu de nível" flutua por cima.</summary>
		private Control Monster(ResultMonster monster)
		{
			var view = new Control { CustomMinimumSize = new Vector2(214, 72), TooltipText = monster.Name, MouseFilter = MouseFilterEnum.Stop };
			var row = Layout.Row(8).Named("Row");
			row.MouseFilter = MouseFilterEnum.Ignore;
            view.AddChild(row);

			var frame = new PanelContainer { Name = "Portrait", SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
			var box = GameTheme.Box(Palette.Inset, monster.Ink, 2, 32, 2);
			frame.AddThemeStyleboxOverride("panel", box);
			frame.AddChild(Layout.Medal(monster.Art, monster.Ink, 58));
			row.AddChild(frame);

			var column = new VBoxContainer { Name = "Experience", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 4);
			var level = new Label { Name = "Level", ThemeTypeVariation = GameTheme.Number, MouseFilter = MouseFilterEnum.Ignore };
			level.AddThemeFontSizeOverride("font_size", 17);
			level.AddThemeColorOverride("font_outline_color", Palette.Background);
			level.AddThemeConstantOverride("outline_size", 4);
			column.AddChild(level);
			var bar = Layout.Energy(Palette.Arcane, 12).Named("Bar");
			bar.CustomMinimumSize = new Vector2(120, 12);
			bar.MaxValue = 1;
			bar.Step = 0;
			bar.MouseFilter = MouseFilterEnum.Ignore;
			column.AddChild(bar);
			row.AddChild(column);

			Prepare(view, level, bar, monster);
			return view;
		}

		/// <summary>
		/// A barra parada no ponto de antes da luta e a subida guardada para depois: trecho a trecho, e a cada
		/// nível atravessado o número sobe e "subiu de nível" flutua.
		/// </summary>
		private void Prepare(Control view, Label level, ProgressBar bar, ResultMonster monster)
		{
			var steps = monster.Steps;
			var first = steps[0];
			ShowLevel(level, first.Level, monster.MaxLevel && steps.Count == 1);
			bar.Value = first.From;
			_animations.Add(() => Rise(view, level, bar, monster));
		}

		private static void Rise(Control view, Label level, ProgressBar bar, ResultMonster monster)
		{
			var steps = monster.Steps;
			var seconds = Math.Min(StepSeconds, RiseSeconds / steps.Count);
			var tween = view.CreateTween();
			tween.TweenInterval(0.35);
			for (var i = 0; i < steps.Count; i++)
			{
				var step = steps[i];
				var last = i == steps.Count - 1;
				if (i > 0)
				{
					tween.TweenCallback(Callable.From(() =>
					{
						bar.Value = step.From;
						ShowLevel(level, step.Level, monster.MaxLevel && last);
						FloatingText.Spawn(view, T("battle.level_up"), Palette.Spirit, 15);
					}));
				}

				if (step.To > step.From)
					tween.TweenProperty(bar, "value", step.To, seconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			}
		}

		private static void ShowLevel(Label label, int level, bool max)
		{
			label.Text = max ? T("battle.max_level") : T("battle.level", level);
			label.AddThemeColorOverride("font_color", max ? Palette.Gold : Palette.Text);
		}

		private void Sell(Rune rune, int value)
		{
			_sell(rune);
			_essenceShown += value;
			if (_essence != null)
				_essence.Text = $"+{_essenceShown}";
		}
	}
}
