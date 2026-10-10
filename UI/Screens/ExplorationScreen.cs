using System;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Audio;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A Exploração Estelar: à esquerda a Exploração do mês (tocar explica o rodízio), as abas das três
	/// faixas do céu — Boreais, Equatoriais e Austrais; uma faixa ainda não alcançada diz em que
	/// constelação abre — e o mapa da aberta (<see cref="StarChart"/>), com o quanto do percurso foi vencido
	/// no mês. À direita a ficha da constelação escolhida, tudo escrito: o nome (e o latino), a Influência
	/// (a explicação, com como vencer, e cada regra com os números), os inimigos onda a onda com o guardião
	/// (tocar num abre o resumo dele), a recompensa da primeira vitória do mês, a última equipe usada na
	/// Exploração e o botão Lutar, que não custa Mana e abre a preparação da luta (<see cref="PrepScreen"/>).
	/// Uma constelação ainda fechada se olha, mas não se luta.
	/// </summary>
	public partial class ExplorationScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly DateTime _now;

		private readonly CurrencyBar _currencies = new();
		private readonly VBoxContainer _chart = new() { Name = "Chart" };
		private readonly HBoxContainer _tabs = new() { Name = "Hemispheres", Alignment = BoxContainer.AlignmentMode.Center };
		private readonly Label _progress = new() { Name = "Progress", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private readonly VBoxContainer _detail = new() { Name = "Detail" };
		private readonly VBoxContainer _actions = new() { Name = "Actions" };
		private readonly Label _message = new() { Name = "Message", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private ScrollContainer? _chartScroll;
		private ScrollContainer? _detailScroll;
		private int _selected;
		private Hemisphere _hemisphere;

		/// <param name="selected">A constelação a abrir (nula: a próxima a vencer no mês).</param>
		public ExplorationScreen(GameDatabase database, PlayerState player, DateTime now, int? selected = null)
		{
			_database = database;
			_player = player;
			_now = now;
			var count = Exploration.Constellations.Count;
			_selected = Math.Clamp(selected ?? Math.Min(Core.Progression.Exploration.Cleared(player, now) + 1, count), 1, Math.Max(1, count));
			_hemisphere = count == 0 ? Hemisphere.Boreal : Exploration.Constellation(_selected).Hemisphere;
		}

		public event Action<int>? FightRequested;
		public event Action? BackRequested;

		/// <summary>A constelação escolhida agora: a tela volta nela depois da luta ou da preparação.</summary>
		public int Selected => _selected;

		private ExplorationDefinition Exploration => _database.Exploration;

		private int Variation => Core.Progression.Exploration.VariationOf(_now);

		private int Cleared => Core.Progression.Exploration.Cleared(_player, _now);

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("exploration.title"), _currencies, () => BackRequested?.Invoke()).Header);

			var body = Layout.Row(20).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var mapPanel = new PanelContainer { Name = "Sky", CustomMinimumSize = new Vector2(600, 0) };
			var mapColumn = new VBoxContainer { Name = "SkyColumn" };
			mapColumn.AddThemeConstantOverride("separation", Space.Medium);
			mapColumn.AddChild(Month());
			mapColumn.AddChild(_tabs);
			_progress.ThemeTypeVariation = GameTheme.Faded;
			mapColumn.AddChild(_progress);
			_chart.SizeFlagsVertical = SizeFlags.ExpandFill;
			mapColumn.AddChild(_chart);
			mapPanel.AddChild(mapColumn);
			body.AddChild(mapPanel);

			var panel = new PanelContainer { Name = "Constellation", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var column = new VBoxContainer { Name = "Column" };
			column.AddThemeConstantOverride("separation", Space.Regular);
			_detail.AddThemeConstantOverride("separation", Space.Large);
			_detailScroll = Layout.Scroll(_detail);
			column.AddChild(_detailScroll);
			_actions.AddThemeConstantOverride("separation", Space.Medium);
			column.AddChild(_actions);
			panel.AddChild(column);
			body.AddChild(panel);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			page.AddChild(_message);

			Refresh();
		}

		/// <summary>Um aviso na faixa de baixo (o motivo de não poder lutar).</summary>
		public void ShowMessage(string text) => _message.Text = text;

		public void Refresh()
		{
			_currencies.Refresh(_player);
			RefreshTabs();
			RefreshChart();
			RefreshDetail();
		}

		/// <summary>"Nova Exploração em 12 dias" (em horas no último dia): também o Grimório do Invocador mostra.</summary>
		public static string NextRotation(DateTime now)
		{
			var left = Core.Progression.Exploration.NextRotation(now) - now;
			var remaining = left.TotalHours < 24 ? T("exploration.hours", Math.Max(1, (int)Math.Ceiling(left.TotalHours)))
				: left.TotalDays < 2 ? T("exploration.day")
				: T("exploration.days", (int)left.TotalDays);
			return T("exploration.next_rotation", remaining);
		}

		/// <summary>"Exploração da Aurora · nova Exploração em 12 dias": tocar explica o rodízio e o que o mês paga.</summary>
		private Control Month()
		{
			var name = Exploration.Explorations.Count > 0 ? Exploration.Explorations[Variation % Exploration.Explorations.Count].Name : "";
			var month = Layout.Labeled("star_exploration", name, NextRotation(_now), Palette.Arcane).Named("Month");
			month.MouseFilter = MouseFilterEnum.Stop;
			month.MouseDefaultCursorShape = CursorShape.PointingHand;
			void Explain()
			{
				var total = Core.Progression.Exploration.MonthlyTotal(Exploration);
				var names = Exploration.Explorations.Select(e => e.Name).Concat(Enumerable.Repeat("", 3)).ToList();
				var pays = T("exploration.rotation_total", Texts.Number(total.Essence), Texts.Number(total.Gold), total.Scrolls, total.Legendary, total.LightDark, total.Cores);
				Dialog.Info(month, T("exploration.rotation_title"), T("exploration.rotation_info", names[0], names[1], names[2], pays));
			}

			Press.On(month, Explain, Explain);
			var center = new CenterContainer { Name = "MonthRow" };
			center.AddChild(month);
			return center;
		}

		/// <summary>
		/// Uma aba por faixa do céu: "Boreais", com as constelações dela, ou a constelação em que ela abre. Toda
		/// faixa se abre para olhar: dá para estudar as Influências que vêm pela frente.
		/// </summary>
		private void RefreshTabs()
		{
			Layout.Clear(_tabs);
			var tabs = new TextTabs(height: 48, compact: true) { Name = "Tabs" };
			var reachable = Math.Min(Cleared + 1, Exploration.Constellations.Count);
			foreach (var hemisphere in Enum.GetValues<Hemisphere>())
			{
				var (first, last) = Exploration.Range(hemisphere);
				var reached = first >= 1 && first <= Math.Max(reachable, _selected);
				var detail = first < 1 ? "" : reached ? T("exploration.hemisphere_range", first, last) : T("exploration.hemisphere_opens", first);
				tabs.Add(Texts.Name(hemisphere), detail, $"hemisphere_{hemisphere.ToString().ToLowerInvariant()}", first >= 1).Name = hemisphere.ToString();
			}

			tabs.Select((int)_hemisphere);
			tabs.Changed += index =>
			{
				// A faixa nova abre na constelação a vencer dela (ou na primeira).
				_hemisphere = (Hemisphere)index;
				var (first, last) = Exploration.Range(_hemisphere);
				Select(Math.Clamp(Cleared + 1, first, last));
			};
			_tabs.AddChild(tabs);
		}

		private void RefreshChart()
		{
			Layout.Clear(_chart);
			_progress.Text = T("exploration.progress", Cleared, Exploration.Constellations.Count)
				+ (_player.ExplorationBest > 0 ? "  ·  " + T("exploration.best", _player.ExplorationBest) : "");
			var chart = new StarChart(Exploration, _hemisphere, Cleared, _selected) { Name = "Map" };
			chart.Chosen += number =>
			{
				Sfx.Play("constellation.star_activate");
				Select(number);
			};
			_chartScroll = Layout.Scroll(chart).Named("MapScroll");
			_chart.AddChild(_chartScroll);
			if (chart.OrbOf(_selected) is { } orb)
				Layout.Reveal(_chartScroll, orb);
		}

		/// <summary>
		/// Abre a constelação <paramref name="number"/>: a coluna da direita volta ao topo, como ao entrar
		/// na tela (na hora, sem animação), e a tela se refaz no fim do quadro.
		/// </summary>
		private void Select(int number)
		{
			_selected = number;
			_message.Text = "";
			if (_detailScroll != null)
				_detailScroll.ScrollVertical = 0;
			Callable.From(Refresh).CallDeferred();
		}

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			Layout.Clear(_actions);
			if (Exploration.Constellations.Count == 0)
				return;

			var number = _selected;
			var constellation = Exploration.Constellation(number);
			var cleared = Core.Progression.Exploration.IsCleared(_player, number, _now);

			var title = Layout.Row(Space.Large).Named("Header");
			// Sem o desenho da constelação (docs/exploracao_estelar_icones.md), o título não guarda o lugar dele.
			if (Art.Icon(constellation.Icon) is { } icon)
				title.AddChild(Doodle.Icon(icon, 56, Palette.Gold).Named("Icon"));
			var names = new VBoxContainer { Name = "Names", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			names.AddThemeConstantOverride("separation", Space.None);
			names.AddChild(new Label { Name = "Title", Text = constellation.Name, ThemeTypeVariation = GameTheme.Heading });
			names.AddChild(new Label { Name = "Latin", Text = $"{constellation.Latin} · {T("exploration.number", number, Exploration.Constellations.Count)}", ThemeTypeVariation = GameTheme.Faded });
			title.AddChild(names);
			if (cleared)
				title.AddChild(Layout.Labeled("confirm", "", T("exploration.cleared"), Palette.Spirit).Named("Cleared"));
			_detail.AddChild(title);

			_detail.AddChild(Influence(constellation));

			var encounter = constellation.Encounter(Variation);
			_detail.AddChild(Heading("EnemiesTitle", T("exploration.enemies", Texts.Stars(constellation.Stars), constellation.Level)));
			if (constellation.Guardian(Variation) is { } guardian)
				_detail.AddChild(Layout.Text(GuardianText(guardian), GameTheme.Faded).Named("Guardian"));
			var waves = Layout.Flow(22).Named("Waves");
			var challenge = constellation.Challenge(Variation);
			for (var i = 0; i < challenge.Waves.Count; i++)
			{
				var column = new VBoxContainer { Name = $"Wave{i + 1}" };
				column.AddThemeConstantOverride("separation", Space.Tight);
				var wave = new Label { Name = "Number", Text = T("campaign.wave", i + 1) };
				wave.AddThemeColorOverride("font_color", Palette.GoldDark.Lightened(0.35f));
				column.AddChild(wave);
				var foes = Layout.Row(Space.Small).Named("Foes");
				for (var k = 0; k < challenge.Waves[i].Count; k++)
					foes.AddChild(Foe(challenge.Waves[i][k], encounter).Named($"Foe{k + 1}"));
				column.AddChild(foes);
				waves.AddChild(column);
			}

			_detail.AddChild(waves);
			_detail.AddChild(Layout.Text(T("campaign.enemies_hint"), GameTheme.Faded).Named("EnemiesHint"));

			_detail.AddChild(Heading("RewardsTitle", T("exploration.rewards")));
			if (cleared)
				_detail.AddChild(Layout.Text(T("exploration.rewards_done"), GameTheme.Faded).Named("RewardsDone"));
			var rewards = Layout.Flow(Space.Medium).Named("Rewards");
			var reward = constellation.Reward;
			rewards.AddChild(Layout.Labeled("essence", reward.Essence.ToString(), T("currency.essence")));
			if (reward.Gold > 0)
				rewards.AddChild(Layout.Labeled("gold", reward.Gold.ToString(), T("currency.gold")).Named("Gold"));
			if (reward.Scrolls > 0)
				rewards.AddChild(Layout.Labeled("scroll", reward.Scrolls.ToString(), T("currency.scrolls_name")).Named("Scrolls"));
			BattleResultPanel.AddPrize(rewards, Core.Progression.Exploration.PrizeOf(reward));
			rewards.AddChild(Layout.Labeled("level_max", reward.Experience.ToString(), T("reward.experience")).Named("Experience"));
			if (cleared)
				rewards.Modulate = new Color(1, 1, 1, 0.5f);
			_detail.AddChild(rewards);

			_detail.AddChild(Heading("TeamTitle", T("common.team")));
			_detail.AddChild(new TeamStrip(_database, _player, Teams.Exploration));
			_detail.AddChild(Layout.Text(T("exploration.manual"), GameTheme.Faded).Named("ManualHint"));

			var problem = Core.Progression.Exploration.Check(_player, Exploration, number, _now);
			if (!Core.Progression.Exploration.IsOpen(_player, Exploration))
				_actions.AddChild(Refusal(T("exploration.closed", Exploration.UnlockStage)));
			else if (problem == EntryProblem.Locked)
				_actions.AddChild(Refusal(T("exploration.locked", number - 1)));

			var fight = GameButton.Of(T("common.fight"), () => FightRequested?.Invoke(number), ButtonKind.Primary, "fight", 68).Named("Fight");
			fight.Disabled = problem != EntryProblem.None;
			var row = Layout.Row(Space.Wide).Named("Buttons");
			row.AddChild(fight.Wide(220));
			row.AddChild(new Label { Name = "Free", Text = T("exploration.free"), ThemeTypeVariation = GameTheme.Faded, VerticalAlignment = VerticalAlignment.Center });
			_actions.AddChild(row);
		}

		/// <summary>
		/// A Influência: o nome (tocar explica o que é uma Influência), a explicação com como vencer, e cada
		/// regra com os números, dizendo em quem ela vale.
		/// </summary>
		private Control Influence(ConstellationDefinition constellation)
		{
			var box = new PanelContainer { Name = "Influence", ThemeTypeVariation = GameTheme.InsetPanel };
			var column = new VBoxContainer { Name = "Column" };
			column.AddThemeConstantOverride("separation", Space.Small);
			box.AddChild(column);

			var heading = Layout.Labeled("influence", "", T("exploration.influence", constellation.Influence.Name), Palette.Arcane).Named("Name");
			heading.MouseFilter = MouseFilterEnum.Stop;
			heading.MouseDefaultCursorShape = CursorShape.PointingHand;
			void Explain() => Dialog.Info(heading, T("exploration.influence", constellation.Influence.Name), T("exploration.influence_info"));
			Press.On(heading, Explain, Explain);
			column.AddChild(heading);
			column.AddChild(Layout.Text(T($"exploration.influence_text.{constellation.Id}")).Named("Text"));
			for (var i = 0; i < constellation.Influence.Rules.Count; i++)
			{
				var rule = constellation.Influence.Rules[i];
				var text = T("exploration.rule", $"[color=#{Palette.Gold.ToHtml(false)}]{Texts.Name(rule.Side)}[/color]", Texts.Describe(rule.Prepared, false));
				column.AddChild(RichText.Label(text).Named($"Rule{i + 1}"));
			}

			return box;
		}

		/// <summary>"Guardião: Pyrrhax, a forma desperta de Dragão de Fogo…" ou o nome do chefe.</summary>
		private string GuardianText(StageEnemy guardian)
		{
			if (guardian.Summon is { } id)
			{
				var summon = _database.Summon(id);
				return T("exploration.guardian", T("exploration.guardian_summon", summon.Awakening.Name, summon.Name));
			}

			return T("exploration.guardian", _database.Foe(guardian).Name);
		}

		/// <summary>Um inimigo: o retrato na cor do elemento (o guardião com a moldura acesa); tocar (ou segurar) abre o resumo dele.</summary>
		private Control Foe(StageEnemy slot, Encounter encounter)
		{
			var (_, image, element) = _database.Foe(slot);
			var boss = slot.Guardian || slot.Enemy != null && !_database.Enemy(slot.Enemy).Minion;
			var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, boss ? Palette.Gold : Palette.Of(element).Darkened(0.3f), boss ? 3 : 2, 30, 3));
			frame.AddChild(Layout.Medal(Art.Creature(image), Palette.Of(element), boss ? 60 : 48));
			void Open() => MonsterSummary.Open(frame, BattleFactory.Foe(_database, slot, encounter));
			Press.On(frame, Open, Open);
			return frame;
		}

		private static Label Refusal(string text)
		{
			var label = Layout.Text(text).Named("Refusal");
			label.AddThemeColorOverride("font_color", Palette.Negative);
			return label;
		}

		private static Label Heading(string name, string text)
		{
			var label = new Label { Name = name, Text = text };
			label.AddThemeColorOverride("font_color", Palette.Gold);
			label.AddThemeFontSizeOverride("font_size", 20);
			return label;
		}
	}
}
