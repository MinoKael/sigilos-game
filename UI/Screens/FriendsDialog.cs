using System;
using System.Collections.Generic;
using Godot;
using Sigilos.Core.Social;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A janela dos amigos (Amigos, na barra do Santuário): no alto, convidar pelo nome da conta; embaixo, os
	/// convites recebidos (Aceitar, Recusar), os amigos com quem está jogando agora (Desfazer, com confirmação)
	/// e os convites enviados (Cancelar). No cabeçalho, os lugares ocupados do limite.
	///
	/// Abre buscando, e o GameRoot responde com <see cref="Show"/> ou <see cref="ShowMessage"/> (sem conta, sem
	/// conexão, erro). As ações só avisam: o GameRoot fala com o servidor, mostra o resultado em
	/// <see cref="Notice"/> e a lista de novo. A linha tocada fica travada até a lista nova chegar.
	/// </summary>
	public sealed class FriendsDialog
	{
		private const float Width = 620;
		private const float ActionWidth = 140;
		private const float ActionHeight = 44;
		private const float DotSize = 12;

		private readonly Dialog _dialog;
		private readonly LineEdit _name;
		private readonly GameButton _invite;
		private readonly Label _notice;
		private readonly VBoxContainer _lists = new() { Name = "Lists" };

		/// <summary>Com a lista na tela e lugar sobrando, dá para convidar.</summary>
		private bool _canInvite;

		private FriendsDialog(Control from)
		{
			_dialog = Dialog.Open(from, T("friends.title"), Width, null, "FriendsDialog");
			_dialog.Closed += () => Closed?.Invoke();

			var invite = Layout.Row(10).Named("Invite");
			_name = new LineEdit
			{
				Name = "Name",
				PlaceholderText = T("friends.name_placeholder"),
				MaxLength = AccountNameDialog.MaxLength,
				CustomMinimumSize = new Vector2(0, GameTheme.Touch),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			};
			_name.TextChanged += _ => RefreshInvite();
			_name.TextSubmitted += _ => Invite();
			invite.AddChild(_name);
			_invite = Fixed(GameButton.Of(T("friends.invite"), Invite, ButtonKind.Primary)).Named("Send");
			invite.AddChild(_invite);
			_dialog.Body.AddChild(invite);

			_notice = Layout.Text("", null, Width - 40).Named("Notice");
			_notice.Visible = false;
			_dialog.Body.AddChild(_notice);

			_lists.AddThemeConstantOverride("separation", 14);
			_dialog.Body.AddChild(_lists);
			ShowMessage(T("friends.loading"));
		}

		public event Action<string>? InviteRequested;
		public event Action<Friend>? AcceptRequested;

		/// <summary>Recusar o convite recebido, cancelar o enviado ou desfazer a amizade: o mesmo pedido ao servidor.</summary>
		public event Action<Friend>? RemoveRequested;

		public event Action? Closed;

		public static FriendsDialog Open(Control from) => new(from);

		/// <summary>Um aviso no lugar da lista: buscando, sem conta, sem conexão, erro.</summary>
		public void ShowMessage(string text)
		{
			Layout.Clear(_lists);
			_canInvite = false;
			RefreshInvite();
			_lists.AddChild(Layout.Text(text, GameTheme.Faded, Width - 40).Named("Message"));
		}

		public void Show(FriendList list)
		{
			Layout.Clear(_lists);
			_dialog.SetCaption(T("friends.count", list.Taken, list.Max), list.Full ? Palette.Negative : Palette.TextFaded);
			_canInvite = !list.Full;
			RefreshInvite();

			if (list.Incoming.Count > 0)
				Section("Incoming", T("friends.incoming"), list.Incoming, IncomingRow);
			Section("Friends", T("friends.friends"), list.Friends, FriendRow, T(list.Full ? "friends.full" : "friends.none"));
			if (list.Outgoing.Count > 0)
				Section("Outgoing", T("friends.outgoing"), list.Outgoing, OutgoingRow);
		}

		/// <summary>O resultado da última ação, embaixo do convite: vermelho quando não deu certo.</summary>
		public void Notice(string text, bool error)
		{
			_notice.Text = text;
			_notice.AddThemeColorOverride("font_color", error ? Palette.Negative : Palette.Spirit);
			_notice.Visible = true;
		}

		private void RefreshInvite() => _invite.Disabled = !_canInvite || _name.Text.Trim().Length == 0;

		private void Invite()
		{
			var name = _name.Text.Trim();
			if (_invite.Disabled || name.Length == 0)
				return;

			_name.Clear();
			RefreshInvite();
			_notice.Visible = false;
			InviteRequested?.Invoke(name);
		}

		private void Section(string name, string title, IReadOnlyList<Friend> people, Func<Friend, Control> row, string? empty = null)
		{
			var section = new VBoxContainer { Name = name };
			section.AddThemeConstantOverride("separation", 6);
			section.AddChild(new Label { Name = "Title", Text = title, ThemeTypeVariation = GameTheme.Heading });
			if (people.Count == 0 && empty != null)
				section.AddChild(Layout.Text(empty, GameTheme.Faded, Width - 40).Named("Empty"));
			for (var i = 0; i < people.Count; i++)
				section.AddChild(row(people[i]).Named($"Friend{i + 1}"));
			_lists.AddChild(section);
		}

		private Control IncomingRow(Friend friend)
		{
			var (panel, row) = Row(friend, null, Palette.TextFaded);
			row.AddChild(Action(row, T("friends.accept"), () => AcceptRequested?.Invoke(friend), ButtonKind.Primary).Named("Accept"));
			row.AddChild(Action(row, T("friends.refuse"), () => RemoveRequested?.Invoke(friend)).Named("Refuse"));
			return panel;
		}

		private Control FriendRow(Friend friend)
		{
			var color = friend.Online ? Palette.Spirit : Palette.TextFaded;
			var (panel, row) = Row(friend, T(friend.Online ? "friends.online" : "friends.away"), color, presence: true);
			var remove = Fixed(GameButton.Of(T("friends.remove"), () => { }, height: ActionHeight)).Named("Remove");
			remove.Pressed += () => Dialog.Confirm(remove, T("friends.remove_title"), T("friends.remove_confirm", friend.Name), T("friends.remove"), () =>
			{
				Lock(row);
				RemoveRequested?.Invoke(friend);
			}, ButtonKind.Danger);
			row.AddChild(remove);
			return panel;
		}

		private Control OutgoingRow(Friend friend)
		{
			var (panel, row) = Row(friend, T("friends.waiting"), Palette.TextFaded);
			row.AddChild(Action(row, T("friends.cancel"), () => RemoveRequested?.Invoke(friend)).Named("Cancel"));
			return panel;
		}

		/// <summary>
		/// Uma linha: o ponto da presença (dos amigos), o nome e, embaixo dele, a situação (jogando, esperando);
		/// os botões entram depois.
		/// </summary>
		private static (PanelContainer Panel, HBoxContainer Row) Row(Friend friend, string? status, Color color, bool presence = false)
		{
			var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel };
			var row = Layout.Row(10).Named("Row");
			panel.AddChild(row);
			if (presence)
				row.AddChild(Dot(color));

			var who = new VBoxContainer { Name = "Who", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			who.AddThemeConstantOverride("separation", 0);
			who.AddChild(new Label { Name = "Name", Text = friend.Name, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis });
			if (status != null)
			{
				var label = new Label { Name = "Status", Text = status, ThemeTypeVariation = GameTheme.Faded };
				label.AddThemeColorOverride("font_color", color);
				who.AddChild(label);
			}

			row.AddChild(who);
			return (panel, row);
		}

		/// <summary>Um botão da linha: tocado, trava a linha inteira (a resposta do servidor refaz a lista).</summary>
		private GameButton Action(HBoxContainer row, string text, Action pressed, ButtonKind kind = ButtonKind.Secondary) =>
			Fixed(GameButton.Of(text, () =>
			{
				Lock(row);
				pressed();
			}, kind, height: ActionHeight));

		/// <summary>Na largura dele, à direita: sozinho, o GameButton se estica e divide a linha com o nome.</summary>
		private static GameButton Fixed(GameButton button)
		{
			button.Wide(ActionWidth).SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
			return button;
		}

		/// <summary>A linha espera o servidor: os botões travam e o aviso da ação anterior sai.</summary>
		private void Lock(HBoxContainer row)
		{
			_notice.Visible = false;
			foreach (var child in row.GetChildren())
			{
				if (child is BaseButton button)
					button.Disabled = true;
			}
		}

		/// <summary>O ponto da presença: verde jogando, apagado fora.</summary>
		private static Control Dot(Color color)
		{
			var dot = new Panel
			{
				Name = "Presence",
				CustomMinimumSize = new Vector2(DotSize, DotSize),
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			dot.AddThemeStyleboxOverride("panel", GameTheme.Box(color, color, 0, (int)(DotSize / 2), 0));
			return dot;
		}
	}
}
