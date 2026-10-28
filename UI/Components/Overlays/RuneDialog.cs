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
		/// <param name="opened">O nível de antes de uma melhora: o que a runa ganhou desde ele fica em verde.</param>
		/// <param name="animate">A melhora acabou de acontecer: a ficha a refaz na tela, de <paramref name="opened"/> até agora.</param>
		public static Dialog Show(Control from, Rune rune, bool anchored = true, string? note = null, int? opened = null, bool animate = false)
		{
			var dialog = Dialog.Open(from, T("runes.dialog_title"), 440, anchored ? from : null, "RuneDialog");
			dialog.SetCaption(Texts.Title(rune), Palette.Of(rune.Rarity));
			dialog.Body.AddChild(new RuneCard(rune, 400, opened, note: note, title: false, animateFrom: animate ? opened : null));
			return dialog;
		}

		/// <summary>
		/// A ficha de uma janela já aberta (<see cref="Show"/>) refeita no lugar, depois de uma melhora: a janela
		/// continua onde está e a runa nova refaz a melhora na tela, de <paramref name="from"/> até agora, com
		/// o verde do que subiu desde <paramref name="opened"/>.
		/// </summary>
		public static void Replay(Dialog dialog, Rune rune, int opened, int from)
		{
			Layout.Clear(dialog.Body);
			dialog.Body.AddChild(new RuneCard(rune, 400, opened, title: false, animateFrom: from));
		}
	}
}
