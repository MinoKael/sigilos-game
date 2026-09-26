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
		private static readonly float[] Speeds = { 1, 2, 4 };

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

			var (panel, content) = Layout.Section(victory ? T("batalha.vitoria") : T("batalha.derrota"));
			panel.CustomMinimumSize = new Vector2(460, 0);
			center.AddChild(panel);

			var lines = new List<string>();
			if (reward != null)
			{
				if (reward.FirstClear)
					lines.Add(T("batalha.primeira_vitoria"));
				lines.Add(T("batalha.mana", reward.Mana));
				if (reward.Scrolls > 0)
					lines.Add($"+{Texts.Scrolls(reward.Scrolls)}");
				if (reward.Gold > 0)
					lines.Add(T("batalha.ouro", reward.Gold));
				lines.Add(T("batalha.ganhos", reward.Essence));
				lines.Add(T("batalha.experiencia", reward.Experience));
				lines.AddRange(levelUps.Select(name => T("batalha.subiu", name)));
				if (reward.AccountLevels > 0)
					lines.Add(T("batalha.conta", accountLevel, reward.AccountLevels * Account.LevelUpGold));
				if (reward.Rune is { } rune)
					lines.Add(T("batalha.runa", Texts.Title(rune), Texts.Name(rune.Rarity), Texts.Stars(rune.Grade), Texts.Format(rune.Main, rune.MainValue)));
				lines.AddRange(reward.Tools.Select(tool => T("batalha.pedra", Texts.Name(tool), Texts.Range(tool))));
			}
			else
			{
				lines.Add(_session.Round > BattleRules.RoundLimit ? T("batalha.tempo", BattleRules.RoundLimit) : T("batalha.todos_cairam"));
				lines.Add(T("batalha.dica_derrota"));
			}

			foreach (var line in lines)
				content.AddChild(Layout.Text(line, width: 420));

			var close = new Button { Text = T("geral.continuar"), CustomMinimumSize = new Vector2(0, 48) };
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

			_effectsButton.Text = T("batalha.efeitos");
			_effectsButton.TooltipText = T("batalha.efeitos_dica");
			_effectsButton.Toggled += on =>
			{
				_effects.Visible = on;
				RefreshEffects();
			};
			row.AddChild(_effectsButton);

			_autoButton.ButtonPressed = _auto;
			_autoButton.TooltipText = T("batalha.automatico_dica");
			_autoButton.Toggled += SetAuto;
			row.AddChild(_autoButton);

			_speedButton.Text = T("batalha.velocidade", 1);
			_speedButton.Pressed += () =>
			{
				_speedIndex = (_speedIndex + 1) % Speeds.Length;
				_speedButton.Text = T("batalha.velocidade", Speed);
			};
			row.AddChild(_speedButton);

			var flee = new Button { Text = T("batalha.recuar"), TooltipText = T("batalha.recuar_dica") };
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
			header.AddChild(new Label { Text = T("batalha.efeitos"), ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill });
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
				var name = new Label { Text = T("batalha.efeitos_unidade", unit.Name, unit.Side == Side.Allies ? T("batalha.aliado") : T("batalha.inimigo")) };
				name.AddThemeColorOverride("font_color", unit.Side == Side.Allies ? Palette.Health : Palette.HealthLow);
				if (unit == _focused)
					name.AddThemeColorOverride("font_color", Palette.Gold);
				_effectsList.AddChild(name);

				if (unit.Statuses.Count == 0)
				{
					_effectsList.AddChild(new Label { Text = T("batalha.sem_efeitos"), ThemeTypeVariation = GameTheme.Faded });
					continue;
				}

				foreach (var status in unit.Statuses)
				{
					var extra = status.Kind == Core.Content.StatusKind.Shield ? T("batalha.escudo_valor", Math.Round(status.Value)) : "";
					_effectsList.AddChild(RichText.Label(T("batalha.efeito_linha", Texts.Term(status.Kind), Texts.Turns(status.Turns), extra, Texts.Explain(status.Kind)), 340));
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
			_prompt.Text = T("batalha.vez_de", ally.Name);

			var costs = string.Join(", ", new[] { ally.Basic, ally.Special }
				.Where(s => s is { CanEnhance: true })
				.Select(s => $"{s!.Name} {s.EnhanceCost}"));
			var enhance = new CheckBox { Text = T("batalha.aprimorar", costs), TooltipText = T("batalha.aprimorar_dica") };

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

			_prompt.Text = T("batalha.escolha_alvo");
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
			_autoButton.Text = _auto ? T("batalha.automatico_ligado") : T("batalha.automatico_desligado");
			_etherHint.Text = _auto ? T("batalha.dica_eter_auto") : T("batalha.dica_eter_manual");
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

			RefreshEffects();
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
					_wave.Text = T("batalha.onda", wave.Wave, wave.WaveCount);
					_banner.Text = T("batalha.onda_faixa", wave.Wave);
					return 0.8;

				case TurnStarted turn:
					foreach (var view in _views.Values)
					{
						view.SetActive(view.Unit == turn.Actor);
						view.Refresh();
					}

					_round.Text = T("batalha.rodada", Math.Min(turn.Round, BattleRules.RoundLimit), BattleRules.RoundLimit);
					_order.Show(_session.PredictOrder(8));
					return 0.12;

				case SkillUsed used:
					_banner.Text = used.Enhanced ? T("batalha.usa_aprimorada", used.Actor.Name, used.Skill.Name) : T("batalha.usa", used.Actor.Name, used.Skill.Name);
					_banner.AddThemeColorOverride("font_color", used.Enhanced ? Palette.Ether : Palette.Text);
					_views[used.Actor].Lunge(used.Actor.Side == Side.Allies ? 1 : -1, Speed);
					return 0.35;

				case ExtraTurn extra:
					_views[extra.Unit].Float(T("batalha.turno_extra"), Palette.Gold);
					return 0.3;

				case Counterattack counter:
					_views[counter.Unit].Float(T("batalha.contra_ataque"), Palette.Gold);
					_views[counter.Unit].Lunge(counter.Unit.Side == Side.Allies ? 1 : -1, Speed);
					return 0.3;

				case MaxHealthReduced reduced:
					_views[reduced.Target].Float(T("batalha.vida_max", reduced.Amount), Palette.Negative);
					_views[reduced.Target].Refresh();
					return 0.1;

				case Damaged damaged:
					var hit = _views[damaged.Target];
					hit.Shake(Speed);
					hit.Float(damaged.Crit ? $"-{damaged.Amount}!" : $"-{damaged.Amount}", damaged.Crit ? Palette.Gold : Palette.Damage);
					hit.Refresh();
					return 0.14;

				case Missed missed:
					_views[missed.Target].Float(T("batalha.errou"), Palette.TextFaded);
					return 0.1;

				case Warded warded:
					_views[warded.Target].Float(T("batalha.egide"), Palette.Shield);
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
					_views[resisted.Target].Float(T("batalha.resistiu"), Palette.TextFaded);
					return 0.06;

				case Immune immune:
					_views[immune.Target].Float(T("batalha.imune"), Palette.Positive);
					return 0.06;

				case StatusRemoved removed:
					_views[removed.Target].Refresh();
					return 0;

				case ImpetoChanged changed:
					_views[changed.Target].Refresh();
					return 0.05;

				case TurnSkipped skipped:
					_views[skipped.Unit].Float(T("batalha.atordoado"), Palette.Negative);
					_banner.Text = T("batalha.perde_turno", skipped.Unit.Name);
					return 0.4;

				case Died died:
					_views[died.Unit].Refresh();
					return 0.3;

				case Revived revived:
					_views[revived.Unit].Float(T("batalha.renasce"), Palette.Gold);
					_views[revived.Unit].Refresh();
					return 0.4;

				case EtherChanged ether:
					_ether.SetValue(ether.Ether);
					return 0;

				case BattleEnded ended:
					_banner.Text = ended.Victory ? T("batalha.vitoria") : T("batalha.derrota");
					foreach (var view in _views.Values)
						view.SetActive(false);
					return 0.6;

				default:
					return 0;
			}
		}
	}
}
