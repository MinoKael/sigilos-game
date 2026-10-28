using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A janela do correio: em cima, quantas cartas há, o ? e Coletar tudo; embaixo, a lista, uma linha por carta
	/// (o símbolo de onde veio, o título, o que ela traz, quanto falta para vencer e Coletar). Tocar na linha abre a carta
	/// inteira (<see cref="OpenLetter"/>). A lista tem altura fixa: a janela não encolhe a cada coleta. O GameRoot
	/// responde com <see cref="Show"/>; Coletar só avisa, e quem aplica a regra, grava e fala com o servidor é ele,
	/// que mostra de novo o que sobrou.
	/// </summary>
	public sealed class MailboxDialog
	{
		private const float Width = 640;

		/// <summary>A altura da lista: cinco linhas; com mais, ela rola.</summary>
		private const float ListHeight = 400;

		private const float RowHeight = 64;
		private const float SymbolSize = 44;
		private const float DeadlineWidth = 86;
		private const float ClaimWidth = 132;

		/// <summary>Quantos prêmios cabem na linha; o resto vira "+n" (a carta inteira mostra todos).</summary>
		private const int RowRewards = 3;

		private const float LetterWidth = 520;

		private readonly Dialog _dialog;
		private readonly Label _count = new() { Name = "Count", ThemeTypeVariation = GameTheme.Faded, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
		private readonly GameButton _claimAll;
		private readonly Label _notice = new() { Name = "Notice", ThemeTypeVariation = GameTheme.Faded, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(10, 0) };
		private readonly VBoxContainer _letters = new() { Name = "Letters" };

		/// <summary>Para os presentes: o nome e o desenho de cada monstro.</summary>
		private readonly GameDatabase? _database;

		private Dialog? _open;

		private MailboxDialog(Control from, GameDatabase? database)
		{
			_database = database;
			_dialog = Dialog.Open(from, T("mail.title"), Width, null, "MailboxDialog");

			var top = Layout.Row(Space.Regular).Named("Top");
			top.AddChild(_count);
			top.AddChild(Layout.HelpButton(from, T("mail.title"), T("mail.help")));
			_claimAll = GameButton.Of(T("mail.claim_all"), () => ClaimAllRequested?.Invoke(), ButtonKind.Primary, "collect", 44).Wide(200).FixedHeight().Named("ClaimAll");
			_claimAll.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
			top.AddChild(_claimAll);
			_dialog.Body.AddChild(top);

			// O aviso fica dentro da lista, em cima das cartas: aparecer ou sumir não muda a altura da janela.
			_notice.Visible = false;
			var content = new VBoxContainer { Name = "Content", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			content.AddThemeConstantOverride("separation", Space.Medium);
			content.AddChild(_notice);
			content.AddChild(_letters);
			_letters.AddThemeConstantOverride("separation", Space.Medium);
			var list = Layout.Scroll(content);
			list.CustomMinimumSize = new Vector2(0, ListHeight);
			_dialog.Body.AddChild(list.Named("List"));
			_dialog.Closed += () =>
			{
				_open?.Close();
				Closed?.Invoke();
			};
			Show(Array.Empty<Mail>(), T("mail.loading"));
		}

		public event Action<Mail>? ClaimRequested;
		public event Action? ClaimAllRequested;
		public event Action? Closed;

		public static MailboxDialog Open(Control from, GameDatabase? database = null) => new(from, database);

		/// <summary>
		/// As cartas que faltam coletar e, se houver, uma linha de aviso em cima delas (buscando, sem conexão, erro). Sem
		/// carta e sem aviso, o aviso de caixa vazia.
		/// </summary>
		public void Show(IReadOnlyList<Mail> mail, string? notice = null)
		{
			_open?.Close();
			_count.Text = T("mail.count", mail.Count);
			_claimAll.Block(mail.Count == 0 ? T("mail.why_claim_all") : null, T("mail.claim_all"));
			_notice.Text = notice ?? (mail.Count == 0 ? T("mail.empty") : "");
			_notice.Visible = _notice.Text.Length > 0;

			Layout.Clear(_letters);
			var now = DateTimeOffset.UtcNow;
			for (var i = 0; i < mail.Count; i++)
				_letters.AddChild(Row(mail[i], now).Named($"Letter{i + 1}"));
		}

		/// <summary>Uma linha: símbolo, título e prêmios, prazo e Coletar. Tocar fora do botão abre a carta.</summary>
		private Control Row(Mail mail, DateTimeOffset now)
		{
			var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel, CustomMinimumSize = new Vector2(0, RowHeight), MouseFilter = Control.MouseFilterEnum.Pass, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
			Press.On(panel, () => OpenLetter(mail), () => OpenLetter(mail));
			var row = Layout.Row(Space.Regular).Named("Row");
			row.MouseFilter = Control.MouseFilterEnum.Pass;
			panel.AddChild(row);

			var symbol = Layout.Medal(Art.Icon(Symbol(mail)), Palette.Gold, SymbolSize).Named("Symbol");
			symbol.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			row.AddChild(symbol);

			var middle = new VBoxContainer { Name = "Middle", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(10, 0), MouseFilter = Control.MouseFilterEnum.Pass, Alignment = BoxContainer.AlignmentMode.Center };
			middle.AddThemeConstantOverride("separation", Space.Small);
			middle.AddChild(new Label { Name = "Title", Text = mail.Title, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, CustomMinimumSize = new Vector2(10, 0) });
			middle.AddChild(Rewards(mail));
			row.AddChild(middle);

			row.AddChild(new Label
			{
				Name = "Deadline",
				Text = Deadline(mail.ExpiresAt, now),
				ThemeTypeVariation = GameTheme.Faded,
				CustomMinimumSize = new Vector2(DeadlineWidth, 0),
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center,
			});

			var claim = GameButton.Of(T("mail.claim"), () => ClaimRequested?.Invoke(mail), ButtonKind.Primary, "collect", 44).Wide(ClaimWidth).FixedHeight().Named("Claim");
			claim.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
			claim.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			row.AddChild(claim);
			return panel;
		}

		/// <summary>Os primeiros prêmios da carta, símbolo e quantidade, e "+n" pelos que não couberam.</summary>
		private Control Rewards(Mail mail)
		{
			var row = Layout.Row(Space.Small).Named("Rewards");
			row.MouseFilter = Control.MouseFilterEnum.Pass;
			// Só os que cabem são montados: a sequência é preguiçosa.
			var chips = mail.Rewards.Select(r => (Control)Layout.Chip(Icon(r.Key), Texts.Number(r.Value), Ink(r.Key)).Named(r.Key.ToString()))
				.Concat(mail.Gifts.Select((g, i) => GiftChip(g).Named($"Gift{i + 1}")));
			foreach (var chip in chips.Take(RowRewards))
				row.AddChild(chip);
			var more = mail.Rewards.Count + mail.Gifts.Count - RowRewards;
			if (more > 0)
				row.AddChild(new Label { Name = "More", Text = T("mail.more", more), ThemeTypeVariation = GameTheme.Faded, VerticalAlignment = VerticalAlignment.Center });
			return row;
		}

		/// <summary>A carta inteira: quando chegou, o texto, tudo o que ela traz, o prazo e Coletar.</summary>
		private void OpenLetter(Mail mail)
		{
			_open?.Close();
			var letter = Dialog.Open(_dialog, mail.Title, LetterWidth, null, "LetterDialog");
			letter.Closed += () =>
			{
				if (_open == letter)
					_open = null;
			};
			_open = letter;
			letter.Body.AddChild(new Label { Name = "Sent", Text = T("mail.sent", mail.SentAt.ToLocalTime().ToString("d", Culture)), ThemeTypeVariation = GameTheme.Faded });
			if (mail.Text.Length > 0)
				letter.Body.AddChild(Layout.Text(mail.Text, null, LetterWidth - 48).Named("Text"));

			if (mail.Rewards.Count > 0 || mail.Gifts.Count > 0)
			{
				var rewards = Layout.Grid(2, Space.Small).Named("Rewards");
				foreach (var (item, amount) in mail.Rewards)
					rewards.AddChild(Layout.Labeled(Icon(item), Texts.Number(amount), T($"mail.item.{item}"), Ink(item)).Named(item.ToString()));
				for (var i = 0; i < mail.Gifts.Count; i++)
					rewards.AddChild(Gift(mail.Gifts[i]).Named($"Gift{i + 1}"));
				letter.Body.AddChild(rewards);
			}

			if (mail.ExpiresAt is { } expires)
				letter.Body.AddChild(new Label { Name = "Until", Text = T("mail.until", expires.ToLocalTime().ToString("g", Culture)), ThemeTypeVariation = GameTheme.Faded });
			letter.AddAction(T("mail.claim"), () => ClaimRequested?.Invoke(mail), ButtonKind.Primary, icon: "collect").Named("Claim");
		}

		/// <summary>Quanto falta para a carta vencer: dias (para cima: uma carta de agora vale 7), ou horas no último dia. Sem prazo, nada.</summary>
		private static string Deadline(DateTimeOffset? expires, DateTimeOffset now)
		{
			if (expires is not { } until)
				return "";
			var left = until - now;
			if (left.TotalDays > 1)
				return T("mail.days_left", (int)Math.Ceiling(left.TotalDays));
			return T("mail.hours_left", Math.Max(1, (int)Math.Ceiling(left.TotalHours)));
		}

		/// <summary>O símbolo da linha, pelo que mandou a carta (as do servidor levam a carta).</summary>
		private static string Symbol(Mail mail) => mail.Kind switch
		{
			Letters.ArenaSeason => "arena",
			Letters.AccountLevel => "level_max",
			Letters.CheckIn => "calendar",
			Letters.SixStarFirst or Letters.SixStar => "evolve",
			_ => "mail",
		};

		/// <summary>Um presente na linha: o desenho e quantos.</summary>
		private Control GiftChip(MailGift gift)
		{
			if (gift.Kind == MailGiftKind.Rune)
				return Layout.Chip("rune", $"×{gift.Count}");
			if (gift.Kind == MailGiftKind.Avatar && SpecialAvatars.Has(gift.Id))
				return Layout.Chip(Art.SpecialAvatar(gift.Id), "");
			return _database is { } database && database.Knows(gift.Id) && database.Summon(gift.Id) is var summon
				? Layout.Chip(Art.Creature(summon.Image), gift.Kind == MailGiftKind.Avatar ? "" : $"×{gift.Count}", Palette.Of(summon.Element))
				: Layout.Chip("monster", $"×{gift.Count}");
		}

		/// <summary>Um presente: o monstro (desenho, ×quantos, nome), a runa (estrelas, raridade e conjunto) ou o retrato (de monstro ou especial).</summary>
		private Control Gift(MailGift gift)
		{
			if (gift.Kind == MailGiftKind.Rune)
			{
				var what = gift.Rarity is { } rarity ? Texts.Name(rarity) : T("mail.gift.any_rarity");
				if (gift.Set is { } set)
					what += $" · {Texts.Name(set)}";
				return Layout.Labeled("rune", $"{gift.Count}× {Texts.Stars(gift.Grade)}", T("mail.gift.rune", what));
			}

			if (gift.Kind == MailGiftKind.Avatar && SpecialAvatars.Has(gift.Id))
				return Layout.Labeled(Art.SpecialAvatar(gift.Id), "", T("mail.gift.avatar", T($"avatar.special.{gift.Id}")), Palette.Gold);

			var summon = _database is { } database && database.Knows(gift.Id) ? database.Summon(gift.Id) : null;
			if (summon == null)
				return Layout.Labeled("monster", gift.Kind == MailGiftKind.Avatar ? "" : $"×{gift.Count}", T("mail.gift.unknown"));
			return gift.Kind == MailGiftKind.Avatar
				? Layout.Labeled(Art.Creature(summon.Image), "", T("mail.gift.avatar", summon.NameFor(gift.Awakened)), Palette.Of(summon.Element))
				: Layout.Labeled(Art.Creature(summon.Image), $"×{gift.Count}", summon.Name, Palette.Of(summon.Element));
		}

		/// <summary>
		/// A tinta de uma moeda do correio (também a do check-in): os três pergaminhos usam o mesmo desenho, então o de Luz e
		/// Trevas leva a cor dos despertos (como na Invocação) e o Lendário a cor da raridade que dá nome a ele.
		/// </summary>
		public static Color Ink(MailItem item) => item switch
		{
			MailItem.LightDarkScrolls => Palette.Awakened,
			MailItem.LegendaryScrolls => Palette.Of(RuneRarity.Legendary),
			_ => Palette.Gold,
		};

		/// <summary>O desenho de uma moeda do correio (também o do check-in).</summary>
		public static string Icon(MailItem item) => item switch
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
