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
	/// Monstros, no jeito do Summoners War: à esquerda a grade de cartões, com as abas Coleção e Baú e o
	/// botão de selecionar vários; à direita a ficha do escolhido, com a régua de abas escritas em pé —
	/// Atributos, Habilidades, Despertar, Runas.
	///
	/// - Atributos: retrato, estrelas, nível e experiência, os botões (subir nível, evoluir, Baú,
	///   liberar), cada um dizendo o que faz e quanto custa, e a ficha (base + runas).
	/// - Habilidades: cada uma com a recarga, o nível e o que os próximos níveis dão; a Liderança; e
	///   Fundir cópia (uma cópia sobe uma habilidade sorteada).
	/// - Despertar: o desenho de agora e o desperto, o que ganha e o botão Despertar com o custo.
	/// - Runas: as 6 no círculo, os conjuntos ativos e o botão que abre a tela de Runas.
	///
	/// Selecionar vários marca cartões (nas duas abas) para fundir no monstro da ficha ou liberar de uma
	/// vez. Toque longo em qualquer cartão abre o resumo. Cada botão vira um evento; o GameRoot aplica a
	/// regra e chama <see cref="Refresh"/>.
	/// </summary>
	public partial class StorageScreen : Control
	{
		private const float DetailWidth = 600;

		private enum Page
		{
			Stats,
			Skills,
			Awaken,
			Runes,
		}

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private int? _selected;
		private bool _showStorage;
		private bool _selecting;
		private Page _page;
		private readonly HashSet<int> _marked = new();

		private readonly CurrencyBar _currencies = new();
		private readonly HBoxContainer _tools = Layout.Row(10).Named("Tools");
		private readonly HFlowContainer _selection = Layout.Flow(8).Named("Selection");
		private readonly TileGrid _roster = new(10) { Name = "Roster" };
		private readonly VBoxContainer _detail = new() { Name = "Detail" };
		private readonly TextTabs _pages = new(vertical: true, 64) { Name = "Pages" };

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
			page.AddChild(Layout.Header(T("destination.Monsters"), _currencies, () => BackRequested?.Invoke()).Header);

			var body = Layout.Row(16).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var rosterPanel = new PanelContainer { Name = "Collection", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var rosterColumn = new VBoxContainer { Name = "Column" };
			rosterColumn.AddThemeConstantOverride("separation", 10);
			rosterPanel.AddChild(rosterColumn);
			rosterColumn.AddChild(_tools);
			rosterColumn.AddChild(_selection);
			rosterColumn.AddChild(Layout.Scroll(_roster));
			body.AddChild(rosterPanel);

			var detailPanel = new PanelContainer { Name = "Sheet", CustomMinimumSize = new Vector2(DetailWidth, 0) };
			var detailRow = Layout.Row(12).Named("Row");
			_detail.AddThemeConstantOverride("separation", 12);
			detailRow.AddChild(Layout.Scroll(_detail));
			_pages.Add(T("monsters.page.stats"), "", "stats").Name = nameof(Page.Stats);
			_pages.Add(T("monsters.page.skills"), "", "skill").Name = nameof(Page.Skills);
			_pages.Add(T("monsters.page.awaken"), "", "awaken").Name = nameof(Page.Awaken);
			_pages.Add(T("monsters.page.runes"), "", "rune").Name = nameof(Page.Runes);
			_pages.CustomMinimumSize = new Vector2(150, 0);
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
			RefreshTools();
			RefreshSelection();
			RefreshRoster();
			RefreshDetail();
		}

		// Abas e seleção ----------------------------------------------------------------------------

		private void RefreshTools()
		{
			Layout.Clear(_tools);
			var tabs = new TextTabs { Name = "Places" };
			tabs.Add(T("monsters.collection"), $"{_player.Collection.Count()}/{PlayerState.CollectionCapacity}").Name = "Collection";
			tabs.Add(T("monsters.vault"), _player.Storage.Count().ToString()).Name = "Vault";
			tabs.Select(_showStorage ? 1 : 0);
			tabs.Changed += index =>
			{
				_showStorage = index == 1;
				Callable.From(Refresh).CallDeferred();
			};
			_tools.AddChild(tabs);
			_tools.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill });

			var select = GameButton.Of(_selecting ? T("monsters.select_done") : T("monsters.select"), () =>
			{
				_selecting = !_selecting;
				_marked.Clear();
				Callable.From(Refresh).CallDeferred();
			}, _selecting ? ButtonKind.Primary : ButtonKind.Secondary, "select", 52).Named("Select");
			_tools.AddChild(select);
		}

		/// <summary>A faixa da seleção: o que fazer, quantos marcados, e os botões de marcar, fundir e liberar.</summary>
		private void RefreshSelection()
		{
			Layout.Clear(_selection);
			_selection.Visible = _selecting;
			if (!_selecting)
				return;

			var marked = _marked.Select(_player.Monster).OfType<OwnedSummon>().ToList();
			_selection.AddChild(new Label { Name = "Hint", Text = T("monsters.select_hint", marked.Count), VerticalAlignment = VerticalAlignment.Center });

			var target = _player.Monster(_selected ?? -1);
			if (target != null)
			{
				var name = _database.Summon(target.SummonId).NameFor(target.Awakened);
				_selection.AddChild(GameButton.Of(T("monsters.mark_copies", name), () =>
				{
					foreach (var copy in _player.Monsters.Where(m => m.SummonId == target.SummonId && m.Id != target.Id))
						_marked.Add(copy.Id);
					Refresh();
				}, ButtonKind.Secondary, "copies", 48).Named("MarkCopies"));
			}

			var unmark = GameButton.Of(T("monsters.unmark"), () =>
			{
				_marked.Clear();
				Refresh();
			}, ButtonKind.Secondary, null, 48).Named("Unmark");
			unmark.Disabled = marked.Count == 0;
			_selection.AddChild(unmark);

			if (target != null)
				_selection.AddChild(FuseMarked(target, marked).Named("FuseMarked"));

			var fragments = marked.Sum(m => Fusion.FragmentsFor(_database.Summon(m.SummonId).Rarity));
			var release = GameButton.Of(T("monsters.release_marked", marked.Count), () => Dialog.Confirm(this,
				T("monsters.release_title"),
				T("monsters.release_many_confirm", marked.Count, fragments) + Warning(marked),
				T("monsters.release_button"),
				() => ReleaseRequested?.Invoke(marked.Select(m => m.Id).ToList()), ButtonKind.Danger), ButtonKind.Danger, "release", 48).Named("ReleaseMarked");
			if (marked.Count > 0)
				release.WithCost("fragments", $"+{fragments}");
			release.Disabled = marked.Count == 0;
			_selection.AddChild(release);
		}

		/// <summary>Funde os marcados no monstro da ficha: só cópias da mesma variante, uma habilidade sorteada sobe por cópia, as mais fracas primeiro.</summary>
		private GameButton FuseMarked(OwnedSummon target, IReadOnlyList<OwnedSummon> marked)
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

			var button = GameButton.Of(T("monsters.fuse_marked", name, materials.Count), () => Dialog.Confirm(this,
				T("monsters.fuse_title"),
				T("monsters.fuse_many_confirm", materials.Count, name, room) + Warning(materials),
				T("monsters.fuse_button"),
				() => FuseRequested?.Invoke(target.Id, materials.Select(m => m.Id).ToList())), ButtonKind.Primary, "fuse", 48);
			button.Disabled = !valid;
			return button;
		}

		/// <summary>Aviso quando a seleção leva monstro desperto, evoluído, com nível, com habilidade subida, em equipe ou com runas.</summary>
		private string Warning(IEnumerable<OwnedSummon> monsters) =>
			monsters.Any(m => m.Awakened || m.Level > 1 || m.Stars > _database.Summon(m.SummonId).Rarity || m.SkillLevels.Any(l => l > 1) || Teams(m.Id).Count > 0 || _player.RunesOn(m.Id).Count > 0)
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
				var inTeam = Teams(monster.Id).Count > 0;
				var card = new CreatureCard(_database.Summon(monster.SummonId), monster, 104, inTeam ? "team" : null) { Name = $"Monster{monster.Id}" };
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
				_roster.AddChild(Layout.Text(T(_showStorage ? "monsters.vault_empty" : "monsters.collection_empty"), GameTheme.Faded, 400).Named("Empty"));
		}

		// Ficha ------------------------------------------------------------------------------------

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			_pages.Visible = _selected != null;
			if (_selected is not { } id || _player.Monster(id) is not { } monster)
			{
				_detail.AddChild(Layout.Text(T("monsters.none_selected"), GameTheme.Faded).Named("Empty"));
				return;
			}

			var summon = _database.Summon(monster.SummonId);
			_detail.AddChild(Identity(summon, monster));
			switch (_page)
			{
				case Page.Stats:
					StatsPage(summon, monster);
					break;
				case Page.Skills:
					SkillsPage(summon, monster);
					break;
				case Page.Awaken:
					AwakenPage(summon, monster);
					break;
				default:
					RunesPage(summon, monster);
					break;
			}
		}

		/// <summary>Retrato, nome, estrelas, elemento e papel, onde está, nível e experiência: igual em toda aba.</summary>
		private Control Identity(SummonDefinition summon, OwnedSummon monster)
		{
			var column = new VBoxContainer { Name = "Identity" };
			column.AddThemeConstantOverride("separation", 6);
			var row = Layout.Row(14).Named("Row");
			var frame = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(112, 112) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 3, 10, 8));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.ImageFor(monster.Awakened)), Palette.Of(summon.Element), MaskShape.Rounded, 6));
			row.AddChild(frame);

			var info = new VBoxContainer { Name = "Info", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			info.AddThemeConstantOverride("separation", 4);
			var name = new Label { Name = "Name", Text = summon.NameFor(monster.Awakened), ThemeTypeVariation = GameTheme.Heading };
			if (monster.Awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			info.AddChild(name);

			var stars = new Label { Name = "Stars", Text = Texts.Stars(monster.Stars) };
			stars.AddThemeColorOverride("font_color", Palette.Stars(monster.Awakened));
			stars.AddThemeFontSizeOverride("font_size", 20);
			info.AddChild(stars);

			var line = Layout.Row(8).Named("Line");
			line.AddChild(Doodle.Icon(Art.Element(summon.Element), 24, Palette.Of(summon.Element)).Named("Element"));
			var element = new Label { Name = "ElementName", Text = Texts.Name(summon.Element), VerticalAlignment = VerticalAlignment.Center };
			element.AddThemeColorOverride("font_color", Palette.Of(summon.Element));
			line.AddChild(element);
			line.AddChild(new Label { Name = "Role", Text = $"· {Texts.Name(summon.Role)}", VerticalAlignment = VerticalAlignment.Center });
			info.AddChild(line);
			row.AddChild(info);
			column.AddChild(row);

			var where = new List<string> { T("monsters.natural_stars", Texts.Stars(summon.Rarity)) };
			if (monster.Stored)
				where.Add(T("monsters.in_vault"));
			var teams = Teams(monster.Id);
			if (teams.Count > 0)
				where.Add(T("monsters.teams", string.Join(", ", teams)));
			column.AddChild(Layout.Text(string.Join(" · ", where), GameTheme.Faded).Named("Where"));

			var max = Leveling.MaxLevel(monster);
			var level = Layout.Row(10).Named("Level");
			level.AddChild(new Label { Name = "Text", Text = T("monsters.level", monster.Level, max), ThemeTypeVariation = GameTheme.Number });
			var bar = Layout.Energy(Palette.Gold, 12).Named("Experience");
			bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			bar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			bar.MaxValue = Math.Max(1, Leveling.ExperienceToNext(monster));
			bar.Value = Leveling.IsMaxLevel(monster) ? bar.MaxValue : monster.Experience;
			level.AddChild(bar);
			level.AddChild(new Label
			{
				Name = "ExperienceText",
				Text = Leveling.IsMaxLevel(monster) ? T("monsters.level_max") : T("monsters.experience", monster.Experience, Leveling.ExperienceToNext(monster)),
				ThemeTypeVariation = GameTheme.Faded,
			});
			column.AddChild(level);
			return column;
		}

		/// <summary>Subir até o máximo gasta muita Essência de uma vez: pergunta antes, com o gasto e o nível a que chega.</summary>
		private void ConfirmLevelMax(SummonDefinition summon, OwnedSummon monster, int full)
		{
			var spend = Math.Min(full, _player.Essence);
			var target = Leveling.LevelAfter(monster, spend);
			Dialog.Confirm(this, T("monsters.max_confirm_title", target),
				T("monsters.max_confirm", spend.ToString("N0", Culture), summon.NameFor(monster.Awakened), target, _player.Essence.ToString("N0", Culture)),
				T("monsters.max_level_button", target), () => InfuseRequested?.Invoke(monster.Id, true));
		}

		private void StatsPage(SummonDefinition summon, OwnedSummon monster)
		{
			var id = monster.Id;
			var actions = Layout.Flow(10).Named("Actions");

			if (!Leveling.IsMaxLevel(monster))
			{
				var (next, full) = Leveling.InfuseCosts(monster);
				var one = GameButton.Of(T("monsters.level_up"), () => InfuseRequested?.Invoke(id, false), ButtonKind.Primary, "essence").WithCost("essence", Texts.Short(next)).Named("LevelUp");
				one.Disabled = _player.Essence < next;
				actions.AddChild(one);
				var all = GameButton.Of(T("monsters.max_level_button", Leveling.MaxLevel(monster)), () => ConfirmLevelMax(summon, monster, full), ButtonKind.Secondary, "level_max").WithCost("essence", Texts.Short(full)).Named("LevelMax");
				all.Disabled = _player.Essence <= 0;
				actions.AddChild(all);
			}
			else if (Evolution.IsReady(monster))
			{
				var (essenceCost, fragmentCost) = Evolution.Cost(monster.Stars);
				var evolve = GameButton.Of(T("monsters.evolve", Texts.Stars(monster.Stars + 1)), () => EvolveRequested?.Invoke(id), ButtonKind.Primary, "evolve")
					.WithCost("essence", $"{Texts.Short(essenceCost)} · {fragmentCost} {T("currency.fragments")}").Named("Evolve");
				evolve.Disabled = !Evolution.CanEvolve(_player, monster);
				actions.AddChild(evolve);
			}

			if (!monster.Stored)
			{
				actions.AddChild(GameButton.Of(T("monsters.store"), () => StoreRequested?.Invoke(id), ButtonKind.Secondary, "chest").Named("Store"));
			}
			else
			{
				var full = Roster.IsFull(_player);
				var retrieve = GameButton.Of(T("monsters.take_from_vault"), () => RetrieveRequested?.Invoke(id), ButtonKind.Secondary, "retrieve").Named("Retrieve");
				retrieve.Disabled = full;
				actions.AddChild(retrieve);
			}

			var fragments = Fusion.FragmentsFor(summon.Rarity);
			actions.AddChild(GameButton.Of(T("monsters.release"), () => Dialog.Confirm(this,
				T("monsters.release_title"),
				T("monsters.release_confirm", summon.NameFor(monster.Awakened), monster.Level, fragments) + Warning(new[] { monster }),
				T("monsters.release_button"),
				() => ReleaseRequested?.Invoke(new[] { id }), ButtonKind.Danger), ButtonKind.Danger, "release").WithCost("fragments", $"+{fragments}").Named("Release"));
			_detail.AddChild(actions);

			if (!Leveling.IsMaxLevel(monster))
				_detail.AddChild(Layout.Text(T("monsters.level_hint", Leveling.ExperiencePerEssence), GameTheme.Faded).Named("LevelHint"));
			else if (monster.Stars >= Growth.MaxStars)
				_detail.AddChild(Layout.Text(T("monsters.fully_grown"), GameTheme.Faded).Named("Grown"));
			else if (Roster.IsFull(_player) && monster.Stored)
				_detail.AddChild(Layout.Text(T("monsters.collection_full"), GameTheme.Faded).Named("Full"));

			_detail.AddChild(new HSeparator { Name = "StatsLine" });
			var sheet = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, _player.RunesOn(monster.Id));
			var table = new StatTable { Name = "Stats" };
			table.Show(sheet);
			_detail.AddChild(table);
			_detail.AddChild(Layout.Text(T("monsters.stats_hint"), GameTheme.Faded).Named("StatsHint"));
		}

		private void RunesPage(SummonDefinition summon, OwnedSummon monster)
		{
			var runes = _player.RunesOn(monster.Id);
			var sheet = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, runes);

			var actions = Layout.Row(0, true).Named("Actions");
			actions.AddChild(GameButton.Of(T("monsters.runes_button"), () => RunesRequested?.Invoke(monster.Id), ButtonKind.Primary, "rune").Named("OpenRunes"));
			_detail.AddChild(actions);

			var ring = new SigilRing(300) { Name = "Ring", Spread = 0.72f, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
			var tiles = new List<Control>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(runes.FirstOrDefault(r => r.Slot == slot), slot, 1.1f) { Name = $"Slot{slot}" };
				tile.Pressed += _ => RunesRequested?.Invoke(monster.Id);
				tiles.Add(tile);
			}

			ring.Set(null, tiles);
			_detail.AddChild(ring);

			// Conjunto de 2 peças pode estar ativo mais de uma vez: a posição deixa o nome único.
			for (var i = 0; i < sheet.Runes.ActiveSets.Count; i++)
			{
				var set = sheet.Runes.ActiveSets[i];
				var row = Layout.Row(8).Named($"{set.Set}{i + 1}");
				row.AddChild(new RuneGlyph(RuneSets.For(set.Set).Glyph, 26, Palette.Gold) { Name = "Glyph" });
				row.AddChild(RichText.Label($"{Texts.Term(set.Set)}: {Texts.Describe(set)}", 360).Named("Effect"));
				_detail.AddChild(row);
			}

			if (sheet.Runes.ActiveSets.Count == 0)
				_detail.AddChild(Layout.Text(runes.Count == 0 ? T("monsters.no_runes") : T("monsters.no_sets"), GameTheme.Faded).Named("NoSets"));
		}

		private void SkillsPage(SummonDefinition summon, OwnedSummon monster)
		{
			var skills = summon.AllSkills;
			for (var i = 0; i < skills.Count; i++)
			{
				var locked = !monster.Awakened && i >= summon.Skills.Count;
				_detail.AddChild(SkillRow.Build(skills[i], monster.SkillLevel(i), monster.Awakened, locked, 330, levels: true).Named($"Skill{i + 1}"));
			}

			if (summon.Leader is { } leader)
			{
				var row = Layout.Row(10).Named("Leader");
				row.AddChild(Doodle.Icon(Art.Icon("leader"), 34, Palette.Gold).Named("Icon"));
				row.AddChild(RichText.Label(T("monsters.leadership", Texts.Percent(leader.Value), Texts.Name(leader.Stat)), 340).Named("Text"));
				_detail.AddChild(row);
			}

			_detail.AddChild(new HSeparator { Name = "ActionsLine" });
			var copies = _player.Monsters.Where(m => Fusion.CanFuse(_player, _database, monster.Id, m.Id)).OrderBy(m => m.Level).ToList();
			var left = Fusion.SkillUpsLeft(_database, monster);
			_detail.AddChild(Layout.Text(left == 0 ? T("monsters.fuse_full") : T("monsters.fuse_hint", left, copies.Count), GameTheme.Faded).Named("FuseHint"));
			var actions = Layout.Flow(10).Named("Actions");
			var fuse = GameButton.Of(T("monsters.fuse_button_one"), () => ChooseCopy(summon, monster, copies), ButtonKind.Primary, "fuse").Named("Fuse");
			fuse.Disabled = copies.Count == 0;
			actions.AddChild(fuse);
			_detail.AddChild(actions);
		}

		private void AwakenPage(SummonDefinition summon, OwnedSummon monster)
		{
			var forms = Layout.Row(14, true).Named("Forms");
			forms.AddChild(Form(summon, false, !monster.Awakened).Named("Normal"));
			forms.AddChild(Doodle.Icon(Art.Icon("awaken"), 40, monster.Awakened ? Palette.Awakened : Palette.GoldDark).Named("Arrow"));
			forms.AddChild(Form(summon, true, monster.Awakened).Named("Awakened"));
			_detail.AddChild(forms);

			var name = new Label { Name = "AwakenedName", Text = summon.Awakening.Name, ThemeTypeVariation = GameTheme.Heading, HorizontalAlignment = HorizontalAlignment.Center };
			name.AddThemeColorOverride("font_color", Palette.Awakened);
			_detail.AddChild(name);
			_detail.AddChild(Layout.Text(monster.Awakened ? T("monsters.awakened_already") : T("monsters.awaken_gains"), GameTheme.Faded).Named("Explain"));

			var gains = Layout.Flow(8).Named("Gains");
			foreach (var (stat, gain) in Texts.AwakeningStats(summon))
				gains.AddChild(Layout.Labeled(Texts.GlyphOf(stat), gain, Texts.Name(stat)).Named(stat.ToString()));
			_detail.AddChild(gains);

			if (summon.Awakening.Skill is { } skill)
				_detail.AddChild(SkillRow.Build(skill, monster.SkillLevel(summon.Skills.Count), true, !monster.Awakened, 330).Named("NewSkill"));
			foreach (var improved in summon.Skills.Where(s => s.ChangesOnAwakening))
			{
				var index = summon.Skills.ToList().IndexOf(improved);
				_detail.AddChild(SkillRow.Build(improved, monster.SkillLevel(index), true, false, 330).Named($"Improved{index + 1}"));
			}

			if (monster.Awakened)
				return;

			var row = Layout.Row(0, true).Named("Actions");
			var cost = Awakening.Cost(summon.Rarity);
			var awaken = GameButton.Of(T("monsters.awaken_button"), () => Dialog.Confirm(this,
				T("monsters.awaken_title"),
				T("monsters.awaken_confirm", summon.Name, summon.Awakening.Name, cost),
				T("monsters.awaken_button"),
				() => AwakenRequested?.Invoke(monster.Id)), ButtonKind.Primary, "awaken", 64).WithCost("essence", Texts.Short(cost)).Named("Awaken");
			awaken.Disabled = !Awakening.CanAwaken(_player, monster, summon);
			row.AddChild(awaken.Wide(260));
			_detail.AddChild(row);
			if (awaken.Disabled)
				_detail.AddChild(Layout.Text(T("monsters.awaken_short", cost - _player.Essence), GameTheme.Faded).Named("Short"));
		}

		/// <summary>Um desenho do monstro (normal ou desperto), aceso se for a forma de agora.</summary>
		private static Control Form(SummonDefinition summon, bool awakened, bool current)
		{
			var frame = new PanelContainer { CustomMinimumSize = new Vector2(140, 140) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, current ? Palette.Arcane : Palette.GoldDark, current ? 3 : 1, 12, 10));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.ImageFor(awakened)), current ? Palette.Of(summon.Element) : Palette.Of(summon.Element).Darkened(0.45f), MaskShape.Rounded, 8));
			return frame;
		}

		/// <summary>Fundir uma cópia: a lista das cópias da mesma variante; a escolhida sobe uma habilidade sorteada.</summary>
		private void ChooseCopy(SummonDefinition summon, OwnedSummon monster, IReadOnlyList<OwnedSummon> copies)
		{
			var options = copies
				.Select(c => new Choice(T("monsters.fuse_item", summon.NameFor(c.Awakened), c.Level, Texts.Stars(c.Stars), c.Stored ? T("monsters.in_vault_short") : ""), Art.Creature(summon.ImageFor(c.Awakened)), Palette.Of(summon.Element)))
				.ToList();
			Choices.Open(this, T("monsters.fuse_choose"), options, -1, anchored: false, chosen: index =>
			{
				var copy = copies[index];
				Dialog.Confirm(this, T("monsters.fuse_title"), T("monsters.fuse_confirm", summon.NameFor(copy.Awakened), copy.Level) + Warning(new[] { copy }), T("monsters.fuse_button"),
					() => FuseRequested?.Invoke(monster.Id, new[] { copy.Id }));
			});
		}

		/// <summary>Os nomes dos conteúdos em que o monstro está na equipe.</summary>
		private List<string> Teams(int monsterId) => _player.Teams
			.Where(pair => pair.Value.Contains(monsterId))
			.Select(pair => pair.Key == Core.Player.Teams.Campaign ? T("teams.campaign") : _database.Dungeons.FirstOrDefault(d => d.Id == pair.Key)?.Name ?? pair.Key)
			.ToList();
	}
}
