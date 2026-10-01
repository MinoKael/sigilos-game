using System;
using Godot;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A janela da versão nova (o executável do Windows, docs/ATUALIZACOES.md): oferece, mostra o download e
	/// avisa que o jogo vai abrir de novo. A obrigatória (esta versão ficou abaixo da mínima) não fecha:
	/// é atualizar ou sair. Quem baixa e troca o executável é o GameRoot, que responde com
	/// <see cref="ShowDownloading"/>, <see cref="ShowProgress"/>, <see cref="ShowOffer"/> (de novo, com o
	/// erro), <see cref="ShowInstalling"/> e, se a troca do executável falhar, <see cref="ShowFailed"/>.
	/// </summary>
	public sealed class UpdateDialog
	{
		/// <summary>O nome do nó: a troca de tela fecha as outras janelas, não esta (GameRoot.CloseDialogs).</summary>
		public const string NodeName = "UpdateDialog";

		private const float Width = 540;

		private readonly Dialog _dialog;
		private readonly string _version;
		private readonly string _current;
		private readonly long _size;
		private readonly bool _required;
		private ProgressBar? _bar;
		private Label? _amount;

		private UpdateDialog(Control from, Version version, Version current, long size, bool required)
		{
			_version = version.ToString();
			_current = current.ToString();
			_size = size;
			_required = required;
			_dialog = Dialog.Open(from, T(required ? "update.required_title" : "update.title"), Width, null, NodeName);
			ShowOffer();
		}

		/// <summary>Atualizar (ou Tentar de novo).</summary>
		public event Action? UpdateRequested;

		/// <summary>Cancelar, no meio do download.</summary>
		public event Action? CancelRequested;

		/// <summary>Sair do jogo, na obrigatória.</summary>
		public event Action? QuitRequested;

		/// <summary>Baixar pelo navegador, quando a pasta do jogo não aceita gravar.</summary>
		public event Action? BrowserRequested;

		public static UpdateDialog Open(Control from, Version version, Version current, long size, bool required) =>
			new(from, version, current, size, required);

		/// <summary>A oferta: o que é, o tamanho e os botões. Com <paramref name="error"/>, o que deu errado na última tentativa.</summary>
		public void ShowOffer(string? error = null, bool browser = false)
		{
			Reset(!_required);
			_dialog.Body.AddChild(RichText.Label(T(_required ? "update.required_text" : "update.text", _version, _current, Megabytes(_size)), Width - 40).Named("Text"));
			if (error != null)
			{
				var message = Layout.Text(error, width: Width - 40).Named("Error");
				message.AddThemeColorOverride("font_color", Palette.Negative);
				_dialog.Body.AddChild(message);
			}

			if (_required)
				_dialog.AddAction(T("update.quit"), () => QuitRequested?.Invoke(), closes: false).Named("Quit");
			else
				_dialog.AddAction(T("update.later"), null).Named("Later");
			if (browser)
				_dialog.AddAction(T("update.browser"), () => BrowserRequested?.Invoke(), closes: false).Named("Browser");
			_dialog.AddAction(T(error == null ? "update.install" : "update.retry"), () => UpdateRequested?.Invoke(), ButtonKind.Primary, closes: false).Named("Install");
		}

		/// <summary>O download começou: a barra, quanto já veio e Cancelar.</summary>
		public void ShowDownloading()
		{
			Reset(false);
			_dialog.Body.AddChild(Layout.Text(T("update.downloading", _version), width: Width - 40).Named("Text"));
			_bar = Layout.Energy(Palette.Arcane, 12).Named("Progress");
			_dialog.Body.AddChild(_bar);
			_amount = Layout.Text("", GameTheme.Faded).Named("Amount");
			_amount.HorizontalAlignment = HorizontalAlignment.Right;
			_dialog.Body.AddChild(_amount);
			_dialog.AddAction(T("common.cancel"), () => CancelRequested?.Invoke(), closes: false).Named("Cancel");
			ShowProgress(0);
		}

		/// <summary>Quantos bytes já vieram. Fora do download (um aviso atrasado), não faz nada.</summary>
		public void ShowProgress(long received)
		{
			if (_bar == null || _amount == null)
				return;
			_bar.Value = _size > 0 ? received * 100.0 / _size : 0;
			_amount.Text = T("update.progress", Megabytes(received), Megabytes(_size));
		}

		/// <summary>Baixou e conferiu: o jogo salva, fecha e abre de novo.</summary>
		public void ShowInstalling()
		{
			Reset(false);
			_dialog.Body.AddChild(Layout.Text(T("update.installing"), width: Width - 40).Named("Text"));
		}

		/// <summary>Não deu para trocar o executável depois do download: o jogo de antes segue inteiro, mas a conta já saiu. Só resta sair.</summary>
		public void ShowFailed(string text)
		{
			Reset(false);
			var message = Layout.Text(text, width: Width - 40).Named("Text");
			message.AddThemeColorOverride("font_color", Palette.Negative);
			_dialog.Body.AddChild(message);
			_dialog.AddAction(T("update.quit"), () => QuitRequested?.Invoke(), ButtonKind.Primary, closes: false).Named("Quit");
		}

		public void Close() => _dialog.Close();

		private void Reset(bool dismissable)
		{
			_bar = null;
			_amount = null;
			Layout.Clear(_dialog.Body);
			_dialog.ClearActions();
			_dialog.Dismissable = dismissable;
		}

		private static string Megabytes(long bytes) => (bytes / 1048576.0).ToString("0.0", Culture);
	}
}
