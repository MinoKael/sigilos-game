using System;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>O que a janela da Batalha automática pede ao GameRoot.</summary>
	public sealed class AutoBattleActions
	{
		public Action Stop { get; init; } = () => { };
		public Action Resume { get; init; } = () => { };

		/// <summary>Encerra de vez uma Batalha automática acabada ou parada: o aviso some.</summary>
		public Action Dismiss { get; init; } = () => { };

		public Action<int> SetRuns { get; init; } = _ => { };
		public Action<Rune> SellRune { get; init; } = _ => { };

		/// <summary>A runa e o nível a alcançar.</summary>
		public Action<Rune, int> UpgradeRune { get; init; } = (_, _) => { };

		/// <summary>Abre a tela de Runas sem parar nada.</summary>
		public Action ManageRunes { get; init; } = () => { };
	}

	/// <summary>
	/// A janela da Batalha automática (a do Summoners War, em outra roupa): à esquerda, a luta de agora
	/// ("4/30"), a barra dela, o tempo que falta, vitórias e derrotas, e quantas lutas fazer (dá para mudar
	/// no meio); à direita, tudo o que rendeu até aqui — moedas, experiência, monstros e as runas, que dá
	/// para vender ou melhorar ali mesmo sem parar nada.
	///
	/// Fechar a janela (✕, tocar fora, Voltar) não para a Batalha automática: ela segue, e o aviso no alto
	/// da tela continua contando. Só o botão Parar para.
	/// </summary>
	public static partial class AutoBattleDialog
	{
		private const float Width = 1060;

		public static Dialog Open(Control from, AutoBattleRun run, PlayerState player, AutoBattleActions actions)
		{
			var dialog = Dialog.Open(from, T("auto.title"), Width, null, "AutoBattleDialog");
			var clock = new AutoClock(run) { Name = "Clock" };
			void Rebuild() => Build(dialog, run, player, actions, clock);
			run.Changed += Rebuild;
			dialog.Closed += () =>
			{
				run.Changed -= Rebuild;
				if (!clock.IsInsideTree())
					clock.QueueFree();
			};
			Rebuild();
			return dialog;
		}

		private static void Build(Dialog dialog, AutoBattleRun run, PlayerState player, AutoBattleActions actions, AutoClock clock)
		{
			if (clock.GetParent() is { } parent)
				parent.RemoveChild(clock);
			Layout.Clear(dialog.Body);
			dialog.ClearActions();

			var columns = Layout.Row(24).Named("Columns");
			dialog.Body.AddChild(columns);
			columns.AddChild(Status(run, actions, clock));
			columns.AddChild(Rewards(dialog, run, player, actions));

			if (run.Running)
			{
				dialog.AddAction(T("auto.manage_runes"), actions.ManageRunes, ButtonKind.Secondary, true, "rune").Named("ManageRunes");
				dialog.AddAction(T("auto.stop"), () => Dialog.Confirm(dialog, T("auto.stop_title"), T("auto.stop_confirm"), T("auto.stop"), actions.Stop, ButtonKind.Danger), ButtonKind.Danger, false, "cancel").Named("Stop");
			}
			else
			{
				dialog.AddAction(T("auto.dismiss"), actions.Dismiss, ButtonKind.Secondary).Named("Dismiss");
				if (run.CanResume)
					dialog.AddAction(T("auto.resume"), actions.Resume, ButtonKind.Primary, false, "repeat").Named("Resume");
			}
		}

		/// <summary>A coluna da esquerda: a luta de agora, o relógio, o placar e quantas lutas fazer.</summary>
		private static Control Status(AutoBattleRun run, AutoBattleActions actions, AutoClock clock)
		{
			var column = new VBoxContainer { Name = "Status", CustomMinimumSize = new Vector2(400, 0) };
			column.AddThemeConstantOverride("separation", 12);
			column.AddChild(new Label { Name = "Fight", Text = run.Title, ThemeTypeVariation = GameTheme.Heading, AutowrapMode = TextServer.AutowrapMode.WordSmart });

			var count = new Label { Name = "Count", Text = $"{Math.Min(Math.Max(run.Number, run.Done), run.Runs)}/{run.Runs}", ThemeTypeVariation = GameTheme.Number, HorizontalAlignment = HorizontalAlignment.Center };
			count.AddThemeFontSizeOverride("font_size", 54);
			count.AddThemeColorOverride("font_color", run.Running ? Palette.Arcane : Palette.Gold);
			column.AddChild(count);

			var state = run.Running ? T("auto.state_running", run.Number)
				: run.StopReason ?? T("auto.done", run.Done);
			var stateLabel = Layout.Text(state, width: 400).Named("State");
			stateLabel.HorizontalAlignment = HorizontalAlignment.Center;
			stateLabel.AddThemeColorOverride("font_color", run.Running ? Palette.Text : run.CanResume ? Palette.Negative : Palette.Spirit);
			column.AddChild(stateLabel);

			if (run.Running)
				column.AddChild(clock);

			var score = Layout.Row(10, true).Named("Score");
			score.AddChild(Layout.Labeled("confirm", run.Victories.ToString(), T("auto.victories"), Palette.Spirit).Named("Victories"));
			score.AddChild(Layout.Labeled("cancel", run.Defeats.ToString(), T("auto.defeats"), Palette.Negative).Named("Defeats"));
			column.AddChild(score);

			if (run.Running || run.CanResume)
				column.AddChild(RunsStepper(run, actions));
			return column;
		}

		/// <summary>Quantas lutas fazer: − e + e o número, e os atalhos 10, 20, 30. Não desce abaixo da luta de agora.</summary>
		private static Control RunsStepper(AutoBattleRun run, AutoBattleActions actions)
		{
			var column = new VBoxContainer { Name = "Runs" };
			column.AddThemeConstantOverride("separation", 8);
			column.AddChild(new Label { Name = "Title", Text = T("auto.runs"), HorizontalAlignment = HorizontalAlignment.Center });

			var minimum = Math.Max(1, run.Number);
			void Set(int value) => actions.SetRuns(Math.Clamp(value, minimum, AutoBattle.MaxRuns));

			var row = Layout.Row(12, true).Named("Stepper");
			var fewer = new SigilButton(null, 52) { Name = "Fewer", Letters = "−", Disabled = run.Runs <= minimum };
			fewer.SetLetterSize(32);
			fewer.Pressed += () => Set(run.Runs - 1);
			row.AddChild(fewer);
			var value = new Label { Name = "Value", Text = run.Runs.ToString(), ThemeTypeVariation = GameTheme.Number, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(70, 0), VerticalAlignment = VerticalAlignment.Center };
			value.AddThemeFontSizeOverride("font_size", 30);
			row.AddChild(value);
			var more = new SigilButton(null, 52) { Name = "More", Letters = "+", Disabled = run.Runs >= AutoBattle.MaxRuns };
			more.SetLetterSize(32);
			more.Pressed += () => Set(run.Runs + 1);
			row.AddChild(more);
			column.AddChild(row);

			var presets = Layout.Row(8, true).Named("Presets");
			foreach (var preset in new[] { 10, 20, AutoBattle.MaxRuns }.Distinct().Where(p => p <= AutoBattle.MaxRuns))
			{
				var button = GameButton.Of(preset.ToString(), () => Set(preset), ButtonKind.Secondary, null, 44).Named($"Preset{preset}");
				button.Disabled = preset < minimum || preset == run.Runs;
				presets.AddChild(button.Wide(64));
			}

			column.AddChild(presets);
			var mana = Layout.Text(T("auto.mana_needed", run.Mana, run.Mana * Math.Max(0, run.Runs - run.Done)), GameTheme.Faded, 400).Named("Mana");
			mana.HorizontalAlignment = HorizontalAlignment.Center;
			column.AddChild(mana);
			return column;
		}

		/// <summary>A coluna da direita: o que rendeu, e as runas (tocar abre a ficha, com Vender e Melhorar).</summary>
		private static Control Rewards(Dialog dialog, AutoBattleRun run, PlayerState player, AutoBattleActions actions)
		{
			var column = new VBoxContainer { Name = "Rewards", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			column.AddThemeConstantOverride("separation", 10);
			column.AddChild(new Label { Name = "Title", Text = T("auto.rewards"), ThemeTypeVariation = GameTheme.Heading });

			var totals = Layout.Flow(8).Named("Totals");
			totals.AddChild(Layout.Labeled("mana", $"−{run.ManaSpent}", T("currency.mana")).Named("Mana"));
			totals.AddChild(Layout.Labeled("essence", $"+{run.Essence}", T("currency.essence")).Named("Essence"));
			if (run.Gold > 0)
				totals.AddChild(Layout.Labeled("gold", $"+{run.Gold}", T("currency.gold")).Named("Gold"));
			if (run.Scrolls > 0)
				totals.AddChild(Layout.Labeled("scroll", $"+{run.Scrolls}", T("currency.scrolls_name")).Named("Scrolls"));
			totals.AddChild(Layout.Labeled("level_max", $"+{run.Experience}", T("reward.experience")).Named("Experience"));
			if (run.LevelUps > 0)
				totals.AddChild(Layout.Labeled("stats", run.LevelUps.ToString(), T("auto.level_ups")).Named("LevelUps"));
			if (run.AccountLevels > 0)
				totals.AddChild(Layout.Labeled("avatar", $"+{run.AccountLevels}", T("auto.account"), Palette.Arcane).Named("AccountLevels"));
			foreach (var group in run.Tools.GroupBy(t => t))
				totals.AddChild(Layout.Labeled(group.Key.Kind == RuneToolKind.Grindstone ? "grindstone" : "gem", $"×{group.Count()}", Texts.Name(group.Key), Palette.Of(group.Key.Grade)).Named($"{group.Key.Kind}{group.Key.Stat}{group.Key.Grade}"));
			column.AddChild(totals);

			if (run.Monsters.Count > 0)
			{
				column.AddChild(new Label { Name = "MonstersTitle", Text = T("auto.monsters", run.Monsters.Count) });
				var monsters = Layout.Flow(8).Named("Monsters");
				for (var i = 0; i < run.Monsters.Count; i++)
				{
					var result = run.Monsters[i];
					var tag = result.Monster.Stored ? T("summon.sent_to_vault") : result.FirstCopy ? T("summon.new") : null;
					monsters.AddChild(new CreatureCard(result.Summon, result.Monster, 76, tag: tag) { Name = $"Monster{i + 1}" });
				}

				column.AddChild(monsters);
			}

			var runes = run.Runes.Where(r => !run.Sold.Contains(r.Id)).ToList();
			column.AddChild(new Label { Name = "RunesTitle", Text = T("auto.runes_title", runes.Count, run.Sold.Count) });
			if (run.Runes.Count == 0)
			{
				column.AddChild(Layout.Text(T("auto.no_runes"), GameTheme.Faded).Named("NoRunes"));
				return column;
			}

			column.AddChild(Layout.Text(T("auto.runes_hint"), GameTheme.Faded).Named("RunesHint"));
			var grid = Layout.Flow(8).Named("Runes");
			foreach (var rune in run.Runes)
			{
				var sold = run.Sold.Contains(rune.Id);
				var tile = new RuneTile(rune, rune.Slot, 1.1f) { Name = $"Rune{rune.Id}" };
				if (sold)
					tile.SetStamp(T("auto.sold"));
				else
					tile.Pressed += t => RuneActions(dialog, t, player, actions);
				grid.AddChild(tile);
			}

			column.AddChild(grid);
			return column;
		}

		/// <summary>A ficha da runa tocada, com Vender e Melhorar; ela continua lá se o jogador só fechar.</summary>
		private static void RuneActions(Dialog owner, RuneTile tile, PlayerState player, AutoBattleActions actions)
		{
			var rune = tile.Rune!;
			var sheet = RuneDialog.Show(tile, rune);
			if (!player.Runes.Contains(rune))
				return;

			var value = RuneRules.SellValue(rune);
			sheet.AddAction(T("runes.sell_button", value), () => actions.SellRune(rune), ButtonKind.Danger, true, "dismantle").Named("Sell");
			if (rune.Level < RuneRules.MaxLevel)
			{
				var target = RuneRules.NextMilestone(rune.Level);
				var cost = RuneRules.UpgradeCost(rune, target);
				var upgrade = sheet.AddAction(T("runes.upgrade_button", target), () => RuneDialog.ConfirmUpgrade(owner, rune, target, player.Essence, () => actions.UpgradeRune(rune, target)),
					ButtonKind.Primary, true, "essence").Named("Upgrade");
				upgrade.WithCost("essence", Texts.Short(cost));
				upgrade.Disabled = player.Essence < cost;
			}
		}

		/// <summary>O relógio da luta de agora: a barra e o tempo que falta, lidos a cada quadro.</summary>
		private sealed partial class AutoClock : VBoxContainer
		{
			private readonly AutoBattleRun _run;
			private readonly ProgressBar _bar = Layout.Energy(Palette.Arcane, 12).Named("Bar");
			private readonly Label _left = new() { Name = "TimeLeft", HorizontalAlignment = HorizontalAlignment.Center };

			public AutoClock(AutoBattleRun run)
			{
				_run = run;
				AddThemeConstantOverride("separation", 6);
				_bar.MaxValue = 1;
				_bar.Step = 0;
				AddChild(_bar);
				AddChild(_left);
			}

			public override void _Process(double delta)
			{
				_bar.Value = _run.FightProgress;
				_left.Text = T("auto.time_left_value", _run.TimeLeft.ToString(@"m\:ss"));
			}
		}
	}
}
