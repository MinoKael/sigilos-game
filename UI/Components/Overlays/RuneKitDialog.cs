using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A abertura do kit de sigilos (<see cref="RuneKits"/>): o jogador escolhe o conjunto e recebe os 6 sigilos
	/// Lendários 6★, um por espaço. "Depois" deixa o kit guardado (ele continua no save).
	/// </summary>
	public static class RuneKitDialog
	{
		/// <param name="open">Abre o kit no conjunto escolhido (quem sorteia é o servidor): os sigilos recebidos, ou nula se não abriu.</param>
		public static Dialog Open(Control from, PlayerState player, Func<RuneSet, Task<IReadOnlyList<Rune>?>> open)
		{
			var dialog = Dialog.Open(from, T("kit.title"), 520, null, "RuneKitDialog");
			dialog.Body.AddChild(Layout.Text(T("kit.text", player.PendingRuneKits, Texts.Stars(RuneKits.Grade)), width: 480).Named("Text"));
			var grid = new GridContainer { Name = "Sets", Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			grid.AddThemeConstantOverride("h_separation", Space.Medium);
			grid.AddThemeConstantOverride("v_separation", Space.Medium);
			foreach (var set in RuneSets.ActiveSets)
			{
				var chosen = set;
				grid.AddChild(Choices.Row(new Choice(Texts.Name(set), Symbol: RuneSets.PlanetOf(set)), false, async () =>
				{
					dialog.Close();
					if (await open(chosen) is { } runes && GodotObject.IsInstanceValid(from) && from.IsInsideTree())
						Show(from, runes);
				}).Named($"Set{(int)set}"));
			}

			dialog.Body.AddChild(grid);
			dialog.AddAction(T("kit.later"), null, ButtonKind.Secondary).Named("Later");
			return dialog;
		}

		private static void Show(Control from, IReadOnlyList<Rune> runes)
		{
			if (runes.Count == 0)
				return;

			var dialog = Dialog.Open(from, T("kit.opened"), 560, null, "RuneKitOpened");
			var row = new GridContainer { Name = "Runes", Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			row.AddThemeConstantOverride("h_separation", Space.Medium);
			row.AddThemeConstantOverride("v_separation", Space.Medium);
			foreach (var rune in runes)
				row.AddChild(new RuneTile(rune, rune.Slot, 1.2f) { Name = $"Rune{rune.Id}" });
			dialog.Body.AddChild(row);
			dialog.AddAction(T("common.continue"), null, ButtonKind.Primary).Named("Continue");
		}
	}
}
