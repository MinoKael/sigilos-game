using System;
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
	/// Monstros: o "storage". À esquerda o time e a coleção; à direita a ficha da
	/// invocação escolhida — nível e experiência, atributos (base + runas), Glifo explicado,
	/// habilidades, Assinatura, Liderança, Despertar e as 6 runas — com os botões de cada ação.
	///
	/// Cada botão vira um evento; o GameRoot aplica a regra e chama <see cref="Refresh"/>.
	/// </summary>
	public partial class StorageScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private string? _selected;

		private readonly CurrencyBar _currencies = new();
		private readonly HBoxContainer _slots = new();
		private readonly GridContainer _roster = new() { Columns = 5 };
		private readonly Label _rosterTitle = new() { ThemeTypeVariation = GameTheme.Heading };
		private readonly VBoxContainer _detail = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };

		public StorageScreen(GameDatabase database, PlayerState player, string? selected)
		{
			_database = database;
			_player = player;
			_selected = selected ?? player.Team.FirstOrDefault(player.Owns) ?? player.Summons.Keys.FirstOrDefault();
		}

		public event Action<string>? ToggleTeamRequested;
		public event Action<string>? MakeLeaderRequested;

		/// <summary>Id e se é até o nível máximo (falso = só o próximo nível).</summary>
		public event Action<string, bool>? InfuseRequested;

		public event Action<string>? AwakenRequested;
		public event Action<string>? RunesRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			var header = new HBoxContainer();
			header.AddChild(new Label { Text = "Monstros", ThemeTypeVariation = GameTheme.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			header.AddChild(_currencies);
			var back = new Button { Text = "Voltar ao Santuário" };
			back.Pressed += () => BackRequested?.Invoke();
			header.AddChild(back);
			page.AddChild(header);

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 16);
			page.AddChild(body);

			var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			body.AddChild(left);

			var (teamPanel, team) = Layout.Section("Time · a primeira é a Líder");
			_slots.AddThemeConstantOverride("separation", 8);
			team.AddChild(_slots);
			left.AddChild(teamPanel);

			var rosterPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			var rosterColumn = new VBoxContainer();
			rosterPanel.AddChild(rosterColumn);
			rosterColumn.AddChild(_rosterTitle);
			var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_roster.AddThemeConstantOverride("h_separation", 8);
			_roster.AddThemeConstantOverride("v_separation", 8);
			scroll.AddChild(_roster);
			rosterColumn.AddChild(scroll);
			left.AddChild(rosterPanel);

			_detail.AddThemeConstantOverride("separation", 6);
			var detailPanel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
			var detailScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			detailScroll.AddChild(_detail);
			detailPanel.AddChild(detailScroll);
			body.AddChild(detailPanel);

			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			RefreshTeam();
			RefreshRoster();
			RefreshDetail();
		}

		private void RefreshTeam()
		{
			Layout.Clear(_slots);
			for (var i = 0; i < PlayerState.TeamSize; i++)
			{
				if (i < _player.Team.Count && _player.Owns(_player.Team[i]))
				{
					var id = _player.Team[i];
					var card = new CreatureCard(_database.Summon(id), _player.Summon(id), i == 0 ? "Líder" : null, 100);
					card.SetSelected(id == _selected);
					card.Pressed += c => Select(c.Summon.Id);
					_slots.AddChild(card);
				}
				else
				{
					var empty = new PanelContainer { CustomMinimumSize = new Vector2(100, 130), ThemeTypeVariation = GameTheme.InsetPanel };
					empty.AddChild(new Label { Text = "vazio", ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
					_slots.AddChild(empty);
				}
			}
		}

		private void RefreshRoster()
		{
			Layout.Clear(_roster);
			_rosterTitle.Text = $"Coleção · {_player.Summons.Count} invocações";
			var owned = _database.Summons
				.Where(s => _player.Owns(s.Id))
				.OrderByDescending(s => s.Rarity)
				.ThenByDescending(s => _player.Summon(s.Id).Level)
				.ThenBy(s => s.Element);

			foreach (var summon in owned)
			{
				var inTeam = _player.Team.Contains(summon.Id);
				var card = new CreatureCard(summon, _player.Summon(summon.Id), inTeam ? "no time" : null, 104);
				card.SetSelected(summon.Id == _selected);
				card.Pressed += c => Select(c.Summon.Id);
				_roster.AddChild(card);
			}
		}

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			if (_selected == null || !_player.Owns(_selected))
			{
				_detail.AddChild(new Label { Text = "Escolha uma invocação.", ThemeTypeVariation = GameTheme.Faded });
				return;
			}

			var summon = _database.Summon(_selected);
			var owned = _player.Summon(_selected);
			var runes = _player.RunesOn(_selected);
			var sheet = SummonStats.For(_database.Roles[summon.Role], summon, owned.Level, owned.Echoes, owned.Awakened, runes);

			_detail.AddChild(Identity(summon, owned));
			_detail.AddChild(Actions(summon, owned));

			Section("Atributos · base e bônus das runas");
			var table = new StatTable();
			table.Show(sheet);
			_detail.AddChild(table);

			Section("Runas");
			var runeRow = new HBoxContainer();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(runes.FirstOrDefault(r => r.Slot == slot), slot);
				tile.Pressed += _ => RunesRequested?.Invoke(summon.Id);
				runeRow.AddChild(tile);
			}

			_detail.AddChild(runeRow);
			Text(sheet.Runes.ActiveSets.Count == 0
				? "Nenhum conjunto completo."
				: string.Join("\n", sheet.Runes.ActiveSets.Select(s => $"{Texts.Name(s.Set)}: {Texts.Describe(s)}")), GameTheme.Faded);

			Section("Habilidades");
			Text($"Básico · {summon.Basic.Name}");
			Text(Texts.Describe(summon.Basic), GameTheme.Faded);
			Text($"Glifo · {summon.GlyphSkill.Name} (recarga {summon.GlyphSkill.Cooldown})");
			Text(Texts.Describe(summon.GlyphSkill), GameTheme.Faded);
			Text($"Assinatura · {summon.Family.Passive.Name}");
			Text(Texts.Describe(summon.Family.Passive, owned.Awakened), GameTheme.Faded);
			if (summon.Leader is { } leader)
				Text($"Liderança · +{leader.Value * 100:0}% de {Texts.Name(leader.Stat)} para o time (só como Líder)");

			Section(owned.Awakened ? $"Desperto · {summon.Awakening.Name}" : "Despertar");
			Text(owned.Awakened
				? "Nome próprio, desenho novo e Assinatura melhorada."
				: $"Vira {summon.Awakening.Name}: +{Awakening.HealthBonus * 100:0}% de Vida, +{Awakening.AttackDefenseBonus * 100:0}% de Ataque e Defesa, " +
				  $"{Texts.AwakeningBonus(summon.Awakening.Stat)}, desenho novo e Assinatura melhorada " +
				  $"({Texts.Describe(summon.Family.Passive, true)})", GameTheme.Faded);
		}

		private Control Identity(SummonDefinition summon, OwnedSummon owned)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 14);

			var frame = new PanelContainer { CustomMinimumSize = new Vector2(150, 150) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 3, 8, 6));
			frame.AddChild(new Doodle(Art.Creature(summon.ImageFor(owned.Awakened)), Palette.Of(summon.Element)));
			row.AddChild(frame);

			var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var name = new Label { Text = summon.NameFor(owned.Awakened), ThemeTypeVariation = GameTheme.Heading };
			if (owned.Awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			info.AddChild(name);
			if (owned.Awakened)
				info.AddChild(new Label { Text = summon.Name, ThemeTypeVariation = GameTheme.Faded });

			var stars = new Label { Text = Texts.Stars(summon.Rarity) + (owned.Echoes > 0 ? $"   Eco {owned.Echoes}/5" : "") };
			stars.AddThemeColorOverride("font_color", Palette.Stars(owned.Awakened));
			info.AddChild(stars);

			var level = new Label { Text = owned.Level >= Leveling.MaxLevel ? $"Nível {owned.Level} (máximo)" : $"Nível {owned.Level} de {Leveling.MaxLevel}" };
			info.AddChild(level);
			if (owned.Level < Leveling.MaxLevel)
			{
				var bar = new ProgressBar { MaxValue = Leveling.ExperienceToNext(owned.Level), Value = owned.Experience, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
				bar.AddThemeStyleboxOverride("fill", GameTheme.Box(Palette.Gold, Palette.Gold, 0, 3, 0));
				bar.TooltipText = $"Experiência: {owned.Experience}/{Leveling.ExperienceToNext(owned.Level)}";
				info.AddChild(bar);
			}

			var identity = new HBoxContainer();
			identity.AddChild(Doodle.Icon(Art.Element(summon.Element), 20, Palette.Of(summon.Element)));
			identity.AddChild(new Label { Text = $"{Texts.Name(summon.Element)} · {Texts.Name(summon.Role)}" });
			info.AddChild(identity);

			var glyph = new HBoxContainer { TooltipText = Texts.Meaning(summon.Glyph), MouseFilter = MouseFilterEnum.Stop };
			glyph.AddChild(Doodle.Icon(Art.Glyph(summon.Glyph), 20, Palette.Gold));
			glyph.AddChild(new Label { Text = $"Glifo {Texts.Name(summon.Glyph)} ({Texts.School(summon.Glyph)})", MouseFilter = MouseFilterEnum.Ignore });
			info.AddChild(glyph);
			info.AddChild(new Label
			{
				Text = Texts.Meaning(summon.Glyph),
				ThemeTypeVariation = GameTheme.Faded,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(340, 0),
			});
			row.AddChild(info);
			return row;
		}

		private Control Actions(SummonDefinition summon, OwnedSummon owned)
		{
			var flow = new HFlowContainer();
			flow.AddThemeConstantOverride("h_separation", 6);
			flow.AddThemeConstantOverride("v_separation", 6);
			var id = summon.Id;
			var inTeam = _player.Team.Contains(id);

			Add(flow, inTeam ? "Tirar do time" : "Pôr no time", !inTeam && _player.Team.Count >= PlayerState.TeamSize,
				inTeam ? "" : _player.Team.Count >= PlayerState.TeamSize ? "O time já tem 4. Tire alguém antes." : "", () => ToggleTeamRequested?.Invoke(id));
			if (inTeam && _player.Team[0] != id)
				Add(flow, "Tornar Líder", false, "A Líder aplica a Liderança dela ao time.", () => MakeLeaderRequested?.Invoke(id));

			var next = Leveling.Missing(owned);
			var max = Leveling.MissingToMax(owned);
			if (owned.Level < Leveling.MaxLevel)
			{
				Add(flow, $"+1 nível ({next} Essência)", _player.Essence < next, "Infunde Essência como experiência.", () => InfuseRequested?.Invoke(id, false));
				Add(flow, $"Nível máximo ({max})", _player.Essence <= 0, "Infunde toda a Essência que der, até o nível 40.", () => InfuseRequested?.Invoke(id, true));
			}

			if (!owned.Awakened)
				Add(flow, $"Despertar ({Awakening.Cost(summon.Rarity)} Essência)", !Awakening.CanAwaken(_player, summon), "Para sempre.", () => AwakenRequested?.Invoke(id));

			Add(flow, "Runas", false, "Equipar, melhorar, afiar e encantar runas.", () => RunesRequested?.Invoke(id));
			return flow;
		}

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

		private void Text(string text, string? variation = null)
		{
			var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0) };
			if (variation != null)
				label.ThemeTypeVariation = variation;
			_detail.AddChild(label);
		}

		private void Select(string id)
		{
			_selected = id;
			Refresh();
		}
	}
}
