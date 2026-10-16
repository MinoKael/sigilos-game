using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Summoning;
using Sigilos.Core.Progression;
using Sigilos.UI.Audio;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Invocação ritual (GDD, seção 9): o portal no centro; à esquerda, tudo escrito — o pergaminho (Místico,
	/// Luz e Trevas ou Lendário, com quantos a conta tem), os botões Invocar ×1 e Invocar ×10 com o custo,
	/// Comprar Pergaminhos, a troca de Fragmentos por uma 4★ escolhida, a garantia de 5★ do Místico (quantas
	/// faltam, com a barra) e as chances do pergaminho escolhido. O círculo gira e brilha antes do resultado (e espera a invocação
	/// chegar à nuvem); cada cartão novo diz embaixo se é novo, cópia ou se foi para o Baú, e segurar um
	/// abre o resumo do monstro. 4★ e 5★ aparecem com destaque, e Luz e Trevas com o deles.
	///
	/// Na primeira invocação da conta, a placa do Mestre (<see cref="TutorialCoach"/>) fica embaixo à
	/// esquerda, por cima da garantia e das chances: pede a ×10 (a ×1 fica apagada) e, com os cartões na
	/// tela, apresenta os primeiros monstros.
	/// </summary>
	public partial class SummonScreen : Control
	{
		private const double RitualSeconds = 1.4;

		/// <summary>A ordem da lista de pergaminhos: o comum, o Lendário e, por último, Luz e Trevas.</summary>
		private static readonly ScrollKind[] ListOrder = { ScrollKind.Mystic, ScrollKind.Legendary, ScrollKind.LightDark };

		/// <summary>O som de cada cartão que aparece, por estrelas (1★ a 5★).</summary>
		private static readonly string[] RevealSounds =
		{
			"summon.reveal_star_1", "summon.reveal_star_2", "summon.reveal_star_3", "summon.reveal_star_4", "summon.reveal_star_5",
		};
		private const float PortalSize = 360;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly EnergyRing _pity = new(Palette.Awakened, 7) { Name = "Ring", CustomMinimumSize = new Vector2(PortalSize, PortalSize) };
		private readonly Label _pityText = new() { Name = "PityText", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private readonly ProgressBar _pityBar = Layout.Energy(Palette.Awakened, 10).Named("PityBar");
		private readonly Control _stage = new() { Name = "Stage", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		private readonly VBoxContainer _kindRow = new() { Name = "Scrolls" };
		/// <summary>A largura do painel das invocações: igual em toda aba, e nunca mais que isso.</summary>
		private const float PullsWidth = 440;

		/// <summary>O CustomMaximumSize com 0 num eixo trava esse eixo em 0: o que não tem teto leva um número enorme.</summary>
		private const float Unbounded = 100000;

		private readonly VBoxContainer _pityBox = new() { Name = "Pity" };
		private readonly GameButton _exchange;

		private readonly VBoxContainer _results = new() { Name = "Results", Visible = false };
		private readonly GridContainer _cards = new() { Name = "Cards", Columns = 5 };
		private Doodle? _sigil;
		private Control? _idle;

		/// <summary>O Mestre, enquanto a primeira invocação da conta não acontece; nulo fora do tutorial.</summary>
		private readonly TutorialCoach? _coach;

		/// <summary>A lição ainda espera a ×10: a ×1 fica apagada.</summary>
		private bool _lesson;

		/// <summary>
		/// Do começo do ritual até os cartões aparecerem: nada invoca de novo, nem depois de trocar a aba
		/// (<see cref="Refresh"/> reabilitaria os botões e a segunda leva empurraria a primeira para fora).
		/// </summary>
		private bool _ritual;

		public SummonScreen(GameDatabase database, PlayerState player, TutorialCoach? coach = null)
		{
			_database = database;
			_player = player;
			_coach = coach;
			_lesson = coach != null;
			_exchange = GameButton.Of(T("summon.exchange"), OpenExchange, ButtonKind.Secondary, "fragments", 52).Named("Exchange");
		}

		/// <summary>Quantidade (1 ou 10) e o pergaminho.</summary>
		public event Action<int, ScrollKind>? SummonRequested;

		/// <summary>A troca de Fragmentos pela variante 4★ escolhida (o id).</summary>
		public event Action<string>? ExchangeRequested;

		public event Action? ShopRequested;
		public event Action? MonstersRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background(_pity));
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Summon"), _currencies, () => BackRequested?.Invoke()).Header);

			var body = Layout.Row(28).Named("Body"); // Fora da escala: as duas metades da tela de Invocação.
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var panel = new PanelContainer { Name = "Pulls", CustomMinimumSize = new Vector2(PullsWidth, 0), CustomMaximumSize = new Vector2(PullsWidth, Unbounded) };
			var column = new VBoxContainer { Name = "Column", Alignment = BoxContainer.AlignmentMode.Begin };
			column.AddThemeConstantOverride("separation", Space.Wide);
			// Rola dentro do painel: com a garantia e as notas, a coluna passa da altura em telas baixas.
			panel.AddChild(Layout.Scroll(column));
			// A lista de pergaminhos: nome, quantos há e os botões ×1 e ×10; o "?" abre as chances.
			var head = Layout.Row(Space.Regular).Named("ScrollsHead");
			head.AddChild(new Label { Name = "Title", Text = T("summon.scrolls_title"), ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
			var help = SigilButton.Of("help", OpenRates, 44).Named("RatesHelp");
			help.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			head.AddChild(help);
			column.AddChild(head);
			column.AddChild(_kindRow);
			// A Loja abre quando a Campanha a apresenta (Features); antes, o botão fica apagado e diz quando abre.
			var shopOpen = Features.IsOpen(_player, _database, Feature.Shop);
			GameButton? shop = null;
			shop = GameButton.Of(
				T("summon.shop"),
				() =>
				{
					if (shopOpen)
						ShopRequested?.Invoke();
					else
						LockedDialog.Open(shop!, _database, Feature.Shop);
				},
				ButtonKind.Secondary,
				shopOpen ? "shop" : "lock",
				52).Named("Shop");
			if (!shopOpen)
				shop.Modulate = new Color(1, 1, 1, 0.5f);
			column.AddChild(shop);
			column.AddChild(_exchange);
			column.AddChild(new HSeparator { Name = "Line" });

			// A garantia é só do Místico: nos outros pergaminhos o bloco some.
			var pityTitle = new Label { Name = "PityTitle", Text = T("summon.pity_title") };
			pityTitle.AddThemeColorOverride("font_color", Palette.Awakened);
			_pityBox.AddChild(pityTitle);
			_pityBar.MaxValue = SummonRates.Pity;
			_pityBox.AddChild(_pityBar);
			_pityBox.AddChild(_pityText);
			column.AddChild(_pityBox);

			body.AddChild(panel);

			body.AddChild(_stage);
			_results.AddThemeConstantOverride("separation", Space.Loose);
			_cards.AddThemeConstantOverride("h_separation", Space.Large);
			_cards.AddThemeConstantOverride("v_separation", Space.Large);
			_results.AddChild(_cards);
			var after = Layout.Row(Space.Large, true).Named("After");
			after.AddChild(GameButton.Of(T("summon.view_monsters"), () => MonstersRequested?.Invoke(), ButtonKind.Secondary, "monster").Wide(300).Named("ViewMonsters"));
			_results.AddChild(after);
			_stage.AddChild(_results);
			_stage.Resized += Center;
			// O resultado não está num contêiner: quando o texto do botão quebra linha, ele primeiro mede
			// uma letra por linha e depois encolhe; sem centralizar de novo, o bloco ficava da altura da tela.
			_results.MinimumSizeChanged += Center;

			// O portal em repouso, dentro do anel da garantia: some quando o primeiro ritual começa.
			var idle = new Control { Name = "Idle", MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(PortalSize, PortalSize) };
			_pity.Size = new Vector2(PortalSize, PortalSize);
			idle.AddChild(_pity);
			var portal = new Control { Name = "Portal", Position = new Vector2(PortalSize * 0.16f, PortalSize * 0.16f), Size = new Vector2(PortalSize * 0.68f, PortalSize * 0.68f), MouseFilter = MouseFilterEnum.Ignore };
			portal.AddChild(Doodle.Masked(Art.Transmutation(), Palette.GoldDark, MaskShape.Circle, inset: 12, boil: false));
			idle.AddChild(portal);
			// O texto decorativo em volta do símbolo de Transmutação.
			idle.AddChild(new RingText(MottoText, PortalSize * 0.43f, 17, Palette.Gold) { Name = "Motto", Size = new Vector2(PortalSize, PortalSize) });
			_idle = idle;
			_stage.AddChild(idle);

			if (_coach != null)
			{
				_coach.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
				_coach.GrowVertical = GrowDirection.Begin;
				_coach.OffsetLeft = _coach.OffsetRight = Layout.ScreenMargin;
				_coach.OffsetTop = _coach.OffsetBottom = -Layout.ScreenMargin;
				AddChild(_coach);
				_coach.Hint(T("tutorial.summon", SummonRitual.CostFor(10), _database.Summon(SummonRates.FirstSummon).Name));
			}

			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			var left = SummonRitual.PullsUntilPity(_player);
			_pity.Progress = 1 - left / (float)SummonRates.Pity;
			_pityBar.Value = SummonRates.Pity - left;
			_pityText.Text = T("summon.pity_progress", SummonRates.Pity - left, SummonRates.Pity);
			Kinds();
			_exchange.WithCost("fragments", Texts.Number(FragmentExchange.Cost));
			_exchange.Block(_ritual ? T("summon.why_ritual") : _lesson ? T("summon.why_lesson") : _player.Fragments < FragmentExchange.Cost ? T("summon.why_fragments") : null, T("summon.exchange"));
		}

		/// <summary>
		/// A lista dos pergaminhos: uma linha por tipo (nome, quantos a conta tem, ×1 e ×10, cada um apagado
		/// quando não há pergaminho para ele); o tutorial só mostra o Místico, com a ×1 apagada.
		/// </summary>
		private void Kinds()
		{
			Layout.Clear(_kindRow);
			_kindRow.AddThemeConstantOverride("separation", Space.Medium);
			foreach (var kind in ListOrder)
			{
				if (_lesson && kind != ScrollKind.Mystic)
					continue;
				var owned = SummonRitual.Scrolls(_player, kind);
				var row = Layout.Row(Space.Regular).Named($"Scroll_{kind}");
                var who = new HBoxContainer { Name = "Who", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(190, 0) };
                who.AddChild(new Label { Name = "Name", Text = T($"summon.scroll.{kind}"), ThemeTypeVariation = GameTheme.Heading, VerticalAlignment = VerticalAlignment.Center});
                who.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
                who.AddChild(new Label { Name = "Owned", Text = Texts.Number(owned), VerticalAlignment = VerticalAlignment.Center });
                row.AddChild(who);

                var single = GameButton.Of(T("summon.times_one"), () => SummonRequested?.Invoke(1, kind), ButtonKind.Primary, null, 52).Wide(76).Named("Single");
				single.Block(_ritual ? T("summon.why_ritual") : _lesson ? T("summon.why_lesson") : owned < SummonRitual.CostFor(1) ? T("summon.why_scrolls") : null, T("summon.times_one"));
				row.AddChild(single);
				var ten = GameButton.Of(T("summon.times_ten"), () => SummonRequested?.Invoke(10, kind), ButtonKind.Primary, null, 52).Wide(76).Named("Ten");
				ten.Block(_ritual ? T("summon.why_ritual") : owned < SummonRitual.CostFor(10) ? T("summon.why_scrolls") : null, T("summon.times_ten"));
				row.AddChild(ten);
				_kindRow.AddChild(row);
			}
		}

		/// <summary>O "?": as chances de cada pergaminho, num diálogo (a garantia fica fora, na coluna).</summary>
		private void OpenRates()
		{
			var dialog = Dialog.Open(this, T("summon.rates"), 620, null, "RatesDialog");
			foreach (var kind in ListOrder)
			{
				var box = new VBoxContainer { Name = $"Rates_{kind}" };
				box.AddThemeConstantOverride("separation", Space.Small);
				box.AddChild(new Label { Name = "Title", Text = T($"summon.scroll.{kind}"), ThemeTypeVariation = GameTheme.Heading });
				AddRates(box, kind);
				dialog.Body.AddChild(box);
			}

			dialog.Body.AddChild(Layout.Text(T("summon.note"), GameTheme.Faded).Named("Note"));
		}

		/// <summary>As chances do pergaminho: o que não é 5★ nem 4★ é 3★ (o Lendário não tem).</summary>
		private void AddRates(Control into, ScrollKind kind)
		{
			var rates = Layout.Row(Space.Wide).Named("Rates");
			var (five, four) = SummonRates.Of(kind);
			// As estrelas de Luz e Trevas são roxas; as das outras abas seguem a moldura da raridade.
			Color Ink(int stars) => kind == ScrollKind.LightDark ? Palette.Awakened : Palette.Frame(stars);
			// Alquímica: o 0,1% de Luz e Trevas sai da fatia das 3★, e aparece à parte, com a regra de repartição.
			var lightDark = kind == ScrollKind.Mystic ? SummonRates.MysticLightDark : 0;
			foreach (var (stars, chance) in new[] { (3, 1 - five - four - lightDark), (4, four), (5, five) })
			{
				if (chance <= 1e-9)
					continue;
				var rate = new Label { Name = $"Rate{stars}", Text = $"{Texts.Stars(stars)} {Texts.Percent(chance)}" };
				rate.AddThemeColorOverride("font_color", Ink(stars));
				rates.AddChild(rate);
			}

			into.AddChild(rates);
			into.AddChild(new Label { Name = "Elements", Text = T($"summon.elements.{kind}"), AutowrapMode = TextServer.AutowrapMode.WordSmart });

			if (lightDark > 0)
			{
				var eligible = _database.Summons.Count(SummonRates.MysticLightDarkEligible);
				var ld = new Label { Name = "RateLightDark", Text = T("summon.light_dark_rate", Texts.Percent(lightDark), Texts.PercentFine(lightDark / eligible)), AutowrapMode = TextServer.AutowrapMode.WordSmart };
				ld.AddThemeColorOverride("font_color", Palette.Arcane);
				into.AddChild(ld);
			}
		}

		/// <summary>A troca de Fragmentos: as 4★ de Fogo, Água e Vento numa grade; tocar pede confirmação.</summary>
		private void OpenExchange()
		{
			var dialog = Dialog.Open(this, T("summon.exchange_title"), 820, null, "ExchangeDialog");
			dialog.Body.AddChild(Layout.Text(T("summon.exchange_hint", Texts.Number(FragmentExchange.Cost), Texts.Number(_player.Fragments)), GameTheme.Faded).Named("Hint"));
			var grid = new GridContainer { Name = "Options", Columns = 5 };
			grid.AddThemeConstantOverride("h_separation", Space.Regular);
			grid.AddThemeConstantOverride("v_separation", Space.Regular);
			foreach (var summon in FragmentExchange.Options(_database))
			{
				var card = new CreatureCard(summon, null, 112) { Name = summon.Id };
				card.Pressed += _ => Dialog.Confirm(this, T("summon.exchange_title"),
					T("summon.exchange_confirm", summon.Name, FragmentExchange.Cost),
					T("summon.exchange_button"), () =>
					{
						dialog.Close();
						ExchangeRequested?.Invoke(summon.Id);
					});
				grid.AddChild(card);
			}

			dialog.Body.AddChild(grid);
		}

		/// <summary>O texto em volta do símbolo de Transmutação: exatamente assim, sem mudar a grafia.</summary>
		public const string MottoText = "sit aliquid boni venire";

		/// <summary>Quanto o resultado espera, no máximo, a invocação chegar à nuvem.</summary>
		private const double SaveWaitSeconds = 4;

		/// <summary>
		/// O círculo gira e brilha; depois os cartões aparecem um a um. Com <paramref name="saved"/> (o envio
		/// para a nuvem), o resultado só aparece depois dele, ou de <see cref="SaveWaitSeconds"/>. O sigilo
		/// acende com som e a energia sobe até o fim do giro.
		/// </summary>
		public void ShowResults(IReadOnlyList<SummonResult> results, Task? saved = null)
		{
			_ritual = true;
			Refresh();
			Layout.Clear(_cards);
			_results.Visible = false;
			_coach?.Clear();

			if (_idle != null)
				_idle.Visible = false;

			if (_sigil != null)
				Layout.Discard(_sigil);
			_sigil = new Doodle(Art.Transmutation(), Palette.Gold) { Name = "Ritual", CustomMinimumSize = new Vector2(240, 240), Size = new Vector2(240, 240) };
			_sigil.AddChild(new RingText(MottoText, 112, 14, Palette.Gold) { Name = "Motto", Size = new Vector2(240, 240), MouseFilter = MouseFilterEnum.Ignore });
			_stage.AddChild(_sigil);
			_sigil.Position = (_stage.Size - _sigil.Size) / 2;
			_sigil.PivotOffset = _sigil.Size / 2;
			_sigil.Scale = Vector2.One * 0.3f;

			var best = results.Max(r => r.Summon.Rarity);
			var tween = CreateTween();
			tween.TweenProperty(_sigil, "scale", Vector2.One, RitualSeconds * 0.6).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			tween.Parallel().TweenProperty(_sigil, "rotation", Mathf.Tau, RitualSeconds);
			tween.Parallel().TweenMethod(Callable.From<Color>(c => _sigil.SetInk(c)), Palette.Gold, Palette.Frame(best).Lerp(Colors.White, 0.3f), RitualSeconds);
			tween.TweenProperty(_sigil, "modulate:a", 0f, 0.25);
			tween.TweenCallback(Callable.From(() => RevealWhenSaved(results, saved)));
			Sfx.Play("summon.summon_start");
			Sfx.Play("summon.energy_build", 0.3);
		}

		private async void RevealWhenSaved(IReadOnlyList<SummonResult> results, Task? saved)
		{
			if (saved is { IsCompleted: false })
				await Task.WhenAny(saved, Task.Delay(TimeSpan.FromSeconds(SaveWaitSeconds)));
			if (IsInsideTree())
				Reveal(results);
		}

		/// <summary>
		/// Os cartões aparecem um a um. 4★ e 5★ ganham destaque: um halo de raios atrás, a faixa no alto
		/// ("4★!", "5★!") e o cartão que salta; o 5★ ainda acende a tela num clarão. Luz e Trevas, os
		/// elementos mais raros, trocam o ouro e a prata pela cor do elemento, com raios em duas cores e
		/// o nome do elemento na faixa. Cada cartão soa as estrelas dele, na mesma cascata; o monstro novo na
		/// conta fecha com o registro no grimório.
		/// </summary>
		private void Reveal(IReadOnlyList<SummonResult> results)
		{
			_ritual = false;
			_sigil?.QueueFree();
			_sigil = null;
			// Só a leva desta invocação: nunca soma cartões a uma anterior.
			Layout.Clear(_cards);
			_cards.Columns = Math.Min(5, results.Count);
			_cards.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			_results.Visible = true;
			var width = results.Count == 1 ? 180 : 124;

			for (var i = 0; i < results.Count; i++)
			{
				var result = results[i];
				var tag = result.Monster.Stored ? T("summon.sent_to_vault")
					: result.FirstCopy ? T("summon.new")
					: T("summon.copy");
				var slot = new Control { Name = $"Result{i + 1}", CustomMinimumSize = new Vector2(width, width * 1.25f), MouseFilter = MouseFilterEnum.Ignore };
				var card = new CreatureCard(result.Summon, result.Monster, width, null, tag) { Name = "Card", Modulate = new Color(1, 1, 1, 0) };
				var delay = 0.08 * i;
				Sfx.Play(RevealSounds[Math.Clamp(result.Summon.Rarity, 1, RevealSounds.Length) - 1], delay);
				var rare = result.Summon.Rarity >= 4;
				if (rare)
				{
					var halo = Halo(result.Summon, width);
					slot.AddChild(halo);
					halo.CreateTween().TweenProperty(halo, "modulate:a", 1f, 0.3).SetDelay(delay);
				}

				slot.AddChild(card);
				var tween = card.CreateTween();
				tween.TweenProperty(card, "modulate:a", 1f, 0.25).SetDelay(delay);
				if (rare)
				{
					card.PivotOffset = new Vector2(width, width * 1.25f) / 2;
					card.Scale = Vector2.One * 0.45f;
					tween.Parallel().TweenProperty(card, "scale", Vector2.One * 1.12f, 0.25).SetDelay(delay).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
					tween.TweenProperty(card, "scale", Vector2.One, 0.15);
					slot.AddChild(Ribbon(result.Summon, width, delay));
					if (result.Summon.Rarity >= 5)
						Flash(Glow(result.Summon), delay);
				}

				_cards.AddChild(slot);
			}

			if (results.Any(result => result.FirstCopy))
				Sfx.Play("grimoire.creature_registered", 0.08 * results.Count + 0.4);

			// Espera o grid medir os cartões antes de centralizar.
			Callable.From(Center).CallDeferred();
			if (_lesson)
			{
				_lesson = false;
				_coach!.Say(T("tutorial.summon_done", _database.Summon(SummonRates.FirstSummon).Name, T("summon.view_monsters")));
			}

			Refresh();
		}

		/// <summary>Luz e Trevas, os elementos mais raros, têm destaque próprio na invocação.</summary>
		private static bool Special(SummonDefinition summon) => summon.Element is Element.Light or Element.Dark;

		/// <summary>A cor do destaque: prata no 4★, ouro no 5★; a do elemento em Luz e Trevas.</summary>
		private static Color Glow(SummonDefinition summon) => Special(summon) ? Palette.Of(summon.Element) : Palette.Frame(summon.Rarity);

		/// <summary>Os raios atrás do cartão, um tanto maiores que ele.</summary>
		private static SummonHalo Halo(SummonDefinition summon, float width)
		{
			var special = Special(summon);
			var accent = !special ? Glow(summon).Lightened(0.35f)
				: summon.Element == Element.Light ? Colors.White
				: Palette.Of(summon.Element).Darkened(0.55f);
			var rays = special ? 16 : summon.Rarity >= 5 ? 12 : 8;
			var halo = new SummonHalo(Glow(summon), accent, rays, summon.Element == Element.Dark ? -0.5f : 0.5f) { Name = "Halo", Modulate = new Color(1, 1, 1, 0) };
			var size = width * 1.7f;
			halo.Size = new Vector2(size, size);
			halo.Position = new Vector2(width, width * 1.25f) / 2 - halo.Size / 2;
			return halo;
		}

		/// <summary>A faixa no alto do cartão: "4★!", "5★!" ou, em Luz e Trevas, o elemento junto.</summary>
		private static Control Ribbon(SummonDefinition summon, float width, double delay)
		{
			var text = Special(summon) ? T("summon.special", Texts.Name(summon.Element), summon.Rarity) : T("summon.rare", summon.Rarity);
			var plate = new PanelContainer { Name = "Ribbon", MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
			var box = GameTheme.Box(Palette.Inset, Glow(summon), 2, 8, 0);
			box.ContentMarginLeft = box.ContentMarginRight = 10;
			plate.AddThemeStyleboxOverride("panel", box);
			var label = new Label { Name = "Text", Text = text, HorizontalAlignment = HorizontalAlignment.Center };
			label.AddThemeFontOverride("font", GameTheme.Serif);
			label.AddThemeFontSizeOverride("font_size", width >= 160 ? 22 : 17);
			label.AddThemeColorOverride("font_color", Glow(summon).Lightened(0.2f));
			plate.AddChild(label);
			plate.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
			plate.GrowHorizontal = GrowDirection.Both;
			plate.GrowVertical = GrowDirection.Begin;
			plate.OffsetTop = plate.OffsetBottom = 10;
			plate.CreateTween().TweenProperty(plate, "modulate:a", 1f, 0.2).SetDelay(delay + 0.2);
			return plate;
		}

		/// <summary>O clarão do 5★: a tela acende na cor dele e apaga.</summary>
		private void Flash(Color color, double delay)
		{
			var flash = new ColorRect { Name = "Flash", Color = new Color(color.Lightened(0.4f), 0), MouseFilter = MouseFilterEnum.Ignore };
			flash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(flash);
			var tween = flash.CreateTween();
			tween.TweenProperty(flash, "color:a", 0.45f, 0.12).SetDelay(delay);
			tween.TweenProperty(flash, "color:a", 0f, 0.5);
			tween.TweenCallback(Callable.From(flash.QueueFree));
		}

		private void Center()
		{
			_results.Size = _results.GetCombinedMinimumSize();
			_results.Position = (_stage.Size - _results.Size) / 2;
			if (_idle != null)
				_idle.Position = (_stage.Size - new Vector2(PortalSize, PortalSize)) / 2;
		}
	}
}
