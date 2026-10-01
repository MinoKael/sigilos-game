using System;
using System.Collections.Generic;
using System.Linq;
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
	/// faltam, com a barra) e as chances. O círculo gira e brilha antes do resultado; cada cartão novo
	/// diz embaixo se é novo, cópia ou se foi para o Baú, e segurar um abre o resumo do monstro.
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

		/// <summary>O círculo gira e brilha; depois os cartões aparecem um a um.</summary>
		public void ShowResults(IReadOnlyList<SummonResult> results)
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
			tween.TweenCallback(Callable.From(() => Reveal(results)));
		}

		private void Reveal(IReadOnlyList<SummonResult> results)
		{
			_sigil?.QueueFree();
			_sigil = null;
			_cards.Columns = Math.Min(5, results.Count);
			_results.Visible = true;

			for (var i = 0; i < results.Count; i++)
			{
				var result = results[i];
				var tag = result.Monster.Stored ? T("summon.sent_to_vault")
					: result.FirstCopy ? T("summon.new")
					: T("summon.copy");
				var card = new CreatureCard(result.Summon, result.Monster, results.Count == 1 ? 180 : 124, null, tag) { Name = $"Result{i + 1}", Modulate = new Color(1, 1, 1, 0) };
				_cards.AddChild(card);
				card.CreateTween().TweenProperty(card, "modulate:a", 1f, 0.25).SetDelay(0.08 * i);
			}

			// Espera o grid medir os cartões antes de centralizar.
			Callable.From(Center).CallDeferred();
			Refresh();
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
