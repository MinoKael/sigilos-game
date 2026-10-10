using System;
using Godot;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A entrada da conta, antes do Santuário (docs/SAVE_NUVEM.md, "Contas"). Deitada para o celular:
	/// - À esquerda, o nome do jogo, o que a conta faz, o emblema (o sigilo de invocar num astrolábio, com
	///   uma constelação riscada em volta) e, embaixo, Jogar sem conta.
	/// - À direita, o cartão da conta, com os campos no alto (o teclado do celular cobre a parte de baixo
	///   da tela). Duas formas:
	///   - Com uma conta lembrada neste aparelho: o nome e o e-mail, Entrar (sem senha) e Usar outra conta.
	///   - Sem: as abas Entrar e Criar conta, com e-mail, senha e, para criar, o nome da conta e o código do
	///     convite.
	///
	/// Só confere o óbvio (e-mail com @, senha de 8 letras, nome de 3 a 20, convite escrito) e avisa por
	/// evento. O resto, inclusive se o nome está livre, é o servidor que diz: quem fala com ele é o
	/// GameRoot, que responde com <see cref="SetBusy"/> e <see cref="ShowMessage"/>.
	/// </summary>
	public partial class LoginScreen : Control
	{
		private const float CardWidth = 520;
		/// <summary>O mínimo da senha (o mesmo do servidor): no cadastro e na senha nova do "Esqueci a senha".</summary>
		public const int MinPassword = 8;

		private readonly string? _email;
		private readonly string? _name;
		private readonly VBoxContainer _card = new() { Name = "Card" };
		private readonly Label _message = new() { Name = "Message", AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
		private readonly GameButton _offline;
		private readonly Doodle _sigil = Doodle.Icon(Art.Icon("summon"), 150, Palette.Gold);
		private bool _remembered;
		private bool _register;
		private bool _busy;

		/// <summary>O e-mail escrito, que passa de uma aba para a outra.</summary>
		private string? _typed;

		private LineEdit? _emailField;
		private LineEdit? _passwordField;
		private LineEdit? _nameField;
		private LineEdit? _inviteField;
		private GameButton? _submit;
		private GameButton? _other;

		/// <param name="email">O e-mail da última conta deste aparelho (preenche o campo).</param>
		/// <param name="name">O nome dessa conta, mostrado com a conta lembrada.</param>
		/// <param name="remembered">Há token guardado: abre com Entrar sem senha.</param>
		public LoginScreen(string? email, string? name, bool remembered)
		{
			_email = email;
			_name = name;
			_remembered = remembered && email != null;
			_offline = GameButton.Of(T("account.offline"), () => OfflineRequested?.Invoke()).Wide(260).Named("Offline");
		}

		public event Action<string, string>? LoginRequested;
		/// <summary>Criar conta: e-mail, senha, convite e nome (já aparado).</summary>
		public event Action<string, string, string, string>? RegisterRequested;

		/// <summary>Entrar com a conta lembrada, sem senha.</summary>
		public event Action? ResumeRequested;

		/// <summary>Usar outra conta: esquecer a lembrada (a tela já passa para o formulário).</summary>
		public event Action? ForgetRequested;

		public event Action? OfflineRequested;

		/// <summary>"Esqueci a senha", com o e-mail que já está no campo (pode vir vazio).</summary>
		public event Action<string>? PasswordResetRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			// O anel do fundo gira em volta do sigilo da esquerda.
			AddChild(Layout.Background(_sigil));
			var page = Layout.Page(this);
			var row = Layout.Row(40).Named("Row");
			row.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(row);
			row.AddChild(Intro());

			var panel = new PanelContainer { Name = "Account", CustomMinimumSize = new Vector2(CardWidth, 0), SizeFlagsVertical = SizeFlags.ShrinkBegin };
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 22));
			_card.AddThemeConstantOverride("separation", 12);
			panel.AddChild(_card);
			row.AddChild(panel);
			Build();
		}

		/// <summary>Esperando o servidor: tudo desligado, com o que está acontecendo escrito.</summary>
		public void SetBusy(string text)
		{
			_busy = true;
			Say(text, Palette.TextFaded);
			Refresh();
		}

		/// <summary>Uma resposta (erro em vermelho, aviso em apagado); devolve os botões.</summary>
		public void ShowMessage(string text, bool error = true)
		{
			_busy = false;
			Say(text, error ? Palette.Negative : Palette.TextFaded);
			Refresh();
		}

		/// <summary>Troca a conta lembrada pelo formulário (o token venceu: entrar de novo com a senha).</summary>
		public void ShowForm()
		{
			if (!_remembered)
				return;
			_remembered = false;
			Build();
		}

		/// <summary>O nome do jogo, o que a conta faz e, embaixo, a saída para jogar sem conta.</summary>
		private Control Intro()
		{
			var column = new VBoxContainer { Name = "Intro", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			column.AddThemeConstantOverride("separation", 14);
			column.AddChild(new Label { Name = "Title", Text = ProjectSettings.GetSetting("application/config/name").AsString(), ThemeTypeVariation = GameTheme.Title });
			column.AddChild(Layout.Text(T("account.subtitle")).Named("Subtitle"));
			_sigil.Name = "Sigil";
			var emblem = new Emblem { SizeFlagsHorizontal = SizeFlags.ShrinkCenter, SizeFlagsVertical = SizeFlags.Expand | SizeFlags.ShrinkCenter };
			emblem.AddChild(_sigil);
			column.AddChild(emblem);
			_offline.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			column.AddChild(_offline);
			column.AddChild(Layout.Text(T("account.offline_hint"), GameTheme.Faded).Named("OfflineHint"));
			return column;
		}

		/// <summary>Monta o cartão da conta na forma de agora (lembrada ou formulário).</summary>
		private void Build()
		{
			// A mensagem fica de uma montagem para a outra: sai antes da limpeza.
			if (_message.GetParent() == _card)
				_card.RemoveChild(_message);
			Layout.Clear(_card);
			_emailField = _passwordField = _nameField = _inviteField = null;
			_other = null;
			if (_remembered)
				BuildRemembered();
			else
				BuildForm();

			_card.AddChild(_message);
			Refresh();
		}

		private void BuildRemembered()
		{
			_card.AddChild(new Label { Name = "Heading", Text = T("account.remembered"), ThemeTypeVariation = GameTheme.Heading });
			if (_name != null)
				_card.AddChild(new Label { Name = "AccountName", Text = _name, ThemeTypeVariation = GameTheme.Number });
			_card.AddChild(new Label { Name = "Email", Text = _email, ThemeTypeVariation = _name != null ? GameTheme.Faded : GameTheme.Number });
			_submit = GameButton.Of(T("account.sign_in"), () => ResumeRequested?.Invoke(), ButtonKind.Primary).Named("Submit");
			_card.AddChild(_submit);
			_other = GameButton.Of(T("account.other"), () =>
			{
				ForgetRequested?.Invoke();
				_message.Visible = false;
				ShowForm();
			}).Named("Other");
			_card.AddChild(_other);
		}

		private void BuildForm()
		{
			var tabs = new TextTabs().Named("Tabs");
			tabs.Add(T("account.sign_in_tab")).Name = "SignIn";
			tabs.Add(T("account.register_tab")).Name = "Register";
			tabs.Select(_register ? 1 : 0);
			tabs.Changed += index =>
			{
				_register = index == 1;
				_typed = _emailField?.Text;
				_message.Visible = false;
				Callable.From(Build).CallDeferred();
			};
			_card.AddChild(tabs);

			_emailField = Field("Email", T("account.email"), "", LineEdit.VirtualKeyboardTypeEnum.EmailAddress);
			_emailField.Text = _typed ?? _email ?? "";
			_passwordField = Field("Password", T("account.password"), _register ? T("account.password_hint") : "", LineEdit.VirtualKeyboardTypeEnum.Password);
			_passwordField.Secret = true;
			if (_register)
			{
				_nameField = Field("AccountName", T("account.name"), T("account.name_hint", AccountNameDialog.MinLength, AccountNameDialog.MaxLength), LineEdit.VirtualKeyboardTypeEnum.Default);
				_nameField.MaxLength = AccountNameDialog.MaxLength;
				AccountNameDialog.NoSpaces(_nameField);
				_inviteField = Field("Invite", T("account.invite"), T("account.invite_hint"), LineEdit.VirtualKeyboardTypeEnum.Default);
			}

			// Enter no último campo é o mesmo que o botão.
			(_inviteField ?? _passwordField).TextSubmitted += _ => Submit();
			_emailField.TextSubmitted += _ => _passwordField.GrabFocus();
			_submit = GameButton.Of(_register ? T("account.register") : T("account.sign_in"), Submit, ButtonKind.Primary).Named("Submit");
			_card.AddChild(_submit);
			if (_register)
				return;

			// Sem botão grande: é o caminho de exceção, discreto embaixo de Entrar.
			var forgot = new GameButton(T("account.forgot"), ButtonKind.Text, height: GameButton.TextHeight) { Name = "Forgot", SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
			forgot.Pressed += () =>
			{
				if (!_busy)
					PasswordResetRequested?.Invoke(_emailField?.Text.Trim() ?? "");
			};
			_card.AddChild(forgot);
		}

		/// <summary>Um campo com o nome em cima; o nó leva <paramref name="name"/> (o campo é <c>{name}Field</c>).</summary>
		private LineEdit Field(string name, string title, string placeholder, LineEdit.VirtualKeyboardTypeEnum keyboard)
		{
			var box = new VBoxContainer { Name = name };
			box.AddThemeConstantOverride("separation", 4);
			box.AddChild(new Label { Name = "Title", Text = title, ThemeTypeVariation = GameTheme.Faded });
			var field = new LineEdit
			{
				Name = $"{name}Field",
				PlaceholderText = placeholder,
				VirtualKeyboardType = keyboard,
				CustomMinimumSize = new Vector2(0, GameTheme.Touch),
				CaretBlink = true,
			};
			box.AddChild(field);
			_card.AddChild(box);
			return field;
		}

		private void Submit()
		{
			if (_busy || _emailField == null || _passwordField == null)
				return;

			var email = _emailField.Text.Trim();
			var password = _passwordField.Text;
			var invite = _inviteField?.Text.Trim() ?? "";
			var name = _nameField != null ? AccountNameDialog.Clean(_nameField.Text) : null;
			var at = email.IndexOf('@');
			if (at <= 0 || at == email.Length - 1)
				ShowMessage(T("account.error_email"));
			else if (_register && password.Length < MinPassword)
				ShowMessage(T("account.error_password", MinPassword));
			else if (password.Length == 0)
				ShowMessage(T("account.error_password_empty"));
			else if (_register && name == null)
				ShowMessage(T("account.error_name", AccountNameDialog.MinLength, AccountNameDialog.MaxLength));
			else if (_register && invite.Length == 0)
				ShowMessage(T("account.error_invite"));
			else if (_register)
				RegisterRequested?.Invoke(email, password, invite, name!);
			else
				LoginRequested?.Invoke(email, password);
		}

		private void Say(string text, Color color)
		{
			_message.Text = text;
			_message.AddThemeColorOverride("font_color", color);
			_message.Visible = text.Length > 0;
		}

		/// <summary>Liga e desliga o que se toca conforme a espera.</summary>
		private void Refresh()
		{
			if (_submit != null)
				_submit.Disabled = _busy;
			if (_other != null)
				_other.Disabled = _busy;
			_offline.Disabled = _busy;
			foreach (var field in new[] { _emailField, _passwordField, _nameField, _inviteField })
			{
				if (field != null)
					field.Editable = !_busy;
			}
		}

		/// <summary>
		/// O emblema do jogo: o sigilo no meio de um astrolábio de índigo (dois anéis e as marcas do grau) e
		/// uma constelação riscada em ouro por cima do anel. Desenho parado, refeito só ao mudar de tamanho.
		/// </summary>
		private sealed partial class Emblem : CenterContainer
		{
			/// <summary>As estrelas da constelação: ângulo (graus, 0 à direita, sentido horário) e distância em fração do raio.</summary>
			private static readonly (float Angle, float Distance)[] Stars =
			{
				(-168, 0.9f), (-136, 1.02f), (-104, 0.88f), (-70, 1.0f), (-34, 0.86f), (-4, 1.04f), (30, 0.92f),
			};

			private static readonly Color Ring = new(Palette.Indigo, 0.8f);
			private static readonly Color Thread = new(Palette.GoldDark, 0.8f);

			public Emblem()
			{
				Name = "Emblem";
				CustomMinimumSize = new Vector2(300, 300);
				MouseFilter = MouseFilterEnum.Ignore;
				Resized += QueueRedraw;
			}

			public override void _Draw()
			{
				var center = Size / 2;
				var radius = Mathf.Min(Size.X, Size.Y) / 2 - 14;
				DrawArc(center, radius, 0, Mathf.Tau, 96, Ring, 1.5f, true);
				DrawArc(center, radius - 10, 0, Mathf.Tau, 96, new Color(Ring, 0.5f), 1, true);
				for (var k = 0; k < 72; k++)
				{
					var direction = Vector2.FromAngle(k * Mathf.Tau / 72);
					DrawLine(center + direction * (radius - 10), center + direction * (radius - (k % 6 == 0 ? 0 : 5)), new Color(Ring, 0.6f), 1, true);
				}

				var previous = (Vector2?)null;
				for (var i = 0; i < Stars.Length; i++)
				{
					var (angle, distance) = Stars[i];
					var at = center + Vector2.FromAngle(Mathf.DegToRad(angle)) * radius * distance;
					if (previous is { } from)
						DrawLine(from, at, Thread, 1, true);
					previous = at;
				}
				for (var i = 0; i < Stars.Length; i++)
				{
					var (angle, distance) = Stars[i];
					var at = center + Vector2.FromAngle(Mathf.DegToRad(angle)) * radius * distance;
					DrawCircle(at, 3, Palette.Background);
					Starlight.Sparkle(this, at, i % 3 == 0 ? 11 : 7, i % 3 == 0 ? Palette.Gold : Palette.Starlight);
				}
			}
		}
	}
}
