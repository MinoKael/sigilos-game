using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A conta nos Ajustes: o e-mail (nulo: jogando sem conta), o nome (nulo numa conta criada antes do nome
	/// existir), como está a nuvem e o que os botões fazem (Entrar ou criar conta; Sair da conta; trocar o
	/// nome).
	/// </summary>
	public sealed record ConfigAccount(string? Email, string? Name, string Status, Action SignIn, Action SignOut, Action Rename);

	/// <summary>
	/// Os Ajustes, numa janela:
	/// - um botão por idioma, com o nome escrito no próprio idioma (o atual aceso). Trocar de idioma avisa o
	///   GameRoot, que recarrega os textos e remonta a tela;
	/// - a conta: o nome e o e-mail, Trocar nome e Sair da conta, ou, sem conta, Entrar ou criar conta;
	/// - a luta de treino, para refazer as lições do começo.
	/// </summary>
	public static class ConfigPanel
	{
		/// <summary>O nome de cada idioma nele mesmo: quem não entende o atual acha o seu.</summary>
		private static readonly Dictionary<string, string> Names = new()
		{
			["en"] = "English",
			["pt-BR"] = "Português (Brasil)",
		};

		public static Dialog Open(Control from, IReadOnlyList<string> languages, string current, Action<string> chosen, ConfigAccount account, Action training)
		{
			var dialog = Dialog.Open(from, T("destination.Config"), 520, null, "ConfigDialog");
			dialog.Body.AddChild(new Label { Name = "LanguageTitle", Text = T("config.language"), ThemeTypeVariation = GameTheme.Heading });
			var column = new VBoxContainer { Name = "Languages" };
			column.AddThemeConstantOverride("separation", 10);
			foreach (var language in languages)
			{
				var code = language;
				var name = Names.GetValueOrDefault(code, code);
				// "pt-BR" → PtBR.
				var button = GameButton.Of(code == current ? T("config.current", name) : name, () =>
				{
					dialog.Close();
					if (code != current)
						chosen(code);
				}, code == current ? ButtonKind.Primary : ButtonKind.Secondary).Named(Layout.NodeName(code.Replace('-', '_')));
				column.AddChild(button);
			}

			dialog.Body.AddChild(column);
			dialog.Body.AddChild(Account(dialog, account));
			dialog.Body.AddChild(new Label { Name = "TrainingTitle", Text = T("config.training"), ThemeTypeVariation = GameTheme.Heading });
			dialog.Body.AddChild(Layout.Text(T("config.training_text"), GameTheme.Faded, 470).Named("TrainingText"));
			dialog.Body.AddChild(GameButton.Of(T("config.training_button"), () =>
			{
				dialog.Close();
				training();
			}, ButtonKind.Secondary, "fight").Named("Training"));
			return dialog;
		}

		private static Control Account(Dialog dialog, ConfigAccount account)
		{
			var column = new VBoxContainer { Name = "Account" };
			column.AddThemeConstantOverride("separation", 10);
			column.AddChild(new Label { Name = "AccountTitle", Text = T("config.account"), ThemeTypeVariation = GameTheme.Heading });
			if (account.Email != null)
			{
				column.AddChild(new Label { Name = "AccountName", Text = account.Name ?? T("config.no_name"), ThemeTypeVariation = GameTheme.Number });
				column.AddChild(new Label { Name = "Email", Text = account.Email, ThemeTypeVariation = GameTheme.Faded });
				if (account.Status.Length > 0)
					column.AddChild(Layout.Text(account.Status, GameTheme.Faded, 470).Named("Status"));
				var rename = GameButton.Of(T(account.Name != null ? "config.rename" : "config.choose_name"), () =>
				{
					dialog.Close();
					account.Rename();
				}).Named("Rename");
				var signOut = GameButton.Of(T("config.sign_out"), () =>
				{
					dialog.Close();
					account.SignOut();
				}).Named("SignOut");
				rename.SizeFlagsHorizontal = signOut.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				var buttons = Layout.Row(10).Named("Buttons");
				buttons.AddChild(rename);
				buttons.AddChild(signOut);
				column.AddChild(buttons);
			}
			else
			{
				column.AddChild(Layout.Text(T("config.offline"), GameTheme.Faded, 470).Named("Offline"));
				column.AddChild(GameButton.Of(T("config.sign_in"), () =>
				{
					dialog.Close();
					account.SignIn();
				}).Named("SignIn"));
			}

			return column;
		}
	}
}
