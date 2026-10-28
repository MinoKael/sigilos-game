using System;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// O check-in diário: à esquerda, o ciclo de 28 dias em quatro fileiras de sete (os dias já contados esmaecidos e com o
	/// sinal de feito, o de hoje com a moldura acesa, os de prêmio maior com a borda de ouro); à direita, o prêmio de hoje
	/// e o do próximo dia grande. O prêmio chega por carta: o botão de baixo abre o Correio. Só mostra a conta como ela
	/// está (quem conta o dia é o GameRoot, pelo servidor).
	/// </summary>
	public static class CheckInDialog
	{
		private const float Width = 840;
		private const int Columns = 7;
		private const float CellWidth = 78;
		private const float CellHeight = 88;
		private const float SideWidth = 230;
		private const int CellIcon = 30;

		/// <param name="mail">Abre o Correio, onde o prêmio espera.</param>
		public static Dialog Open(Control from, PlayerState player, GameDatabase database, DateTime now, Action mail)
		{
			var dialog = Dialog.Open(from, T("checkin.title"), Width, null, "CheckInDialog");
			var counted = player.CheckIn.LastDate == now.Date;
			var current = player.CheckIn.Day;

			var layout = Layout.Row(Space.Large).Named("Layout");
			layout.AddChild(Calendar(from, player, database, now, counted));
			layout.AddChild(Side(player, database, counted));
			dialog.Body.AddChild(layout);
			dialog.AddAction(T("checkin.open_mail"), mail, ButtonKind.Primary, icon: "mail").Named("OpenMail");
			return dialog;
		}

		private static Control Calendar(Control from, PlayerState player, GameDatabase database, DateTime now, bool counted)
		{
			var calendar = new VBoxContainer { Name = "Calendar", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
			calendar.AddThemeConstantOverride("separation", Space.Medium);

			var top = Layout.Row(Space.Regular).Named("Top");
			top.AddChild(new Label { Name = "Progress", Text = T("checkin.progress", player.CheckIn.Day, CheckInDay.Cycle), ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
			var turn = new DateTime(now.Date.AddDays(1).Ticks, DateTimeKind.Utc).ToLocalTime();
			top.AddChild(Layout.HelpButton(from, T("checkin.title"), T("checkin.help", turn.ToString("t", Culture))));
			calendar.AddChild(top);

			var grid = Layout.Grid(Columns, Space.Small).Named("Days");
			grid.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
			foreach (var day in database.CheckIn)
				grid.AddChild(Cell(day, player.CheckIn.Day, counted));
			calendar.AddChild(grid);
			return calendar;
		}

		/// <summary>Um dia: o número (e o sinal de feito), o desenho do primeiro prêmio e a quantidade, com "+n" pelos outros.</summary>
		private static Control Cell(CheckInDay day, int current, bool counted)
		{
			var received = day.Day <= current;
			var today = counted && day.Day == current;
			var cell = new PanelContainer { Name = $"Day{day.Day}", ThemeTypeVariation = GameTheme.InsetPanel, CustomMinimumSize = new Vector2(CellWidth, CellHeight) };
			if (today || day.Big)
				cell.AddThemeStyleboxOverride("panel", Frame(today, day.Big));
			if (received && !today)
				cell.Modulate = new Color(1, 1, 1, 0.55f);

			var box = new VBoxContainer { Name = "Content", Alignment = BoxContainer.AlignmentMode.Center };
			box.AddThemeConstantOverride("separation", Space.Small);
			var head = Layout.Row(Space.Small).Named("Head");
			head.AddChild(new Label { Name = "Number", Text = T("checkin.day", day.Day), ThemeTypeVariation = GameTheme.Faded, ClipText = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(10, 0) });
			if (received)
				head.AddChild(Doodle.Icon(Art.Icon("confirm"), 14, Palette.Positive).Named("Done"));
			box.AddChild(head);

			var rewards = Rewards(day);
			if (rewards.Count > 0)
			{
				var icon = Doodle.Icon(Art.Icon(MailboxDialog.Icon(rewards[0].Item)), CellIcon, MailboxDialog.Ink(rewards[0].Item)).Named("Icon");
				icon.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
				box.AddChild(icon);
				var more = rewards.Count - 1;
				box.AddChild(new Label
				{
					Name = "Amount",
					Text = Texts.Number(rewards[0].Amount) + (more > 0 ? $" +{more}" : ""),
					ThemeTypeVariation = GameTheme.Number,
					HorizontalAlignment = HorizontalAlignment.Center,
					ClipText = true,
					CustomMinimumSize = new Vector2(10, 0),
				});
			}

			cell.AddChild(box);
			return cell;
		}

		private static StyleBoxFlat Frame(bool today, bool big)
		{
			var box = GameTheme.Carved(big ? Palette.PanelLight.Darkened(0.35f) : Palette.Inset, 6, today ? Palette.Gold : Palette.GoldDark);
			box.SetBorderWidthAll(today ? 2 : 1);
			return box;
		}

		/// <summary>O prêmio do dia (no dia certo da conta, só os que a moeda do correio conhece), na ordem dos dados.</summary>
		private static System.Collections.Generic.List<(MailItem Item, int Amount)> Rewards(CheckInDay day) =>
			day.Rewards.Where(r => r.Value > 0 && Enum.TryParse<MailItem>(r.Key, true, out _))
				.Select(r => (Enum.Parse<MailItem>(r.Key, true), r.Value))
				.ToList();

		/// <summary>À direita: o prêmio de hoje (ou, sem ter contado, o do próximo) e o do próximo dia grande.</summary>
		private static Control Side(PlayerState player, GameDatabase database, bool counted)
		{
			var side = new PanelContainer { Name = "Side", ThemeTypeVariation = GameTheme.InsetPanel, CustomMinimumSize = new Vector2(SideWidth, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
			var box = new VBoxContainer { Name = "Content" };
			box.AddThemeConstantOverride("separation", Space.Regular);

			var today = counted ? DailyCheckIn.Of(database, player.CheckIn.Day) : DailyCheckIn.Of(database, DailyCheckIn.NextDay(player));
			box.AddChild(Section(T(counted ? "checkin.today" : "checkin.next"), today == null ? "" : T("checkin.day", today.Day), today));
			if (!counted)
				box.AddChild(Layout.Text(T("checkin.pending"), GameTheme.Faded, 10).Named("Pending"));

			if (DailyCheckIn.NextBig(player, database) is { } big)
			{
				box.AddChild(new HSeparator { Name = "Divider" });
				box.AddChild(Section(T("checkin.big"), big.Away == 1 ? T("checkin.big_next", big.Day.Day) : T("checkin.big_when", big.Day.Day, big.Away), big.Day));
			}

			side.AddChild(box);
			return side;
		}

		private static Control Section(string title, string subtitle, CheckInDay? day)
		{
			var section = new VBoxContainer { Name = "Section" };
			section.AddThemeConstantOverride("separation", Space.Small);
			section.AddChild(new Label { Name = "Title", Text = title, ThemeTypeVariation = GameTheme.Heading });
			if (subtitle.Length > 0)
				section.AddChild(new Label { Name = "Subtitle", Text = subtitle, ThemeTypeVariation = GameTheme.Faded, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(10, 0) });
			if (day != null)
			{
				var chips = Layout.Grid(2, Space.Small).Named("Rewards");
				foreach (var (item, amount) in Rewards(day))
					chips.AddChild(Layout.Chip(MailboxDialog.Icon(item), Texts.Number(amount), MailboxDialog.Ink(item)).Named(item.ToString()));
				section.AddChild(chips);
			}

			return section;
		}
	}
}
