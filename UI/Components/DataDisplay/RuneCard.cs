using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A ficha de uma runa: o jeito de mostrar runa em todo o jogo (a tela de Runas, a vitória, a
	/// Batalha automática e o toque longo em toda runa em miniatura).
	///
	/// No alto, o título (conjunto e espaço) na cor da raridade e a plaquinha da raridade; ao lado da runa,
	/// o atributo principal grande e o inato em ouro; embaixo, os subatributos com o planeta; por fim, o
	/// bônus do conjunto em verde.
	///
	/// Com <c>opened</c> (o nível da runa quando a tela abriu), o que ela ganhou desde então fica em verde
	/// ao lado do valor, e o subatributo novo inteiro em verde, com "new". <c>tools</c> põe sigilos no fim
	/// de cada linha de subatributo (as pedras da janela Afiar e Encantar, na tela de Runas); <c>note</c>
	/// é uma linha apagada no pé (quem usa a runa). Sem <c>title</c>, o título fica de fora: a
	/// <see cref="RuneDialog"/> o põe no cabeçalho da janela.
	///
	/// Com <c>animateFrom</c> (o nível de antes de uma melhora que acabou de acontecer), a ficha refaz a
	/// melhora na tela, um marco por vez (+3, +6, +9, +12, +15; e o nível final, se não é um marco): de +0 a
	/// +3 é um passo só, não três. Em cada passo o número da pedra pula, o principal soma o que ganhou e o
	/// subatributo que mudou aparece, com o ganho subindo em verde sobre ele e sumindo. É o mesmo em toda
	/// melhora, de +1 a +12 de uma vez.
	/// </summary>
	public partial class RuneCard : VBoxContainer
	{
		/// <summary>Quanto dura, no máximo, a melhora toda na tela (s): o +12 de uma vez não pode demorar.</summary>
		private const float TotalSeconds = 1.5f;

		/// <summary>O menor e o maior tempo de cada nível (s).</summary>
		private const float MinStep = 0.09f;
		private const float MaxStep = 0.3f;

		/// <summary>A espera antes do primeiro nível (s): a ficha se acomoda na tela antes de se mexer.</summary>
		private const float Lead = 0.12f;

		private readonly Rune _rune;
		private readonly int _finalLevel;
		private readonly int _opened;
		private Tween? _tween;
		private readonly RuneTile? _tile;
		private readonly Label _mainValue;
		private Label? _mainGain;
		private readonly List<SubstatRow> _rows = new();

		public RuneCard(Rune rune, float width = 320, int? opened = null, Func<int, IEnumerable<Control>>? tools = null, string? note = null, bool title = true, int? animateFrom = null)
		{
			Name = "RuneCard";
			_rune = rune;
			_finalLevel = rune.Level;
			_opened = opened ?? rune.Level;
			CustomMinimumSize = new Vector2(width, 0);
			MouseFilter = MouseFilterEnum.Pass;
			AddThemeConstantOverride("separation", Space.Medium);
			var color = Palette.Of(rune.Rarity);

			if (title)
			{
				var header = Layout.Row(Space.Regular).Named("Header");
				var name = new Label { Name = "Title", Text = Texts.Title(rune), HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
				name.AddThemeColorOverride("font_color", color);
				header.AddChild(name);
				AddChild(header);
			}

			var body = Layout.Row(Space.Wide).Named("Body");
			_tile = new RuneTile(rune, rune.Slot, 1.35f) { Name = "Tile", MouseFilter = MouseFilterEnum.Ignore };
			body.AddChild(_tile);
			var stats = new VBoxContainer { Name = "Stats", Alignment = AlignmentMode.Begin, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			stats.AddThemeConstantOverride("separation", Space.Tight);

			var main = new VBoxContainer { Name = "Main", SizeFlagsHorizontal = SizeFlags.ExpandFill };

			var tagBox = new VBoxContainer { Name = "TagBox", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var tag = RarityTag(rune.Rarity);
			tag.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; // Empurra a tag para a direita
			tag.SizeFlagsVertical = SizeFlags.ShrinkCenter; // Centraliza a tag verticalmente
			tagBox.AddChild(tag);
			main.AddChild(tagBox);

			var valuesBox = new HBoxContainer { Name = "ValuesBox", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			valuesBox.AddThemeConstantOverride("separation", Space.Tight);

			_mainValue = new Label { Name = "Value", Text = Texts.Format(rune.Main, rune.MainValue) };
			_mainValue.AddThemeFontOverride("font", GameTheme.Serif);
			_mainValue.AddThemeFontSizeOverride("font_size", FontSize.Large);
			_mainValue.HorizontalAlignment = HorizontalAlignment.Left;
			valuesBox.AddChild(_mainValue);

			if (rune.Level > _opened)
			{
				_mainGain = Gain("Gain", Texts.Amount(rune.Main, rune.MainValue - RuneRules.MainValue(rune.Main, rune.Grade, _opened)), 16);
				valuesBox.AddChild(_mainGain);
			}

			main.AddChild(valuesBox);
			stats.AddChild(main);

			if (rune.Innate is { } innate)
			{
				var label = new Label { Name = "Innate", Text = Texts.Format(innate) };
				label.AddThemeColorOverride("font_color", Palette.Gold);
				stats.AddChild(label);
			}

			body.AddChild(stats);
			AddChild(body);

			if (rune.Substats.Count > 0)
			{
				AddChild(new HSeparator { Name = "SubstatsLine" });
				for (var i = 0; i < rune.Substats.Count; i++)
				{
					var row = new SubstatRow(rune, i, opened, tools);
					_rows.Add(row);
					AddChild(row.Root.Named($"Substat{i + 1}"));
				}
			}

			var bonus = RichText.Label(Texts.Describe(RuneSets.For(rune.Set)), width, null, 14).Named("SetBonus");
			bonus.AddThemeColorOverride("default_color", Palette.TextFaded);
			AddChild(bonus);

			if (note != null)
				AddChild(new Label { Name = "Note", Text = note, ThemeTypeVariation = GameTheme.Faded, AutowrapMode = TextServer.AutowrapMode.WordSmart });

			_animateFrom = animateFrom is { } from && from >= _opened && from < rune.Level ? from : null;
		}

		private readonly int? _animateFrom;

		/// <summary>A melhora ainda está sendo refeita na tela.</summary>
		public bool Animating => _waiting || (_tween != null && _tween.IsValid() && _tween.IsRunning());

		private bool _waiting;

		/// <summary>Esta ficha é desta runa, neste nível: quem a refaz pode deixá-la terminar em vez de trocá-la.</summary>
		public bool Shows(Rune rune) => ReferenceEquals(rune, _rune) && rune.Level == _finalLevel;

		public override void _Ready()
		{
			if (_animateFrom is not { } from)
				return;

			Play(from);
		}

		/// <summary>
		/// Espera dois quadros e só então começa: quem abre a ficha costuma ter acabado de remontar uma tela, o
		/// quadro seguinte sai longo, e a animação, medida pelo tempo, saltaria direto para o fim.
		/// </summary>
		private async void Play(int from)
		{
			ShowAt(from);
			_waiting = true;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInstanceValid(this) || !IsInsideTree())
				return;

			_waiting = false;

			// Começa como a runa era (o nível, o principal, os subatributos de antes) e refaz um marco por vez:
			// só neles um subatributo muda, então nenhum passo mostra um nível em que nada aconteceu.
			var stops = new List<int>();
			for (var level = from + 1; level <= _rune.Level; level++)
			{
				if (RuneRules.IsMilestone(level) || level == _rune.Level)
					stops.Add(level);
			}

			var interval = Mathf.Clamp(TotalSeconds / stops.Count, MinStep, MaxStep);
			_tween = CreateTween();
			_tween.TweenInterval(Lead);
			var previous = from;
			foreach (var stop in stops)
			{
				var (reached, last) = (stop, previous);
				_tween.TweenCallback(Callable.From(() => Advance(reached, last)));
				_tween.TweenInterval(interval);
				previous = stop;
			}
		}

		/// <summary>
		/// A ficha como a runa era no nível <paramref name="level"/>: o número da pedra, o principal e o ganho
		/// dele, os subatributos já nascidos e o que cada um tinha. O que ainda não nasceu fica invisível,
		/// mas ocupa o lugar (a ficha não se remonta a cada nível).
		/// </summary>
		private void ShowAt(int level)
		{
			_tile?.ShowLevel(level);
			var value = RuneRules.MainValue(_rune.Main, _rune.Grade, level);
			_mainValue.Text = Texts.Format(_rune.Main, value);
			if (_mainGain != null)
			{
				var gained = value - RuneRules.MainValue(_rune.Main, _rune.Grade, _opened);
				_mainGain.Text = Texts.Amount(_rune.Main, gained);
				_mainGain.Modulate = new Color(1, 1, 1, gained > 0 ? 1 : 0);
			}

			foreach (var row in _rows)
				row.ShowAt(level);
		}

		/// <summary>Um marco alcançado, vindo de <paramref name="from"/>: mostra a ficha nele e sobe, em verde, o que o trecho trouxe.</summary>
		private void Advance(int level, int from)
		{
			var before = RuneRules.MainValue(_rune.Main, _rune.Grade, from);
			var after = RuneRules.MainValue(_rune.Main, _rune.Grade, level);
			ShowAt(level);
			if (after > before)
				Rise(_mainGain ?? _mainValue, Texts.Amount(_rune.Main, after - before));

			foreach (var row in _rows)
			{
				if (row.Born(level))
				{
					var fade = CreateTween();
					fade.TweenProperty(row.Root, "modulate:a", 1f, 0.25f).From(0f);
					Rise(row.Value, T("runes.new"));
				}
				else if (row.Rolled(level) is { } amount)
				{
					Rise(row.Value, Texts.Amount(row.Stat, amount));
				}
			}
		}

		/// <summary>Um ganho que aparece sobre o valor e sobe, sumindo (<see cref="GainPop"/>).</summary>
		private void Rise(Control from, string text) => GainPop.Show(this, from, text);

		/// <summary>
		/// Um subatributo: o nome em ouro, o valor (com a Pedra de Afiar) em branco e, entre parênteses e
		/// apagado, o quanto veio das melhoras, o da pedra e o "encantado": "Vida +28% (+22%)". O que mudou
		/// desde <c>opened</c> fica em verde: a linha inteira com "new" se ele nasceu depois, ou o quanto
		/// subiu ao lado do valor.
		/// </summary>
		private sealed class SubstatRow
		{
			private readonly RuneSubstat _substat;
			private readonly bool _isNew;
			private readonly int _opened;
			private readonly Label? _upgraded;
			private readonly Label? _gain;

			public SubstatRow(Rune rune, int index, int? opened, Func<int, IEnumerable<Control>>? tools)
			{
				_substat = rune.Substats[index];
				_opened = opened ?? rune.Level;
				Root = Layout.Row(Space.Small);
				_isNew = opened is { } level && _substat.Rolls.Count > 0 && _substat.Rolls[0].Level > level;
				var gained = opened is { } since ? _substat.Rolls.Where(r => r.Level > since).Sum(r => r.Amount) : 0;
				Root.AddChild(new PlanetSymbol(Texts.PlanetOf(_substat.Stat), 18, _isNew ? Palette.Positive : Palette.GoldDark.Lightened(0.3f)) { Name = "Planet" });
				Root.AddChild(Part("Name", Texts.Name(_substat.Stat), _isNew ? Palette.Positive : Palette.Gold));
				Value = Part("Value", Texts.Amount(_substat.Stat, _substat.Total), _isNew ? Palette.Positive : Palette.Text);
				Root.AddChild(Value);
				if (_substat.Upgraded > 0)
				{
					_upgraded = Part("Upgraded", $"({Texts.Amount(_substat.Stat, _substat.Upgraded)})", Palette.TextFaded);
					Root.AddChild(_upgraded);
				}

				if (_substat.Grind > 0)
					Root.AddChild(Part("Ground", T("rune.ground", Texts.Amount(_substat.Stat, _substat.Grind)), Palette.TextFaded));
				if (_substat.Enchanted)
					Root.AddChild(Part("Enchanted", T("runes.enchanted_line"), Palette.TextFaded));

				if (_isNew)
				{
					Root.AddChild(Gain("New", T("runes.new")));
				}
				else if (gained > 0)
				{
					_gain = Gain("Gain", Texts.Amount(_substat.Stat, gained));
					Root.AddChild(_gain);
				}

				Root.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
				foreach (var tool in tools?.Invoke(index) ?? Array.Empty<Control>())
					Root.AddChild(tool);
			}

			public HBoxContainer Root { get; }

			public Label Value { get; }

			public RuneStat Stat => _substat.Stat;

			/// <summary>Nasceu exatamente neste nível (um subatributo novo da melhora).</summary>
			public bool Born(int level) => _isNew && _substat.Rolls[0].Level == level;

			/// <summary>O sorteio que este nível trouxe a um subatributo que já existia, se trouxe.</summary>
			public double? Rolled(int level) =>
				!_isNew && _substat.Rolls.Skip(1).FirstOrDefault(r => r.Level == level && r.Level > _opened) is { } roll ? roll.Amount : null;

			/// <summary>O subatributo como era no nível <paramref name="level"/> (o que ainda não nasceu fica invisível).</summary>
			public void ShowAt(int level)
			{
				var rolls = _substat.Rolls.Where(r => r.Level <= level).ToList();
				if (_isNew)
				{
					Root.Modulate = new Color(1, 1, 1, _substat.Rolls[0].Level <= level ? 1 : 0);
					Value.Text = Texts.Amount(_substat.Stat, rolls.Sum(r => r.Amount) + _substat.Grind);
					return;
				}

				Value.Text = Texts.Amount(_substat.Stat, rolls.Sum(r => r.Amount) + _substat.Grind);
				if (_upgraded != null)
				{
					var upgraded = rolls.Skip(1).Sum(r => r.Amount);
					_upgraded.Text = $"({Texts.Amount(_substat.Stat, upgraded)})";
					_upgraded.Modulate = new Color(1, 1, 1, upgraded > 0 ? 1 : 0);
				}

				if (_gain != null)
				{
					var gained = rolls.Where(r => r.Level > _opened).Sum(r => r.Amount);
					_gain.Text = Texts.Amount(_substat.Stat, gained);
					_gain.Modulate = new Color(1, 1, 1, gained > 0 ? 1 : 0);
				}
			}
		}

		private static Label Part(string name, string text, Color color)
		{
			var label = new Label { Name = name, Text = text, VerticalAlignment = VerticalAlignment.Center };
			label.AddThemeColorOverride("font_color", color);
			return label;
		}

		/// <summary>O que a runa ganhou, em verde, ao lado do valor.</summary>
		private static Label Gain(string name, string text, int size = 0)
		{
			var label = new Label { Name = name, Text = text, VerticalAlignment = VerticalAlignment.Center };
			label.AddThemeColorOverride("font_color", Palette.Positive);
			if (size > 0)
			{
				label.AddThemeFontOverride("font", GameTheme.Serif);
				label.AddThemeFontSizeOverride("font_size", size);
			}

			return label;
		}

		/// <summary>A plaquinha da raridade, na cor dela ("Legend").</summary>
		private static PanelContainer RarityTag(Core.Content.RuneRarity rarity)
		{
			var color = Palette.Of(rarity);
			var tag = new PanelContainer { Name = "Rarity", SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore };
			var box = GameTheme.Box(color.Darkened(0.55f), color, 1, 6, 0);
			box.ContentMarginLeft = box.ContentMarginRight = 8;
			box.ContentMarginTop = box.ContentMarginBottom = 2;
			tag.AddThemeStyleboxOverride("panel", box);
			var label = new Label { Name = "Label", Text = Texts.Name(rarity), MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", FontSize.Caption);
			label.AddThemeColorOverride("font_color", color.Lightened(0.35f));
			tag.AddChild(label);
			return tag;
		}
	}
}
