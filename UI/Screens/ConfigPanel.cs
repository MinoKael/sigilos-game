using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Os Ajustes, numa janela: um botão por idioma, com o nome escrito no próprio idioma (o atual
	/// aceso). Trocar de idioma avisa o GameRoot, que recarrega os textos e remonta a tela.
	/// </summary>
	public static class ConfigPanel
	{
		/// <summary>O nome de cada idioma nele mesmo: quem não entende o atual acha o seu.</summary>
		private static readonly Dictionary<string, string> Names = new()
		{
			["en"] = "English",
			["pt-BR"] = "Português (Brasil)",
		};

		public static Dialog Open(Control from, IReadOnlyList<string> languages, string current, Action<string> chosen)
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
			return dialog;
		}
	}
}
