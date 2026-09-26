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
	/// Equipes: uma para a Campanha e uma para cada Masmorra. Em cima as vagas da equipe escolhida (a
	/// primeira é a Líder); embaixo a coleção — clicar num monstro põe ou tira da equipe.
	/// </summary>
	public partial class TeamScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private string _content;

		private readonly HBoxContainer _contents = new();
		private readonly HBoxContainer _slots = new();
		private readonly VBoxContainer _leader = new();
		private readonly GridContainer _roster = new() { Columns = 9 };

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
			page.AddChild(Layout.Header(T("equipes.titulo"), null, T("geral.voltar"), () => BackRequested?.Invoke()));

			_contents.AddThemeConstantOverride("separation", 6);
			page.AddChild(_contents);

			var (teamPanel, team) = Layout.Section(T("equipes.vagas", PlayerState.TeamSize));
			var teamRow = new HBoxContainer();
			teamRow.AddThemeConstantOverride("separation", 16);
			_slots.AddThemeConstantOverride("separation", 8);
			teamRow.AddChild(_slots);
			_leader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			teamRow.AddChild(_leader);
			team.AddChild(teamRow);
			page.AddChild(teamPanel);

			var rosterPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			var rosterColumn = new VBoxContainer();
			rosterColumn.AddChild(new Label { Text = T("equipes.colecao"), ThemeTypeVariation = GameTheme.Heading });
			var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_roster.AddThemeConstantOverride("h_separation", 8);
			_roster.AddThemeConstantOverride("v_separation", 8);
			scroll.AddChild(_roster);
			rosterColumn.AddChild(scroll);
			rosterPanel.AddChild(rosterColumn);
			page.AddChild(rosterPanel);

			Refresh();
		}

		public void Refresh()
		{
			RefreshContents();
			RefreshTeam();
			RefreshRoster();
		}

		private void RefreshContents()
		{
			Layout.Clear(_contents);
			ContentButton(Teams.Campaign, T("equipes.campanha"), "campaign", true);
			foreach (var dungeon in _database.Dungeons)
				ContentButton(dungeon.Id, dungeon.Name, "dungeon", Dungeons.IsUnlocked(_player, dungeon));
		}

		private void ContentButton(string content, string title, string icon, bool open)
		{
			var button = Layout.IconButton(title, Art.Icon(icon), 24, content == _content ? Palette.Background : Palette.Gold);
			button.ToggleMode = true;
			button.ButtonPressed = content == _content;
			button.CustomMinimumSize = new Vector2(0, 40);
			button.TooltipText = open ? T("equipes.conteudo_dica", Teams.Of(_player, content).Count, PlayerState.TeamSize) : T("equipes.conteudo_fechado");
			button.Pressed += () =>
			{
				_content = content;
				Refresh();
			};
			_contents.AddChild(button);
		}

		private void RefreshTeam()
		{
			Layout.Clear(_slots);
			Layout.Clear(_leader);
			var team = Teams.Of(_player, _content).Select(_player.Monster).OfType<OwnedSummon>().ToList();
			for (var i = 0; i < PlayerState.TeamSize; i++)
			{
				var slot = new VBoxContainer();
				if (i < team.Count)
				{
					var monster = team[i];
					var card = new CreatureCard(_database.Summon(monster.SummonId), monster, i == 0 ? T("equipes.lider") : null, 110);
					card.TooltipText = T("equipes.tirar_dica");
					card.Pressed += c => ToggleRequested?.Invoke(_content, c.Monster!.Id);
					slot.AddChild(card);
					if (i > 0)
					{
						var leader = new Button { Text = T("equipes.tornar_lider") };
						leader.Pressed += () => LeaderRequested?.Invoke(_content, monster.Id);
						slot.AddChild(leader);
					}
				}
				else
				{
					var empty = new PanelContainer { CustomMinimumSize = new Vector2(110, 143), ThemeTypeVariation = GameTheme.InsetPanel };
					empty.AddChild(new Label { Text = T("equipes.vazio"), ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
					slot.AddChild(empty);
				}

				_slots.AddChild(slot);
			}

			var leaderSummon = team.Count > 0 ? _database.Summon(team[0].SummonId) : null;
			_leader.AddChild(new Label { Text = T("equipes.lideranca"), ThemeTypeVariation = GameTheme.Heading });
			_leader.AddChild(Layout.Text(leaderSummon?.Leader is { } skill
				? T("equipes.lideranca_texto", leaderSummon.NameFor(team[0].Awakened), Texts.Percent(skill.Value), Texts.Name(skill.Stat))
				: T("equipes.sem_lideranca"), GameTheme.Faded, 300));
			_leader.AddChild(Layout.Text(T("equipes.dica"), GameTheme.Faded, 300));
		}

		private void RefreshRoster()
		{
			Layout.Clear(_roster);
			var team = Teams.Of(_player, _content);
			var monsters = _player.Collection
				.Where(m => _database.HasSummon(m.SummonId))
				.OrderByDescending(m => team.Contains(m.Id))
				.ThenByDescending(m => _database.Summon(m.SummonId).Rarity)
				.ThenByDescending(m => m.Level)
				.ThenBy(m => m.Id);

			foreach (var monster in monsters)
			{
				var inTeam = team.Contains(monster.Id);
				var card = new CreatureCard(_database.Summon(monster.SummonId), monster, inTeam ? T("equipes.na_equipe") : null, 104);
				card.SetSelected(inTeam);
				card.Pressed += c => ToggleRequested?.Invoke(_content, c.Monster!.Id);
				_roster.AddChild(card);
			}
		}
	}
}
