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
	/// A região 1: as 20 fases, o detalhe da escolhida e os botões Lutar e Resolver, cada um com o custo
	/// em Mana. Resolver só aparece em fase já vencida (GDD, seção 7) e simula na hora, sem tela de
	/// batalha. Sem Mana, o motivo aparece embaixo, ao lado da Loja.
	/// </summary>
	public partial class CampaignScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly GridContainer _grid = new() { Columns = 5 };
		private readonly VBoxContainer _detail = new();
		private readonly Label _message = new();
		private StageDefinition _selected;

		public CampaignScreen(GameDatabase database, PlayerState player, int? selected = null)
		{
			_database = database;
			_player = player;
			_selected = database.Stage(selected ?? Math.Min(player.HighestStage + 1, database.Stages.Count));
		}

		public event Action<StageDefinition>? FightRequested;
		public event Action<StageDefinition>? ResolveRequested;
		public event Action? TeamRequested;
		public event Action? ShopRequested;
		public event Action? BackRequested;

		/// <summary>A fase escolhida agora: a tela volta nela depois da luta ou da Loja.</summary>
		public int Selected => _selected.Number;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			page.AddChild(Layout.Header(T("campanha.titulo"), _currencies, T("geral.voltar_santuario"), () => BackRequested?.Invoke()));
			page.AddChild(new Label { Text = T("campanha.subtitulo"), ThemeTypeVariation = GameTheme.Faded });

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 20);
			page.AddChild(body);

			_grid.AddThemeConstantOverride("h_separation", 10);
			_grid.AddThemeConstantOverride("v_separation", 10);
			body.AddChild(_grid);

			_detail.AddThemeConstantOverride("separation", 8);
			var (panel, content) = Layout.Section(T("campanha.fase"));
			panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			content.AddChild(_detail);
			body.AddChild(panel);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			_message.HorizontalAlignment = HorizontalAlignment.Center;
			page.AddChild(_message);

			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			RefreshGrid();
			RefreshDetail();
		}

		/// <summary>Resultado do Resolver, na faixa de baixo.</summary>
		public void ShowMessage(string text) => _message.Text = text;

		private void RefreshGrid()
		{
			Layout.Clear(_grid);

			foreach (var stage in _database.Stages)
			{
				var cleared = Campaign.IsCleared(_player, stage.Number);
				var button = new Button
				{
					Text = cleared ? T("campanha.numero_vencida", stage.Number) : stage.Number.ToString(),
					CustomMinimumSize = new Vector2(92, 64),
					Disabled = !Campaign.IsUnlocked(_player, stage.Number),
					TooltipText = stage.Name,
				};
				if (stage == _selected)
					button.AddThemeStyleboxOverride("normal", GameTheme.Box(Palette.Gold, Palette.Text, 3, 5, 8));
				var captured = stage;
				button.Pressed += () =>
				{
					_selected = captured;
					_message.Text = "";
					Refresh();
				};
				_grid.AddChild(button);
			}
		}

		private void RefreshDetail()
		{
			Layout.Clear(_detail);

			var stage = _selected;
			var cleared = Campaign.IsCleared(_player, stage.Number);
			_detail.AddChild(new Label { Text = T("campanha.fase_titulo", stage.Number, stage.Name), ThemeTypeVariation = GameTheme.Heading });
			var levels = Teams.Of(_player, Teams.Campaign).Select(_player.Monster).OfType<OwnedSummon>().Select(m => m.Level).ToList();
			_detail.AddChild(new Label { Text = T("campanha.niveis", stage.Level, levels.Count == 0 ? "—" : string.Join(", ", levels)) });

			for (var i = 0; i < stage.Waves.Count; i++)
			{
				var row = new HBoxContainer();
				row.AddChild(new Label { Text = T("campanha.onda", i + 1), CustomMinimumSize = new Vector2(70, 0) });
				foreach (var slot in stage.Waves[i])
				{
					var enemy = _database.Enemy(slot.Enemy);
					var icon = Doodle.Icon(Art.Creature(enemy.Image), 40, Palette.Of(slot.Element));
					icon.TooltipText = T("campanha.inimigo_dica", enemy.Name, Texts.Name(slot.Element));
					icon.MouseFilter = MouseFilterEnum.Stop;
					row.AddChild(icon);
				}

				_detail.AddChild(row);
			}

			var reward = cleared
				? T("campanha.recompensa", stage.Essence, stage.Experience, Texts.Percent(Campaign.RepeatRuneChance), Texts.Stars(stage.RuneGrade))
				: T("campanha.recompensa_primeira", Texts.Scrolls(stage.FirstClearScrolls), stage.Essence + stage.FirstClearEssence, stage.Experience, Texts.Stars(stage.RuneGrade));
			_detail.AddChild(Layout.Text(reward, width: 400));

			foreach (var line in stage.Lines)
				_detail.AddChild(Layout.Text(T("campanha.fala", line), GameTheme.Faded, 400));

			var buttons = new HBoxContainer();
			buttons.AddThemeConstantOverride("separation", 12);
			var problem = Campaign.Check(_player, stage);
			var blocked = Teams.Of(_player, Teams.Campaign).Count == 0 || problem != EntryProblem.None;
			var fight = new Button { Text = T("geral.lutar_mana", stage.Mana), CustomMinimumSize = new Vector2(150, 52), Disabled = blocked };
			fight.Pressed += () => FightRequested?.Invoke(stage);
			buttons.AddChild(fight);
			if (cleared)
			{
				var resolve = new Button { Text = T("geral.resolver_mana", stage.Mana), CustomMinimumSize = new Vector2(150, 52), TooltipText = T("geral.resolver_dica"), Disabled = blocked };
				resolve.Pressed += () => ResolveRequested?.Invoke(stage);
				buttons.AddChild(resolve);
			}

			var team = new Button { Text = T("geral.equipe"), CustomMinimumSize = new Vector2(120, 52) };
			team.Pressed += () => TeamRequested?.Invoke();
			buttons.AddChild(team);
			var shop = new Button { Text = T("geral.loja"), CustomMinimumSize = new Vector2(120, 52), TooltipText = T("geral.loja_dica") };
			shop.Pressed += () => ShopRequested?.Invoke();
			buttons.AddChild(shop);
			_detail.AddChild(buttons);

			if (problem is EntryProblem.NoMana or EntryProblem.RunesFull)
			{
				var refusal = Layout.Text(Texts.Refusal(problem, stage.Mana), width: 400);
				refusal.AddThemeColorOverride("font_color", Palette.Negative);
				_detail.AddChild(refusal);
			}

			var size = Teams.Of(_player, Teams.Campaign).Count;
			if (size < PlayerState.TeamSize)
				_detail.AddChild(new Label { Text = T("geral.equipe_incompleta", size, PlayerState.TeamSize), ThemeTypeVariation = GameTheme.Faded });
		}
	}
}
