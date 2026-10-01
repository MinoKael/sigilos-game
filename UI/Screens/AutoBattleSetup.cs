using System;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Antes de começar a Batalha automática: quantas lutas seguidas (− e +, ou os atalhos), a Mana que
	/// isso custa se todas forem vitórias, e o aviso de que ela segue sozinha depois — dá para sair da
	/// tela, cuidar das runas e voltar. Cancelar ou Iniciar.
	/// </summary>
	public static class AutoBattleSetup
	{
		public static Dialog Open(Control from, string title, int mana, int available, Action<int> start)
		{
			var dialog = Dialog.Open(from, T("auto.setup_title"), 560, null, "AutoBattleSetup");
			var runs = AutoBattle.RepeatRuns;
			var body = dialog.Body;
			body.AddChild(new Label { Name = "Fight", Text = title, ThemeTypeVariation = GameTheme.Heading, HorizontalAlignment = HorizontalAlignment.Center });
			body.AddChild(new Label { Name = "Question", Text = T("auto.runs"), HorizontalAlignment = HorizontalAlignment.Center });

			var value = new Label { Name = "Value", ThemeTypeVariation = GameTheme.Number, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(90, 0), VerticalAlignment = VerticalAlignment.Center };
			value.AddThemeFontSizeOverride("font_size", 40);
			var cost = Layout.Text("", null, 500).Named("Cost");
			cost.HorizontalAlignment = HorizontalAlignment.Center;
			var fewer = new SigilButton(null, 60) { Name = "Fewer", Letters = "−" };
			var more = new SigilButton(null, 60) { Name = "More", Letters = "+" };
			fewer.SetLetterSize(36);
			more.SetLetterSize(36);

			void Show()
			{
				value.Text = runs.ToString();
				cost.Text = T("auto.setup_cost", mana, runs * mana, available);
				cost.AddThemeColorOverride("font_color", available < mana ? Palette.Negative : Palette.TextFaded);
				fewer.Disabled = runs <= 1;
				more.Disabled = runs >= AutoBattle.MaxRuns;
			}

			void Set(int count)
			{
				runs = Math.Clamp(count, 1, AutoBattle.MaxRuns);
				Show();
			}

			fewer.Pressed += () => Set(runs - 1);
			more.Pressed += () => Set(runs + 1);
			var row = Layout.Row(16, true).Named("Stepper");
			row.AddChild(fewer);
			row.AddChild(value);
			row.AddChild(more);
			body.AddChild(row);

			var presets = Layout.Row(8, true).Named("Presets");
			foreach (var preset in new[] { 1, 5, 10, 20, AutoBattle.MaxRuns })
			{
				if (preset > AutoBattle.MaxRuns)
					continue;
				presets.AddChild(GameButton.Of(preset.ToString(), () => Set(preset), ButtonKind.Secondary, null, 44).Named($"Preset{preset}").Wide(60));
			}

			body.AddChild(presets);
			body.AddChild(cost);
			var note = Layout.Text(T("auto.setup_note"), GameTheme.Faded, 500).Named("Note");
			note.HorizontalAlignment = HorizontalAlignment.Center;
			body.AddChild(note);
			Show();

			dialog.AddAction(T("common.cancel"), null).Named("Cancel");
			dialog.AddAction(T("auto.start"), () => start(runs), ButtonKind.Primary, true, "repeat").Named("Start");
			return dialog;
		}
	}
}
