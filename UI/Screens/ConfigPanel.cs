using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Audio;
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

	/// <summary>Um volume nos Ajustes: o nome do controle, o título, o valor de agora (0 a 1) e quem ouve a mudança.</summary>
	public sealed record ConfigVolume(string Id, string Title, float Value, Action<float> Changed);

	/// <summary>
	/// Os Ajustes, numa janela:
	/// - um botão por idioma, com o nome escrito no próprio idioma (o atual aceso). Trocar de idioma avisa o
	///   GameRoot, que recarrega os textos e remonta a tela;
	/// - o som: um controle deslizante por volume (geral, música e efeitos), que vale na hora;
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

		public static Dialog Open(Control from, IReadOnlyList<string> languages, string current, Action<string> chosen, IReadOnlyList<ConfigVolume> volumes, ConfigAccount account, Action training)
		{
			var dialog = Dialog.Open(from, T("destination.Config"), 520, null, "ConfigDialog");
			dialog.Body.AddChild(new Label { Name = "LanguageTitle", Text = T("config.language"), ThemeTypeVariation = GameTheme.Heading });
			var column = new VBoxContainer { Name = "Languages" };
			column.AddThemeConstantOverride("separation", Space.Regular);
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
			dialog.Body.AddChild(Sound(volumes));
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

		private static Control Sound(IReadOnlyList<ConfigVolume> volumes)
		{
			var column = new VBoxContainer { Name = "Sound" };
			column.AddThemeConstantOverride("separation", Space.Regular);
			column.AddChild(new Label { Name = "SoundTitle", Text = T("config.sound"), ThemeTypeVariation = GameTheme.Heading });
			var grid = Layout.Grid(3, 14).Named("Volumes");
			foreach (var volume in volumes)
			{
				var amount = new Label
				{
					Name = volume.Id + "Amount",
					Text = Percent(volume.Value),
					ThemeTypeVariation = GameTheme.Number,
					HorizontalAlignment = HorizontalAlignment.Right,
					CustomMinimumSize = new Vector2(64, 0),
				};
				// Alto o bastante para o dedo.
				var slider = new HSlider
				{
					Name = volume.Id,
					MinValue = 0,
					MaxValue = 1,
					Step = 0.05,
					Value = volume.Value,
					CustomMinimumSize = new Vector2(0, 44),
					SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				};
				var last = volume.Value;
				// Cada degrau soa já no volume novo (o de Efeitos ouve a si mesmo).
				slider.ValueChanged += value =>
				{
					amount.Text = Percent(value);
					volume.Changed((float)value);
					Sfx.Play(value > last ? "ui.slider_up" : "ui.slider_down");
					last = (float)value;
				};
				grid.AddChild(new Label { Name = volume.Id + "Title", Text = volume.Title });
				grid.AddChild(slider);
				grid.AddChild(amount);
			}

			column.AddChild(grid);
			return column;
		}

		private static string Percent(double value) => $"{Mathf.RoundToInt(value * 100)}%";

		private static Control Account(Dialog dialog, ConfigAccount account)
		{
			var column = new VBoxContainer { Name = "Account" };
			column.AddThemeConstantOverride("separation", Space.Regular);
			column.AddChild(new Label { Name = "AccountTitle", Text = T("config.account"), ThemeTypeVariation = GameTheme.Heading });
			if (account.Email != null)
			{
				column.AddChild(new Label { Name = "AccountName", Text = account.Name ?? T("config.no_name"), ThemeTypeVariation = GameTheme.Number });
				column.AddChild(new Label { Name = "Email", Text = account.Email, ThemeTypeVariation = GameTheme.Faded });
				if (account.Status.Length > 0)
					column.AddChild(Layout.Text(account.Status, GameTheme.Faded, 470).Named("Status"));
				var signOut = GameButton.Of(T("config.sign_out"), () =>
				{
					dialog.Close();
					account.SignOut();
				}).Named("SignOut");
				signOut.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				var buttons = Layout.Row(Space.Regular).Named("Buttons");
				// Trocar o nome é na Loja; aqui só a conta que ainda não tem nome escolhe o primeiro, de graça.
				if (account.Name == null)
				{
					var choose = GameButton.Of(T("config.choose_name"), () =>
					{
						dialog.Close();
						account.Rename();
					}).Named("ChooseName");
					choose.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
					buttons.AddChild(choose);
				}
				else
				{
					column.AddChild(Layout.Text(T("config.rename_in_shop"), GameTheme.Faded, 470).Named("RenameHint"));
				}

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
