using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;
using Side = Sigilos.Core.Battle.Side;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A luta na tela. Quem decide as regras é o <see cref="BattleSession"/>; esta tela só:
	///
	/// 1. pede o próximo turno,
	/// 2. anima os <see cref="BattleEvent"/> que voltam,
	/// 3. na vez de um aliado, espera o toque do jogador (manual) ou pergunta ao
	///    <see cref="AutoPilot"/> (automático). O automático pode ser ligado e desligado no meio.
	///
	/// Tudo é sigilo: no alto, a onda e a rodada em cápsulas e os sigilos de Efeitos, automático,
	/// velocidade e Recuar; embaixo, o retrato de quem age e um sigilo por habilidade (o Glifo dela, a
	/// recarga na plaquinha). O painel de Efeitos mostra o que está sobre cada unidade. No fim avisa
	/// <see cref="Finished"/>; o GameRoot aplica a recompensa e chama <see cref="ShowResult"/>.
	/// </summary>
	public partial class BattleScreen : Control
	{
		private readonly BattleSession _session;
		private readonly string _title;
		private readonly Dictionary<BattleUnit, UnitView> _views = new();

		private readonly GridContainer _allies = new() { Columns = 3 };
		private readonly GridContainer _enemies = new() { Columns = 3 };
		private readonly HBoxContainer _counters = Layout.Row(8);
		private string _wave = "";
		private string _round = "";
		private readonly Label _banner = new();
		private readonly HBoxContainer _actor = Layout.Row(10);
		private readonly HBoxContainer _actions = Layout.Row(14);
		private readonly SigilButton _autoButton = new(Art.Icon("auto"), "", 52, SigilShape.Square) { ToggleMode = true };
		private readonly SigilButton _speedButton = new(Art.Icon("speed"), "", 52, SigilShape.Square);
		private readonly SigilButton _effectsButton = new(Art.Icon("effects"), "", 52, SigilShape.Square) { ToggleMode = true };
		private readonly TurnOrderBar _order = new();
		private readonly PanelContainer _effects = new() { Visible = false };
		private readonly VBoxContainer _effectsList = new();

		private bool _auto;
		private int _speedIndex;
		private bool _closed;

		/// <summary>Decide no automático a vez que está esperando o jogador; nulo quando ninguém espera.</summary>
		private Action? _decideAutomatically;

		/// <summary>O que um toque em unidade faz agora; nulo fora da escolha de alvo.</summary>
		private Action<BattleUnit>? _pickTarget;

		/// <summary>A unidade cujo efeito o painel destaca (toque numa unidade fora da escolha de alvo).</summary>
		private BattleUnit? _focused;

		public BattleScreen(BattleSession session, string title, bool auto)
		{
			_session = session;
			_title = title;
			_auto = auto;
		}

		/// <summary>A luta acabou: verdadeiro na vitória.</summary>
		public event Action<bool>? Finished;

		/// <summary>O jogador saiu da tela (depois do resultado ou recuando). O valor é a preferência de automático.</summary>
		public event Action<bool>? Closed;

		private float Speed => BattlePace.Speeds[_speedIndex].Factor;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			page.AddChild(TopBar());
			page.AddChild(_order);
			page.AddChild(Field());
			page.AddChild(BottomBar());
			AddChild(EffectsPanel());
			RefreshAuto();
			_order.Show(_session.PredictOrder(8));

			Run();
		}

		/// <summary>
		/// Mostra vitória ou derrota e o que a luta rendeu, em cápsulas. <paramref name="reward"/> é nulo
		/// na derrota; <paramref name="levelUps"/> são os nomes de quem subiu de nível;
		/// <paramref name="accountLevel"/> é o nível da conta depois da luta.
		/// </summary>
		public void ShowResult(bool victory, VictoryReward? reward, IReadOnlyList<string> levelUps, int accountLevel)
		{
			var overlay = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
			overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(overlay);

			var center = new CenterContainer();
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			overlay.AddChild(center);

			var panel = new PanelContainer { CustomMinimumSize = new Vector2(460, 0) };
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, victory ? Palette.Gold : Palette.Negative.Darkened(0.3f), 24));
			center.AddChild(panel);
			var content = new VBoxContainer();
			content.AddThemeConstantOverride("separation", 16);
			panel.AddChild(content);

			var title = new Label { Text = victory ? T("battle.victory") : T("battle.defeat"), ThemeTypeVariation = GameTheme.Title, HorizontalAlignment = HorizontalAlignment.Center };
			if (!victory)
				title.AddThemeColorOverride("font_color", Palette.Negative);
			content.AddChild(title);

			var chips = Layout.Flow(8);
			chips.Alignment = FlowContainer.AlignmentMode.Center;
			if (reward != null)
			{
				if (reward.FirstClear)
					chips.AddChild(Layout.Chip("collect", "", T("battle.first_victory"), Palette.Spirit));
				chips.AddChild(Layout.Chip("mana", $"−{reward.Mana}", T("currency.mana")));
				if (reward.Scrolls > 0)
					chips.AddChild(Layout.Chip("scroll", $"+{reward.Scrolls}", T("currency.scrolls_name")));
				if (reward.Gold > 0)
					chips.AddChild(Layout.Chip("gold", $"+{reward.Gold}", T("currency.gold")));
				chips.AddChild(Layout.Chip("essence", $"+{reward.Essence}", T("currency.essence")));
				chips.AddChild(Layout.Chip("level_max", $"+{reward.Experience}", T("reward.experience")));
				if (reward.AccountLevels > 0)
					chips.AddChild(Layout.Chip("avatar", accountLevel.ToString(), T("battle.account", accountLevel, reward.AccountLevels * Account.LevelUpGold), Palette.Arcane));
				foreach (var tool in reward.Tools)
					chips.AddChild(Layout.Chip(tool.Kind == Core.Runes.RuneToolKind.Grindstone ? "grindstone" : "gem", "", $"{Texts.Name(tool)} ({Texts.Range(tool)})", Palette.Of(tool.Grade)));
			}
			else
			{
				var reason = _session.Round > BattleRules.RoundLimit ? T("battle.timeout", BattleRules.RoundLimit) : T("battle.all_fell");
				chips.AddChild(Layout.Chip(_session.Round > BattleRules.RoundLimit ? "resolve" : "retreat", "", reason, Palette.Negative));
			}

			content.AddChild(chips);

			if (reward?.Rune is { } rune)
			{
				var runeRow = Layout.Row(0, true);
				runeRow.AddChild(new RuneTile(rune, rune.Slot, 1.4f) { MouseFilter = MouseFilterEnum.Pass });
				content.AddChild(runeRow);
			}

			if (levelUps.Count > 0)
			{
				var ups = Layout.Row(6, true);
				var arrow = Doodle.Icon(Art.Icon("level_max"), 24, Palette.Spirit);
				ups.AddChild(arrow);
				ups.AddChild(new Label { Text = string.Join(", ", levelUps), ThemeTypeVariation = GameTheme.Faded, TooltipText = T("battle.leveled_up_tip"), MouseFilter = MouseFilterEnum.Stop });
				content.AddChild(ups);
			}

			var row = Layout.Row(0, true);
			row.AddChild(SigilButton.Of("confirm", T("common.continue"), Close, 64));
			content.AddChild(row);
		}

		public override void _ExitTree() => _closed = true;

		private Control TopBar()
		{
			var bar = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel };
			var row = Layout.Row(14);
			bar.AddChild(row);

			var title = new Label { Text = _title, VerticalAlignment = VerticalAlignment.Center };
			title.AddThemeFontOverride("font", GameTheme.Serif);
			title.AddThemeFontSizeOverride("font_size", 20);
			title.AddThemeColorOverride("font_color", Palette.Gold);
			row.AddChild(title);
			row.AddChild(_counters);
			row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

			_effectsButton.TooltipText = T("battle.effects");
			_effectsButton.Toggled += on =>
			{
				_effects.Visible = on;
				RefreshEffects();
			};
			row.AddChild(_effectsButton);

			_autoButton.ButtonPressed = _auto;
			_autoButton.Toggled += SetAuto;
			row.AddChild(_autoButton);

			_speedButton.Badge = $"{BattlePace.Speeds[0].Label}×";
			_speedButton.TooltipText = T("battle.speed", BattlePace.Speeds[0].Label);
			_speedButton.Pressed += () =>
			{
				_speedIndex = (_speedIndex + 1) % BattlePace.Speeds.Count;
				_speedButton.Badge = $"{BattlePace.Speeds[_speedIndex].Label}×";
				_speedButton.TooltipText = T("battle.speed", BattlePace.Speeds[_speedIndex].Label);
			};
			row.AddChild(_speedButton);

			row.AddChild(SigilButton.Of("retreat", T("battle.retreat"), Close, 52, SigilShape.Square));
			return bar;
		}

		/// <summary>A onda e a rodada, em cápsulas.</summary>
		private void RefreshCounters()
		{
			Layout.Clear(_counters);
			if (_wave.Length > 0)
				_counters.AddChild(Layout.Chip("fight", _wave, T("battle.wave_tip")));
			if (_round.Length > 0)
				_counters.AddChild(Layout.Chip("resolve", _round, T("battle.round_tip")));
		}

		private Control Field()
		{
			var field = Layout.Row(12);
			field.SizeFlagsVertical = SizeFlags.ExpandFill;

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
			_banner.AddThemeFontSizeOverride("font_size", 22);
			_banner.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
			field.AddChild(_banner);

			_enemies.AddThemeConstantOverride("h_separation", 10);
			_enemies.AddThemeConstantOverride("v_separation", 10);
			_enemies.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			field.AddChild(_enemies);
			return field;
		}

		private Control BottomBar()
		{
			var bottom = Layout.Row(16);
			bottom.CustomMinimumSize = new Vector2(0, 80);
			bottom.AddChild(_actor);
			bottom.AddChild(_actions);
			return bottom;
		}

		/// <summary>O painel de Efeitos: por cima do centro do campo, com o que está sobre cada unidade viva.</summary>
		private Control EffectsPanel()
		{
			_effects.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 14));
			// No vão entre aliados e inimigos: o painel não cobre nenhum dos dois lados.
			_effects.AnchorLeft = 0.5f;
			_effects.AnchorRight = 0.5f;
			_effects.AnchorTop = 0;
			_effects.AnchorBottom = 1;
			_effects.OffsetLeft = -190;
			_effects.OffsetRight = 190;
			_effects.OffsetTop = 140;
			_effects.OffsetBottom = -120;

			var column = new VBoxContainer();
			var header = Layout.Row(8);
			header.AddChild(Doodle.Icon(Art.Icon("effects"), 30, Palette.Gold));
			header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
			header.AddChild(SigilButton.Of("cancel", T("common.close"), () => _effectsButton.ButtonPressed = false, 40));
			column.AddChild(header);
			_effectsList.AddThemeConstantOverride("separation", 8);
			column.AddChild(Layout.Scroll(_effectsList));
			_effects.AddChild(column);
			return _effects;
		}

		private void RefreshEffects()
		{
			if (!_effects.Visible)
				return;

			Layout.Clear(_effectsList);
			var units = _session.Allies.Concat(_session.Enemies).Where(u => u.IsAlive).OrderByDescending(u => u == _focused).ToList();
			foreach (var unit in units)
			{
				var row = Layout.Row(8);
				var frame = new PanelContainer { TooltipText = unit.Name, MouseFilter = MouseFilterEnum.Stop };
				var ring = unit == _focused ? Palette.Gold : unit.Side == Side.Allies ? Palette.Health : Palette.HealthLow;
				frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, ring, 2, 18, 2));
				frame.AddChild(Layout.Medal(Art.Creature(unit.Image), Palette.Of(unit.Element), 32));
				row.AddChild(frame);

				var statuses = Layout.Flow(6);
				statuses.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				foreach (var status in unit.Statuses)
				{
					var ink = BattleRules.IsNegative(status.Kind) ? Palette.Negative : Palette.Positive;
					var value = status.Kind == Core.Content.StatusKind.Shield ? $" {Math.Round(status.Value)}" : "";
					var tip = T("battle.effect_tip", Texts.Name(status.Kind), Texts.Turns(status.Turns));
					statuses.AddChild(Layout.Chip(Art.Effect(status.Kind), $"{status.Turns}{value}", tip, ink));
				}

				row.AddChild(statuses);
				_effectsList.AddChild(row);
			}
		}

		private UnitView ViewFor(BattleUnit unit)
		{
			var view = new UnitView(unit);
			view.Pressed += v =>
			{
				if (_pickTarget != null)
				{
					_pickTarget(v.Unit);
					return;
				}

				_focused = v.Unit;
				if (_effectsButton.ButtonPressed)
					RefreshEffects();
				else
					_effectsButton.ButtonPressed = true;
			};
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
			var medal = new PanelContainer { TooltipText = ally.Name, MouseFilter = MouseFilterEnum.Stop };
			medal.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Gold, 2, 30, 4));
			medal.AddChild(Layout.Medal(Art.Creature(ally.Image), Palette.Of(ally.Element), 52));
			_actor.AddChild(medal);

			for (var i = 0; i < ally.Skills.Count; i++)
			{
				var index = i;
				var skill = ally.Skill(index);
				var ready = ally.IsReady(index);
				var button = new SigilButton(Art.Skill(skill), $"{skill.Name}\n{Texts.Plain(Texts.Describe(skill))}", 66)
				{
					Disabled = !ready,
					Badge = ready ? Texts.Roman(index + 1) : $"⟳{ally.Cooldown(index)}",
				};
				button.Pressed += () =>
				{
					if (skill.NeedsTarget)
						PickTarget(ally, button, target => Decide(new UnitAction(index, target)));
					else
						Decide(new UnitAction(index, null));
				};
				_actions.AddChild(button);
			}

			return decision.Task;
		}

		/// <summary>Os alvos possíveis acendem em azul; o sigilo da habilidade fica aceso até o toque no alvo.</summary>
		private void PickTarget(BattleUnit actor, SigilButton skill, Action<BattleUnit> onPicked)
		{
			foreach (var view in _views.Values)
				view.SetTargetable(false);
			foreach (var child in _actions.GetChildren().OfType<SigilButton>())
				child.Highlight = child == skill;

			var choosable = _session.ChoosableTargets(actor);
			foreach (var unit in choosable)
				_views[unit].SetTargetable(true);

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
			foreach (var view in _views.Values)
				view.SetTargetable(false);
			Layout.Clear(_actor);
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
			_autoButton.TooltipText = _auto ? T("battle.auto_on") : T("battle.auto_off");
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

				Show(battleEvent);
				var seconds = BattlePace.Seconds(battleEvent);
				if (seconds > 0)
					await ToSignal(GetTree().CreateTimer(seconds / Speed), SceneTreeTimer.SignalName.Timeout);
			}

			RefreshEffects();
		}

		/// <summary>Aplica um evento na tela. Quanto esperar depois é do <see cref="BattlePace"/>.</summary>
		private void Show(BattleEvent battleEvent)
		{
			switch (battleEvent)
			{
				case WaveStarted wave:
					foreach (var old in _views.Keys.Where(u => u.Side == Side.Enemies).ToList())
						_views.Remove(old);
					Layout.Clear(_enemies);
					foreach (var enemy in wave.Enemies)
						_enemies.AddChild(ViewFor(enemy));
					_wave = $"{wave.Wave}/{wave.WaveCount}";
					RefreshCounters();
					_banner.Text = T("battle.wave_banner", wave.Wave);
					return;

				case TurnStarted turn:
					foreach (var view in _views.Values)
					{
						view.SetActive(view.Unit == turn.Actor);
						view.Refresh();
					}

					_round = $"{Math.Min(turn.Round, BattleRules.RoundLimit)}/{BattleRules.RoundLimit}";
					RefreshCounters();
					_order.Show(_session.PredictOrder(8));
					return;

				case SkillUsed used:
					_banner.Text = T("battle.uses", used.Actor.Name, used.Skill.Name);
					_banner.AddThemeColorOverride("font_color", Palette.Text);
					_views[used.Actor].Lunge(used.Actor.Side == Side.Allies ? 1 : -1, Speed);
					return;

				case ExtraTurn extra:
					_views[extra.Unit].Float(T("battle.extra_turn"), Palette.Gold);
					return;

				case Counterattack counter:
					_views[counter.Unit].Float(T("battle.counterattack"), Palette.Gold);
					_views[counter.Unit].Lunge(counter.Unit.Side == Side.Allies ? 1 : -1, Speed);
					return;

				case MaxHealthReduced reduced:
					_views[reduced.Target].Float(T("battle.max_hp", reduced.Amount), Palette.Negative);
					_views[reduced.Target].Refresh();
					return;

				case Damaged damaged:
					var hit = _views[damaged.Target];
					hit.Shake(Speed);
					hit.Float(damaged.Crit ? $"-{damaged.Amount}!" : $"-{damaged.Amount}", damaged.Crit ? Palette.Gold : Palette.Damage);
					hit.Refresh();
					return;

				case Missed missed:
					_views[missed.Target].Float(T("battle.missed"), Palette.TextFaded);
					return;

				case Warded warded:
					_views[warded.Target].Float(T("battle.aegis"), Palette.Shield);
					_views[warded.Target].Refresh();
					return;

				case Healed healed:
					_views[healed.Target].Float($"+{healed.Amount}", Palette.Heal);
					_views[healed.Target].Refresh();
					return;

				case StatusApplied applied:
					_views[applied.Target].Float(Texts.Name(applied.Status), BattleRules.IsNegative(applied.Status) ? Palette.Negative : Palette.Positive);
					_views[applied.Target].Refresh();
					return;

				case Resisted resisted:
					_views[resisted.Target].Float(T("battle.resisted"), Palette.TextFaded);
					return;

				case Immune immune:
					_views[immune.Target].Float(T("battle.immune"), Palette.Positive);
					return;

				case StatusRemoved removed:
					_views[removed.Target].Refresh();
					return;

				case ImpetoChanged changed:
					_views[changed.Target].Refresh();
					return;

				case TurnSkipped skipped:
					_views[skipped.Unit].Float(T("battle.stunned"), Palette.Negative);
					_banner.Text = T("battle.loses_turn", skipped.Unit.Name);
					return;

				case Died died:
					_views[died.Unit].Refresh();
					return;

				case Revived revived:
					_views[revived.Unit].Float(T("battle.revives"), Palette.Gold);
					_views[revived.Unit].Refresh();
					return;

				case BattleEnded ended:
					_banner.Text = ended.Victory ? T("battle.victory") : T("battle.defeat");
					foreach (var view in _views.Values)
						view.SetActive(false);
					return;

				default:
					return;
			}
		}
	}
}
