using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Monstros: à esquerda a grade de cartões (coleção ou Baú); à direita a ficha do escolhido, com a
	/// régua de abas em pé na borda — Atributos, Runas, Habilidades, Despertar.
	///
	/// - Atributos: retrato, estrelas, nível com a barra de experiência, as ações (subir nível, evoluir,
	///   Baú, liberar) e a ficha (base + runas).
	/// - Runas: as 6 no círculo e os conjuntos ativos; tocar leva à tela de Runas.
	/// - Habilidades: o Glifo, a recarga e os níveis de cada uma, a Liderança e o sigilo de fundir
	///   (uma cópia sobe uma habilidade sorteada).
	/// - Despertar: o desenho de agora e o desperto, o que ganha e o sigilo de despertar.
	///
	/// O sigilo de seleção marca vários cartões (nas duas abas) para fundir no monstro da ficha ou
	/// liberar de uma vez. Cada sigilo vira um evento; o GameRoot aplica a regra e chama <see cref="Refresh"/>.
	/// </summary>
	public partial class StorageScreen : Control
	{
		private const float DetailWidth = 560;

		private enum Page
		{
			Stats,
			Runes,
			Skills,
			Awaken,
		}

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private int? _selected;
		private bool _showStorage;
		private bool _selecting;
		private Page _page;
		private readonly HashSet<int> _marked = new();

		private readonly CurrencyBar _currencies = new();
		private readonly Label _count = new() { ThemeTypeVariation = GameTheme.Number, VerticalAlignment = VerticalAlignment.Center };
		private readonly HBoxContainer _headerTools = Layout.Row(8);
		private readonly HBoxContainer _selection = Layout.Row(8);
		private readonly GridContainer _roster = new() { Columns = 5 };
		private readonly VBoxContainer _detail = new();
		private readonly SigilTabs _pages = new(vertical: true, 60);

		public StorageScreen(GameDatabase database, PlayerState player, int? selected)
		{
			_database = database;
			_player = player;
			_selected = player.Monster(selected ?? -1)?.Id ?? player.Collection.FirstOrDefault()?.Id;
			_showStorage = player.Monster(_selected ?? -1)?.Stored ?? false;
		}

		/// <summary>Id e se é até o nível máximo (falso = só o próximo nível).</summary>
		public event Action<int, bool>? InfuseRequested;

		public event Action<int>? AwakenRequested;
		public event Action<int>? EvolveRequested;
		public event Action<int>? RunesRequested;
		public event Action<int>? StoreRequested;
		public event Action<int>? RetrieveRequested;

		/// <summary>Alvo e materiais, na ordem em que sobem habilidades.</summary>
		public event Action<int, IReadOnlyList<int>>? FuseRequested;

		public event Action<IReadOnlyList<int>>? ReleaseRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			var (header, extra) = Layout.Header(T("destination.Monsters"), "storage", _currencies, () => BackRequested?.Invoke());
			extra.AddChild(_count);
			extra.AddChild(new Control { CustomMinimumSize = new Vector2(12, 0) });
			extra.AddChild(_headerTools);
			page.AddChild(header);

			var body = Layout.Row(16);
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var rosterPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var rosterColumn = new VBoxContainer();
			rosterColumn.AddThemeConstantOverride("separation", 10);
			rosterPanel.AddChild(rosterColumn);
			rosterColumn.AddChild(_selection);
			_roster.AddThemeConstantOverride("h_separation", 10);
			_roster.AddThemeConstantOverride("v_separation", 10);
			rosterColumn.AddChild(Layout.Scroll(_roster));
			body.AddChild(rosterPanel);

			var detailPanel = new PanelContainer { CustomMinimumSize = new Vector2(DetailWidth, 0) };
			var detailRow = Layout.Row(10);
			_detail.AddThemeConstantOverride("separation", 10);
			detailRow.AddChild(Layout.Scroll(_detail));
			_pages.Add(Art.Icon("stats"), T("monsters.page.stats"));
			_pages.Add(Art.Icon("rune"), T("monsters.page.runes"));
			_pages.Add(Art.Icon("skill"), T("monsters.page.skills"));
			_pages.Add(Art.Icon("awaken"), T("monsters.page.awaken"));
			_pages.Changed += index =>
			{
				_page = (Page)index;
				RefreshDetail();
			};
			detailRow.AddChild(_pages);
			detailPanel.AddChild(detailRow);
			body.AddChild(detailPanel);

			Refresh();
		}

		public void Refresh()
		{
			if (_selected is { } id && _player.Monster(id) == null)
				_selected = _player.Collection.FirstOrDefault()?.Id;
			_marked.RemoveWhere(marked => _player.Monster(marked) == null);

			_currencies.Refresh(_player);
			_count.Text = _showStorage
				? _player.Storage.Count().ToString()
				: $"{_player.Collection.Count()}/{PlayerState.CollectionCapacity}";
			RefreshTools();
			RefreshSelection();
			RefreshRoster();
			RefreshDetail();
		}

		// Cabeçalho e seleção ------------------------------------------------------------------------

		private void RefreshTools()
		{
			Layout.Clear(_headerTools);
			var tabs = new SigilTabs(vertical: false, 48);
			tabs.Add(Art.Icon("storage"), T("monsters.collection"));
			tabs.Add(Art.Icon("chest"), T("monsters.vault"), _player.Storage.Count().ToString());
			tabs.Select(_showStorage ? 1 : 0);
			tabs.Changed += index =>
			{
				_showStorage = index == 1;
				Callable.From(Refresh).CallDeferred();
			};
			_headerTools.AddChild(tabs);

			var select = new SigilButton(Art.Icon("select"), T("monsters.select"), 48, SigilShape.Square) { ToggleMode = true, ButtonPressed = _selecting };
			select.Toggled += on =>
			{
				_selecting = on;
				_marked.Clear();
				Callable.From(Refresh).CallDeferred();
			};
			_headerTools.AddChild(select);
		}

		/// <summary>A barra da seleção em massa: marcar cópias, desmarcar, fundir no monstro da ficha, liberar.</summary>
		private void RefreshSelection()
		{
			Layout.Clear(_selection);
			_selection.Visible = _selecting;
			if (!_selecting)
				return;

			var marked = _marked.Select(_player.Monster).OfType<OwnedSummon>().ToList();
			_selection.AddChild(Layout.Chip("select", marked.Count.ToString(), T("monsters.marked")));

			var target = _player.Monster(_selected ?? -1);
			if (target != null)
			{
				var name = _database.Summon(target.SummonId).NameFor(target.Awakened);
				_selection.AddChild(Action("copies", T("monsters.mark_copies", name), false, () =>
				{
					foreach (var copy in _player.Monsters.Where(m => m.SummonId == target.SummonId && m.Id != target.Id))
						_marked.Add(copy.Id);
					Refresh();
				}));
			}

			_selection.AddChild(Action("cancel", T("monsters.unmark"), marked.Count == 0, () =>
			{
				_marked.Clear();
				Refresh();
			}));

			if (target != null)
				_selection.AddChild(FuseMarked(target, marked));

			var fragments = marked.Sum(m => Fusion.FragmentsFor(_database.Summon(m.SummonId).Rarity));
			var release = Action("release", T("monsters.release_marked", marked.Count, fragments), marked.Count == 0, () => SigilDialog.Ask(this,
				T("monsters.release_many_confirm", marked.Count, fragments) + Warning(marked),
				() => ReleaseRequested?.Invoke(marked.Select(m => m.Id).ToList())));
			release.Badge = marked.Count > 0 ? fragments.ToString() : "";
			_selection.AddChild(release);
		}

		/// <summary>Funde os marcados no monstro da ficha: só cópias da mesma variante, uma habilidade sorteada sobe por cópia, as mais fracas primeiro.</summary>
		private SigilButton FuseMarked(OwnedSummon target, IReadOnlyList<OwnedSummon> marked)
		{
			var name = _database.Summon(target.SummonId).NameFor(target.Awakened);
			var room = Fusion.SkillUpsLeft(_database, target);
			var valid = marked.Count > 0 && room > 0 && marked.All(m => Fusion.CanFuse(_player, _database, target.Id, m.Id));
			var materials = marked
				.OrderBy(m => m.Awakened)
				.ThenBy(m => m.Stars)
				.ThenBy(m => m.Level)
				.Take(Math.Max(0, room))
				.ToList();

			var tip = valid ? T("monsters.fuse_marked", name, materials.Count, room) : T("monsters.fuse_marked_invalid", name, room);
			var button = Action("fuse", tip, !valid, () => SigilDialog.Ask(this,
				T("monsters.fuse_many_confirm", materials.Count, name, room) + Warning(materials),
				() => FuseRequested?.Invoke(target.Id, materials.Select(m => m.Id).ToList())));
			button.Badge = valid ? materials.Count.ToString() : "";
			return button;
		}

		/// <summary>Aviso quando a seleção leva monstro desperto, evoluído, com nível, com habilidade subida, em equipe ou com runas.</summary>
		private string Warning(IEnumerable<OwnedSummon> monsters) =>
			monsters.Any(m => m.Awakened || m.Level > 1 || m.Stars > _database.Summon(m.SummonId).Rarity || m.SkillLevels.Any(l => l > 1) || TeamIcons(m.Id).Count > 0 || _player.RunesOn(m.Id).Count > 0)
				? "\n\n" + T("monsters.valuable_warning")
				: "";

		private void RefreshRoster()
		{
			Layout.Clear(_roster);
			var monsters = (_showStorage ? _player.Storage : _player.Collection)
				.Where(m => _database.HasSummon(m.SummonId))
				.OrderByDescending(m => m.Stars)
				.ThenByDescending(m => _database.Summon(m.SummonId).Rarity)
				.ThenByDescending(m => m.Level)
				.ThenBy(m => _database.Summon(m.SummonId).Element)
				.ThenBy(m => m.Id)
				.ToList();

			foreach (var monster in monsters)
			{
				var teams = TeamIcons(monster.Id);
				var card = new CreatureCard(_database.Summon(monster.SummonId), monster, 104,
					teams.Count > 0 ? "team" : null,
					teams.Count > 0 ? T("monsters.teams", string.Join(", ", teams.Select(t => t.Name))) : null);
				card.SetSelected(monster.Id == _selected);
				card.SetMarked(_marked.Contains(monster.Id));
				card.Pressed += c =>
				{
					var clicked = c.Monster!.Id;
					if (_selecting && clicked != _selected)
					{
						if (!_marked.Remove(clicked))
							_marked.Add(clicked);
					}
					else
					{
						_selected = clicked;
					}

					Refresh();
				};
				_roster.AddChild(card);
			}

			if (monsters.Count == 0)
			{
				var empty = Doodle.Icon(Art.Icon(_showStorage ? "chest" : "summon"), 96, new Color(Palette.GoldDark, 0.5f));
				empty.TooltipText = T(_showStorage ? "monsters.vault_empty" : "monsters.collection_empty");
				empty.MouseFilter = MouseFilterEnum.Stop;
				_roster.AddChild(empty);
			}
		}

		// Ficha ------------------------------------------------------------------------------------

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			_pages.Visible = _selected != null;
			if (_selected is not { } id || _player.Monster(id) is not { } monster)
				return;

			var summon = _database.Summon(monster.SummonId);
			_detail.AddChild(Identity(summon, monster));
			switch (_page)
			{
				case Page.Stats:
					StatsPage(summon, monster);
					break;
				case Page.Runes:
					RunesPage(summon, monster);
					break;
				case Page.Skills:
					SkillsPage(summon, monster);
					break;
				default:
					AwakenPage(summon, monster);
					break;
			}
		}

		/// <summary>Retrato, nome, estrelas, nível e barra de experiência, elemento e papel: igual em toda aba.</summary>
		private Control Identity(SummonDefinition summon, OwnedSummon monster)
		{
			var row = Layout.Row(14);

			var frame = new PanelContainer { CustomMinimumSize = new Vector2(120, 120), TooltipText = T("monsters.natural_stars", summon.Rarity) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 3, 10, 8));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.ImageFor(monster.Awakened)), Palette.Of(summon.Element), MaskShape.Rounded, 6));
			row.AddChild(frame);

			var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			info.AddThemeConstantOverride("separation", 4);
			var name = new Label { Text = summon.NameFor(monster.Awakened), ThemeTypeVariation = GameTheme.Heading };
			if (monster.Awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			info.AddChild(name);

			var line = Layout.Row(8);
			var stars = new Label { Text = Texts.Stars(monster.Stars) };
			stars.AddThemeColorOverride("font_color", Palette.Stars(monster.Awakened));
			stars.AddThemeFontSizeOverride("font_size", 18);
			line.AddChild(stars);
			var element = Doodle.Icon(Art.Element(summon.Element), 22, Palette.Of(summon.Element));
			element.TooltipText = T("monsters.element_role", Texts.Name(summon.Element), Texts.Name(summon.Role));
			element.MouseFilter = MouseFilterEnum.Stop;
			line.AddChild(element);
			line.AddChild(new Label { Text = Texts.Name(summon.Role), ThemeTypeVariation = GameTheme.Faded, VerticalAlignment = VerticalAlignment.Center });
			foreach (var (icon, teamName) in TeamIcons(monster.Id))
			{
				var team = Doodle.Icon(Art.Icon(icon), 20, Palette.Gold);
				team.TooltipText = teamName;
				team.MouseFilter = MouseFilterEnum.Stop;
				line.AddChild(team);
			}

			if (monster.Stored)
			{
				var vault = Doodle.Icon(Art.Icon("chest"), 20, Palette.TextFaded);
				vault.TooltipText = T("monsters.in_vault");
				vault.MouseFilter = MouseFilterEnum.Stop;
				line.AddChild(vault);
			}

			info.AddChild(line);

			var max = Leveling.MaxLevel(monster);
			var level = Layout.Row(8);
			var levelText = new Label { Text = T("monsters.level", monster.Level, max), ThemeTypeVariation = GameTheme.Number };
			level.AddChild(levelText);
			var bar = Layout.Energy(Palette.Gold, 10);
			bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			bar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			bar.MaxValue = Math.Max(1, Leveling.ExperienceToNext(monster));
			bar.Value = Leveling.IsMaxLevel(monster) ? bar.MaxValue : monster.Experience;
			bar.TooltipText = Leveling.IsMaxLevel(monster) ? T("monsters.level_max") : T("monsters.experience", monster.Experience, Leveling.ExperienceToNext(monster));
			level.AddChild(bar);
			info.AddChild(level);
			row.AddChild(info);
			return row;
		}

		private void StatsPage(SummonDefinition summon, OwnedSummon monster)
		{
			var id = monster.Id;
			var actions = Layout.Flow(10);

			if (!Leveling.IsMaxLevel(monster))
			{
				var (next, full) = Leveling.InfuseCosts(monster);
				var one = Action("essence", T("monsters.level_up", next), _player.Essence < next, () => InfuseRequested?.Invoke(id, false));
				one.Badge = "+1";
				actions.AddChild(one);
				var all = Action("level_max", T("monsters.max_level_button", Leveling.MaxLevel(monster), full), _player.Essence <= 0, () => InfuseRequested?.Invoke(id, true));
				all.Badge = Texts.Short(full);
				actions.AddChild(all);
			}
			else if (Evolution.IsReady(monster))
			{
				var (essenceCost, fragmentCost) = Evolution.Cost(monster.Stars);
				var evolve = Action("evolve", T("monsters.evolve", Texts.Stars(monster.Stars + 1), essenceCost, fragmentCost, Growth.MaxLevel(monster.Stars + 1)),
					!Evolution.CanEvolve(_player, monster), () => EvolveRequested?.Invoke(id));
				evolve.Badge = Texts.Short(essenceCost);
				evolve.Highlight = Evolution.CanEvolve(_player, monster);
				actions.AddChild(evolve);
			}

			if (!monster.Stored)
			{
				actions.AddChild(Action("chest", T("monsters.store"), false, () => StoreRequested?.Invoke(id)));
			}
			else
			{
				var full = Roster.IsFull(_player);
				actions.AddChild(Action("retrieve", full ? T("monsters.collection_full") : T("monsters.take_from_vault"), full, () => RetrieveRequested?.Invoke(id)));
			}

			var fragments = Fusion.FragmentsFor(summon.Rarity);
			var release = Action("release", T("monsters.release", fragments), false, () => SigilDialog.Ask(this,
				T("monsters.release_confirm", summon.NameFor(monster.Awakened), monster.Level, fragments),
				() => ReleaseRequested?.Invoke(new[] { id })));
			release.Badge = fragments.ToString();
			actions.AddChild(release);
			_detail.AddChild(actions);

			_detail.AddChild(new HSeparator());
			var sheet = SummonStats.For(_database.Roles[summon.Role], summon, monster.Stars, monster.Level, monster.Awakened, _player.RunesOn(monster.Id));
			var table = new StatTable();
			table.Show(sheet);
			_detail.AddChild(table);
		}

		private void RunesPage(SummonDefinition summon, OwnedSummon monster)
		{
			var runes = _player.RunesOn(monster.Id);
			var sheet = SummonStats.For(_database.Roles[summon.Role], summon, monster.Stars, monster.Level, monster.Awakened, runes);

			var ring = new SigilRing(340) { Spread = 0.72f, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
			var tiles = new List<Control>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(runes.FirstOrDefault(r => r.Slot == slot), slot, 1.25f);
				tile.Pressed += _ => RunesRequested?.Invoke(monster.Id);
				tiles.Add(tile);
			}

			var open = SigilButton.Of("rune", T("monsters.runes_button"), () => RunesRequested?.Invoke(monster.Id), 76);
			ring.Set(open, tiles);
			_detail.AddChild(ring);

			foreach (var set in sheet.Runes.ActiveSets)
			{
				var row = Layout.Row(8);
				row.AddChild(new RuneGlyph(RuneSets.For(set.Set).Glyph, 24, Palette.Gold));
				row.AddChild(RichText.Label($"{Texts.Term(set.Set)}: {Texts.Describe(set)}", 440));
				_detail.AddChild(row);
			}
		}

		private void SkillsPage(SummonDefinition summon, OwnedSummon monster)
		{
			var skills = summon.AllSkills;
			for (var i = 0; i < skills.Count; i++)
			{
				var locked = !monster.Awakened && i >= summon.Skills.Count;
				_detail.AddChild(SkillRow.Build(skills[i], monster.SkillLevel(i), monster.Awakened, locked));
			}

			if (summon.Leader is { } leader)
			{
				var row = Layout.Row(10);
				row.AddChild(Doodle.Icon(Art.Icon("leader"), 32, Palette.Gold));
				row.AddChild(RichText.Label(T("monsters.leadership", Texts.Percent(leader.Value), Texts.Name(leader.Stat)), 440));
				_detail.AddChild(row);
			}

			_detail.AddChild(new HSeparator());
			var actions = Layout.Row(10);
			actions.AddChild(FuseMenu(summon, monster));
			_detail.AddChild(actions);
		}

		private void AwakenPage(SummonDefinition summon, OwnedSummon monster)
		{
			var forms = Layout.Row(18, true);
			forms.AddChild(Form(summon, false, !monster.Awakened));
			forms.AddChild(Doodle.Icon(Art.Icon("awaken"), 40, monster.Awakened ? Palette.Awakened : Palette.GoldDark));
			forms.AddChild(Form(summon, true, monster.Awakened));
			_detail.AddChild(forms);

			var name = new Label { Text = summon.Awakening.Name, ThemeTypeVariation = GameTheme.Heading, HorizontalAlignment = HorizontalAlignment.Center };
			name.AddThemeColorOverride("font_color", Palette.Awakened);
			_detail.AddChild(name);

			var gains = Layout.Row(8, true);
			gains.AddChild(Layout.Chip(Texts.GlyphOf(Stat.Health), $"+{Texts.Percent(Awakening.HealthBonus)}", Texts.Name(Stat.Health)));
			gains.AddChild(Layout.Chip(Texts.GlyphOf(Stat.Attack), $"+{Texts.Percent(Awakening.AttackDefenseBonus)}", Texts.Name(Stat.Attack)));
			gains.AddChild(Layout.Chip(Texts.GlyphOf(Stat.Defense), $"+{Texts.Percent(Awakening.AttackDefenseBonus)}", Texts.Name(Stat.Defense)));
			if (summon.Awakening.Stat is { } stat)
				gains.AddChild(Layout.Chip(Texts.GlyphOf(stat), Texts.AwakeningAmount(stat), Texts.Name(stat)));
			_detail.AddChild(gains);

			if (summon.Awakening.Skill is { } skill)
				_detail.AddChild(SkillRow.Build(skill, monster.SkillLevel(summon.Skills.Count), true, !monster.Awakened));
			foreach (var improved in summon.Skills.Where(s => s.ChangesOnAwakening))
				_detail.AddChild(SkillRow.Build(improved, monster.SkillLevel(summon.Skills.ToList().IndexOf(improved)), true, !monster.Awakened));

			if (monster.Awakened)
				return;

			var row = Layout.Row(0, true);
			var cost = Awakening.Cost(summon.Rarity);
			var awaken = Action("awaken", T("monsters.awaken_button", summon.Awakening.Name, cost), !Awakening.CanAwaken(_player, monster, summon), () => AwakenRequested?.Invoke(monster.Id), 76);
			awaken.Badge = Texts.Short(cost);
			awaken.Highlight = Awakening.CanAwaken(_player, monster, summon);
			row.AddChild(awaken);
			_detail.AddChild(row);
		}

		/// <summary>Um desenho do monstro (normal ou desperto), aceso se for a forma de agora.</summary>
		private static Control Form(SummonDefinition summon, bool awakened, bool current)
		{
			var frame = new PanelContainer { CustomMinimumSize = new Vector2(150, 150), TooltipText = summon.NameFor(awakened) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, current ? Palette.Arcane : Palette.GoldDark, current ? 3 : 1, 12, 10));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.ImageFor(awakened)), current ? Palette.Of(summon.Element) : Palette.Of(summon.Element).Darkened(0.45f), MaskShape.Rounded, 8));
			return frame;
		}

		/// <summary>Fundir: o carrossel mostra as cópias da mesma variante; a escolhida sobe uma habilidade sorteada.</summary>
		private SigilButton FuseMenu(SummonDefinition summon, OwnedSummon monster)
		{
			var copies = _player.Monsters.Where(m => Fusion.CanFuse(_player, _database, monster.Id, m.Id)).OrderBy(m => m.Level).ToList();
			var left = Fusion.SkillUpsLeft(_database, monster);
			var tip = left == 0 ? T("monsters.fuse_full") : T("monsters.fuse", copies.Count, left);
			var button = Action("fuse", tip, copies.Count == 0, () =>
			{
				var items = copies
					.Select(c => new ArcItem(Art.Creature(summon.ImageFor(c.Awakened)), T("monsters.fuse_item", summon.NameFor(c.Awakened), c.Level, c.Stored ? T("monsters.in_vault_short") : ""), Palette.Of(summon.Element), Accent: Palette.Frame(c.Stars)))
					.ToList();
				ArcPicker.Open(this, items, 0, GetGlobalMousePosition(), index =>
				{
					var copy = copies[index];
					SigilDialog.Ask(this, T("monsters.fuse_confirm", summon.NameFor(copy.Awakened), copy.Level), () => FuseRequested?.Invoke(monster.Id, new[] { copy.Id }));
				});
			}, 64);
			button.Badge = copies.Count > 0 ? copies.Count.ToString() : "";
			return button;
		}

		/// <summary>Os conteúdos em que o monstro está na equipe, com o símbolo de cada um.</summary>
		private List<(string Icon, string Name)> TeamIcons(int monsterId) => _player.Teams
			.Where(pair => pair.Value.Contains(monsterId))
			.Select(pair => pair.Key == Teams.Campaign
				? ("campaign", T("teams.campaign"))
				: ("dungeon", _database.Dungeons.FirstOrDefault(d => d.Id == pair.Key)?.Name ?? pair.Key))
			.ToList();

		private static SigilButton Action(string icon, string tooltip, bool disabled, Action onPressed, float size = 64)
		{
			var button = SigilButton.Of(icon, tooltip, onPressed, size, SigilShape.Diamond);
			button.Disabled = disabled;
			return button;
		}
	}
}
