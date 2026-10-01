using System;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Equipes: uma para a Campanha e uma para cada Masmorra, nas abas escritas do alto (com quantos
	/// monstros cada uma tem). Em cima, as 5 vagas (a primeira é a Líder) e a Liderança; tocar num
	/// monstro da equipe abre o que dá para fazer com ele (tornar Líder, tirar da equipe). Embaixo, a
	/// coleção: tocar num monstro põe ou tira da equipe. Segurar qualquer monstro abre o resumo.
	/// </summary>
	public partial class TeamScreen : Control
	{
		private const float SlotWidth = 116;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private string _content;

		private readonly CurrencyBar _currencies = new();
		private readonly HBoxContainer _contents = Layout.Row(8).Named("Contents");
		private readonly HBoxContainer _slots = Layout.Row(12).Named("Slots");
		private readonly VBoxContainer _leader = new() { Name = "Leadership" };
		private readonly TileGrid _roster = new(10) { Name = "Roster" };

		public TeamScreen(GameDatabase database, PlayerState player, string content)
		{
			_database = database;
			_player = player;
			_content = content;
		}

		/// <summary>Conteúdo e monstro.</summary>
		public event Action<string, int>? ToggleRequested;

		/// <summary>Conteúdo e monstro que vira Líder.</summary>
		public event Action<string, int>? LeaderRequested;

		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Teams"), _currencies, () => BackRequested?.Invoke()).Header);
			// Muitas Masmorras não cabem numa fileira: as abas rolam de lado.
			var contents = new ScrollContainer
			{
				Name = "ContentsScroll",
				HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
				VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
				CustomMinimumSize = new Vector2(0, 66),
			};
			contents.AddChild(_contents);
			page.AddChild(contents);

			var teamPanel = new PanelContainer { Name = "Team" };
			var teamRow = Layout.Row(24).Named("Row");
			teamRow.AddChild(_slots);
			_leader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_leader.AddThemeConstantOverride("separation", 8);
			teamRow.AddChild(_leader);
			teamPanel.AddChild(teamRow);
			page.AddChild(teamPanel);

			var rosterPanel = new PanelContainer { Name = "Collection", SizeFlagsVertical = SizeFlags.ExpandFill };
			var rosterColumn = new VBoxContainer { Name = "Column" };
			rosterColumn.AddThemeConstantOverride("separation", 8);
			rosterColumn.AddChild(Layout.Text(T("teams.hint"), GameTheme.Faded).Named("Hint"));
			rosterColumn.AddChild(Layout.Scroll(_roster));
			rosterPanel.AddChild(rosterColumn);
			page.AddChild(rosterPanel);

			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			RefreshContents();
			RefreshTeam();
			RefreshRoster();
		}

		private void RefreshContents()
		{
			Layout.Clear(_contents);
			var tabs = new TextTabs { Name = "Tabs" };
			var keys = new[] { Teams.Campaign }.Concat(_database.Dungeons.Select(d => d.Id)).ToList();
			AddTab(tabs, Teams.Campaign, T("teams.campaign"), "campaign", true, 0);
			foreach (var dungeon in _database.Dungeons)
				AddTab(tabs, dungeon.Id, dungeon.Name, "dungeon", Dungeons.IsUnlocked(_player, dungeon), dungeon.UnlockStage);
			tabs.Select(Math.Max(0, keys.IndexOf(_content)));
			tabs.Changed += index =>
			{
				_content = keys[index];
				Callable.From(Refresh).CallDeferred();
			};
			_contents.AddChild(tabs);
		}

		private void AddTab(TextTabs tabs, string content, string name, string icon, bool open, int unlockStage)
		{
			var count = Teams.Of(_player, content).Count;
			var detail = open ? $"{count}/{PlayerState.TeamSize}" : T("teams.opens_at", unlockStage);
			tabs.Add(name, detail, icon, open).Name = Layout.NodeName(content);
		}

		private void RefreshTeam()
		{
			Layout.Clear(_slots);
			Layout.Clear(_leader);
			var team = Teams.Of(_player, _content).Select(_player.Monster).OfType<OwnedSummon>().ToList();
			for (var i = 0; i < PlayerState.TeamSize; i++)
			{
				var slot = new VBoxContainer { Name = $"Slot{i + 1}" };
				slot.AddThemeConstantOverride("separation", 4);
				if (i < team.Count)
				{
					var monster = team[i];
					var leader = i == 0;
					var card = new CreatureCard(_database.Summon(monster.SummonId), monster, SlotWidth, leader ? "leader" : null, leader ? T("teams.leader") : null) { Name = "Card" };
					card.Pressed += c => SlotActions(c, monster, leader);
					slot.AddChild(card);
				}
				else
				{
					var empty = new PanelContainer { Name = "Empty", CustomMinimumSize = new Vector2(SlotWidth, SlotWidth * 1.25f), ThemeTypeVariation = GameTheme.InsetPanel };
					var text = new Label { Name = "Text", Text = T("teams.empty"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
					text.AddThemeColorOverride("font_color", new Color(Palette.GoldDark, 0.9f));
					empty.AddChild(text);
					slot.AddChild(empty);
				}

				_slots.AddChild(slot);
			}

			_leader.AddChild(new Label { Name = "Title", Text = T("teams.leadership_title"), ThemeTypeVariation = GameTheme.Heading });
			var leaderSummon = team.Count > 0 ? _database.Summon(team[0].SummonId) : null;
			if (leaderSummon?.Leader is { } skill)
				_leader.AddChild(RichText.Label(T("teams.leadership_text", leaderSummon.NameFor(team[0].Awakened), Texts.Percent(skill.Value), Texts.Name(skill.Stat)), 300).Named("Text"));
			else
				_leader.AddChild(Layout.Text(team.Count == 0 ? T("teams.no_team") : T("teams.no_leadership"), GameTheme.Faded, 300).Named("Text"));
			_leader.AddChild(Layout.Text(T("teams.leader_hint"), GameTheme.Faded, 300).Named("Hint"));
		}

		/// <summary>O que fazer com um monstro da equipe: tornar Líder ou tirar.</summary>
		private void SlotActions(CreatureCard card, OwnedSummon monster, bool leader)
		{
			var summon = card.Summon;
			var dialog = Dialog.Open(card, summon.NameFor(monster.Awakened), 420, card, "SlotDialog");
			dialog.Body.AddChild(Layout.Text(leader ? T("teams.is_leader") : T("teams.slot_text"), GameTheme.Faded, 380).Named("Text"));
			var column = new VBoxContainer { Name = "Buttons" };
			column.AddThemeConstantOverride("separation", 10);
			if (!leader)
				column.AddChild(GameButton.Of(T("teams.make_leader"), () =>
				{
					dialog.Close();
					LeaderRequested?.Invoke(_content, monster.Id);
				}, ButtonKind.Primary, "leader").Named("MakeLeader"));
			column.AddChild(GameButton.Of(T("teams.remove"), () =>
			{
				dialog.Close();
				ToggleRequested?.Invoke(_content, monster.Id);
			}, ButtonKind.Secondary, "cancel").Named("Remove"));
			column.AddChild(GameButton.Of(T("teams.details"), () =>
			{
				dialog.Close();
				MonsterSummary.Open(card, summon, monster);
			}, ButtonKind.Secondary, "stats").Named("Details"));
			dialog.Body.AddChild(column);
		}

		private void RefreshRoster()
		{
			Layout.Clear(_roster);
			var team = Teams.Of(_player, _content);
			var monsters = _player.Collection
				.Where(m => _database.HasSummon(m.SummonId))
				.OrderByDescending(m => team.Contains(m.Id))
				.ThenByDescending(m => m.Stars)
				.ThenByDescending(m => _database.Summon(m.SummonId).Rarity)
				.ThenByDescending(m => m.Level)
				.ThenBy(m => m.Id);

			foreach (var monster in monsters)
			{
				var inTeam = team.Contains(monster.Id);
				var card = new CreatureCard(_database.Summon(monster.SummonId), monster, 100, null, inTeam ? T("teams.on_team") : null) { Name = $"Monster{monster.Id}" };
				card.SetSelected(inTeam);
				card.Pressed += c => ToggleRequested?.Invoke(_content, c.Monster!.Id);
				_roster.AddChild(card);
			}
		}
	}
}
