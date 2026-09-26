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
	/// Invocação ritual (GDD, seção 9): gaste Pergaminhos e veja o círculo girar antes do resultado. A
	/// garantia está sempre à vista. Cada resultado é uma cópia nova, no nível 1 e sem Despertar.
	/// </summary>
	public partial class SummonScreen : Control
	{
		private const double RitualSeconds = 1.4;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly Label _pity = new();
		private readonly Button _single = new() { CustomMinimumSize = new Vector2(0, 56) };
		private readonly Button _ten = new() { CustomMinimumSize = new Vector2(0, 56) };
		private readonly Control _stage = new() { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		private readonly GridContainer _results = new() { Columns = 5 };
		private Doodle? _sigil;
		private Doodle? _idleCircle;

		public SummonScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
		}

		/// <summary>Quantidade: 1 ou 10.</summary>
		public event Action<int>? SummonRequested;

		public event Action? ShopRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("summon.title"), _currencies, T("common.back_to_hub"), () => BackRequested?.Invoke()));

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 20);
			page.AddChild(body);

			var (panel, content) = Layout.Section(T("summon.ritual"));
			panel.CustomMinimumSize = new Vector2(400, 0);
			var threeStar = 1 - SummonRates.FiveStar - SummonRates.FourStar;
			content.AddChild(Layout.Text(T("summon.rates", Texts.Percent(threeStar), Texts.Percent(SummonRates.FourStar), Texts.Percent(SummonRates.FiveStar)), GameTheme.Faded));
			_pity.AddThemeFontOverride("font", GameTheme.Serif);
			_pity.AddThemeFontSizeOverride("font_size", 20);
			content.AddChild(_pity);
			content.AddChild(Layout.Text(T("summon.copies", PlayerState.CollectionCapacity), GameTheme.Faded));
			_single.Pressed += () => SummonRequested?.Invoke(1);
			_ten.Pressed += () => SummonRequested?.Invoke(10);
			content.AddChild(_single);
			content.AddChild(_ten);
			var shop = Layout.IconButton(T("summon.shop"), Art.Icon("shop"), 26);
			shop.TooltipText = T("summon.shop_tip");
			shop.Pressed += () => ShopRequested?.Invoke();
			content.AddChild(shop);
			body.AddChild(panel);

			body.AddChild(_stage);
			_results.AddThemeConstantOverride("h_separation", 10);
			_results.AddThemeConstantOverride("v_separation", 10);
			_results.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
			_stage.AddChild(_results);

			// O círculo em repouso: some quando o primeiro ritual começa.
			var idle = new Doodle(Art.Icon("summon"), Palette.PanelLight);
			idle.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_idleCircle = idle;
			_stage.AddChild(idle);

			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			_pity.Text = T("summon.pity", SummonRitual.PullsUntilPity(_player));
			_single.Text = T("summon.pull", 1, Texts.Scrolls(SummonRitual.CostFor(1)));
			_ten.Text = T("summon.pull", 10, Texts.Scrolls(SummonRitual.CostFor(10)));
			_single.Disabled = _player.Scrolls < SummonRitual.CostFor(1);
			_ten.Disabled = _player.Scrolls < SummonRitual.CostFor(10);
		}

		/// <summary>O círculo gira e brilha; depois os cartões aparecem um a um.</summary>
		public void ShowResults(IReadOnlyList<SummonResult> results)
		{
			Refresh();
			Layout.Clear(_results);
			_single.Disabled = _ten.Disabled = true;

			_idleCircle?.QueueFree();
			_idleCircle = null;

			_sigil?.QueueFree();
			_sigil = new Doodle(Art.Icon("summon"), Palette.Gold) { CustomMinimumSize = new Vector2(220, 220), Size = new Vector2(220, 220) };
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
			_results.Columns = Math.Min(5, results.Count);

			for (var i = 0; i < results.Count; i++)
			{
				var result = results[i];
				var badge = result.Monster.Stored ? T("summon.sent_to_vault") : result.FirstCopy ? T("summon.new") : T("summon.copy");
				var card = new CreatureCard(result.Summon, result.Monster, badge, 140) { Modulate = new Color(1, 1, 1, 0) };
				_results.AddChild(card);
				card.CreateTween().TweenProperty(card, "modulate:a", 1f, 0.25).SetDelay(0.08 * i);
			}

			// Espera o grid medir os cartões antes de centralizar.
			Callable.From(() => _results.Position = (_stage.Size - _results.GetCombinedMinimumSize()) / 2).CallDeferred();
			Refresh();
		}
	}
}
