using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Summoning;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Invocação ritual (GDD, seção 9): o portal no centro; à esquerda, tudo escrito — os botões
	/// Invocar ×1 e Invocar ×10 com o custo em Pergaminhos, Comprar Pergaminhos, a garantia de 5★ (quantas
	/// faltam, com a barra) e as chances. O círculo gira e brilha antes do resultado (e espera a invocação
	/// chegar à nuvem); cada cartão novo diz embaixo se é novo, cópia ou se foi para o Baú, e segurar um
	/// abre o resumo do monstro. 4★ e 5★ aparecem com destaque, e Luz e Trevas com o deles.
	/// </summary>
	public partial class SummonScreen : Control
	{
		private const double RitualSeconds = 1.4;
		private const float PortalSize = 360;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly GameButton _single;
		private readonly GameButton _ten;
		private readonly EnergyRing _pity = new(Palette.Awakened, 7) { Name = "Ring", CustomMinimumSize = new Vector2(PortalSize, PortalSize) };
		private readonly Label _pityText = new() { Name = "PityText", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private readonly ProgressBar _pityBar = Layout.Energy(Palette.Awakened, 10).Named("PityBar");
		private readonly Control _stage = new() { Name = "Stage", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		private readonly VBoxContainer _results = new() { Name = "Results", Visible = false };
		private readonly GridContainer _cards = new() { Name = "Cards", Columns = 5 };
		private Doodle? _sigil;
		private Control? _idle;

		public SummonScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
			_single = GameButton.Of(T("summon.pull_one"), () => SummonRequested?.Invoke(1), ButtonKind.Primary, "summon", 68).Named("Single");
			_ten = GameButton.Of(T("summon.pull_ten"), () => SummonRequested?.Invoke(10), ButtonKind.Primary, "summon", 68).Named("Ten");
		}

		/// <summary>Quantidade: 1 ou 10.</summary>
		public event Action<int>? SummonRequested;

		public event Action? ShopRequested;
		public event Action? MonstersRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background(_pity));
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Summon"), _currencies, () => BackRequested?.Invoke()).Header);

			var body = Layout.Row(28).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var panel = new PanelContainer { Name = "Pulls", CustomMinimumSize = new Vector2(360, 0) };
			var column = new VBoxContainer { Name = "Column", Alignment = BoxContainer.AlignmentMode.Center };
			column.AddThemeConstantOverride("separation", 14);
			panel.AddChild(column);
			column.AddChild(_single);
			column.AddChild(_ten);
			column.AddChild(GameButton.Of(T("summon.shop"), () => ShopRequested?.Invoke(), ButtonKind.Secondary, "shop", 52).Named("Shop"));
			column.AddChild(new HSeparator { Name = "Line" });

			var pityTitle = new Label { Name = "PityTitle", Text = T("summon.pity_title") };
			pityTitle.AddThemeColorOverride("font_color", Palette.Awakened);
			column.AddChild(pityTitle);
			_pityBar.MaxValue = SummonRates.Pity;
			column.AddChild(_pityBar);
			column.AddChild(_pityText);

			var threeStar = 1 - SummonRates.FiveStar - SummonRates.FourStar;
			column.AddChild(new Label { Name = "RatesTitle", Text = T("summon.rates") });
			var rates = Layout.Row(14).Named("Rates");
			foreach (var (stars, chance) in new[] { (3, threeStar), (4, SummonRates.FourStar), (5, SummonRates.FiveStar) })
			{
				var rate = new Label { Name = $"Rate{stars}", Text = $"{Texts.Stars(stars)} {Texts.Percent(chance)}" };
				rate.AddThemeColorOverride("font_color", Palette.Frame(stars));
				rates.AddChild(rate);
			}

			column.AddChild(rates);
			column.AddChild(Layout.Text(T("summon.note"), GameTheme.Faded).Named("Note"));
			body.AddChild(panel);

			body.AddChild(_stage);
			_results.AddThemeConstantOverride("separation", 16);
			_cards.AddThemeConstantOverride("h_separation", 12);
			_cards.AddThemeConstantOverride("v_separation", 12);
			_results.AddChild(_cards);
			var after = Layout.Row(12, true).Named("After");
			after.AddChild(GameButton.Of(T("summon.view_monsters"), () => MonstersRequested?.Invoke(), ButtonKind.Secondary, "monster").Named("ViewMonsters"));
			_results.AddChild(after);
			_stage.AddChild(_results);
			_stage.Resized += Center;

			// O portal em repouso, dentro do anel da garantia: some quando o primeiro ritual começa.
			var idle = new Control { Name = "Idle", MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(PortalSize, PortalSize) };
			_pity.Size = new Vector2(PortalSize, PortalSize);
			idle.AddChild(_pity);
			var portal = new Control { Name = "Portal", Position = new Vector2(PortalSize * 0.16f, PortalSize * 0.16f), Size = new Vector2(PortalSize * 0.68f, PortalSize * 0.68f), MouseFilter = MouseFilterEnum.Ignore };
			portal.AddChild(Doodle.Masked(Art.Icon("summon"), Palette.GoldDark, MaskShape.Circle, inset: 12));
			idle.AddChild(portal);
			_idle = idle;
			_stage.AddChild(idle);

			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			var left = SummonRitual.PullsUntilPity(_player);
			_pity.Progress = 1 - left / (float)SummonRates.Pity;
			_pityBar.Value = SummonRates.Pity - left;
			_pityText.Text = T("summon.pity", left);
			Cost(_single, 1);
			Cost(_ten, 10);
		}

		/// <summary>Quanto o resultado espera, no máximo, a invocação chegar à nuvem.</summary>
		private const double SaveWaitSeconds = 4;

		/// <summary>
		/// O círculo gira e brilha; depois os cartões aparecem um a um. Com <paramref name="saved"/> (o envio
		/// para a nuvem), o resultado só aparece depois dele, ou de <see cref="SaveWaitSeconds"/>.
		/// </summary>
		public void ShowResults(IReadOnlyList<SummonResult> results, Task? saved = null)
		{
			Refresh();
			Layout.Clear(_cards);
			_results.Visible = false;
			_single.Disabled = _ten.Disabled = true;

			if (_idle != null)
				_idle.Visible = false;

			if (_sigil != null)
				Layout.Discard(_sigil);
			_sigil = new Doodle(Art.Icon("summon"), Palette.Gold) { Name = "Ritual", CustomMinimumSize = new Vector2(240, 240), Size = new Vector2(240, 240) };
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
		/// o nome do elemento na faixa.
		/// </summary>
		private void Reveal(IReadOnlyList<SummonResult> results)
		{
			_sigil?.QueueFree();
			_sigil = null;
			_cards.Columns = Math.Min(5, results.Count);
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

			// Espera o grid medir os cartões antes de centralizar.
			Callable.From(Center).CallDeferred();
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

		private void Cost(GameButton button, int count)
		{
			var cost = SummonRitual.CostFor(count);
			button.WithCost("scroll", Texts.Scrolls(cost));
			button.Disabled = _player.Scrolls < cost;
		}
	}
}
