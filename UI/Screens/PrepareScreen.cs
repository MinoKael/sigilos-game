using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Montagem antes da luta (GDD, seção 7): até 4 invocações (a primeira é a Líder), as páginas do
	/// Grimório em ordem de prioridade e a Postura de Éter do automático. Montar o time é escolher o
	/// vocabulário do Conjurador: página cujo Glifo não está no time não entra.
	///
	/// A tela edita uma cópia e só entrega o resultado em <see cref="Confirmed"/>.
	/// </summary>
	public partial class PrepareScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly ConjurerDefinition _conjurer;
		private readonly List<string> _team;
		private readonly List<string> _pages;
		private Posture _posture;
		private SummonDefinition? _focused;

		private readonly HBoxContainer _slots = new();
		private readonly GridContainer _roster = new() { Columns = 6 };
		private readonly VBoxContainer _detail = new();
		private readonly VBoxContainer _grimoire = new();

		public PrepareScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
			_conjurer = database.Conjurer(player.ConjurerId);
			_team = player.Team.Where(id => database.HasSummon(id) && player.Owns(id)).ToList();
			_pages = player.Grimoire.Where(database.HasPage).ToList();
			_posture = player.Posture;
			_focused = _team.Count > 0 ? database.Summon(_team[0]) : null;
		}

		public event Action<IReadOnlyList<string>, IReadOnlyList<string>, Posture>? Confirmed;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			var header = new HBoxContainer();
			header.AddChild(new Label { Text = "Time e Grimório", ThemeTypeVariation = GameTheme.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var cancel = new Button { Text = "Cancelar" };
			cancel.Pressed += () => BackRequested?.Invoke();
			var confirm = new Button { Text = "Confirmar" };
			confirm.Pressed += () => Confirmed?.Invoke(_team, _pages, _posture);
			header.AddChild(cancel);
			header.AddChild(confirm);
			page.AddChild(header);

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 16);
			page.AddChild(body);

			var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			body.AddChild(left);

			var (teamPanel, team) = Layout.Section("Time · a primeira é a Líder");
			_slots.AddThemeConstantOverride("separation", 10);
			team.AddChild(_slots);
			left.AddChild(teamPanel);

			var (rosterPanel, roster) = Layout.Section("Coleção · clique para pôr ou tirar do time");
			rosterPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
			var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_roster.AddThemeConstantOverride("h_separation", 8);
			_roster.AddThemeConstantOverride("v_separation", 8);
			scroll.AddChild(_roster);
			roster.AddChild(scroll);
			left.AddChild(rosterPanel);

			var right = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
			body.AddChild(right);

			var (detailPanel, detail) = Layout.Section("Invocação");
			detailPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
			var detailScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			detailScroll.AddChild(_detail);
			detail.AddChild(detailScroll);
			right.AddChild(detailPanel);

			var (grimoirePanel, grimoire) = Layout.Section($"Grimório do {_conjurer.Name} · até {_conjurer.PageSlots} páginas");
			grimoirePanel.SizeFlagsVertical = SizeFlags.ExpandFill;
			var grimoireScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_grimoire.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			grimoireScroll.AddChild(_grimoire);
			grimoire.AddChild(grimoireScroll);
			grimoire.AddChild(PostureRow());
			right.AddChild(grimoirePanel);

			Rebuild();
		}

		private void Rebuild()
		{
			RebuildSlots();
			RebuildRoster();
			RebuildDetail();
			RebuildGrimoire();
		}

		private void RebuildSlots()
		{
			Layout.Clear(_slots);
			for (var i = 0; i < PlayerState.TeamSize; i++)
			{
				if (i < _team.Count)
				{
					var card = new CreatureCard(_database.Summon(_team[i]), _player.Echoes(_team[i]), i == 0 ? "Líder" : null, 110);
					card.Pressed += c => Toggle(c.Summon);
					_slots.AddChild(card);
				}
				else
				{
					var empty = new PanelContainer { CustomMinimumSize = new Vector2(110, 143) };
					empty.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.ParchmentDark, Palette.InkFaded, 1, 6, 6));
					empty.AddChild(new Label { Text = "vazio", ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
					_slots.AddChild(empty);
				}
			}
		}

		private void RebuildRoster()
		{
			Layout.Clear(_roster);
			var owned = _database.Summons
				.Where(s => _player.Owns(s.Id))
				.OrderByDescending(s => s.Rarity)
				.ThenBy(s => s.FamilyId)
				.ThenBy(s => s.Element);

			foreach (var summon in owned)
			{
				var card = new CreatureCard(summon, _player.Echoes(summon.Id), width: 110);
				card.SetSelected(_team.Contains(summon.Id));
				card.Pressed += c => Toggle(c.Summon);
				_roster.AddChild(card);
			}
		}

		private void RebuildDetail()
		{
			Layout.Clear(_detail);
			if (_focused is not { } summon)
			{
				_detail.AddChild(new Label { Text = "Escolha uma invocação.", ThemeTypeVariation = GameTheme.Faded });
				return;
			}

			var stats = Growth.Stats(_database.Roles[summon.Role], summon.Rarity, _player.Level, _player.Echoes(summon.Id));
			Add(_detail, $"{summon.Name}  {Texts.Stars(summon.Rarity)}", GameTheme.Heading);
			Add(_detail, $"{Texts.Name(summon.Element)} · Glifo {Texts.Name(summon.Glyph)} · {Texts.Name(summon.Role)}");
			Add(_detail, $"Nível {_player.Level}: Vida {stats.Health:0} · Ataque {stats.Attack:0} · Defesa {stats.Defense:0} · Velocidade {stats.Speed:0}", GameTheme.Faded);
			Add(_detail, $"Básico · {summon.Basic.Name}");
			Add(_detail, Texts.Describe(summon.Basic), GameTheme.Faded);
			Add(_detail, $"Glifo · {summon.GlyphSkill.Name} (recarga {summon.GlyphSkill.Cooldown})");
			Add(_detail, Texts.Describe(summon.GlyphSkill), GameTheme.Faded);
			Add(_detail, $"Assinatura · {summon.Family.Passive.Name}");
			Add(_detail, PassiveText(summon.Family.Passive), GameTheme.Faded);
			if (summon.Leader is { } leader)
				Add(_detail, $"Liderança · +{leader.Value * 100:0}% de {Texts.Name(leader.Stat)} para o time");
		}

		private void RebuildGrimoire()
		{
			Layout.Clear(_grimoire);
			var glyphCounts = _team.Select(id => _database.Summon(id).Glyph).GroupBy(g => g).ToDictionary(g => g.Key, g => g.Count());

			foreach (var page in _database.Pages.OrderBy(p => p.Glyph).ThenBy(p => p.Circle))
			{
				var count = glyphCounts.TryGetValue(page.Glyph, out var n) ? n : 0;
				var resonant = count > 0;
				var index = _pages.IndexOf(page.Id);
				var cost = PageFormula.Cost(page, _conjurer, count);

				var row = new HBoxContainer { TooltipText = Texts.Describe(page) };
				var check = new CheckBox
				{
					ButtonPressed = index >= 0,
					Disabled = index < 0 && (!resonant || _pages.Count >= _conjurer.PageSlots),
					Text = index >= 0 ? $"{index + 1}." : "",
					CustomMinimumSize = new Vector2(52, 0),
				};
				check.Toggled += on => TogglePage(page.Id, on);
				row.AddChild(check);
				row.AddChild(Doodle.Icon(Art.Glyph(page.Glyph), 22, resonant ? Palette.Ink : Palette.InkFaded));
				var label = new Label
				{
					Text = $"{page.Name} · {Texts.Name(page.Form)} · Círculo {Texts.Circle(page.Circle)} · {cost} Éter{(resonant ? "" : " · sem Ressonância")}",
					MouseFilter = MouseFilterEnum.Pass,
				};
				if (!resonant)
					label.ThemeTypeVariation = GameTheme.Faded;
				row.AddChild(label);
				_grimoire.AddChild(row);
			}
		}

		private Control PostureRow()
		{
			var row = new HBoxContainer();
			row.AddChild(new Label { Text = "Postura de Éter do automático:" });
			var options = new OptionButton();
			foreach (var posture in Enum.GetValues<Posture>())
				options.AddItem(Texts.Name(posture), (int)posture);
			options.Selected = (int)_posture;
			options.TooltipText = "Agressiva gasta em aprimoramentos; Econômica guarda para o Círculo III; Equilibrada fica no meio.";
			options.ItemSelected += index => _posture = (Posture)options.GetItemId((int)index);
			row.AddChild(options);
			return row;
		}

		private void Toggle(SummonDefinition summon)
		{
			_focused = summon;
			if (_team.Contains(summon.Id))
				_team.Remove(summon.Id);
			else if (_team.Count < PlayerState.TeamSize)
				_team.Add(summon.Id);
			Rebuild();
		}

		private void TogglePage(string id, bool on)
		{
			if (on && !_pages.Contains(id) && _pages.Count < _conjurer.PageSlots)
				_pages.Add(id);
			else if (!on)
				_pages.Remove(id);
			Callable.From(RebuildGrimoire).CallDeferred();
		}

		private static string PassiveText(PassiveDefinition passive) => passive.Kind switch
		{
			PassiveKind.SpeedWhenLowest => $"+{passive.Value * 100:0}% de Velocidade enquanto for o aliado com menos Vida.",
			PassiveKind.ShieldOnDeath => $"Ao cair, dá escudo de {passive.Value * 100:0}% da própria Vida a todos os aliados.",
			PassiveKind.RebirthOnce => $"Na primeira vez que cai, renasce com {passive.Value * 100:0}% da Vida no turno seguinte dela.",
			_ => "",
		};

		private static void Add(VBoxContainer parent, string text, string? variation = null)
		{
			var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(400, 0) };
			if (variation != null)
				label.ThemeTypeVariation = variation;
			parent.AddChild(label);
		}
	}
}
