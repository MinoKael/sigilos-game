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
	/// A luta na tela, no molde de Summoners War. Quem decide as regras é o <see cref="BattleSession"/>;
	/// esta tela só:
	///
	/// 1. pede o próximo turno,
	/// 2. anima os <see cref="BattleEvent"/> que voltam,
	/// 3. na vez de um aliado, espera o clique do jogador (manual) ou pergunta ao
	///    <see cref="AutoPilot"/> (automático). O automático pode ser ligado e desligado no meio e
	///    nunca gasta Éter: aprimorar é decisão do jogador.
	///
	/// No fim avisa <see cref="Finished"/>; o GameRoot aplica a recompensa e chama <see cref="ShowResult"/>.
	/// </summary>
	public partial class BattleScreen : Control
	{
		private static readonly float[] Speeds = { 1, 2, 4 };

		private readonly BattleSession _session;
		private readonly StageDefinition _stage;
		private readonly Dictionary<BattleUnit, UnitView> _views = new();

		private readonly GridContainer _allies = new() { Columns = 2 };
		private readonly GridContainer _enemies = new() { Columns = 3 };
		private readonly Label _wave = new();
		private readonly Label _round = new();
		private readonly Label _banner = new();
		private readonly Label _prompt = new();
		private readonly HBoxContainer _actions = new();
		private readonly EtherGauge _ether = new();
		private readonly Label _etherHint = new() { ThemeTypeVariation = GameTheme.Faded };
		private readonly Button _autoButton = new() { ToggleMode = true };
		private readonly Button _speedButton = new();
		private readonly TurnOrderBar _order = new();

		private bool _auto;
		private int _speedIndex;
		private bool _closed;

		/// <summary>Decide no automático a vez que está esperando o jogador; nulo quando ninguém espera.</summary>
		private Action? _decideAutomatically;

		/// <summary>O que um clique em unidade faz agora; nulo fora da escolha de alvo.</summary>
		private Action<BattleUnit>? _pickTarget;

		public BattleScreen(BattleSession session, StageDefinition stage, bool auto)
		{
			_session = session;
			_stage = stage;
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
			page.AddChild(_order);
			page.AddChild(Field());
			page.AddChild(BottomBar());
			RefreshAuto();
			_order.Show(_session.PredictOrder(8));

			Run();
		}

		/// <summary>Mostra vitória ou derrota e o que a luta rendeu. <paramref name="reward"/> é nulo na derrota.</summary>
		public void ShowResult(bool victory, StageReward? reward)
		{
			var overlay = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
			overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(overlay);

			var center = new CenterContainer();
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			overlay.AddChild(center);

			var (panel, content) = Layout.Section(victory ? "Vitória" : "Derrota");
			panel.CustomMinimumSize = new Vector2(440, 0);
			center.AddChild(panel);

			var lines = new List<string>();
			if (reward != null)
			{
				if (reward.FirstClear)
					lines.Add("Primeira vitória nesta fase!");
				if (reward.Scrolls > 0)
					lines.Add($"+{Texts.Scrolls(reward.Scrolls)}");
				lines.Add($"+{reward.Essence} Essência   +{reward.Dust} Pó de Sigilo");
				lines.Add($"+{reward.Experience} de experiência para cada invocação do time");
				foreach (var id in reward.LevelUps)
					lines.Add($"{_session.Allies.First(a => a.DefinitionId == id).Name} subiu de nível!");
				if (reward.Rune is { } rune)
					lines.Add($"Runa de {Texts.Name(rune.Set)} {Texts.Stars(rune.Grade)} (espaço {rune.Slot}): {Texts.Format(rune.Main, rune.MainValue)}");
			}
			else
			{
				lines.Add(_session.Round > BattleRules.RoundLimit
					? $"O tempo acabou: {BattleRules.RoundLimit} rodadas."
					: "Todas as invocações caíram.");
				lines.Add("Suba o nível, equipe runas ou desperte invocações em Monstros. No manual, o Éter paga aprimoramentos.");
			}

			foreach (var line in lines)
				content.AddChild(new Label { Text = line, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(400, 0) });

			var close = new Button { Text = "Continuar", CustomMinimumSize = new Vector2(0, 48) };
			close.Pressed += Close;
			content.AddChild(close);
		}

		public override void _ExitTree() => _closed = true;

		private Control TopBar()
		{
			var bar = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel };
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 24);
			bar.AddChild(row);

			var title = new Label { Text = $"Fase {_stage.Number} · {_stage.Name}" };
			title.AddThemeFontOverride("font", GameTheme.Serif);
			title.AddThemeFontSizeOverride("font_size", 20);
			title.AddThemeColorOverride("font_color", Palette.Gold);
			row.AddChild(title);
			row.AddChild(_wave);
			row.AddChild(_round);
			row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

			_autoButton.ButtonPressed = _auto;
			_autoButton.TooltipText = "No automático as invocações agem sozinhas e o Éter não é usado.";
			_autoButton.Toggled += SetAuto;
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
			field.AddChild(_banner);

			_enemies.AddThemeConstantOverride("h_separation", 10);
			_enemies.AddThemeConstantOverride("v_separation", 10);
			_enemies.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			field.AddChild(_enemies);
			return field;
		}

		private Control BottomBar()
		{
			var bottom = new VBoxContainer();
			var etherRow = new HBoxContainer();
			etherRow.AddThemeConstantOverride("separation", 16);
			etherRow.AddChild(_ether);
			etherRow.AddChild(_etherHint);
			bottom.AddChild(etherRow);

			var actionRow = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) };
			actionRow.AddThemeConstantOverride("separation", 10);
			actionRow.AddChild(_prompt);
			_actions.AddThemeConstantOverride("separation", 8);
			actionRow.AddChild(_actions);
			bottom.AddChild(actionRow);
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

				var action = turn.Actor.Side == Side.Allies
					? await DecideAlly(turn.Actor)
					: AutoPilot.ForEnemy(_session, turn.Actor);
				if (_closed)
					return;
				await Play(_session.Act(action));
			}

			if (!_closed)
				Finished?.Invoke(_session.Victory == true);
		}

		// Decisões ----------------------------------------------------------------------------------

		private Task<UnitAction> DecideAlly(BattleUnit ally)
		{
			if (_auto)
				return Task.FromResult(AutoPilot.ForAlly(_session, ally));

			var decision = new TaskCompletionSource<UnitAction>(TaskCreationOptions.RunContinuationsAsynchronously);
			void Decide(UnitAction action)
			{
				ClearActions();
				decision.TrySetResult(action);
			}

			_decideAutomatically = () => Decide(AutoPilot.ForAlly(_session, ally));
			_prompt.Text = $"Vez de {ally.Name}:";

			var costs = string.Join(", ", new[] { ally.Basic, ally.GlyphSkill }
				.Where(s => s is { CanEnhance: true })
				.Select(s => $"{s!.Name} {s.EnhanceCost}"));
			var enhance = new CheckBox { Text = $"Aprimorar (Éter: {costs})", TooltipText = "Paga Éter para usar a versão aprimorada da habilidade escolhida." };

			foreach (var slot in new[] { SkillSlot.Basic, SkillSlot.Glyph })
			{
				var skill = ally.Skill(slot);
				var ready = slot == SkillSlot.Basic || ally.IsGlyphReady;
				var wait = ready ? "" : $" (⟳{ally.GlyphCooldown})";
				var button = new Button { Text = $"{skill.Name}{wait}", Disabled = !ready, TooltipText = Texts.Describe(skill) };
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

		private void PickTarget(BattleUnit actor, Action<BattleUnit> onPicked)
		{
			foreach (var view in _views.Values)
				view.SetTargetable(false);

			var choosable = _session.ChoosableTargets(actor);
			foreach (var unit in choosable)
				_views[unit].SetTargetable(true);

			_prompt.Text = "Escolha o alvo:";
			_pickTarget = unit =>
			{
				if (choosable.Contains(unit))
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
			RefreshAuto();
			if (on)
				_decideAutomatically?.Invoke();
		}

		private void RefreshAuto()
		{
			_autoButton.Text = _auto ? "Automático: ligado" : "Automático: desligado";
			_etherHint.Text = _auto
				? "Automático não usa Éter. Desligue para aprimorar habilidades."
				: "Glifo e inimigo derrubado geram Éter; marque Aprimorar para gastá-lo.";
		}

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
						view.SetActive(view.Unit == turn.Actor);
						view.Refresh();
					}

					_round.Text = $"Rodada {Math.Min(turn.Round, BattleRules.RoundLimit)}/{BattleRules.RoundLimit}";
					_order.Show(_session.PredictOrder(8));
					return 0.12;

				case SkillUsed used:
					_banner.Text = $"{used.Actor.Name}\n{used.Skill.Name}{(used.Enhanced ? " · aprimorada" : "")}";
					_banner.AddThemeColorOverride("font_color", used.Enhanced ? Palette.Ether : Palette.Text);
					_views[used.Actor].Lunge(used.Actor.Side == Side.Allies ? 1 : -1, Speed);
					return 0.35;

				case ExtraTurn extra:
					_views[extra.Unit].Float("Turno extra!", Palette.Gold);
					return 0.3;

				case Damaged damaged:
					var hit = _views[damaged.Target];
					hit.Shake(Speed);
					hit.Float(damaged.Crit ? $"-{damaged.Amount}!" : $"-{damaged.Amount}", damaged.Crit ? Palette.Gold : Palette.Damage);
					hit.Refresh();
					return 0.14;

				case Missed missed:
					_views[missed.Target].Float("Errou", Palette.TextFaded);
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
					_views[applied.Target].Float(Texts.Name(applied.Status), BattleRules.IsNegative(applied.Status) ? Palette.Negative : Palette.Positive);
					_views[applied.Target].Refresh();
					return 0.06;

				case Resisted resisted:
					_views[resisted.Target].Float("Resistiu", Palette.TextFaded);
					return 0.06;

				case StatusRemoved removed:
					_views[removed.Target].Refresh();
					return 0;

				case ImpetoChanged changed:
					_views[changed.Target].Refresh();
					return 0.05;

				case TurnSkipped skipped:
					_views[skipped.Unit].Float("Atordoado", Palette.Negative);
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
