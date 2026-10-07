using System;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A Campanha: à esquerda as abas das regiões e a trilha das fases da aberta (<see cref="StagePath"/>),
	/// com o nome dela; uma região fechada diz em que fase abre. À direita a ficha da
	/// escolhida, tudo escrito — o nome, os inimigos (nível e estrelas, onda por onda; tocar num abre o
	/// resumo dele), o que a vitória rende, a equipe com o botão de editar, e embaixo os botões Lutar
	/// (com o custo em Mana) e Batalha automática, que só abre em fase já vencida (GDD, seção 7).
	/// </summary>
	public partial class CampaignScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly CenterContainer _path = new() { Name = "Path" };
		private readonly HBoxContainer _regions = new() { Name = "Regions", Alignment = BoxContainer.AlignmentMode.Center };
		private readonly Label _regionName = new() { Name = "RegionName", HorizontalAlignment = HorizontalAlignment.Center };
		private readonly VBoxContainer _detail = new() { Name = "Detail" };
		private readonly VBoxContainer _actions = new() { Name = "Actions" };
		private readonly Label _message = new() { Name = "Message", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private StageDefinition _selected;
		private int _region;

		public CampaignScreen(GameDatabase database, PlayerState player, int? selected = null)
		{
			_database = database;
			_player = player;
			_selected = database.Stage(selected ?? Math.Min(player.HighestStage + 1, database.Stages.Count));
			_region = Campaign.RegionOf(_selected.Number);
		}

		public event Action<StageDefinition>? FightRequested;

		/// <summary>A Batalha automática da fase: o jogador toca o botão, e o GameRoot abre a escolha de quantas lutas.</summary>
		public event Action<StageDefinition>? RepeatRequested;

		public event Action? TeamRequested;

		/// <summary>Falta Mana: o botão que leva à Loja.</summary>
		public event Action? ShopRequested;

		public event Action? BackRequested;

		/// <summary>A fase escolhida agora: a tela volta nela depois da luta ou da Loja.</summary>
		public int Selected => _selected.Number;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Campaign"), _currencies, () => BackRequested?.Invoke()).Header);

			var body = Layout.Row(20).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var pathPanel = new PanelContainer { Name = "Stages", CustomMinimumSize = new Vector2(540, 0) };
			var pathColumn = new VBoxContainer { Name = "PathColumn" };
			pathColumn.AddThemeConstantOverride("separation", 8);
			pathColumn.AddChild(_regions);
			_regionName.ThemeTypeVariation = GameTheme.Heading;
			_regionName.AddThemeColorOverride("font_color", Palette.Gold);
			pathColumn.AddChild(_regionName);
			_path.SizeFlagsVertical = SizeFlags.ExpandFill;
			pathColumn.AddChild(_path);
			pathPanel.AddChild(pathColumn);
			body.AddChild(pathPanel);

			var panel = new PanelContainer { Name = "Stage", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var column = new VBoxContainer { Name = "Column" };
			column.AddThemeConstantOverride("separation", 10);
			_detail.AddThemeConstantOverride("separation", 12);
			column.AddChild(Layout.Scroll(_detail));
			_actions.AddThemeConstantOverride("separation", 8);
			column.AddChild(_actions);
			panel.AddChild(column);
			body.AddChild(panel);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			page.AddChild(_message);

			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			RefreshPath();
			RefreshDetail();
		}

		/// <summary>Um aviso na faixa de baixo (o motivo de não poder lutar).</summary>
		public void ShowMessage(string text) => _message.Text = text;

		private void RefreshPath()
		{
			Layout.Clear(_path);
			var unlocked = _database.Stages.Where(s => Campaign.IsUnlocked(_player, s.Number)).Select(s => s.Number).DefaultIfEmpty(1).Max();
			RefreshRegions(unlocked);
			var (first, last) = Campaign.Region(_region, _database.Stages.Count);
			_regionName.Text = T($"campaign.region.{_region + 1}");
			var path = new StagePath(first, last, _player.HighestStage, unlocked, _selected.Number);
			path.Chosen += number =>
			{
				_selected = _database.Stage(number);
				_message.Text = "";
				Callable.From(Refresh).CallDeferred();
			};
			_path.AddChild(path.Named("Path"));
		}

		/// <summary>Uma aba por região: "Região 2", com as fases embaixo, ou a fase em que ela abre.</summary>
		private void RefreshRegions(int unlocked)
		{
			Layout.Clear(_regions);
			var tabs = new TextTabs(height: 48, compact: true) { Name = "Tabs" };
			for (var region = 0; region < Campaign.RegionStarts.Count; region++)
			{
				var (first, last) = Campaign.Region(region, _database.Stages.Count);
				var open = first <= unlocked;
				var detail = open ? T("campaign.region_stages", first, last) : T("campaign.region_opens", first);
				tabs.Add(T("campaign.region_tab", region + 1), detail, null, open).Name = $"Region{region + 1}";
			}

			tabs.Select(_region);
			tabs.Changed += region =>
			{
				// A região nova abre na fase a vencer dela (ou na última aberta).
				_region = region;
				var (first, last) = Campaign.Region(region, _database.Stages.Count);
				_selected = _database.Stage(Math.Clamp(Math.Min(_player.HighestStage + 1, last), first, last));
				_message.Text = "";
				Callable.From(Refresh).CallDeferred();
			};
			_regions.AddChild(tabs);
		}

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			Layout.Clear(_actions);

			var stage = _selected;
			var cleared = Campaign.IsCleared(_player, stage.Number);
			var title = Layout.Row(12).Named("Header");
			title.AddChild(new Label { Name = "Title", Text = T("battle.title_stage", stage.Number, stage.Name), ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			if (cleared)
				title.AddChild(Layout.Labeled("confirm", "", T("campaign.cleared"), Palette.Spirit).Named("Cleared"));
			_detail.AddChild(title);

			_detail.AddChild(Heading("EnemiesTitle", T("campaign.enemies", stage.Level, Texts.Stars(stage.Stars))));
			// As ondas lado a lado (o nome em cima, os inimigos embaixo): cabe a equipe sem rolar.
			var waves = Layout.Flow(22).Named("Waves");
			for (var i = 0; i < stage.Waves.Count; i++)
			{
				var column = new VBoxContainer { Name = $"Wave{i + 1}" };
				column.AddThemeConstantOverride("separation", 4);
				var wave = new Label { Name = "Number", Text = T("campaign.wave", i + 1) };
				wave.AddThemeColorOverride("font_color", Palette.GoldDark.Lightened(0.35f));
				column.AddChild(wave);
				var foes = Layout.Row(6).Named("Foes");
				for (var k = 0; k < stage.Waves[i].Count; k++)
					foes.AddChild(Foe(stage.Waves[i][k], stage.Encounter).Named($"Foe{k + 1}"));
				column.AddChild(foes);
				waves.AddChild(column);
			}

			_detail.AddChild(waves);

			_detail.AddChild(Layout.Text(T("campaign.enemies_hint"), GameTheme.Faded).Named("EnemiesHint"));

			_detail.AddChild(Heading("RewardsTitle", cleared ? T("campaign.rewards") : T("campaign.rewards_first")));
			var rewards = Layout.Flow(8).Named("Rewards");
			if (!cleared)
			{
				rewards.AddChild(Layout.Labeled("scroll", stage.FirstClearScrolls.ToString(), T("currency.scrolls_name")));
				rewards.AddChild(Layout.Labeled("essence", Texts.Number(stage.Essence + stage.FirstClearEssence), T("currency.essence")));
				rewards.AddChild(Layout.Labeled("rune", Texts.Stars(stage.RuneGrade), T("campaign.rune_always")));
				BattleResultPanel.AddPrize(rewards, Milestones.ForFirstClear(stage));
			}
			else
			{
				rewards.AddChild(Layout.Labeled("essence", Texts.Number(stage.Essence), T("currency.essence")));
				rewards.AddChild(Layout.Labeled("rune", Texts.Stars(stage.RuneGrade), T("campaign.rune_chance_short", Texts.Percent(Campaign.RepeatRuneChance))));
			}

			rewards.AddChild(Layout.Labeled("monster", Texts.Percent(Campaign.MonsterChance), T("campaign.monster_chance", Texts.Stars(Campaign.MonsterRarityDrop))).Named("Monster"));
			rewards.AddChild(Layout.Labeled("level_max", stage.Experience.ToString(), T("reward.experience")).Named("Experience"));
			_detail.AddChild(rewards);

			_detail.AddChild(Heading("TeamTitle", T("common.team")));
			_detail.AddChild(new TeamStrip(_database, _player, Teams.Campaign, () => TeamRequested?.Invoke()));

			var problem = Campaign.Check(_player, stage);
			var noTeam = Teams.Of(_player, Teams.Campaign).Count == 0;
			if (problem is EntryProblem.NoMana or EntryProblem.RunesFull)
			{
				var refusal = Layout.Text(Texts.Refusal(problem, stage.Mana)).Named("Refusal");
				refusal.AddThemeColorOverride("font_color", Palette.Negative);
				_actions.AddChild(refusal);
			}

			if (problem == EntryProblem.NoMana)
				_actions.AddChild(GameButton.Of(T("common.buy_mana"), () => ShopRequested?.Invoke(), ButtonKind.Secondary, "shop", 48).Named("Shop"));

			var row2 = Layout.Row(14).Named("Buttons");
			var fight = GameButton.Of(T("common.fight"), () => FightRequested?.Invoke(stage), ButtonKind.Primary, "fight", 68).WithCost("mana", stage.Mana.ToString()).Named("Fight");
			fight.Disabled = noTeam || problem != EntryProblem.None;
			row2.AddChild(fight.Wide(220));
			// A Batalha automática só aparece quando a Campanha a apresenta (Features); depois, em fase já vencida.
			var autoOpen = Features.IsOpen(_player, _database, Feature.AutoBattle);
			if (autoOpen)
			{
				var repeat = GameButton.Of(T("common.auto_battle"), () => RepeatRequested?.Invoke(stage), ButtonKind.Secondary, "repeat", 68).Named("AutoBattle");
				repeat.Disabled = !cleared || noTeam || problem != EntryProblem.None;
				row2.AddChild(repeat.Wide(240));
			}

			_actions.AddChild(row2);
			if (autoOpen && !cleared)
				_actions.AddChild(Layout.Text(T("campaign.auto_locked"), GameTheme.Faded).Named("AutoHint"));
		}

		/// <summary>Um inimigo: o retrato na cor do elemento; tocar (ou segurar) abre o resumo dele.</summary>
		private Control Foe(StageEnemy slot, Encounter encounter)
		{
			var (_, image, element) = _database.Foe(slot);
			var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Of(element).Darkened(0.3f), 2, 30, 3));
			frame.AddChild(Layout.Medal(Art.Creature(image), Palette.Of(element), 48));
			void Open() => MonsterSummary.Open(frame, BattleFactory.Foe(_database, slot, encounter));
			Press.On(frame, Open, Open);
			return frame;
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
