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
	/// Invocação ritual (GDD, seção 9): o portal no centro, com a garantia de 5★ como um anel de
	/// energia em volta (cheio = a próxima é 5★); à esquerda os sigilos de invocar 1 e 10, com o custo
	/// em Pergaminhos na plaquinha, e o da Loja. O círculo gira e brilha antes do resultado; os cartões
	/// novos trazem um símbolo: novo, cópia ou mandado ao Baú.
	/// </summary>
	public partial class SummonScreen : Control
	{
		private const double RitualSeconds = 1.4;
		private const float PortalSize = 360;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly SigilButton _single = new(Art.Icon("summon"), "", 104) { Name = "Single" };
		private readonly SigilButton _ten = new(Art.Icon("summon"), "", 104) { Name = "Ten" };
		private readonly EnergyRing _pity = new(Palette.Awakened, 7) { Name = "Pity", CustomMinimumSize = new Vector2(PortalSize, PortalSize) };
		private readonly Label _pityCount = new() { Name = "PityCount", ThemeTypeVariation = GameTheme.Number, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Stop };
		private readonly Control _stage = new() { Name = "Stage", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		private readonly GridContainer _results = new() { Name = "Results", Columns = 5 };
		private Doodle? _sigil;
		private Control? _idle;

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
			AddChild(Layout.Background(_pity));
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Summon"), "summon", _currencies, () => BackRequested?.Invoke()).Header);

			var body = Layout.Row(28).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var column = new VBoxContainer { Name = "Pulls", Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(150, 0) };
			column.AddThemeConstantOverride("separation", 26);
			_single.Letters = "×1";
			_ten.Letters = "×10";
			_single.Pressed += () => SummonRequested?.Invoke(1);
			_ten.Pressed += () => SummonRequested?.Invoke(10);
			column.AddChild(Centered("SingleRow", _single));
			column.AddChild(Centered("TenRow", _ten));
			column.AddChild(Centered("ShopRow", SigilButton.Of("shop", T("summon.shop"), () => ShopRequested?.Invoke(), 60, SigilShape.Square)));

			var rates = new VBoxContainer { Name = "Rates" };
			rates.AddThemeConstantOverride("separation", 4);
			var threeStar = 1 - SummonRates.FiveStar - SummonRates.FourStar;
			foreach (var (stars, chance) in new[] { (3, threeStar), (4, SummonRates.FourStar), (5, SummonRates.FiveStar) })
			{
				var rate = new Label { Name = $"Rate{stars}", Text = $"{Texts.Stars(stars)}  {Texts.Percent(chance)}", HorizontalAlignment = HorizontalAlignment.Center, TooltipText = T("summon.rate", stars), MouseFilter = MouseFilterEnum.Stop };
				rate.AddThemeColorOverride("font_color", Palette.Frame(stars));
				rate.AddThemeFontSizeOverride("font_size", 13);
				rates.AddChild(rate);
			}

			column.AddChild(rates);
			body.AddChild(column);

			body.AddChild(_stage);
			_results.AddThemeConstantOverride("h_separation", 12);
			_results.AddThemeConstantOverride("v_separation", 12);
			_stage.AddChild(_results);
			_stage.Resized += Center;

			// O portal em repouso, dentro do anel da garantia: some quando o primeiro ritual começa.
			var idle = new Control { Name = "Idle", MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(PortalSize, PortalSize) };
			_pity.Size = new Vector2(PortalSize, PortalSize);
			idle.AddChild(_pity);
			var portal = new Control { Name = "Portal", Position = new Vector2(PortalSize * 0.16f, PortalSize * 0.16f), Size = new Vector2(PortalSize * 0.68f, PortalSize * 0.68f), MouseFilter = MouseFilterEnum.Ignore };
			portal.AddChild(Doodle.Masked(Art.Icon("summon"), Palette.GoldDark, MaskShape.Circle, inset: 12));
			idle.AddChild(portal);
			_pityCount.AddThemeFontSizeOverride("font_size", 30);
			_pityCount.AddThemeColorOverride("font_color", Palette.Awakened);
			_pityCount.Position = new Vector2(0, PortalSize + 6);
			_pityCount.Size = new Vector2(PortalSize, 40);
			idle.AddChild(_pityCount);
			_idle = idle;
			_stage.AddChild(idle);

			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			var left = SummonRitual.PullsUntilPity(_player);
			_pity.Progress = 1 - left / (float)SummonRates.Pity;
			_pityCount.Text = left.ToString();
			_pityCount.TooltipText = T("summon.pity", left);
			Cost(_single, 1);
			Cost(_ten, 10);
		}

		/// <summary>O círculo gira e brilha; depois os cartões aparecem um a um.</summary>
		public void ShowResults(IReadOnlyList<SummonResult> results)
		{
			Refresh();
			Layout.Clear(_results);
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
			_results.Columns = Math.Min(5, results.Count);

			for (var i = 0; i < results.Count; i++)
			{
				var result = results[i];
				var (marker, tip) = result.Monster.Stored ? ("chest", T("summon.sent_to_vault"))
					: result.FirstCopy ? ("collect", T("summon.new"))
					: ("copies", T("summon.copy"));
				var card = new CreatureCard(result.Summon, result.Monster, 130, marker, tip) { Name = $"Result{i + 1}", Modulate = new Color(1, 1, 1, 0) };
				_results.AddChild(card);
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
				_idle.Position = (_stage.Size - new Vector2(PortalSize, PortalSize + 46)) / 2;
		}

		private void Cost(SigilButton button, int count)
		{
			var cost = SummonRitual.CostFor(count);
			button.Badge = cost.ToString();
			button.TooltipText = T("summon.pull", count, Texts.Scrolls(cost));
			button.Disabled = _player.Scrolls < cost;
			button.Highlight = _player.TotalPulls == 0 && !button.Disabled;
		}

		private static Control Centered(string name, Control control)
		{
			var box = new CenterContainer { Name = name };
			box.AddChild(control);
			return box;
		}
	}
}
