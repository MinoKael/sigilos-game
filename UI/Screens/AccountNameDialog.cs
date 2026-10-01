using System;
using System.Text.RegularExpressions;
using Godot;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A janela do nome da conta: o que ele é, o campo e Salvar. Serve para trocar o nome (Ajustes) e para
	/// pedir um à conta que ainda não tem (<c>prompt</c>, logo depois de entrar).
	///
	/// O nome é único no servidor e não tem espaço: o campo os tira enquanto se digita (<see cref="NoSpaces"/>).
	/// Esta janela só confere o tamanho (<see cref="Clean"/>); quem confere o resto da regra e se o nome
	/// está livre é o servidor, pelo GameRoot, que responde com <see cref="SetBusy"/>, <see cref="ShowError"/>
	/// ou <see cref="Close"/>.
	/// </summary>
	public sealed class AccountNameDialog
	{
		public const int MinLength = 3;
		public const int MaxLength = 20;

		private const float Width = 520;

		private readonly Dialog _dialog;
		private readonly LineEdit _field;
		private readonly Label _message = new() { Name = "Message", AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
		private readonly GameButton _save;

		private AccountNameDialog(Control from, string? current, bool prompt)
		{
			_dialog = Dialog.Open(from, T("account.name"), Width, null, "AccountNameDialog");
			_dialog.Body.AddChild(RichText.Label(T(prompt ? "account.name_prompt" : "account.name_text"), Width - 40).Named("Text"));
			_field = new LineEdit
			{
				Name = "NameField",
				Text = current ?? "",
				PlaceholderText = T("account.name_hint", MinLength, MaxLength),
				MaxLength = MaxLength,
				CustomMinimumSize = new Vector2(0, GameTheme.Touch),
				CaretBlink = true,
			};
			NoSpaces(_field);
			_field.TextSubmitted += _ => Submit();
			_dialog.Body.AddChild(_field);
			_message.CustomMinimumSize = new Vector2(Width - 40, 0);
			_dialog.Body.AddChild(_message);

			_dialog.AddAction(T(prompt ? "account.name_later" : "common.cancel"), null).Named("Cancel");
			_save = _dialog.AddAction(T("account.name_save"), Submit, ButtonKind.Primary, closes: false).Named("Save");

			// Trocar o nome foi pedido: o campo já vem com o foco. A pergunta logo depois de entrar não abre o
			// teclado do celular sem o jogador tocar.
			if (!prompt)
				Callable.From(() => _field.GrabFocus()).CallDeferred();
		}

		/// <summary>O nome escrito, sem espaço, que passou na conferência de tamanho.</summary>
		public event Action<string>? Submitted;

		public static AccountNameDialog Open(Control from, string? current, bool prompt) => new(from, current, prompt);

		/// <summary>O nome sem espaço nenhum; nulo fora do tamanho.</summary>
		public static string? Clean(string text)
		{
			var clean = Regex.Replace(text, @"\s", "");
			return clean.Length is >= MinLength and <= MaxLength ? clean : null;
		}

		/// <summary>Tira os espaços do campo enquanto se digita (e do que se cola), sem perder o lugar do cursor.</summary>
		public static void NoSpaces(LineEdit field)
		{
			field.TextChanged += text =>
			{
				var clean = Regex.Replace(text, @"\s", "");
				if (clean == text)
					return;
				var caret = field.CaretColumn - (text.Length - clean.Length);
				field.Text = clean;
				field.CaretColumn = Math.Max(0, caret);
			};
		}

		/// <summary>Esperando o servidor: Salvar e o campo desligados.</summary>
		public void SetBusy()
		{
			_save.Disabled = true;
			_field.Editable = false;
			Say(T("account.saving"), Palette.TextFaded);
		}

		public void ShowError(string text)
		{
			_save.Disabled = false;
			_field.Editable = true;
			Say(text, Palette.Negative);
		}

		public void Close() => _dialog.Close();

		private void Submit()
		{
			if (_save.Disabled)
				return;
			if (Clean(_field.Text) is { } name)
				Submitted?.Invoke(name);
			else
				ShowError(T("account.error_name", MinLength, MaxLength));
		}

		private void Say(string text, Color color)
		{
			_message.Text = text;
			_message.AddThemeColorOverride("font_color", color);
			_message.Visible = text.Length > 0;
		}
	}
}
