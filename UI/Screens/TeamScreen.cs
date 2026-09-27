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
	/// Equipes: uma para a Campanha e uma para cada Masmorra, escolhidas pelos sigilos do cabeçalho (o
	/// chefe de cada Masmorra). Em cima as 5 vagas (a primeira é a Líder, com a coroa; a coroa embaixo
	/// das outras faz Líder) e a Liderança; embaixo a coleção — tocar num monstro põe ou tira da equipe.
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
		private readonly HBoxContainer _leader = Layout.Row(10).Named("Leadership");
		private readonly GridContainer _roster = new() { Name = "Roster", Columns = 10 };

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
			var (header, extra) = Layout.Header(T("destination.Teams"), "team", _currencies, () => BackRequested?.Invoke());
			extra.AddChild(_contents);
			page.AddChild(header);

			var teamPanel = new PanelContainer { Name = "Team" };
			var teamRow = Layout.Row(24).Named("Row");
			teamRow.AddChild(_slots);
			_leader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			teamRow.AddChild(_leader);
			teamPanel.AddChild(teamRow);
			page.AddChild(teamPanel);

			var rosterPanel = new PanelContainer { Name = "Collection", SizeFlagsVertical = SizeFlags.ExpandFill };
			_roster.AddThemeConstantOverride("h_separation", 10);
			_roster.AddThemeConstantOverride("v_separation", 10);
			rosterPanel.AddChild(Layout.Scroll(_roster));
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
			var tabs = new SigilTabs(vertical: false, 52) { Name = "Tabs" };
			var keys = new[] { Teams.Campaign }.Concat(_database.Dungeons.Select(d => d.Id)).ToList();
			AddTab(tabs, Teams.Campaign, Art.Icon("campaign"), T("teams.campaign"), true);
			foreach (var dungeon in _database.Dungeons)
				AddTab(tabs, dungeon.Id, Art.Creature(dungeon.Image), dungeon.Name, Dungeons.IsUnlocked(_player, dungeon));
			tabs.Select(Math.Max(0, keys.IndexOf(_content)));
			tabs.Changed += index =>
			{
				_content = keys[index];
				Callable.From(Refresh).CallDeferred();
			};
			_contents.AddChild(tabs);
		}

		private void AddTab(SigilTabs tabs, string content, Texture2D? icon, string name, bool open)
		{
			var count = Teams.Of(_player, content).Count;
			var tab = tabs.Add(icon, open ? name : T("teams.content_locked", name), $"{count}/{PlayerState.TeamSize}");
			tab.Name = Layout.NodeName(content);
			if (!open)
				tab.Ink = Palette.TextFaded;
		}

		private void RefreshTeam()
		{
			Layout.Clear(_slots);
			Layout.Clear(_leader);
			var team = Teams.Of(_player, _content).Select(_player.Monster).OfType<OwnedSummon>().ToList();
			for (var i = 0; i < PlayerState.TeamSize; i++)
			{
				var slot = new VBoxContainer { Name = $"Slot{i + 1}" };
				slot.AddThemeConstantOverride("separation", 6);
				if (i < team.Count)
				{
					var monster = team[i];
					var card = new CreatureCard(_database.Summon(monster.SummonId), monster, SlotWidth, i == 0 ? "leader" : null, i == 0 ? T("teams.leader") : null) { Name = "Card" };
					card.Pressed += c => ToggleRequested?.Invoke(_content, c.Monster!.Id);
					slot.AddChild(card);
					var crown = new CenterContainer { Name = "Crown", CustomMinimumSize = new Vector2(0, 44) };
					if (i > 0)
						crown.AddChild(SigilButton.Of("leader", T("teams.make_leader"), () => LeaderRequested?.Invoke(_content, monster.Id), 42).Named("MakeLeader"));
					slot.AddChild(crown);
				}
				else
				{
					var empty = new PanelContainer { Name = "Empty", CustomMinimumSize = new Vector2(SlotWidth, SlotWidth * 1.25f), ThemeTypeVariation = GameTheme.InsetPanel, TooltipText = T("teams.empty") };
					var plus = new Label { Name = "Plus", Text = "+", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
					plus.AddThemeFontSizeOverride("font_size", 40);
					plus.AddThemeColorOverride("font_color", new Color(Palette.GoldDark, 0.6f));
					empty.AddChild(plus);
					slot.AddChild(empty);
					slot.AddChild(new Control { Name = "Spacer", CustomMinimumSize = new Vector2(0, 44) });
				}

				_slots.AddChild(slot);
			}

			var leaderSummon = team.Count > 0 ? _database.Summon(team[0].SummonId) : null;
			if (leaderSummon?.Leader is not { } skill)
				return;
			var leaderIcon = Doodle.Icon(Art.Icon("leader"), 40, Palette.Gold).Named("Icon");
			leaderIcon.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			_leader.AddChild(leaderIcon);
			_leader.AddChild(RichText.Label(T("teams.leadership_text", leaderSummon.NameFor(team[0].Awakened), Texts.Percent(skill.Value), Texts.Name(skill.Stat)), 260).Named("Text"));
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
				var card = new CreatureCard(_database.Summon(monster.SummonId), monster, 96, inTeam ? "team" : null, inTeam ? T("teams.on_team") : null) { Name = $"Monster{monster.Id}" };
				card.SetSelected(inTeam);
				card.Pressed += c => ToggleRequested?.Invoke(_content, c.Monster!.Id);
				_roster.AddChild(card);
			}
		}
	}
}
