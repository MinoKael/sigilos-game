using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Summoning;
using Sigilos.UI.Components;
using Sigilos.UI.Style;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Invocação ritual (GDD, seção 9): escolha até 2 Glifos conhecidos para direcionar, gaste
	/// Pergaminhos e veja o sigilo girar antes do resultado. A garantia está sempre à vista.
	///
	/// O traçado do Glifo com o mouse entra na v0.5; aqui o ritual é só a animação.
	/// </summary>
	public partial class SummonScreen : Control
	{
		private const double RitualSeconds = 1.4;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly List<Glyph> _directed = new();

		private readonly CurrencyBar _currencies = new();
		private readonly Label _pity = new();
		private readonly HBoxContainer _glyphs = new();
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

		/// <summary>Quantidade (1 ou 10) e os Glifos de direcionamento.</summary>
		public event Action<int, IReadOnlyList<Glyph>>? SummonRequested;

		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			var header = new HBoxContainer();
			header.AddChild(new Label { Text = "Invocação Ritual", ThemeTypeVariation = GameTheme.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			header.AddChild(_currencies);
			var back = new Button { Text = "Voltar ao Santuário" };
			back.Pressed += () => BackRequested?.Invoke();
			header.AddChild(back);
			page.AddChild(header);

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 20);
			page.AddChild(body);

			var (panel, content) = Layout.Section("O ritual");
			panel.CustomMinimumSize = new Vector2(400, 0);
			content.AddChild(new Label
			{
				Text = $"Taxas: 3★ 65% · 4★ 28% · 5★ 7%.\nLuz e Trevas têm metade da chance das outras variantes.\nDuplicatas viram Ecos (até 5: habilidades mais fortes) e depois Fragmentos.",
				ThemeTypeVariation = GameTheme.Faded,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			});
			_pity.AddThemeFontOverride("font", GameTheme.Serif);
			_pity.AddThemeFontSizeOverride("font_size", 20);
			content.AddChild(_pity);
			content.AddChild(new Label
			{
				Text = "Direcionar com Glifos que você conhece (até 2): com 1 Glifo, metade dos resultados vem dele; com 2, 30% de cada.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			});
			_glyphs.AddThemeConstantOverride("separation", 6);
			content.AddChild(_glyphs);
			_single.Pressed += () => SummonRequested?.Invoke(1, _directed);
			_ten.Pressed += () => SummonRequested?.Invoke(10, _directed);
			content.AddChild(_single);
			content.AddChild(_ten);
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
			_pity.Text = $"5★ garantida em {SummonRitual.PullsUntilPity(_player)} invocações";
			_single.Text = $"Invocar 1 ({Texts.Scrolls(SummonRitual.CostFor(1))})";
			_ten.Text = $"Invocar 10 ({Texts.Scrolls(SummonRitual.CostFor(10))})";
			_single.Disabled = _player.Scrolls < SummonRitual.CostFor(1);
			_ten.Disabled = _player.Scrolls < SummonRitual.CostFor(10);
			RebuildGlyphs();
		}

		/// <summary>O sigilo gira e brilha; depois os cartões aparecem um a um.</summary>
		public void ShowResults(IReadOnlyList<SummonResult> results)
		{
			Refresh();
			Layout.Clear(_results);
			_single.Disabled = _ten.Disabled = true;

			_idleCircle?.QueueFree();
			_idleCircle = null;

			var glyph = _directed.Count > 0 ? _directed[0] : results[0].Summon.Glyph;
			_sigil?.QueueFree();
			_sigil = new Doodle(Art.Glyph(glyph), Palette.Gold) { CustomMinimumSize = new Vector2(220, 220), Size = new Vector2(220, 220) };
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
				var badge = result.Outcome switch
				{
					SummonOutcome.New => "NOVA!",
					SummonOutcome.Echo => $"Eco {result.Echoes}",
					_ => $"+{result.Fragments} Fragmentos",
				};
				var card = new CreatureCard(result.Summon, _player.Summons.GetValueOrDefault(result.Summon.Id), badge, 140) { Modulate = new Color(1, 1, 1, 0) };
				_results.AddChild(card);
				card.CreateTween().TweenProperty(card, "modulate:a", 1f, 0.25).SetDelay(0.08 * i);
			}

			// Espera o grid medir os cartões antes de centralizar.
			Callable.From(() => _results.Position = (_stage.Size - _results.GetCombinedMinimumSize()) / 2).CallDeferred();
			Refresh();
		}

		private void RebuildGlyphs()
		{
			Layout.Clear(_glyphs);
			foreach (var glyph in SummonRitual.KnownGlyphs(_player, _database))
			{
				var chosen = _directed.Contains(glyph);
				var button = Layout.IconButton("", Art.Glyph(glyph), 28, chosen ? Palette.Background : Palette.Gold);
				button.ToggleMode = true;
				button.ButtonPressed = chosen;
				button.TooltipText = $"{Texts.Name(glyph)}: {Texts.Meaning(glyph)}";
				button.CustomMinimumSize = new Vector2(48, 52);
				button.Toggled += on =>
				{
					if (on && _directed.Count < SummonRates.MaxDirectedGlyphs)
						_directed.Add(glyph);
					else
						_directed.Remove(glyph);
					Callable.From(RebuildGlyphs).CallDeferred();
				};
				_glyphs.AddChild(button);
			}

			var names = _directed.Count == 0 ? "nenhum" : string.Join(" e ", _directed.Select(Texts.Name));
			_glyphs.AddChild(new Label { Text = $"Direcionado: {names}", ThemeTypeVariation = GameTheme.Faded });
		}
	}
}
