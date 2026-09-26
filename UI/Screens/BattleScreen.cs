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
	/// 3. na vez de um aliado, espera o clique do jogador (manual) ou pergunta ao
	///    <see cref="AutoPilot"/> (automático). O automático pode ser ligado e desligado no meio e
	///    nunca gasta Éter: aprimorar é decisão do jogador.
	///
	/// O painel de Efeitos mostra, a qualquer momento, o que está sobre cada aliado e inimigo. No fim
	/// avisa <see cref="Finished"/>; o GameRoot aplica a recompensa e chama <see cref="ShowResult"/>.
	/// </summary>
	public partial class BattleScreen : Control
	{
		private readonly BattleSession _session;
		private readonly string _title;
		private readonly Dictionary<BattleUnit, UnitView> _views = new();

		private readonly GridContainer _allies = new() { Columns = 3 };
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
		private readonly Button _effectsButton = new() { ToggleMode = true };
		private readonly TurnOrderBar _order = new();
		private readonly PanelContainer _effects = new() { Visible = false };
		private readonly VBoxContainer _effectsList = new();

		private bool _auto;
		private int _speedIndex;
		private bool _closed;

		/// <summary>Decide no automático a vez que está esperando o jogador; nulo quando ninguém espera.</summary>
		private Action? _decideAutomatically;

		/// <summary>O que um clique em unidade faz agora; nulo fora da escolha de alvo.</summary>
		private Action<BattleUnit>? _pickTarget;

		/// <summary>A unidade cujo efeito o painel destaca (clique numa unidade fora da escolha de alvo).</summary>
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
		/// Mostra vitória ou derrota e o que a luta rendeu. <paramref name="reward"/> é nulo na derrota;
		/// <paramref name="levelUps"/> são os nomes de quem subiu de nível; <paramref name="accountLevel"/>
		/// é o nível da conta depois da luta.
		/// </summary>
		public void ShowResult(bool victory, VictoryReward? reward, IReadOnlyList<string> levelUps, int accountLevel)
		{
			var overlay = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
			overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(overlay);

			var center = new CenterContainer();
			center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			overlay.AddChild(center);

			var (panel, content) = Layout.Section(victory ? T("battle.victory") : T("battle.defeat"));
			panel.CustomMinimumSize = new Vector2(460, 0);
			center.AddChild(panel);

			var lines = new List<string>();
			if (reward != null)
			{
				if (reward.FirstClear)
					lines.Add(T("battle.first_victory"));
				lines.Add(T("battle.mana", reward.Mana));
				if (reward.Scrolls > 0)
					lines.Add($"+{Texts.Scrolls(reward.Scrolls)}");
				if (reward.Gold > 0)
					lines.Add(T("battle.gold", reward.Gold));
				lines.Add(T("battle.gains", reward.Essence));
				lines.Add(T("battle.experience", reward.Experience));
				lines.AddRange(levelUps.Select(name => T("battle.leveled_up", name)));
				if (reward.AccountLevels > 0)
					lines.Add(T("battle.account", accountLevel, reward.AccountLevels * Account.LevelUpGold));
				if (reward.Rune is { } rune)
					lines.Add(T("battle.rune", Texts.Title(rune), Texts.Name(rune.Rarity), Texts.Stars(rune.Grade), Texts.Format(rune.Main, rune.MainValue)));
				lines.AddRange(reward.Tools.Select(tool => T("battle.tool", Texts.Name(tool), Texts.Range(tool))));
			}
			else
			{
				lines.Add(_session.Round > BattleRules.RoundLimit ? T("battle.timeout", BattleRules.RoundLimit) : T("battle.all_fell"));
				lines.Add(T("battle.defeat_tip"));
			}

			foreach (var line in lines)
				content.AddChild(Layout.Text(line, width: 420));

			var close = new Button { Text = T("common.continue"), CustomMinimumSize = new Vector2(0, 48) };
			close.Pressed += Close;
			content.AddChild(close);
		}

		public override void _ExitTree() => _closed = true;

		private Control TopBar()
		{
			var bar = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel };
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 20);
			bar.AddChild(row);

			var title = new Label { Text = _title };
			title.AddThemeFontOverride("font", GameTheme.Serif);
			title.AddThemeFontSizeOverride("font_size", 20);
			title.AddThemeColorOverride("font_color", Palette.Gold);
			row.AddChild(title);
			row.AddChild(_wave);
			row.AddChild(_round);
			row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

			_effectsButton.Text = T("battle.effects");
			_effectsButton.TooltipText = T("battle.effects_tip");
			_effectsButton.Toggled += on =>
			{
				_effects.Visible = on;
				RefreshEffects();
			};
			row.AddChild(_effectsButton);

			_autoButton.ButtonPressed = _auto;
			_autoButton.TooltipText = T("battle.auto_tip");
			_autoButton.Toggled += SetAuto;
			row.AddChild(_autoButton);

			_speedButton.Text = T("battle.speed", BattlePace.Speeds[0].Label);
			_speedButton.Pressed += () =>
			{
				_speedIndex = (_speedIndex + 1) % BattlePace.Speeds.Count;
				_speedButton.Text = T("battle.speed", BattlePace.Speeds[_speedIndex].Label);
			};
			row.AddChild(_speedButton);

			var flee = new Button { Text = T("battle.retreat"), TooltipText = T("battle.retreat_tip") };
			flee.Pressed += Close;
			row.AddChild(flee);
			return bar;
		}

		private Control Field()
		{
			var field = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			field.AddThemeConstantOverride("separation", 12);

			_allies.AddThemeConstantOverride("h_separation", 8);
			_allies.AddThemeConstantOverride("v_separation", 8);
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
			field.AddChild(_banner);

			_enemies.AddThemeConstantOverride("h_separation", 8);
			_enemies.AddThemeConstantOverride("v_separation", 8);
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

		/// <summary>O painel de Efeitos: por cima do centro do campo, com a lista de cada unidade viva.</summary>
		private Control EffectsPanel()
		{
			_effects.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Panel, Palette.Gold, 2, 8, 10));
			// No vão entre aliados e inimigos: o painel não cobre nenhum dos dois lados.
			_effects.AnchorLeft = 0.5f;
			_effects.AnchorRight = 0.5f;
			_effects.AnchorTop = 0;
			_effects.AnchorBottom = 1;
			_effects.OffsetLeft = -200;
			_effects.OffsetRight = 200;
			_effects.OffsetTop = 130;
			_effects.OffsetBottom = -110;

			var column = new VBoxContainer();
			var header = new HBoxContainer();
			header.AddChild(new Label { Text = T("battle.effects"), ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var close = new Button { Text = "✕" };
			close.Pressed += () => _effectsButton.ButtonPressed = false;
			header.AddChild(close);
			column.AddChild(header);
			var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_effectsList.AddThemeConstantOverride("separation", 8);
			_effectsList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			scroll.AddChild(_effectsList);
			column.AddChild(scroll);
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
				var name = new Label { Text = T("battle.effects_unit", unit.Name, unit.Side == Side.Allies ? T("battle.ally") : T("battle.enemy")) };
				name.AddThemeColorOverride("font_color", unit.Side == Side.Allies ? Palette.Health : Palette.HealthLow);
				if (unit == _focused)
					name.AddThemeColorOverride("font_color", Palette.Gold);
				_effectsList.AddChild(name);

				if (unit.Statuses.Count == 0)
				{
					_effectsList.AddChild(new Label { Text = T("battle.no_effects"), ThemeTypeVariation = GameTheme.Faded });
					continue;
				}

				foreach (var status in unit.Statuses)
				{
					var extra = status.Kind == Core.Content.StatusKind.Shield ? T("battle.shield_value", Math.Round(status.Value)) : "";
					_effectsList.AddChild(RichText.Label(T("battle.effect_line", Texts.Term(status.Kind), Texts.Turns(status.Turns), extra, Texts.Explain(status.Kind)), 340));
				}
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
			_prompt.Text = T("battle.turn_of", ally.Name);

			// Só a habilidade especial aprimora: a caixa some quando ela não tem aprimoramento.
			var special = ally.Special;
			var enhance = new CheckBox
			{
				Text = special is { CanEnhance: true } ? T("battle.enhance", special.Name, special.EnhanceCost) : "",
				TooltipText = T("battle.enhance_tip"),
				Visible = special is { CanEnhance: true },
			};

			foreach (var slot in new[] { SkillSlot.Basic, SkillSlot.Special })
			{
				var skill = ally.Skill(slot);
				var ready = slot == SkillSlot.Basic || ally.IsSpecialReady;
				var wait = ready ? "" : $" (⟳{ally.SpecialCooldown})";
				var button = new Button { Text = $"{skill.Name}{wait}", Disabled = !ready, TooltipText = Texts.Plain(Texts.Describe(skill)) };
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

			_prompt.Text = T("battle.choose_target");
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
			_autoButton.Text = _auto ? T("battle.auto_on") : T("battle.auto_off");
			_etherHint.Text = _auto ? T("battle.aether_tip_auto") : T("battle.aether_tip_manual");
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
					_wave.Text = T("battle.wave", wave.Wave, wave.WaveCount);
					_banner.Text = T("battle.wave_banner", wave.Wave);
					return;

				case TurnStarted turn:
					foreach (var view in _views.Values)
					{
						view.SetActive(view.Unit == turn.Actor);
						view.Refresh();
					}

					_round.Text = T("battle.round", Math.Min(turn.Round, BattleRules.RoundLimit), BattleRules.RoundLimit);
					_order.Show(_session.PredictOrder(8));
					return;

				case SkillUsed used:
					_banner.Text = used.Enhanced ? T("battle.uses_enhanced", used.Actor.Name, used.Skill.Name) : T("battle.uses", used.Actor.Name, used.Skill.Name);
					_banner.AddThemeColorOverride("font_color", used.Enhanced ? Palette.Ether : Palette.Text);
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

				case EtherChanged ether:
					_ether.SetValue(ether.Ether);
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
