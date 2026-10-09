using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Audio;
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

		/// <summary>Quem é, para o resumo do toque longo no retrato.</summary>
		public SummonDefinition? Summon { get; init; }

		public OwnedSummon? Monster { get; init; }

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

	/// <summary>
	/// O que o resultado recebe do GameRoot além do que a própria luta sabe. Na derrota, <paramref name="Tips"/>
	/// diz o que fazer para ficar mais forte; na primeira vitória de uma fase, <paramref name="Opened"/> diz
	/// o que ela abriu no Santuário.
	/// </summary>
	public sealed record BattleOutcome(bool Victory, VictoryReward? Reward, IReadOnlyList<ResultMonster> Team, int AccountLevel, double? Best, bool NewBest, IReadOnlyList<DefeatTip>? Tips = null, IReadOnlyList<Feature>? Opened = null);

	/// <summary>
	/// O fim da luta, por cima do campo: "Vitória" ou "Derrota" grande no alto; no canto, o tempo da luta
	/// e o melhor tempo dela (aceso quando foi batido); no meio, a faixa com o que a luta rendeu, cada
	/// item escrito; embaixo, a equipe, cada monstro com o nome e a barra de experiência subindo nível a
	/// nível (ou "nível máximo"), e os botões Lutar de novo e Continuar. A runa que caiu abre por cima,
	/// na <see cref="RuneCard"/>, com Vender, Bloquear (guarda e tranca) e Guardar.
	///
	/// Os sons vêm depois da vitória que a luta já tocou, quando o jogador pode ver: a runa ao abrir (mais
	/// rica quanto mais rara); com as barras, o ouro, o destaque do que veio e, por último, a conta que
	/// subiu de nível ou o melhor tempo batido; cada nível que um monstro sobe, o seu.
	/// </summary>
	public partial class BattleResultPanel : ColorRect
	{
		/// <summary>Quanto um trecho da barra leva para encher; muitos níveis de uma vez dividem <see cref="RiseSeconds"/>.</summary>
		private const double StepSeconds = 0.55;

		private const double RiseSeconds = 2.5;

		private readonly BattleOutcome _outcome;
		private readonly Action<Rune> _sell;
		private readonly Action<Rune> _lock;

		/// <summary>As barras de experiência, prontas para subir quando o jogador puder vê-las.</summary>
		private readonly List<Action> _animations = new();
		private Label? _essence;
		private int _essenceShown;

		/// <param name="seconds">O tempo da luta na tela.</param>
		/// <param name="defeat">Na derrota, o motivo e o símbolo dele (o tempo esgotado ou todos caídos).</param>
		/// <param name="onClose">Sair do resultado, de volta para de onde a luta veio.</param>
		/// <param name="next">
		/// A luta seguinte, quando dá para entrar nela agora (a próxima fase da Campanha, com a Mana dela): o
		/// Continuar já entra nela, mostrando a Mana que custa, e o Sair fica ao lado. Sem ela, o Continuar
		/// sai.
		/// </param>
		public BattleResultPanel(BattleOutcome outcome, double seconds, (string Icon, string Text)? defeat, Action onClose, Action onRestart, Action<Rune> sell, Action<Rune> lockRune, (int Mana, Action Start)? next = null)
		{
			_outcome = outcome;
			_sell = sell;
			_lock = lockRune;
			Name = "Result";
			Color = new Color(0, 0, 0, 0.62f);
			MouseFilter = MouseFilterEnum.Stop;
			ZIndex = 60;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			AddChild(Title(outcome.Victory));
			AddChild(Time(seconds, outcome));
			AddChild(Rewards(outcome, defeat));
			AddChild(Team(outcome.Team));

			var actions = Layout.Row(20, true).Named("Actions");
			if (next is { } following)
			{
				actions.AddChild(GameButton.Of(T("battle.leave"), onClose, ButtonKind.Secondary, "back", 64).Named("Leave").Wide(200));
				actions.AddChild(GameButton.Of(T("battle.again"), onRestart, ButtonKind.Secondary, "repeat", 64).Named("Again").Wide(240));
				actions.AddChild(GameButton.Of(T("common.continue"), following.Start, ButtonKind.Primary, "confirm", 64).WithCost("mana", following.Mana > 0 ? following.Mana.ToString() : "").Named("Continue").Wide(240));
			}
			else
			{
				actions.AddChild(GameButton.Of(T("battle.again"), onRestart, ButtonKind.Secondary, "repeat", 64).Named("Again").Wide(240));
				actions.AddChild(GameButton.Of(T("common.continue"), onClose, ButtonKind.Primary, "confirm", 64).Named("Continue").Wide(240));
			}

			actions.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
			actions.GrowHorizontal = GrowDirection.Both;
			actions.GrowVertical = GrowDirection.Begin;
			actions.OffsetTop = actions.OffsetBottom = -Layout.ScreenMargin;
			AddChild(actions);
		}

		public override void _Ready()
		{
			if (_outcome.Reward?.Rune is not { } rune)
			{
				Animate();
				return;
			}

			// A runa que caiu, por cima de tudo: o jogador decide antes de ver o resto.
			Sfx.Play(rune.Rarity switch
			{
				RuneRarity.Legendary => "rewards.item_legendary",
				RuneRarity.Hero => "rewards.item_epic",
				RuneRarity.Rare => "rewards.item_rare",
				_ => "rewards.rune",
			});
			var value = RuneRules.SellValue(rune);
			var dialog = RuneDialog.Show(this, rune, anchored: false);
			dialog.Dismissable = false;
			dialog.AddAction(T("runes.sell_button", value), () => Sell(rune, value), ButtonKind.Danger, true, "dismantle").Named("Sell");
			// Bloquear também guarda: a runa fica, e com o cadeado não sai numa venda em lote.
			dialog.AddAction(T("lock.lock"), () => _lock(rune), ButtonKind.Secondary, true, "lock").Named("Lock");
			dialog.AddAction(T("battle.keep_rune"), null, ButtonKind.Primary, true, "confirm").Named("Keep");
			// As barras só sobem depois que a runa sai da frente, para o jogador ver.
			dialog.Closed += Animate;
		}

		private void Animate()
		{
			foreach (var animation in _animations)
				animation();
			_animations.Clear();
			Fanfare();
		}

		/// <summary>
		/// O que a vitória rendeu, em no máximo dois sons: o destaque (a criatura invocada, ou o prêmio raro
		/// e a página nova do grimório) e a subida de nível da conta. O ouro e o resto ficam calados: a
		/// vitória já soou na luta.
		/// </summary>
		private void Fanfare()
		{
			if (_outcome.Reward is not { } reward)
				return;

			var highlight = reward.SummonResult != null ? "summon.creature_summoned"
				: reward.Prize is { IsEmpty: false } || _outcome.Opened is { Count: > 0 } ? "rewards.item_rare"
				: null;
			if (highlight != null)
				Sfx.Play(highlight, 0.6);
			if (reward.AccountLevels > 0)
				Sfx.Play("rewards.level_up", 1.4);
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
			// Cresce para os dois lados da linha: a derrota tem mais linhas que a vitória.
			band.GrowVertical = GrowDirection.Both;

			if (outcome.Reward == null && defeat is { } reason)
			{
				band.AddChild(DefeatBand(reason, outcome.Tips ?? Array.Empty<DefeatTip>()));
				return band;
			}

			var lines = new VBoxContainer { Name = "Lines", MouseFilter = MouseFilterEnum.Ignore };
			lines.AddThemeConstantOverride("separation", 10);
			band.AddChild(lines);
			var chips = Layout.Row(14, true).Named("Chips");
			lines.AddChild(chips);
			if (outcome.Opened is { Count: > 0 } opened)
				lines.AddChild(Opened(opened));
			if (outcome.Reward is { } reward)
			{
				if (reward.Scrolls > 0)
					chips.AddChild(Layout.Labeled("scroll", $"+{Texts.Number(reward.Scrolls)}", T("currency.scrolls_name")));
				if (reward.Gold > 0)
					chips.AddChild(Layout.Labeled("gold", $"+{Texts.Number(reward.Gold)}", T("currency.gold")));
				var essence = Layout.Labeled("essence", $"+{Texts.Number(reward.Essence)}", T("currency.essence"));
				_essence = essence.GetNode<Label>("Row/Value");
				_essenceShown = reward.Essence;
				chips.AddChild(essence);
				chips.AddChild(Layout.Labeled("level_max", $"+{reward.Experience}", T("reward.experience")).Named("Experience"));
				if (reward.AccountLevels > 0)
					chips.AddChild(Layout.Labeled("avatar", outcome.AccountLevel.ToString(), T("battle.account", reward.AccountLevels * Account.LevelUpGold), Palette.Arcane).Named("AccountLevel"));
				AddPrize(chips, reward.Prize);
				if (reward.SummonResult is { } summon)
				{
					var caption = summon.Monster.Stored ? T("summon.sent_to_vault") : summon.FirstCopy ? T("battle.monster_drop") : T("battle.monster_copy");
					var chip = Layout.Labeled(Art.Creature(summon.Summon.Image), summon.Summon.Name, caption, Palette.Of(summon.Summon.Element)).Named("Monster");
					chip.MouseFilter = MouseFilterEnum.Stop;
					chip.MouseDefaultCursorShape = CursorShape.PointingHand;
					void Open() => MonsterSummary.Open(chip, summon.Summon, summon.Monster);
					Press.On(chip, Open, Open);
					chips.AddChild(chip);
				}

				for (var i = 0; i < reward.Tools.Count; i++)
				{
					var tool = reward.Tools[i];
					chips.AddChild(Layout.Labeled(tool.Kind == RuneToolKind.Grindstone ? "grindstone" : "gem", "", Texts.Name(tool), Palette.Of(tool.Grade)).Named($"Tool{i + 1}"));
				}
			}

			return band;
		}

		/// <summary>O que a primeira vitória abriu: "Abriu no Santuário", e cada parte com o símbolo e o nome.</summary>
		private static Control Opened(IReadOnlyList<Feature> opened)
		{
			var row = Layout.Row(14, true).Named("Opened");
			var caption = new Label { Name = "Caption", Text = T("battle.opened"), VerticalAlignment = VerticalAlignment.Center };
			caption.AddThemeColorOverride("font_color", Palette.Spirit);
			row.AddChild(caption);
			foreach (var feature in opened)
			{
				var (icon, name) = Destinations.Of(feature);
				row.AddChild(Layout.Labeled(icon, name, T("battle.opened_new"), Palette.Spirit).Named(feature.ToString()));
			}

			return row;
		}

		/// <summary>
		/// A derrota: o motivo e, embaixo, o que fazer para ficar mais forte (as runas primeiro), uma linha
		/// por conselho, cada uma dizendo onde se faz.
		/// </summary>
		private static VBoxContainer DefeatBand((string Icon, string Text) reason, IReadOnlyList<DefeatTip> tips)
		{
			const float width = 760;
			var column = new VBoxContainer { Name = "Defeat", Alignment = BoxContainer.AlignmentMode.Center };
			column.AddThemeConstantOverride("separation", 8);
			var top = Layout.Row(14, true).Named("Reason");
			top.AddChild(Layout.Labeled(reason.Icon, "", reason.Text, Palette.Negative).Named("Chip"));
			column.AddChild(top);

			if (tips.Count == 0)
			{
				column.AddChild(Centered(T("battle.defeat_tip"), width).Named("Tip"));
				return column;
			}

			var title = Centered(T("battle.advice_title"), width).Named("Title");
			title.AddThemeColorOverride("font_color", Palette.Gold);
			column.AddChild(title);
			for (var i = 0; i < tips.Count; i++)
			{
				var tip = tips[i];
				var row = Layout.Row(10, true).Named($"Tip{i + 1}");
				row.AddChild(Doodle.Icon(Art.Icon(TipIcon(tip.Kind)), 28, Palette.Gold).Named("Icon"));
				var text = Layout.Text(TipText(tip), GameTheme.Faded, width - 40).Named("Text");
				row.AddChild(text);
				column.AddChild(row);
			}

			return column;
		}

		private static Label Centered(string text, float width)
		{
			var label = Layout.Text(text, GameTheme.Faded, width);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			return label;
		}

		private static string TipIcon(DefeatAdvice.Kind kind) => kind switch
		{
			DefeatAdvice.Kind.EquipRunes or DefeatAdvice.Kind.UpgradeRunes => "rune",
			DefeatAdvice.Kind.LevelUp => "level_max",
			DefeatAdvice.Kind.Evolve => "evolve",
			DefeatAdvice.Kind.Awaken => "awaken",
			_ => "fight",
		};

		private static string TipText(DefeatTip tip) => tip.Kind == DefeatAdvice.Kind.Element
			? T("battle.advice.Element", Texts.Name(tip.Element!.Value), Texts.Name(tip.Foe!.Value), tip.Count)
			: tip.Kind == DefeatAdvice.Kind.UpgradeRunes
				? T("battle.advice.UpgradeRunes", tip.Count, DefeatAdvice.RuneTarget)
				: T($"battle.advice.{tip.Kind}", tip.Count);

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
			var view = new Control { CustomMinimumSize = new Vector2(220, 72), MouseFilter = MouseFilterEnum.Ignore };
			var row = Layout.Row(8).Named("Row");
			row.MouseFilter = MouseFilterEnum.Ignore;
            view.AddChild(row);

			var frame = new PanelContainer { Name = "Portrait", SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
			var box = GameTheme.Box(Palette.Inset, monster.Ink, 2, 32, 2);
			frame.AddThemeStyleboxOverride("panel", box);
			frame.AddChild(Layout.Medal(monster.Art, monster.Ink, 58));
			if (monster.Summon is { } summon)
			{
				// Tocar ou segurar o retrato abre o resumo, como em qualquer lugar em que um monstro aparece.
				frame.MouseFilter = MouseFilterEnum.Stop;
				frame.MouseDefaultCursorShape = CursorShape.PointingHand;
				void Open() => MonsterSummary.Open(frame, summon, monster.Monster);
				Press.On(frame, Open, Open);
			}

			row.AddChild(frame);

			var column = new VBoxContainer { Name = "Experience", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 4);
			var name = new Label { Name = "Name", Text = monster.Name, MouseFilter = MouseFilterEnum.Ignore, ClipText = true, CustomMinimumSize = new Vector2(130, 0) };
			name.AddThemeFontSizeOverride("font_size", 15);
			name.AddThemeColorOverride("font_outline_color", Palette.Background);
			name.AddThemeConstantOverride("outline_size", 4);
			column.AddChild(name);
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
						Sfx.Play("rewards.experience");
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

		/// <summary>O prêmio de marco (Pergaminhos especiais e Núcleos de Infusão), um selo por item que veio.</summary>
		public static void AddPrize(Container row, Prize? prize)
		{
			if (prize is not { IsEmpty: false })
				return;
			if (prize.LegendaryScrolls > 0)
				row.AddChild(Layout.Labeled("scroll", $"+{prize.LegendaryScrolls}", T("summon.scroll.Legendary"), Palette.Gold).Named("Legendary"));
			if (prize.LightDarkScrolls > 0)
				row.AddChild(Layout.Labeled("scroll", $"+{prize.LightDarkScrolls}", T("summon.scroll.LightDark"), Palette.Arcane).Named("LightDark"));
			if (prize.InfusionCores > 0)
				row.AddChild(Layout.Labeled("monster", $"+{prize.InfusionCores}", T("reward.infusion_cores"), Palette.Arcane).Named("InfusionCores"));
		}
	}
}
