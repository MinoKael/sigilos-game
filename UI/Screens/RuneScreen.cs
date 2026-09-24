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

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// As runas de uma invocação. Os 6 espaços ficam em círculo em volta do desenho — "o próprio círculo
	/// de conjuração" (GDD, seção 10) —; ao lado, o inventário filtrado pelo espaço escolhido, as pedras
	/// guardadas e a ficha da runa selecionada: equipar, tirar, melhorar, afiar, encantar e desfazer.
	/// </summary>
	public partial class RuneScreen : Control
	{
		private const float CircleRadius = 150;
		private static readonly Vector2 CircleCenter = new(200, 196);

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly string _summonId;
		private int _slotFilter;
		private int? _selectedRune;
		private int _maxRuneInventory = 800;

		private readonly CurrencyBar _currencies = new();
		private readonly Control _circle = new() { CustomMinimumSize = new Vector2(400, 400) };
		private readonly VBoxContainer _summary = new();
		private readonly HBoxContainer _filters = new();
		private readonly GridContainer _inventory = new() { Columns = 4 };
		private readonly Label _inventoryTitle = new() { ThemeTypeVariation = GameTheme.Heading };
		private readonly Label _tools = new() { ThemeTypeVariation = GameTheme.Faded, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private readonly VBoxContainer _detail = new();

		public RuneScreen(GameDatabase database, PlayerState player, string summonId)
		{
			_database = database;
			_player = player;
			_summonId = summonId;
		}

		public event Action<int>? EquipRequested;
		public event Action<int>? UnequipRequested;

		/// <summary>Id da runa e o nível a alcançar.</summary>
		public event Action<int, int>? UpgradeRequested;

		/// <summary>Id da runa, índice do subatributo e a Pedra de Afiar.</summary>
		public event Action<int, int, RuneTool>? GrindRequested;

		/// <summary>Id da runa, índice do subatributo e a Gema Encantada.</summary>
		public event Action<int, int, RuneTool>? EnchantRequested;

		public event Action<int>? SellRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			var summon = _database.Summon(_summonId);
			var header = new HBoxContainer();
			header.AddChild(new Label { Text = $"Runas · {summon.NameFor(_player.Summon(_summonId).Awakened)}", ThemeTypeVariation = GameTheme.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			header.AddChild(_currencies);
			var back = new Button { Text = "Voltar" };
			back.Pressed += () => BackRequested?.Invoke();
			header.AddChild(back);
			page.AddChild(header);

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 12);
			page.AddChild(body);

			var left = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
			var leftColumn = new VBoxContainer();
			leftColumn.AddChild(_circle);
			var summaryScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			summaryScroll.AddChild(_summary);
			leftColumn.AddChild(summaryScroll);
			left.AddChild(leftColumn);
			body.AddChild(left);

			var middle = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var middleColumn = new VBoxContainer();
			middleColumn.AddChild(_inventoryTitle);
			_filters.AddThemeConstantOverride("separation", 4);
			middleColumn.AddChild(_filters);
			var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_inventory.AddThemeConstantOverride("h_separation", 6);
			_inventory.AddThemeConstantOverride("v_separation", 6);
			scroll.AddChild(_inventory);
			middleColumn.AddChild(scroll);
			middleColumn.AddChild(new Label { Text = "Pedras", ThemeTypeVariation = GameTheme.Heading });
			middleColumn.AddChild(_tools);
			middle.AddChild(middleColumn);
			body.AddChild(middle);

			var right = new PanelContainer { CustomMinimumSize = new Vector2(360, 0) };
			var detailScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_detail.AddThemeConstantOverride("separation", 6);
			detailScroll.AddChild(_detail);
			right.AddChild(detailScroll);
			body.AddChild(right);

			Refresh();
		}

		public void Refresh()
		{
			if (_selectedRune is { } id && _player.Runes.All(r => r.Id != id))
				_selectedRune = null;

			_currencies.Refresh(_player);
			RefreshCircle();
			RefreshSummary();
			RefreshInventory();
			RefreshDetail();
		}

		private void RefreshCircle()
		{
			Layout.Clear(_circle);
			var summon = _database.Summon(_summonId);
			var owned = _player.Summon(_summonId);

			var ring = new Doodle(Art.Icon("summon"), Palette.PanelLight) { Size = new Vector2(380, 380), Position = CircleCenter - new Vector2(190, 190) };
			_circle.AddChild(ring);
			var portrait = new Doodle(Art.Creature(summon.ImageFor(owned.Awakened)), Palette.Of(summon.Element)) { Size = new Vector2(150, 150), Position = CircleCenter - new Vector2(75, 75) };
			_circle.AddChild(portrait);

			var equipped = _player.RunesOn(_summonId);
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var angle = Mathf.DegToRad(-90 + 60 * (slot - 1));
				var tile = new RuneTile(equipped.FirstOrDefault(r => r.Slot == slot), slot);
				tile.Position = CircleCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * CircleRadius - RuneTile.TileSize / 2;
				tile.SetSelected(tile.Rune != null ? tile.Rune.Id == _selectedRune : slot == _slotFilter);
				tile.Pressed += t =>
				{
					_slotFilter = t.Slot;
					_selectedRune = t.Rune?.Id;
					Refresh();
				};
				_circle.AddChild(tile);
			}
		}

		private void RefreshSummary()
		{
			Layout.Clear(_summary);
			var summon = _database.Summon(_summonId);
			var owned = _player.Summon(_summonId);
			var sheet = SummonStats.For(_database.Roles[summon.Role], summon, owned.Level, owned.Echoes, owned.Awakened, _player.RunesOn(_summonId));

			_summary.AddChild(new Label { Text = "Conjuntos ativos", ThemeTypeVariation = GameTheme.Heading });
			_summary.AddChild(new Label
			{
				Text = sheet.Runes.ActiveSets.Count == 0
					? "Nenhum. Junte 2 ou 4 runas do mesmo conjunto."
					: string.Join("\n", sheet.Runes.ActiveSets.Select(s => $"{Texts.Name(s.Set)}: {Texts.Describe(s)}")),
				ThemeTypeVariation = GameTheme.Faded,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(380, 0),
			});
			var table = new StatTable();
			table.Show(sheet);
			_summary.AddChild(table);
		}

		private void RefreshInventory()
		{
			Layout.Clear(_filters);
			for (var slot = 0; slot <= RuneRules.Slots; slot++)
			{
				var value = slot;
				var button = new Button { Text = slot == 0 ? "Todas" : $"{slot}", ToggleMode = true, ButtonPressed = slot == _slotFilter, CustomMinimumSize = new Vector2(slot == 0 ? 70 : 40, 0) };
				button.Pressed += () =>
				{
					_slotFilter = value;
					Refresh();
				};
				_filters.AddChild(button);
			}

			Layout.Clear(_inventory);
			var free = _player.Runes
				.Where(r => r.EquippedOn == null && (_slotFilter == 0 || r.Slot == _slotFilter))
				.OrderByDescending(r => r.Grade)
				.ThenByDescending(r => r.Rarity)
				.ThenByDescending(r => r.Level)
				.ThenBy(r => r.Slot)
				.ToList();
			_inventoryTitle.Text = $"Inventário · {free.Count}/{_maxRuneInventory}";

			foreach (var rune in free)
			{
				var tile = new RuneTile(rune, rune.Slot);
				tile.SetSelected(rune.Id == _selectedRune);
				tile.Pressed += t =>
				{
					_selectedRune = t.Rune!.Id;
					Refresh();
				};
				_inventory.AddChild(tile);
			}

			_tools.Text = _player.Tools.Count == 0
				? "Nenhuma."
				: string.Join("\n", _player.Tools
					.GroupBy(t => t)
					.OrderBy(g => g.Key.Kind)
					.ThenByDescending(g => g.Key.Grade)
					.Select(g => $"{g.Count()}× {Texts.Name(g.Key)} ({Texts.Range(g.Key)})"));
		}

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			var rune = _player.Runes.FirstOrDefault(r => r.Id == _selectedRune);
			if (rune == null)
			{
				_detail.AddChild(new Label { Text = "Runa", ThemeTypeVariation = GameTheme.Heading });
				Text("Escolha um espaço no círculo ou uma runa do inventário.\n\n" +
					"Espaços 1, 3 e 5 dão sempre Ataque, Defesa e Vida fixos. Os espaços 2, 4 e 6 variam: é neles que moram Velocidade, " +
					"Crítico, Dano crítico, Resistência, Precisão e as porcentagens. Duas ou quatro runas do mesmo conjunto dão o bônus dele.", GameTheme.Faded);
				return;
			}

			var color = Palette.Of(rune.Rarity);
			var title = new Label { Text = $"{Texts.Title(rune)}  +{rune.Level}", ThemeTypeVariation = GameTheme.Heading };
			title.AddThemeColorOverride("font_color", color);
			_detail.AddChild(title);
			var grade = new Label { Text = $"{Texts.Name(rune.Rarity)}  {Texts.Stars(rune.Grade)}" };
			grade.AddThemeColorOverride("font_color", color);
			_detail.AddChild(grade);
			Text($"Conjunto: {Texts.Describe(RuneSets.For(rune.Set))}", GameTheme.Faded);

			var main = new Label { Text = Texts.Format(rune.Main, rune.MainValue) };
			main.AddThemeFontOverride("font", GameTheme.Serif);
			main.AddThemeFontSizeOverride("font_size", 20);
			_detail.AddChild(main);

			if (rune.Innate is { } innate)
			{
				var label = new Label { Text = $"Nativo: {Texts.Format(innate)}", TooltipText = "Vem no drop e nunca cresce.", MouseFilter = MouseFilterEnum.Stop };
				label.AddThemeColorOverride("font_color", Palette.Gold);
				_detail.AddChild(label);
			}

			for (var i = 0; i < rune.Substats.Count; i++)
				_detail.AddChild(SubstatRow(rune, i));

			Text(rune.Level < RuneRules.MaxLevel
				? "Em +3, +6, +9 e +12 entra um subatributo novo (até 4) ou, com 4, um deles cresce. Em +15 o principal dá o salto final. A melhora nunca falha."
				: "Melhora máxima.", GameTheme.Faded);

			if (rune.EquippedOn is { } owner && owner != _summonId)
				Text($"Equipada em {_database.Summon(owner).NameFor(_player.Summon(owner).Awakened)}.", GameTheme.Faded);

			var actions = new HFlowContainer();
			actions.AddThemeConstantOverride("h_separation", 6);
			actions.AddThemeConstantOverride("v_separation", 6);
			if (rune.EquippedOn == _summonId)
			{
				Add(actions, $"Remover", false, () => UnequipRequested?.Invoke(rune.Id));
			}
			else
			{
				Add(actions, "Equipar", false, () => EquipRequested?.Invoke(rune.Id));
			}

			if (rune.Level < RuneRules.MaxLevel)
			{
				var next = RuneRules.UpgradeCost(rune);
				Add(actions, $"Melhorar +{rune.Level + 1} ({next} Pó)", _player.Dust < next, () => UpgradeRequested?.Invoke(rune.Id, rune.Level + 1));

				var milestone = RuneRules.NextMilestone(rune.Level);
				if (milestone > rune.Level + 1)
				{
					var total = RuneRules.UpgradeCost(rune, milestone);
					Add(actions, $"Até +{milestone} ({total} Pó)", _player.Dust < total, () => UpgradeRequested?.Invoke(rune.Id, milestone));
				}
			}

			if (rune.EquippedOn == null)
				Add(actions, $"Desfazer (+{RuneRules.SellValue(rune)} Pó)", false, () => SellRequested?.Invoke(rune.Id));
			_detail.AddChild(actions);

			if (rune.EquippedOn != null)
				Text($"Tirar uma runa {Texts.Stars(rune.Grade)} de uma invocação é gratuito.", GameTheme.Faded);
		}

		/// <summary>Um subatributo com os botões das pedras que servem nele.</summary>
		private Control SubstatRow(Rune rune, int index)
		{
			var substat = rune.Substats[index];
			var row = new HBoxContainer();
			var label = new Label { Text = Texts.Format(substat), SizeFlagsHorizontal = SizeFlags.ExpandFill };
			if (substat.Enchanted)
			{
				label.Text += " ◆";
				label.TooltipText = "Encantado por Gema. Só um subatributo por runa pode ser encantado.";
				label.MouseFilter = MouseFilterEnum.Stop;
			}

			row.AddChild(label);

			var grindstones = Usable(t => RuneForge.CanGrind(rune, index, t));
			if (grindstones.Count > 0)
				row.AddChild(ToolMenu("Afiar", grindstones, tool => GrindRequested?.Invoke(rune.Id, index, tool)));

			var gems = Usable(t => RuneForge.CanEnchant(rune, index, t));
			if (gems.Count > 0)
				row.AddChild(ToolMenu("Encantar", gems, tool => EnchantRequested?.Invoke(rune.Id, index, tool)));

			return row;
		}

		/// <summary>Pedras diferentes que servem, uma de cada.</summary>
		private List<RuneTool> Usable(Func<RuneTool, bool> fits) =>
			_player.Tools.Distinct().Where(fits).OrderByDescending(t => t.Grade).ToList();

		private static MenuButton ToolMenu(string text, IReadOnlyList<RuneTool> tools, Action<RuneTool> chosen)
		{
			var menu = new MenuButton { Text = text, Flat = false };
			menu.AddThemeFontSizeOverride("font_size", 13);
			var popup = menu.GetPopup();
			for (var i = 0; i < tools.Count; i++)
				popup.AddItem($"{Texts.Name(tools[i])} ({Texts.Range(tools[i])})", i);
			popup.IdPressed += id => chosen(tools[(int)id]);
			return menu;
		}

		private static void Add(HFlowContainer flow, string text, bool disabled, Action onPressed)
		{
			var button = new Button { Text = text, Disabled = disabled };
			button.Pressed += onPressed;
			flow.AddChild(button);
		}

		private void Text(string text, string? variation = null)
		{
			var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(330, 0) };
			if (variation != null)
				label.ThemeTypeVariation = variation;
			_detail.AddChild(label);
		}
	}
}
