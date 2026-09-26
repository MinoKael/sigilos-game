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
	/// Monstros: à esquerda a coleção e o Baú, em abas; à direita a ficha do escolhido — nível e
	/// experiência, atributos (base + runas), habilidades, Assinatura, Liderança, Despertar e as 6
	/// runas — com os botões de cada ação: subir nível, despertar, runas, Baú, fundir e liberar.
	///
	/// "Selecionar" marca vários cartões (nas duas abas) para fundir no monstro da ficha ou liberar de
	/// uma vez. Cada botão vira um evento; o GameRoot aplica a regra e chama <see cref="Refresh"/>.
	/// </summary>
	public partial class StorageScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private int? _selected;
		private bool _showStorage;
		private bool _selecting;
		private readonly HashSet<int> _marked = new();

		private readonly CurrencyBar _currencies = new();
		private readonly HBoxContainer _tabs = new();
		private readonly HFlowContainer _selection = new();
		private readonly GridContainer _roster = new() { Columns = 5 };
		private readonly VBoxContainer _detail = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };

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
		public event Action<int>? RunesRequested;
		public event Action<int>? StoreRequested;
		public event Action<int>? RetrieveRequested;

		/// <summary>Alvo e materiais, na ordem em que viram Eco.</summary>
		public event Action<int, IReadOnlyList<int>>? FuseRequested;

		public event Action<IReadOnlyList<int>>? ReleaseRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("monsters.title"), _currencies, T("common.back_to_hub"), () => BackRequested?.Invoke()));

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 16);
			page.AddChild(body);

			var rosterPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var rosterColumn = new VBoxContainer();
			rosterPanel.AddChild(rosterColumn);
			_tabs.AddThemeConstantOverride("separation", 6);
			rosterColumn.AddChild(_tabs);
			_selection.AddThemeConstantOverride("h_separation", 6);
			_selection.AddThemeConstantOverride("v_separation", 6);
			rosterColumn.AddChild(_selection);
			var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_roster.AddThemeConstantOverride("h_separation", 8);
			_roster.AddThemeConstantOverride("v_separation", 8);
			scroll.AddChild(_roster);
			rosterColumn.AddChild(scroll);
			body.AddChild(rosterPanel);

			_detail.AddThemeConstantOverride("separation", 6);
			var detailPanel = new PanelContainer { CustomMinimumSize = new Vector2(580, 0) };
			var detailScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			detailScroll.AddChild(_detail);
			detailPanel.AddChild(detailScroll);
			body.AddChild(detailPanel);

			Refresh();
		}

		public void Refresh()
		{
			if (_selected is { } id && _player.Monster(id) == null)
				_selected = _player.Collection.FirstOrDefault()?.Id;
			_marked.RemoveWhere(marked => _player.Monster(marked) == null);

			_currencies.Refresh(_player);
			RefreshTabs();
			RefreshSelection();
			RefreshRoster();
			RefreshDetail();
		}

		private void RefreshTabs()
		{
			Layout.Clear(_tabs);
			Tab(T("monsters.collection", _player.Collection.Count(), PlayerState.CollectionCapacity), false);
			Tab(T("monsters.vault", _player.Storage.Count()), true);
			_tabs.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var select = new CheckButton { Text = T("monsters.select"), ButtonPressed = _selecting, TooltipText = T("monsters.select_tip") };
			select.Toggled += on =>
			{
				_selecting = on;
				_marked.Clear();
				Callable.From(Refresh).CallDeferred();
			};
			_tabs.AddChild(select);
		}

		/// <summary>A barra da seleção em massa: marcar cópias, fundir no monstro da ficha, liberar.</summary>
		private void RefreshSelection()
		{
			Layout.Clear(_selection);
			_selection.Visible = _selecting;
			if (!_selecting)
				return;

			var marked = _marked.Select(_player.Monster).OfType<OwnedSummon>().ToList();
			_selection.AddChild(new Label { Text = T("monsters.marked", marked.Count) });

			var target = _player.Monster(_selected ?? -1);
			if (target != null)
			{
				var name = _database.Summon(target.SummonId).NameFor(target.Awakened);
				var copies = new Button { Text = T("monsters.mark_copies", name), TooltipText = T("monsters.mark_copies_tip") };
				copies.Pressed += () =>
				{
					foreach (var copy in _player.Monsters.Where(m => m.SummonId == target.SummonId && m.Id != target.Id))
						_marked.Add(copy.Id);
					Refresh();
				};
				_selection.AddChild(copies);
			}

			var none = new Button { Text = T("monsters.unmark"), Disabled = marked.Count == 0 };
			none.Pressed += () =>
			{
				_marked.Clear();
				Refresh();
			};
			_selection.AddChild(none);

			if (target != null)
				_selection.AddChild(FuseMarked(target, marked));

			var fragments = marked.Sum(m => Fusion.FragmentsFor(_database.Summon(m.SummonId).Rarity));
			var release = new Button { Text = T("monsters.release_marked", marked.Count, fragments), Disabled = marked.Count == 0 };
			release.Pressed += () => Confirm(
				T("monsters.release_many_confirm", marked.Count, fragments) + Warning(marked),
				() => ReleaseRequested?.Invoke(marked.Select(m => m.Id).ToList()));
			_selection.AddChild(release);
		}

		/// <summary>Funde os marcados no monstro da ficha: só cópias da mesma variante, até 5 Ecos, as mais fracas primeiro.</summary>
		private Button FuseMarked(OwnedSummon target, IReadOnlyList<OwnedSummon> marked)
		{
			var name = _database.Summon(target.SummonId).NameFor(target.Awakened);
			var room = Growth.MaxEchoes - target.Echoes;
			var valid = marked.Count > 0 && room > 0 && marked.All(m => Fusion.CanFuse(_player, target.Id, m.Id));
			var materials = marked
				.OrderBy(m => m.Awakened)
				.ThenBy(m => m.Echoes)
				.ThenBy(m => m.Level)
				.Take(Math.Max(0, room))
				.ToList();

			var button = new Button { Text = T("monsters.fuse_marked", name, materials.Count), Disabled = !valid };
			button.TooltipText = valid ? T("monsters.fuse_marked_tip", room) : T("monsters.fuse_marked_invalid", name, room);
			button.Pressed += () => Confirm(
				T("monsters.fuse_many_confirm", materials.Count, name, target.Echoes, target.Echoes + materials.Count) + Warning(materials),
				() => FuseRequested?.Invoke(target.Id, materials.Select(m => m.Id).ToList()));
			return button;
		}

		/// <summary>Aviso quando a seleção leva monstro desperto, com nível, com Ecos, em equipe ou com runas.</summary>
		private string Warning(IEnumerable<OwnedSummon> monsters) =>
			monsters.Any(m => m.Awakened || m.Level > 1 || m.Echoes > 0 || TeamNames(m.Id).Count > 0 || _player.RunesOn(m.Id).Count > 0)
				? "\n\n" + T("monsters.valuable_warning")
				: "";

		private void Tab(string text, bool storage)
		{
			var button = Layout.IconButton(text, Art.Icon(storage ? "chest" : "storage"), 26, _showStorage == storage ? Palette.Background : Palette.Gold);
			button.ToggleMode = true;
			button.ButtonPressed = _showStorage == storage;
			button.CustomMinimumSize = new Vector2(0, 40);
			button.Pressed += () =>
			{
				_showStorage = storage;
				Refresh();
			};
			_tabs.AddChild(button);
		}

		private void RefreshRoster()
		{
			Layout.Clear(_roster);
			var monsters = (_showStorage ? _player.Storage : _player.Collection)
				.Where(m => _database.HasSummon(m.SummonId))
				.OrderByDescending(m => _database.Summon(m.SummonId).Rarity)
				.ThenByDescending(m => m.Level)
				.ThenBy(m => _database.Summon(m.SummonId).Element)
				.ThenBy(m => m.Id)
				.ToList();

			foreach (var monster in monsters)
			{
				var inTeam = TeamNames(monster.Id).Count > 0;
				var card = new CreatureCard(_database.Summon(monster.SummonId), monster, inTeam ? T("monsters.in_team") : null, 104);
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
				_roster.AddChild(Layout.Text(T(_showStorage ? "monsters.vault_empty" : "monsters.collection_empty"), GameTheme.Faded, 400));
		}

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			if (_selected is not { } id || _player.Monster(id) is not { } monster)
			{
				_detail.AddChild(new Label { Text = T("monsters.choose"), ThemeTypeVariation = GameTheme.Faded });
				return;
			}

			var summon = _database.Summon(monster.SummonId);
			var runes = _player.RunesOn(monster.Id);
			var sheet = SummonStats.For(_database.Roles[summon.Role], summon, monster.Level, monster.Echoes, monster.Awakened, runes);

			_detail.AddChild(Identity(summon, monster));
			_detail.AddChild(Actions(summon, monster));

			var teams = TeamNames(monster.Id);
			_detail.AddChild(Layout.Text(
				monster.Stored ? T("monsters.in_vault") : teams.Count == 0 ? T("monsters.no_team") : T("monsters.teams", string.Join(", ", teams)),
				GameTheme.Faded,
				540));

			Section(T("monsters.stats"));
			var table = new StatTable();
			table.Show(sheet);
			_detail.AddChild(table);

			Section(T("monsters.runes"));
			var runeRow = new HBoxContainer();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(runes.FirstOrDefault(r => r.Slot == slot), slot);
				tile.Pressed += _ => RunesRequested?.Invoke(monster.Id);
				runeRow.AddChild(tile);
			}

			_detail.AddChild(runeRow);
			Rich(sheet.Runes.ActiveSets.Count == 0
				? T("monsters.no_set")
				: string.Join("\n", sheet.Runes.ActiveSets.Select(s => $"{Texts.Term(s.Set)}: {Texts.Describe(s)}")), GameTheme.Faded);

			Section(T("monsters.skills"));
			Rich(T("monsters.basic", summon.Basic.Name));
			Rich(Texts.Describe(summon.Basic), GameTheme.Faded);
			Rich(T("monsters.special", summon.Special.Name, summon.Special.Cooldown));
			Rich(Texts.Describe(summon.Special), GameTheme.Faded);
			Rich(T("monsters.signature", summon.Family.Passive.Name));
			Rich(Texts.Describe(summon.Family.Passive, monster.Awakened), GameTheme.Faded);
			if (summon.Leader is { } leader)
				Rich(T("monsters.leadership", Texts.Percent(leader.Value), Texts.Name(leader.Stat)));

			Section(monster.Awakened ? T("monsters.awakened", summon.Awakening.Name) : T("monsters.awaken"));
			Rich(monster.Awakened
				? T("monsters.awakened_text")
				: T("monsters.awaken_text", summon.Awakening.Name, Texts.Percent(Awakening.HealthBonus), Texts.Percent(Awakening.AttackDefenseBonus),
					Texts.AwakeningBonus(summon.Awakening.Stat), Texts.Describe(summon.Family.Passive, true)), GameTheme.Faded);
		}

		private Control Identity(SummonDefinition summon, OwnedSummon monster)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 14);

			var frame = new PanelContainer { CustomMinimumSize = new Vector2(140, 140) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 3, 8, 6));
			frame.AddChild(new Doodle(Art.Creature(summon.ImageFor(monster.Awakened)), Palette.Of(summon.Element)));
			row.AddChild(frame);

			var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var name = new Label { Text = summon.NameFor(monster.Awakened), ThemeTypeVariation = GameTheme.Heading };
			if (monster.Awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			info.AddChild(name);
			if (monster.Awakened)
				info.AddChild(new Label { Text = summon.Name, ThemeTypeVariation = GameTheme.Faded });

			var stars = new Label { Text = Texts.Stars(summon.Rarity) + (monster.Echoes > 0 ? "   " + T("monsters.echoes", monster.Echoes, Growth.MaxEchoes) : "") };
			stars.AddThemeColorOverride("font_color", Palette.Stars(monster.Awakened));
			info.AddChild(stars);

			info.AddChild(new Label { Text = monster.Level >= Leveling.MaxLevel ? T("monsters.level_max", monster.Level) : T("monsters.level", monster.Level, Leveling.MaxLevel) });
			if (monster.Level < Leveling.MaxLevel)
			{
				var bar = new ProgressBar { MaxValue = Leveling.ExperienceToNext(monster.Level), Value = monster.Experience, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
				bar.AddThemeStyleboxOverride("fill", GameTheme.Box(Palette.Gold, Palette.Gold, 0, 3, 0));
				bar.TooltipText = T("monsters.experience", monster.Experience, Leveling.ExperienceToNext(monster.Level));
				info.AddChild(bar);
			}

			var identity = new HBoxContainer();
			identity.AddChild(Doodle.Icon(Art.Element(summon.Element), 20, Palette.Of(summon.Element)));
			identity.AddChild(new Label { Text = T("monsters.element_role", Texts.Name(summon.Element), Texts.Name(summon.Role)) });
			info.AddChild(identity);
			row.AddChild(info);
			return row;
		}

		private Control Actions(SummonDefinition summon, OwnedSummon monster)
		{
			var flow = new HFlowContainer();
			flow.AddThemeConstantOverride("h_separation", 6);
			flow.AddThemeConstantOverride("v_separation", 6);
			var id = monster.Id;

			if (monster.Level < Leveling.MaxLevel)
			{
				var next = Leveling.Missing(monster);
				Add(flow, T("monsters.level_up", next), _player.Essence < next, T("monsters.level_up_tip"), () => InfuseRequested?.Invoke(id, false));
				Add(flow, T("monsters.max_level_button", Leveling.MissingToMax(monster)), _player.Essence <= 0, T("monsters.max_level_tip"), () => InfuseRequested?.Invoke(id, true));
			}

			if (!monster.Awakened)
				Add(flow, T("monsters.awaken_button", Awakening.Cost(summon.Rarity)), !Awakening.CanAwaken(_player, monster, summon), T("monsters.awaken_tip"), () => AwakenRequested?.Invoke(id));

			Add(flow, T("monsters.runes_button"), false, T("monsters.runes_tip"), () => RunesRequested?.Invoke(id));
			if (!monster.Stored)
			{
				Add(flow, T("monsters.store"), false, T("monsters.store_tip"), () => StoreRequested?.Invoke(id));
			}
			else
			{
				var full = Roster.IsFull(_player);
				Add(flow, T("monsters.take_from_vault"), full, full ? T("monsters.collection_full") : "", () => RetrieveRequested?.Invoke(id));
			}

			flow.AddChild(FuseMenu(summon, monster));

			var fragments = Fusion.FragmentsFor(summon.Rarity);
			var release = new Button { Text = T("monsters.release", fragments), TooltipText = T("monsters.release_tip") };
			release.Pressed += () => Confirm(T("monsters.release_confirm", summon.NameFor(monster.Awakened), monster.Level, fragments), () => ReleaseRequested?.Invoke(new[] { id }));
			flow.AddChild(release);
			return flow;
		}

		/// <summary>Fundir: escolhe qual cópia da mesma variante vira Eco deste monstro.</summary>
		private Control FuseMenu(SummonDefinition summon, OwnedSummon monster)
		{
			var copies = _player.Monsters.Where(m => Fusion.CanFuse(_player, monster.Id, m.Id)).OrderBy(m => m.Level).ToList();
			var menu = new MenuButton { Text = T("monsters.fuse", copies.Count), Flat = false, Disabled = copies.Count == 0 };
			menu.TooltipText = monster.Echoes >= Growth.MaxEchoes
				? T("monsters.fuse_full")
				: T("monsters.fuse_tip", Texts.Percent(Growth.SkillPowerPerEcho), Growth.MaxEchoes);
			var popup = menu.GetPopup();
			for (var i = 0; i < copies.Count; i++)
			{
				var copy = copies[i];
				popup.AddItem(T("monsters.fuse_item", summon.NameFor(copy.Awakened), copy.Level, copy.Stored ? T("monsters.in_vault_short") : ""), i);
			}

			popup.IdPressed += index =>
			{
				var copy = copies[(int)index];
				Confirm(T("monsters.fuse_confirm", summon.NameFor(copy.Awakened), copy.Level), () => FuseRequested?.Invoke(monster.Id, new[] { copy.Id }));
			};
			return menu;
		}

		private void Confirm(string text, Action onConfirmed)
		{
			var dialog = new ConfirmationDialog { DialogText = text, Title = T("common.confirm"), OkButtonText = T("common.yes"), CancelButtonText = T("common.no") };
			dialog.Confirmed += onConfirmed;
			dialog.Confirmed += dialog.QueueFree;
			dialog.Canceled += dialog.QueueFree;
			AddChild(dialog);
			dialog.PopupCentered();
		}

		/// <summary>Os conteúdos em que o monstro está na equipe.</summary>
		private List<string> TeamNames(int monsterId) => _player.Teams
			.Where(pair => pair.Value.Contains(monsterId))
			.Select(pair => pair.Key == Teams.Campaign ? T("teams.campaign") : _database.Dungeons.FirstOrDefault(d => d.Id == pair.Key)?.Name ?? pair.Key)
			.ToList();

		private static void Add(HFlowContainer flow, string text, bool disabled, string tooltip, Action onPressed)
		{
			var button = new Button { Text = text, Disabled = disabled, TooltipText = tooltip };
			button.Pressed += onPressed;
			flow.AddChild(button);
		}

		private void Section(string title)
		{
			_detail.AddChild(new HSeparator());
			_detail.AddChild(new Label { Text = title, ThemeTypeVariation = GameTheme.Heading });
		}

		private void Rich(string text, string? variation = null) => _detail.AddChild(RichText.Label(text, 540, variation));
	}
}
