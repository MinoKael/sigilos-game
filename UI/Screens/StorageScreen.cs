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
		private readonly Label _count = new() { Name = "Count", ThemeTypeVariation = GameTheme.Number, VerticalAlignment = VerticalAlignment.Center };
		private readonly HBoxContainer _headerTools = Layout.Row(8).Named("Tools");
		private readonly HBoxContainer _selection = Layout.Row(8).Named("Selection");
		private readonly GridContainer _roster = new() { Name = "Roster", Columns = 5 };
		private readonly VBoxContainer _detail = new() { Name = "Detail" };
		private readonly SigilTabs _pages = new(vertical: true, 60) { Name = "Pages" };

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
			var (header, extra) = Layout.Header(T("destination.Monsters"), "monster", _currencies, () => BackRequested?.Invoke());
			extra.AddChild(_count);
			extra.AddChild(new Control { Name = "Spacer", CustomMinimumSize = new Vector2(12, 0) });
			extra.AddChild(_headerTools);
			page.AddChild(header);

			var body = Layout.Row(16).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var rosterPanel = new PanelContainer { Name = "Collection", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var rosterColumn = new VBoxContainer { Name = "Column" };
			rosterColumn.AddThemeConstantOverride("separation", 10);
			rosterPanel.AddChild(rosterColumn);
			rosterColumn.AddChild(_selection);
			_roster.AddThemeConstantOverride("h_separation", 10);
			_roster.AddThemeConstantOverride("v_separation", 10);
			rosterColumn.AddChild(Layout.Scroll(_roster));
			body.AddChild(rosterPanel);

			var detailPanel = new PanelContainer { Name = "Sheet", CustomMinimumSize = new Vector2(DetailWidth, 0) };
			var detailRow = Layout.Row(10).Named("Row");
			_detail.AddThemeConstantOverride("separation", 10);
			detailRow.AddChild(Layout.Scroll(_detail));
			_pages.Add(Art.Icon("stats"), T("monsters.page.stats")).Name = nameof(Page.Stats);
			_pages.Add(Art.Icon("rune"), T("monsters.page.runes")).Name = nameof(Page.Runes);
			_pages.Add(Art.Icon("skill"), T("monsters.page.skills")).Name = nameof(Page.Skills);
			_pages.Add(Art.Icon("awaken"), T("monsters.page.awaken")).Name = nameof(Page.Awaken);
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
			var tabs = new SigilTabs(vertical: false, 48) { Name = "Places" };
			tabs.Add(Art.Icon("storage"), T("monsters.collection")).Name = "Collection";
			tabs.Add(Art.Icon("chest"), T("monsters.vault"), _player.Storage.Count().ToString()).Name = "Vault";
			tabs.Select(_showStorage ? 1 : 0);
			tabs.Changed += index =>
			{
				_showStorage = index == 1;
				Callable.From(Refresh).CallDeferred();
			};
			_headerTools.AddChild(tabs);

			_headerTools.AddChild(SelectToggle(48));
		}

		/// <summary>O sigilo que liga e desliga a seleção em massa: no cabeçalho e, de atalho, na aba de habilidades.</summary>
		private SigilButton SelectToggle(float size)
		{
			var select = new SigilButton(Art.Icon("select"), T("monsters.select"), size, SigilShape.Square) { Name = "Select", ToggleMode = true, ButtonPressed = _selecting };
			select.Toggled += on =>
			{
				_selecting = on;
				_marked.Clear();
				Callable.From(Refresh).CallDeferred();
			};
			return select;
		}

		/// <summary>A barra da seleção em massa: marcar cópias, desmarcar, fundir no monstro da ficha, liberar.</summary>
		private void RefreshSelection()
		{
			Layout.Clear(_selection);
			_selection.Visible = _selecting;
			if (!_selecting)
				return;

			var marked = _marked.Select(_player.Monster).OfType<OwnedSummon>().ToList();
			_selection.AddChild(Layout.Chip("select", marked.Count.ToString(), T("monsters.marked")).Named("Marked"));

			var target = _player.Monster(_selected ?? -1);
			if (target != null)
			{
				var name = _database.Summon(target.SummonId).NameFor(target.Awakened);
				_selection.AddChild(Action("copies", T("monsters.mark_copies", name), false, () =>
				{
					foreach (var copy in _player.Monsters.Where(m => m.SummonId == target.SummonId && m.Id != target.Id))
						_marked.Add(copy.Id);
					Refresh();
				}).Named("MarkCopies"));
			}

			_selection.AddChild(Action("cancel", T("monsters.unmark"), marked.Count == 0, () =>
			{
				_marked.Clear();
				Refresh();
			}).Named("Unmark"));

			if (target != null)
				_selection.AddChild(FuseMarked(target, marked).Named("FuseMarked"));

			var fragments = marked.Sum(m => Fusion.FragmentsFor(_database.Summon(m.SummonId).Rarity));
			var release = Action("release", T("monsters.release_marked", marked.Count, fragments), marked.Count == 0, () => SigilDialog.Ask(this,
				T("monsters.release_many_confirm", marked.Count, fragments) + Warning(marked),
				() => ReleaseRequested?.Invoke(marked.Select(m => m.Id).ToList()))).Named("ReleaseMarked");
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
					teams.Count > 0 ? T("monsters.teams", string.Join(", ", teams.Select(t => t.Name))) : null) { Name = $"Monster{monster.Id}" };
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
				var empty = Doodle.Icon(Art.Icon(_showStorage ? "chest" : "summon"), 96, new Color(Palette.GoldDark, 0.5f)).Named("Empty");
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
			var row = Layout.Row(14).Named("Identity");

			var frame = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(120, 120), TooltipText = T("monsters.natural_stars", summon.Rarity) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 3, 10, 8));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.ImageFor(monster.Awakened)), Palette.Of(summon.Element), MaskShape.Rounded, 6));
			row.AddChild(frame);

			var info = new VBoxContainer { Name = "Info", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			info.AddThemeConstantOverride("separation", 4);
			var name = new Label { Name = "Name", Text = summon.NameFor(monster.Awakened), ThemeTypeVariation = GameTheme.Heading };
			if (monster.Awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			info.AddChild(name);

			var line = Layout.Row(8).Named("Line");
			var stars = new Label { Name = "Stars", Text = Texts.Stars(monster.Stars) };
			stars.AddThemeColorOverride("font_color", Palette.Stars(monster.Awakened));
			stars.AddThemeFontSizeOverride("font_size", 18);
			line.AddChild(stars);
			var element = Doodle.Icon(Art.Element(summon.Element), 22, Palette.Of(summon.Element)).Named("Element");
			element.TooltipText = T("monsters.element_role", Texts.Name(summon.Element), Texts.Name(summon.Role));
			element.MouseFilter = MouseFilterEnum.Stop;
			line.AddChild(element);
			line.AddChild(new Label { Name = "Role", Text = Texts.Name(summon.Role), ThemeTypeVariation = GameTheme.Faded, VerticalAlignment = VerticalAlignment.Center });
			var teams = TeamIcons(monster.Id);
			for (var i = 0; i < teams.Count; i++)
			{
				var (icon, teamName) = teams[i];
				var team = Doodle.Icon(Art.Icon(icon), 20, Palette.Gold).Named($"Team{i + 1}");
				team.TooltipText = teamName;
				team.MouseFilter = MouseFilterEnum.Stop;
				line.AddChild(team);
			}

			if (monster.Stored)
			{
				var vault = Doodle.Icon(Art.Icon("chest"), 20, Palette.TextFaded).Named("Vault");
				vault.TooltipText = T("monsters.in_vault");
				vault.MouseFilter = MouseFilterEnum.Stop;
				line.AddChild(vault);
			}

			info.AddChild(line);

			var max = Leveling.MaxLevel(monster);
			var level = Layout.Row(8).Named("Level");
			var levelText = new Label { Name = "Text", Text = T("monsters.level", monster.Level, max), ThemeTypeVariation = GameTheme.Number };
			level.AddChild(levelText);
			var bar = Layout.Energy(Palette.Gold, 10).Named("Experience");
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
			var actions = Layout.Flow(10).Named("Actions");

			if (!Leveling.IsMaxLevel(monster))
			{
				var (next, full) = Leveling.InfuseCosts(monster);
				var one = Action("essence", T("monsters.level_up", next), _player.Essence < next, () => InfuseRequested?.Invoke(id, false)).Named("LevelUp");
				one.Badge = "+1";
				actions.AddChild(one);
				var all = Action("level_max", T("monsters.max_level_button", Leveling.MaxLevel(monster), full), _player.Essence <= 0, () => InfuseRequested?.Invoke(id, true)).Named("LevelMax");
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
				actions.AddChild(Action("chest", T("monsters.store"), false, () => StoreRequested?.Invoke(id)).Named("Store"));
			}
			else
			{
				var full = Roster.IsFull(_player);
				actions.AddChild(Action("retrieve", full ? T("monsters.collection_full") : T("monsters.take_from_vault"), full, () => RetrieveRequested?.Invoke(id)));
			}

			var fragments = Fusion.FragmentsFor(summon.Rarity);
			var release = Action("release", T("monsters.release", fragments), false, () => SigilDialog.Ask(this,
				T("monsters.release_confirm", summon.NameFor(monster.Awakened), monster.Level, fragments),
				() => ReleaseRequested?.Invoke(new[] { id }))).Named("Release");
			release.Badge = fragments.ToString();
			actions.AddChild(release);
			_detail.AddChild(actions);

			_detail.AddChild(new HSeparator { Name = "StatsLine" });
			var sheet = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, _player.RunesOn(monster.Id));
			var table = new StatTable { Name = "Stats" };
			table.Show(sheet);
			_detail.AddChild(table);
		}

		private void RunesPage(SummonDefinition summon, OwnedSummon monster)
		{
			var runes = _player.RunesOn(monster.Id);
			var sheet = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, runes);

			var ring = new SigilRing(340) { Name = "Ring", Spread = 0.72f, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
			var tiles = new List<Control>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(runes.FirstOrDefault(r => r.Slot == slot), slot, 1.25f) { Name = $"Slot{slot}" };
				tile.Pressed += _ => RunesRequested?.Invoke(monster.Id);
				tiles.Add(tile);
			}

			var open = SigilButton.Of("rune", T("monsters.runes_button"), () => RunesRequested?.Invoke(monster.Id), 76).Named("OpenRunes");
			ring.Set(open, tiles);
			_detail.AddChild(ring);

			// Conjunto de 2 peças pode estar ativo mais de uma vez: a posição deixa o nome único.
			for (var i = 0; i < sheet.Runes.ActiveSets.Count; i++)
			{
				var set = sheet.Runes.ActiveSets[i];
				var row = Layout.Row(8).Named($"{set.Set}{i + 1}");
				row.AddChild(new RuneGlyph(RuneSets.For(set.Set).Glyph, 24, Palette.Gold) { Name = "Glyph" });
				row.AddChild(RichText.Label($"{Texts.Term(set.Set)}: {Texts.Describe(set)}", 440).Named("Effect"));
				_detail.AddChild(row);
			}
		}

		private void SkillsPage(SummonDefinition summon, OwnedSummon monster)
		{
			var skills = summon.AllSkills;
			for (var i = 0; i < skills.Count; i++)
			{
				var locked = !monster.Awakened && i >= summon.Skills.Count;
				_detail.AddChild(SkillRow.Build(skills[i], monster.SkillLevel(i), monster.Awakened, locked).Named($"Skill{i + 1}"));
			}

			if (summon.Leader is { } leader)
			{
				var row = Layout.Row(10).Named("Leader");
				row.AddChild(Doodle.Icon(Art.Icon("leader"), 32, Palette.Gold).Named("Icon"));
				row.AddChild(RichText.Label(T("monsters.leadership", Texts.Percent(leader.Value), Texts.Name(leader.Stat)), 420).Named("Text"));
				_detail.AddChild(row);
			}

			_detail.AddChild(new HSeparator { Name = "ActionsLine" });
			var actions = Layout.Row(10).Named("Actions");
			actions.AddChild(FuseMenu(summon, monster).Named("Fuse"));
			actions.AddChild(SelectToggle(64));
			_detail.AddChild(actions);
		}

		private void AwakenPage(SummonDefinition summon, OwnedSummon monster)
		{
			var forms = Layout.Row(18, true).Named("Forms");
			forms.AddChild(Form(summon, false, !monster.Awakened).Named("Normal"));
			forms.AddChild(Doodle.Icon(Art.Icon("awaken"), 40, monster.Awakened ? Palette.Awakened : Palette.GoldDark).Named("Arrow"));
			forms.AddChild(Form(summon, true, monster.Awakened).Named("Awakened"));
			_detail.AddChild(forms);

			var name = new Label { Name = "AwakenedName", Text = summon.Awakening.Name, ThemeTypeVariation = GameTheme.Heading, HorizontalAlignment = HorizontalAlignment.Center };
			name.AddThemeColorOverride("font_color", Palette.Awakened);
			_detail.AddChild(name);

			var gains = Layout.Row(8, true).Named("Gains");
			foreach (var (stat, gain) in Texts.AwakeningStats(summon))
				gains.AddChild(Layout.Chip(Texts.GlyphOf(stat), gain, Texts.Name(stat)).Named(stat.ToString()));
			_detail.AddChild(gains);

			if (summon.Awakening.Skill is { } skill)
				_detail.AddChild(SkillRow.Build(skill, monster.SkillLevel(summon.Skills.Count), true, !monster.Awakened).Named("NewSkill"));
			foreach (var improved in summon.Skills.Where(s => s.ChangesOnAwakening))
			{
				var index = summon.Skills.ToList().IndexOf(improved);
				_detail.AddChild(SkillRow.Build(improved, monster.SkillLevel(index), true, !monster.Awakened).Named($"Improved{index + 1}"));
			}

			if (monster.Awakened)
				return;

			var row = Layout.Row(0, true).Named("Actions");
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
