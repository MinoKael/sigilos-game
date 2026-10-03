using System;
using Godot;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A chave de recuperação da conta, mostrada uma vez só (o servidor guarda só o hash): no cadastro, ou na
	/// primeira entrada de uma conta criada antes da chave existir. Quatro palavras grandes, Copiar e "Já
	/// guardei"; não fecha tocando fora, para o jogador não perder a chave sem querer.
	/// </summary>
	public static class RecoveryKeyDialog
	{
		private const float Width = 560;

		/// <param name="saved">O jogador disse que guardou: o aparelho esquece a chave.</param>
		public static Dialog Open(Control from, string key, Action saved)
		{
			var dialog = Dialog.Open(from, T("account.recovery_title"), Width, null, "RecoveryKeyDialog");
			dialog.Dismissable = false;
			dialog.Body.AddChild(RichText.Label(T("account.recovery_text"), Width - 40).Named("Text"));

			var label = new Label { Name = "Key", Text = key, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			label.AddThemeFontOverride("font", GameTheme.Serif);
			label.AddThemeFontSizeOverride("font_size", 28);
			label.AddThemeColorOverride("font_color", Palette.Gold);
			var plate = new PanelContainer { Name = "Plate" };
			plate.AddThemeStyleboxOverride("panel", GameTheme.Carved(Palette.Inset, 12));
			plate.AddChild(label);
			dialog.Body.AddChild(plate);

			var copied = Layout.Text("", GameTheme.Faded, Width - 40).Named("Copied");
			dialog.Body.AddChild(copied);
			dialog.AddAction(T("account.recovery_copy"), () =>
			{
				DisplayServer.ClipboardSet(key);
				copied.Text = T("account.recovery_copied");
			}, ButtonKind.Secondary, closes: false).Named("Copy");
			dialog.AddAction(T("account.recovery_saved"), saved, ButtonKind.Primary).Named("Saved");
			return dialog;
		}
	}
}
