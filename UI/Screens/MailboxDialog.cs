using System;
using System.Collections.Generic;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A janela do correio: as cartas que o servidor mandou, cada uma com o título, a data, o texto, o que
	/// ela traz e o botão Coletar (com prazo, até quando dá); com mais de uma, Coletar tudo embaixo. Abre
	/// buscando, e o GameRoot responde com <see cref="Show"/> ou <see cref="ShowMessage"/> (sem conta, sem
	/// conexão, erro). Coletar só avisa: quem aplica a regra (<see cref="Mailbox"/>), grava e fala com o
	/// servidor é o GameRoot, que mostra de novo o que sobrou.
	/// </summary>
	public sealed class MailboxDialog
	{
		private const float Width = 620;

		private readonly Dialog _dialog;
		private readonly VBoxContainer _letters = new() { Name = "Letters" };

		/// <summary>Para os presentes: o nome e o desenho de cada monstro.</summary>
		private readonly GameDatabase? _database;

		private MailboxDialog(Control from, GameDatabase? database)
		{
			_database = database;
			_dialog = Dialog.Open(from, T("mail.title"), Width, null, "MailboxDialog");
			_letters.AddThemeConstantOverride("separation", 12);
			_dialog.Body.AddChild(_letters);
			_dialog.Closed += () => Closed?.Invoke();
			ShowMessage(T("mail.loading"));
		}

		public event Action<Mail>? ClaimRequested;
		public event Action? ClaimAllRequested;
		public event Action? Closed;

		public static MailboxDialog Open(Control from, GameDatabase? database = null) => new(from, database);

		/// <summary>Um aviso no lugar das cartas: buscando, sem conta, sem conexão, erro.</summary>
		public void ShowMessage(string text)
		{
			Layout.Clear(_letters);
			_dialog.ClearActions();
			_letters.AddChild(Layout.Text(text, GameTheme.Faded, Width - 40).Named("Message"));
		}

		/// <summary>As cartas que faltam coletar (sem nenhuma, o aviso de caixa vazia).</summary>
		public void Show(IReadOnlyList<Mail> mail)
		{
			if (mail.Count == 0)
			{
				ShowMessage(T("mail.empty"));
				return;
			}

			Layout.Clear(_letters);
			_dialog.ClearActions();
			for (var i = 0; i < mail.Count; i++)
				_letters.AddChild(Letter(mail[i]).Named($"Letter{i + 1}"));
			if (mail.Count > 1)
				_dialog.AddAction(T("mail.claim_all"), () => ClaimAllRequested?.Invoke(), ButtonKind.Primary, closes: false, icon: "collect").Named("ClaimAll");
		}

		private Control Letter(Mail mail)
		{
			var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel };
			var column = new VBoxContainer { Name = "Column" };
			column.AddThemeConstantOverride("separation", 8);
			panel.AddChild(column);

			var head = Layout.Row(10).Named("Head");
			head.AddChild(Doodle.Icon(Art.Icon("mail"), 30, Palette.Gold));
			head.AddChild(new Label { Name = "Title", Text = mail.Title, ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart });
			head.AddChild(new Label { Name = "Sent", Text = mail.SentAt.ToLocalTime().ToString("d", Culture), ThemeTypeVariation = GameTheme.Faded, VerticalAlignment = VerticalAlignment.Center });
			column.AddChild(head);

			if (mail.Text.Length > 0)
				column.AddChild(Layout.Text(mail.Text, null, Width - 80).Named("Text"));

			if (mail.Rewards.Count > 0 || mail.Gifts.Count > 0)
			{
				var rewards = Layout.Flow(6).Named("Rewards");
				foreach (var (item, amount) in mail.Rewards)
					rewards.AddChild(Layout.Labeled(Icon(item), Texts.Number(amount), T($"mail.item.{item}")).Named(item.ToString()));
				for (var i = 0; i < mail.Gifts.Count; i++)
					rewards.AddChild(Gift(mail.Gifts[i]).Named($"Gift{i + 1}"));
				column.AddChild(rewards);
			}

			var foot = Layout.Row(10).Named("Foot");
			var until = new Label
			{
				Name = "Until",
				Text = mail.ExpiresAt is { } expires ? T("mail.until", expires.ToLocalTime().ToString("d", Culture)) : "",
				ThemeTypeVariation = GameTheme.Faded,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center,
			};
			foot.AddChild(until);
			foot.AddChild(GameButton.Of(T("mail.claim"), () => ClaimRequested?.Invoke(mail), ButtonKind.Primary, "collect", 48).Wide(200).Named("Claim"));
			column.AddChild(foot);
			return panel;
		}

		/// <summary>Um presente: o monstro (desenho, ×quantos, nome), a runa (estrelas, raridade e conjunto) ou o retrato.</summary>
		private Control Gift(MailGift gift)
		{
			if (gift.Kind == MailGiftKind.Rune)
			{
				var what = gift.Rarity is { } rarity ? Texts.Name(rarity) : T("mail.gift.any_rarity");
				if (gift.Set is { } set)
					what += $" · {Texts.Name(set)}";
				return Layout.Labeled("rune", $"{gift.Count}× {Texts.Stars(gift.Grade)}", T("mail.gift.rune", what));
			}

			var summon = _database is { } database && database.HasSummon(gift.Id) ? database.Summon(gift.Id) : null;
			if (summon == null)
				return Layout.Labeled("monster", gift.Kind == MailGiftKind.Avatar ? "" : $"×{gift.Count}", T("mail.gift.unknown"));
			return gift.Kind == MailGiftKind.Avatar
				? Layout.Labeled(Art.Creature(summon.Image), "", T("mail.gift.avatar", summon.NameFor(gift.Awakened)), Palette.Of(summon.Element))
				: Layout.Labeled(Art.Creature(summon.Image), $"×{gift.Count}", summon.Name, Palette.Of(summon.Element));
		}

		private static string Icon(MailItem item) => item switch
		{
			MailItem.Gold => "gold",
			MailItem.Mana => "mana",
			MailItem.Scrolls => "scroll",
			MailItem.Essence => "essence",
			MailItem.Fragments => "fragments",
			MailItem.LightDarkScrolls or MailItem.LegendaryScrolls => "scroll",
			MailItem.InfusionCores => "monster",
			_ => "gem",
		};
	}
}
