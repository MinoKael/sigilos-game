using System;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A região 1: as 20 fases, o detalhe da escolhida e os botões Lutar e Resolver. Resolver só
	/// aparece em fase já vencida (GDD, seção 7) e simula na hora, sem tela de batalha.
	/// </summary>
	public partial class CampaignScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly GridContainer _grid = new() { Columns = 5 };
		private readonly VBoxContainer _detail = new();
		private readonly Label _message = new();
		private StageDefinition _selected;

		public CampaignScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
			_selected = database.Stage(Math.Min(player.HighestStage + 1, database.Stages.Count));
		}

		public event Action<StageDefinition>? FightRequested;
		public event Action<StageDefinition>? ResolveRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			var header = new HBoxContainer();
			header.AddChild(new Label { Text = "Planície dos Menires", ThemeTypeVariation = GameTheme.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var back = new Button { Text = "Voltar ao Santuário" };
			back.Pressed += () => BackRequested?.Invoke();
			header.AddChild(back);
			page.AddChild(header);
			page.AddChild(new Label { Text = "Região 1 · sem regra de batalha: ensina o básico.", ThemeTypeVariation = GameTheme.OnStone });

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 20);
			page.AddChild(body);

			_grid.AddThemeConstantOverride("h_separation", 10);
			_grid.AddThemeConstantOverride("v_separation", 10);
			body.AddChild(_grid);

			_detail.AddThemeConstantOverride("separation", 8);
			var (panel, content) = Layout.Section("Fase");
			panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			content.AddChild(_detail);
			body.AddChild(panel);

			_message.ThemeTypeVariation = GameTheme.OnStone;
			_message.HorizontalAlignment = HorizontalAlignment.Center;
			page.AddChild(_message);

			Refresh();
		}

		public void Refresh()
		{
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
					Text = $"{stage.Number}{(cleared ? " ✓" : "")}",
					CustomMinimumSize = new Vector2(92, 64),
					Disabled = !Campaign.IsUnlocked(_player, stage.Number),
					TooltipText = stage.Name,
				};
				if (stage == _selected)
					button.AddThemeStyleboxOverride("normal", GameTheme.Box(Palette.Gold, Palette.Ink, 3, 5, 8));
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
			_detail.AddChild(new Label { Text = $"{stage.Number}. {stage.Name}", ThemeTypeVariation = GameTheme.Heading });
			_detail.AddChild(new Label { Text = $"Inimigos no nível {stage.Level} · seu time no nível {_player.Level}" });

			for (var i = 0; i < stage.Waves.Count; i++)
			{
				var row = new HBoxContainer();
				row.AddChild(new Label { Text = $"Onda {i + 1}:", CustomMinimumSize = new Vector2(70, 0) });
				foreach (var slot in stage.Waves[i])
				{
					var enemy = _database.Enemy(slot.Enemy);
					var icon = Doodle.Icon(Art.Creature(enemy.Image), 40, Palette.Of(slot.Element));
					icon.TooltipText = $"{enemy.Name} ({Texts.Name(slot.Element)})";
					icon.MouseFilter = MouseFilterEnum.Stop;
					row.AddChild(icon);
				}

				_detail.AddChild(row);
			}

			var reward = cleared
				? $"Vitória: {stage.Essence} Essência"
				: $"Primeira vitória: {Texts.Scrolls(stage.FirstClearScrolls)} e {stage.Essence + stage.FirstClearEssence} Essência";
			_detail.AddChild(new Label { Text = reward });

			foreach (var line in stage.Lines)
			{
				_detail.AddChild(new Label
				{
					Text = $"“{line}”",
					ThemeTypeVariation = GameTheme.Faded,
					AutowrapMode = TextServer.AutowrapMode.WordSmart,
					CustomMinimumSize = new Vector2(400, 0),
				});
			}

			var buttons = new HBoxContainer();
			buttons.AddThemeConstantOverride("separation", 12);
			var fight = new Button { Text = "Lutar", CustomMinimumSize = new Vector2(160, 52), Disabled = _player.Team.Count == 0 };
			fight.Pressed += () => FightRequested?.Invoke(stage);
			buttons.AddChild(fight);
			if (cleared)
			{
				var resolve = new Button { Text = "Resolver", CustomMinimumSize = new Vector2(160, 52), TooltipText = "Simula a luta na hora, no automático." };
				resolve.Pressed += () => ResolveRequested?.Invoke(stage);
				buttons.AddChild(resolve);
			}

			_detail.AddChild(buttons);

			if (_player.Team.Count == 0)
				_detail.AddChild(new Label { Text = "Monte um time em Time e Grimório.", ThemeTypeVariation = GameTheme.Faded });
			else if (_player.Team.Count(_player.Owns) < PlayerState.TeamSize)
				_detail.AddChild(new Label { Text = $"O time tem {_player.Team.Count} de {PlayerState.TeamSize} invocações.", ThemeTypeVariation = GameTheme.Faded });
		}
	}
}
