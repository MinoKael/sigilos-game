using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using Side = Sigilos.Core.Battle.Side;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A luta na tela. Quem decide as regras é o <see cref="BattleSession"/>; esta tela só:
	///
	/// 1. pede o próximo turno,
	/// 2. anima os <see cref="BattleEvent"/> que voltam,
	/// 3. na vez de um aliado ou do Conjurador, espera o clique do jogador (manual) ou pergunta ao
	///    <see cref="AutoPilot"/> (automático). O automático pode ser ligado e desligado no meio.
	///
	/// No fim avisa <see cref="Finished"/>; o GameRoot aplica a recompensa e chama <see cref="ShowResult"/>.
	/// </summary>
	public partial class BattleScreen : Control
	{
		private static readonly float[] Speeds = { 1, 2, 4 };

		private readonly BattleSession _session;
		private readonly StageDefinition _stage;
		private readonly Posture _posture;
		private readonly Dictionary<BattleUnit, UnitView> _views = new();

		private readonly GridContainer _allies = new() { Columns = 2 };
		private readonly GridContainer _enemies = new() { Columns = 3 };
		private readonly Label _wave = new() { ThemeTypeVariation = GameTheme.OnStone };
		private readonly Label _round = new() { ThemeTypeVariation = GameTheme.OnStone };
		private readonly Label _banner = new();
		private readonly Label _prompt = new() { ThemeTypeVariation = GameTheme.OnStone };
		private readonly HBoxContainer _actions = new();
		private readonly EtherGauge _ether = new();
		private readonly Button _autoButton = new() { ToggleMode = true };
		private readonly Button _speedButton = new();
		private readonly PanelContainer _conjurerCard = new();
		private readonly ProgressBar _conjurerImpeto = new() { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 6), MaxValue = 100 };
		private TurnOrderBar _order = null!;

		private bool _auto;
		private int _speedIndex;
		private bool _closed;

		/// <summary>A decisão que a tela está esperando do jogador; nula quando ninguém espera.</summary>
		private Action? _decideAutomatically;

		/// <summary>O que um clique em unidade faz agora; nulo fora da escolha de alvo.</summary>
		private Action<BattleUnit>? _pickTarget;

		public BattleScreen(BattleSession session, StageDefinition stage, Posture posture, bool auto)
		{
			_session = session;
			_stage = stage;
			_posture = posture;
			_auto = auto;
		}

		/// <summary>A luta acabou: verdadeiro na vitória.</summary>
		public event Action<bool>? Finished;

		/// <summary>O jogador saiu da tela (depois do resultado ou recuando). O valor é a preferência de automático.</summary>
		public event Action<bool>? Closed;

		private float Speed => Speeds[_speedIndex];

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			page.AddChild(TopBar());
			_order = new TurnOrderBar(_session.Conjurer.Definition.Image);
			page.AddChild(_order);
			page.AddChild(Field());
			page.AddChild(BottomBar());

			Run();
		}

		/// <summary>Mostra vitória ou derrota e o que a luta rendeu. <paramref name="reward"/> é nulo na derrota.</summary>
		public void ShowResult(bool victory, StageReward? reward)
		{
			var overlay = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
			overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(overlay);

			var center = new CenterContainer();
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			overlay.AddChild(center);

			var (panel, content) = Layout.Section(victory ? "Vitória" : "Derrota");
			panel.CustomMinimumSize = new Vector2(420, 0);
			center.AddChild(panel);

			var lines = new List<string>();
			if (reward != null)
			{
				if (reward.FirstClear)
					lines.Add("Primeira vitória nesta fase!");
				if (reward.Scrolls > 0)
					lines.Add($"+{Texts.Scrolls(reward.Scrolls)}");
				lines.Add($"+{reward.Essence} Essência");
			}
			else
			{
				lines.Add(_session.Round > BattleRules.RoundLimit
					? $"O tempo acabou: {BattleRules.RoundLimit} rodadas."
					: "Todas as invocações caíram.");
				lines.Add("Eleve o nível no Santuário ou troque o time e o Grimório.");
			}

			foreach (var line in lines)
				content.AddChild(new Label { Text = line, AutowrapMode = TextServer.AutowrapMode.WordSmart });

			var close = new Button { Text = "Continuar", CustomMinimumSize = new Vector2(0, 48) };
			close.Pressed += Close;
			content.AddChild(close);
		}

		public override void _ExitTree() => _closed = true;

		private Control TopBar()
		{
			var bar = new PanelContainer { ThemeTypeVariation = GameTheme.DarkPanel };
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 24);
			bar.AddChild(row);

			var title = new Label { Text = $"Fase {_stage.Number} · {_stage.Name}", ThemeTypeVariation = GameTheme.OnStone };
			title.AddThemeFontOverride("font", GameTheme.Serif);
			title.AddThemeFontSizeOverride("font_size", 20);
			row.AddChild(title);
			row.AddChild(_wave);
			row.AddChild(_round);
			row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

			_autoButton.ButtonPressed = _auto;
			_autoButton.Toggled += SetAuto;
			RefreshAutoText();
			row.AddChild(_autoButton);

			_speedButton.Text = "Velocidade 1×";
			_speedButton.Pressed += () =>
			{
				_speedIndex = (_speedIndex + 1) % Speeds.Length;
				_speedButton.Text = $"Velocidade {Speed:0}×";
			};
			row.AddChild(_speedButton);

			var flee = new Button { Text = "Recuar", TooltipText = "Sai da luta sem recompensa." };
			flee.Pressed += Close;
			row.AddChild(flee);
			return bar;
		}

		private Control Field()
		{
			var field = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			field.AddThemeConstantOverride("separation", 16);

			_allies.AddThemeConstantOverride("h_separation", 10);
			_allies.AddThemeConstantOverride("v_separation", 10);
			_allies.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			foreach (var ally in _session.Allies)
				_allies.AddChild(ViewFor(ally));
			field.AddChild(_allies);

			_banner.HorizontalAlignment = HorizontalAlignment.Center;
			_banner.VerticalAlignment = VerticalAlignment.Center;
			_banner.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_banner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_banner.AddThemeFontOverride("font", GameTheme.Serif);
			_banner.AddThemeFontSizeOverride("font_size", 24);
			_banner.AddThemeColorOverride("font_color", Palette.Bone);
			field.AddChild(_banner);

			_enemies.AddThemeConstantOverride("h_separation", 10);
			_enemies.AddThemeConstantOverride("v_separation", 10);
			_enemies.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			field.AddChild(_enemies);
			return field;
		}

		private Control BottomBar()
		{
			var bottom = new HBoxContainer();
			bottom.AddThemeConstantOverride("separation", 16);

			_conjurerCard.CustomMinimumSize = new Vector2(150, 0);
			_conjurerCard.TooltipText = $"{_session.Conjurer.Name}: fora de campo, não pode ser atacado. No turno dele, lança uma página ou canaliza Éter.";
			var column = new VBoxContainer();
			var row = new HBoxContainer();
			row.AddChild(Doodle.Icon(Art.Creature(_session.Conjurer.Definition.Image), 40, Palette.Gold));
			row.AddChild(new Label { Text = _session.Conjurer.Name, VerticalAlignment = VerticalAlignment.Center });
			column.AddChild(row);
			_conjurerImpeto.AddThemeStyleboxOverride("fill", GameTheme.Box(Palette.Gold, Palette.Gold, 0, 3, 0));
			column.AddChild(_conjurerImpeto);
			_conjurerCard.AddChild(column);
			bottom.AddChild(_conjurerCard);

			var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			right.AddChild(_ether);
			var actionRow = new HBoxContainer();
			actionRow.AddThemeConstantOverride("separation", 10);
			actionRow.AddChild(_prompt);
			_actions.AddThemeConstantOverride("separation", 8);
			actionRow.AddChild(_actions);
			right.AddChild(actionRow);
			bottom.AddChild(right);
			return bottom;
		}

		private UnitView ViewFor(BattleUnit unit)
		{
			var view = new UnitView(unit);
			view.Pressed += v => _pickTarget?.Invoke(v.Unit);
			_views[unit] = view;
			return view;
		}

		private async void Run()
		{
			await Play(_session.Start());
			while (!_session.IsOver && !_closed)
			{
				var turn = _session.BeginTurn();
				await Play(turn.Events);
				if (!turn.NeedsDecision || _closed)
					continue;

				IReadOnlyList<BattleEvent> events = turn.Actor switch
				{
					ConjurerSeat => _session.Act(await DecideConjurer()),
					BattleUnit { Side: Side.Allies } ally => _session.Act(await DecideAlly(ally)),
					BattleUnit enemy => _session.Act(AutoPilot.ForEnemy(_session, enemy)),
					_ => Array.Empty<BattleEvent>(),
				};
				if (_closed)
					return;
				await Play(events);
			}

			if (!_closed)
				Finished?.Invoke(_session.Victory == true);
		}

		// Decisões ----------------------------------------------------------------------------------

		private Task<UnitAction> DecideAlly(BattleUnit ally)
		{
			if (_auto)
				return Task.FromResult(AutoPilot.ForAlly(_session, ally, _posture));

			var decision = new TaskCompletionSource<UnitAction>(TaskCreationOptions.RunContinuationsAsynchronously);
			void Decide(UnitAction action)
			{
				ClearActions();
				decision.TrySetResult(action);
			}

			_decideAutomatically = () => Decide(AutoPilot.ForAlly(_session, ally, _posture));
			_prompt.Text = $"Vez de {ally.Name}:";

			var enhance = new CheckBox { Text = "Aprimorar", TooltipText = "Paga Éter para usar a versão aprimorada." };
			enhance.AddThemeColorOverride("font_color", Palette.Bone);
			enhance.AddThemeColorOverride("font_hover_color", Palette.Bone);
			enhance.AddThemeColorOverride("font_pressed_color", Palette.Bone);

			foreach (var slot in new[] { SkillSlot.Basic, SkillSlot.Glyph })
			{
				var skill = ally.Skill(slot);
				var ready = slot == SkillSlot.Basic || ally.IsGlyphReady;
				var cost = skill.CanEnhance ? $" [+{skill.EnhanceCost}]" : "";
				var wait = ready ? "" : $" (⟳{ally.GlyphCooldown})";
				var button = new Button { Text = $"{skill.Name}{cost}{wait}", Disabled = !ready, TooltipText = Texts.Describe(skill) };
				button.Pressed += () =>
				{
					var enhanced = enhance.ButtonPressed && _session.CanEnhance(ally, skill);
					if (skill.NeedsTarget)
						PickTarget(ally, target => Decide(new UnitAction(slot, enhanced, target)));
					else
						Decide(new UnitAction(slot, enhanced, null));
				};
				_actions.AddChild(button);
			}

			_actions.AddChild(enhance);
			return decision.Task;
		}

		private Task<ConjurerAction> DecideConjurer()
		{
			if (_auto)
				return Task.FromResult(AutoPilot.ForConjurer(_session, _posture));

			var decision = new TaskCompletionSource<ConjurerAction>(TaskCreationOptions.RunContinuationsAsynchronously);
			void Decide(ConjurerAction action)
			{
				ClearActions();
				decision.TrySetResult(action);
			}

			_decideAutomatically = () => Decide(AutoPilot.ForConjurer(_session, _posture));
			_prompt.Text = $"Vez do {_session.Conjurer.Name}:";

			foreach (var page in _session.Conjurer.Pages)
			{
				var why = !page.Resonant ? "sem Ressonância" : page.Cooldown > 0 ? $"recarga {page.Cooldown}" : _session.Ether < page.Cost ? "Éter insuficiente" : "";
				var button = new Button
				{
					Text = $"{page.Page.Name} ({page.Cost})",
					Icon = Art.Glyph(page.Page.Glyph),
					ExpandIcon = true, 
					Disabled = !_session.CanCast(page),
					TooltipText = $"{Texts.Name(page.Page.Glyph)} · {Texts.Name(page.Page.Form)} · Círculo {Texts.Circle(page.Page.Circle)}\n{Texts.Describe(page.Page)}{(why.Length > 0 ? $"\n({why})" : "")}",
                };
                button.AddThemeConstantOverride("icon_max_width", 40);
                var captured = page;
				button.Pressed += () =>
				{
					if (captured.NeedsTarget)
						PickTarget(_session.Conjurer, target => Decide(new ConjurerAction(captured, target)));
					else
						Decide(new ConjurerAction(captured, null));
				};
				_actions.AddChild(button);
			}

			var channel = new Button { Text = $"Canalizar (+{_session.Conjurer.Definition.ChannelGain} Éter)" };
			channel.Pressed += () => Decide(ConjurerAction.Channel);
			_actions.AddChild(channel);
			return decision.Task;
		}

		private void PickTarget(ITurnTaker actor, Action<BattleUnit> onPicked)
		{
			var choosable = _session.ChoosableTargets(actor);
			foreach (var unit in choosable)
				_views[unit].SetTargetable(true);

			_prompt.Text = "Escolha o alvo:";
			_pickTarget = unit =>
			{
				if (!choosable.Contains(unit))
					return;
				foreach (var view in _views.Values)
					view.SetTargetable(false);
				onPicked(unit);
			};
		}

		private void ClearActions()
		{
			_decideAutomatically = null;
			_pickTarget = null;
			_prompt.Text = "";
			foreach (var view in _views.Values)
				view.SetTargetable(false);
			Layout.Clear(_actions);
		}

		private void SetAuto(bool on)
		{
			_auto = on;
			RefreshAutoText();
			if (on)
				_decideAutomatically?.Invoke();
		}

		private void RefreshAutoText() => _autoButton.Text = _auto ? "Automático: ligado" : "Automático: desligado";

		private void Close()
		{
			if (_closed)
				return;
			_closed = true;
			Closed?.Invoke(_auto);
		}

		// Animação dos eventos ----------------------------------------------------------------------

		private async Task Play(IReadOnlyList<BattleEvent> events)
		{
			foreach (var battleEvent in events)
			{
				if (_closed || !IsInsideTree())
					return;

				var seconds = Show(battleEvent);
				if (seconds > 0)
					await ToSignal(GetTree().CreateTimer(seconds / Speed), SceneTreeTimer.SignalName.Timeout);
			}
		}

		/// <summary>Aplica um evento na tela e devolve quanto tempo esperar antes do próximo.</summary>
		private double Show(BattleEvent battleEvent)
		{
			switch (battleEvent)
			{
				case WaveStarted wave:
					foreach (var old in _views.Keys.Where(u => u.Side == Side.Enemies).ToList())
						_views.Remove(old);
					Layout.Clear(_enemies);
					foreach (var enemy in wave.Enemies)
						_enemies.AddChild(ViewFor(enemy));
					_wave.Text = $"Onda {wave.Wave}/{wave.WaveCount}";
					_banner.Text = $"Onda {wave.Wave}";
					return 0.8;

				case TurnStarted turn:
					foreach (var view in _views.Values)
					{
						view.SetActive(ReferenceEquals(view.Unit, turn.Actor));
						view.Refresh();
					}

					_conjurerCard.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Parchment, turn.Actor is ConjurerSeat ? Palette.Gold : Palette.Ink, turn.Actor is ConjurerSeat ? 4 : 2, 6, 8));
					_conjurerImpeto.Value = _session.Conjurer.Impeto;
					_round.Text = $"Rodada {Math.Min(turn.Round, BattleRules.RoundLimit)}/{BattleRules.RoundLimit}";
					_order.Show(_session.PredictOrder(8));
					return 0.12;

				case SkillUsed used:
					_banner.Text = $"{used.Actor.Name}\n{used.Skill.Name}{(used.Enhanced ? " ✦" : "")}";
					_views[used.Actor].Lunge(used.Actor.Side == Side.Allies ? 1 : -1, Speed);
					return 0.35;

				case PageCast cast:
					_banner.Text = $"{_session.Conjurer.Name} lança\n{cast.Page.Page.Name}";
					return 0.45;

				case Channeled channeled:
					_banner.Text = $"{_session.Conjurer.Name} canaliza\n+{channeled.Gain} Éter";
					return 0.3;

				case Damaged damaged:
					var hit = _views[damaged.Target];
					hit.Shake(Speed);
					hit.Float(damaged.Crit ? $"-{damaged.Amount}!" : $"-{damaged.Amount}", damaged.Crit ? Palette.Gold : Palette.Damage);
					hit.Refresh();
					return 0.14;

				case Missed missed:
					_views[missed.Target].Float("Errou", Palette.Bone);
					return 0.1;

				case Warded warded:
					_views[warded.Target].Float("Égide!", Palette.Shield);
					_views[warded.Target].Refresh();
					return 0.1;

				case Healed healed:
					_views[healed.Target].Float($"+{healed.Amount}", Palette.Heal);
					_views[healed.Target].Refresh();
					return 0.1;

				case StatusApplied applied:
					_views[applied.Target].Float(Texts.Name(applied.Status), Palette.Bone);
					_views[applied.Target].Refresh();
					return 0.06;

				case Resisted resisted:
					_views[resisted.Target].Float("Resistiu", Palette.Bone);
					return 0.06;

				case StatusRemoved removed:
					_views[removed.Target].Refresh();
					return 0;

				case ImpetoChanged changed:
					_views[changed.Target].Refresh();
					return 0.05;

				case TurnSkipped skipped:
					_views[skipped.Unit].Float("Atordoado", Palette.Bone);
					_banner.Text = $"{skipped.Unit.Name}\nperde o turno";
					return 0.4;

				case Died died:
					_views[died.Unit].Refresh();
					return 0.3;

				case Revived revived:
					_views[revived.Unit].Float("Renasce!", Palette.Gold);
					_views[revived.Unit].Refresh();
					return 0.4;

				case EtherChanged ether:
					_ether.SetValue(ether.Ether);
					return 0;

				case BattleEnded ended:
					_banner.Text = ended.Victory ? "Vitória" : "Derrota";
					foreach (var view in _views.Values)
						view.SetActive(false);
					return 0.6;

				default:
					return 0;
			}
		}
	}
}
