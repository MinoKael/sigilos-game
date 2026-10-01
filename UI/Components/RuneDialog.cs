using System;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A ficha de uma runa (<see cref="RuneCard"/>) numa janela: colada na runa que o jogador segurou,
	/// ou no centro quando quem abre é o jogo (a runa que caiu na vitória). O cabeçalho tem numa linha só
	/// "Runa", o conjunto e o espaço dela no meio (na cor da raridade) e o ✕. Quem abre põe os botões de
	/// ação que fizerem sentido ali (Vender, Melhorar, Guardar) com <see cref="Dialog.AddAction"/>.
	/// </summary>
	public static class RuneDialog
	{
		public static Dialog Show(Control from, Rune rune, bool anchored = true, string? note = null)
		{
			var dialog = Dialog.Open(from, T("runes.dialog_title"), 440, anchored ? from : null, "RuneDialog");
			dialog.SetCaption(Texts.Title(rune), Palette.Of(rune.Rarity));
			dialog.Body.AddChild(new RuneCard(rune, 400, note: note, title: false));
			return dialog;
		}

		/// <summary>
		/// Melhorar vários níveis de uma vez gasta muita Essência: pergunta antes, com o gasto, de onde a
		/// runa sai e aonde chega. <paramref name="essence"/> é o que o jogador tem.
		/// </summary>
		public static Dialog ConfirmUpgrade(Control from, Rune rune, int target, int essence, Action confirmed)
		{
			var cost = RuneRules.UpgradeCost(rune, target);
			return Dialog.Confirm(from, T("runes.upgrade_confirm_title", target),
				T("runes.upgrade_confirm", cost.ToString("N0", Culture), Texts.Title(rune), rune.Level, target, essence.ToString("N0", Culture)),
				T("runes.upgrade_button", target), confirmed);
		}
	}
}
