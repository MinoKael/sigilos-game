using System;
using Godot;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// "Esqueci a senha": o e-mail da conta, a chave de recuperação dela (as quatro palavras mostradas no
	/// cadastro, <see cref="RecoveryKeyDialog"/>) e a senha nova. O servidor dá a mesma resposta para chave
	/// errada, conta que não existe e recuperação travada: a janela diz só "chave inválida" e lembra que o
	/// suporte destrava.
	///
	/// Esta janela só confere o formato; quem fala com o servidor é o GameRoot, que responde com
	/// <see cref="SetBusy"/>, <see cref="ShowError"/> ou <see cref="Close"/>.
	/// </summary>
	public sealed class PasswordResetDialog
	{
		private const float Width = 520;

		private readonly Dialog _dialog;
		private readonly LineEdit _email;
		private readonly LineEdit _key;
		private readonly LineEdit _password;
		private readonly Label _message = new() { Name = "Message", AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
		private readonly GameButton _reset;
		private bool _busy;

		private PasswordResetDialog(Control from, string? email)
		{
			_dialog = Dialog.Open(from, T("account.reset_title"), Width, null, "PasswordResetDialog");
			_dialog.Body.AddChild(RichText.Label(T("account.reset_text"), Width - 40).Named("Text"));
			_email = Field("EmailField", T("account.email"), LineEdit.VirtualKeyboardTypeEnum.EmailAddress);
			_email.Text = email ?? "";
			_key = Field("KeyField", T("account.reset_key"), LineEdit.VirtualKeyboardTypeEnum.Default);
			_password = Field("PasswordField", T("account.reset_password"), LineEdit.VirtualKeyboardTypeEnum.Password);
			_password.Secret = true;
			foreach (var field in new[] { _email, _key, _password })
				_dialog.Body.AddChild(field);
			_message.CustomMinimumSize = new Vector2(Width - 40, 0);
			_dialog.Body.AddChild(_message);

			_email.TextSubmitted += _ => _key.GrabFocus();
			_key.TextSubmitted += _ => _password.GrabFocus();
			_password.TextSubmitted += _ => Reset();
			_dialog.AddAction(T("common.cancel"), null).Named("Cancel");
			_reset = _dialog.AddAction(T("account.reset_button"), Reset, ButtonKind.Primary, closes: false).Named("Reset");
			Callable.From(() => (_email.Text.Length > 0 ? _key : _email).GrabFocus()).CallDeferred();
		}

		/// <summary>E-mail, chave de recuperação e senha nova.</summary>
		public event Action<string, string, string>? ResetSubmitted;

		public static PasswordResetDialog Open(Control from, string? email) => new(from, email);

		/// <summary>Esperando o servidor: o botão desligado.</summary>
		public void SetBusy()
		{
			_busy = true;
			_reset.Disabled = true;
			Say(T("account.saving"), Palette.TextFaded);
		}

		public void ShowError(string text)
		{
			_busy = false;
			_reset.Disabled = false;
			Say(text, Palette.Negative);
		}

		public void Close() => _dialog.Close();

		private void Reset()
		{
			if (_busy)
				return;
			var email = _email.Text.Trim();
			var at = email.IndexOf('@');
			var key = _key.Text.Trim();
			if (at <= 0 || at == email.Length - 1)
				ShowError(T("account.error_email"));
			else if (key.Length == 0)
				ShowError(T("account.error_recovery_key"));
			else if (_password.Text.Length < LoginScreen.MinPassword)
				ShowError(T("account.error_password", LoginScreen.MinPassword));
			else
				ResetSubmitted?.Invoke(email, key, _password.Text);
		}

		private static LineEdit Field(string name, string placeholder, LineEdit.VirtualKeyboardTypeEnum keyboard) => new()
		{
			Name = name,
			PlaceholderText = placeholder,
			VirtualKeyboardType = keyboard,
			CustomMinimumSize = new Vector2(0, GameTheme.Touch),
			CaretBlink = true,
		};

		private void Say(string text, Color color)
		{
			_message.Text = text;
			_message.AddThemeColorOverride("font_color", color);
			_message.Visible = text.Length > 0;
		}
	}
}
