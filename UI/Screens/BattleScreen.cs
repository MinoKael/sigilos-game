using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
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
	/// O campo é a <see cref="BattleArena"/>: o círculo oval com os aliados embaixo à esquerda e os
	/// inimigos em cima à direita, e o que acontece escrito no meio dele. Em volta: no canto de cima à
	/// esquerda o nome da luta, a onda e a rodada, escritas, e embaixo, em pé, quem age a seguir; no de
	/// cima à direita a pausa (<see cref="PauseMenu"/>: continuar, recomeçar, sair); embaixo à esquerda
	/// os botões Automático, a velocidade e Efeitos; embaixo à direita uma habilidade por botão, com o
	/// nome embaixo (segurar mostra o que ela faz). Tocar numa unidade (fora da escolha de alvo) ou
	/// segurá-la abre o resumo dela, sem parar a luta. O painel de Efeitos abre no centro do círculo. No
	/// fim avisa <see cref="Finished"/>; o GameRoot aplica a recompensa, grava o melhor tempo e chama
	/// <see cref="ShowResult"/>, que mostra o <see cref="BattleResultPanel"/>.
	///
	/// Na luta de treino, o <see cref="TutorialCoach"/> fala no alto: a tela espera as explicações dele
	/// antes de cada vez do jogador, acende só as habilidades e os alvos que ele deixa e lhe mostra cada
	/// evento; o Automático fica desligado até a última lição.
	/// </summary>
	public partial class BattleScreen : Control
	{
		private readonly BattleSession _session;
		private readonly string _title;
		private readonly TutorialCoach? _coach;
		private readonly Dictionary<BattleUnit, UnitView> _views = new();

		/// <summary>Quantos próximos a ordem de turno mostra.</summary>
		private const int TurnsShown = 6;

		/// <summary>A faixa da ordem de turno, à esquerda do campo.</summary>
		private const float ArenaLeft = 96;

		/// <summary>Onde o centro do oval fica na altura da tela (o mesmo da <see cref="BattleArena"/>).</summary>
		private const float ArenaMiddle = 0.52f;

		private readonly BattleArena _arena = new() { Name = "Arena" };
		private readonly HBoxContainer _counters = Layout.Row(8).Named("Counters");
		private string _wave = "";
		private string _round = "";
		private readonly Label _banner = new() { Name = "Banner" };
		private readonly HBoxContainer _actions = Layout.Row(12).Named("Skills");
		private readonly GameButton _autoButton = new("", ButtonKind.Secondary, "auto", 56) { Name = "Auto", ToggleMode = true };
		private readonly SigilButton _speedButton = new(null, 56, SigilShape.Square) { Name = "Speed" };
		private readonly GameButton _effectsButton = new("", ButtonKind.Secondary, "effects", 56) { Name = "Effects", ToggleMode = true };
		private readonly TurnOrderBar _order = new();
		private readonly PanelContainer _effects = new() { Name = "EffectsPanel", Visible = false };
		private readonly VBoxContainer _effectsList = new() { Name = "Units" };

		private bool _auto;
		private int _speedIndex = 1;
		private bool _closed;
		private bool _finished;

		/// <summary>Decide no automático a vez que está esperando o jogador; nulo quando ninguém espera.</summary>
		private Action? _decideAutomatically;

		/// <summary>O que um toque em unidade faz agora; nulo fora da escolha de alvo.</summary>
		private Action<BattleUnit>? _pickTarget;

		/// <summary>Quem está fora do lugar, na frente do alvo: dá o tranco a cada golpe até voltar.</summary>
		private UnitView? _striker;

		/// <param name="coach">O Mestre da luta de treino; nulo nas outras lutas.</param>
		public BattleScreen(BattleSession session, string title, bool auto, TutorialCoach? coach = null)
		{
			_session = session;
			_title = title;
			_auto = auto && coach == null;
			_coach = coach;
		}

		/// <summary>A luta acabou: verdadeiro na vitória.</summary>
		public event Action<bool>? Finished;

		/// <summary>O jogador saiu da tela (depois do resultado ou recuando). O valor é a preferência de automático.</summary>
		public event Action<bool>? Closed;

		/// <summary>O jogador vendeu a runa que caiu, no resultado.</summary>
		public event Action<Rune>? RuneSellRequested;

		/// <summary>O jogador bloqueou a runa que caiu, no resultado (ela fica, bloqueada).</summary>
		public event Action<Rune>? RuneLockRequested;

		/// <summary>O jogador pediu a mesma luta de novo, do começo (no menu de pausa). O valor é a preferência de automático.</summary>
		public event Action<bool>? RestartRequested;

		private float Speed => BattlePace.Speeds[_speedIndex].Factor;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background(_arena, ring: false));

			// O campo ocupa a tela toda, menos a faixa da ordem de turno à esquerda.
			_arena.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_arena.OffsetLeft = ArenaLeft;
			_arena.OffsetRight = -Layout.ScreenMargin;
			_arena.OffsetTop = 16;
			_arena.OffsetBottom = -16;
			AddChild(_arena);
			_arena.SetAllies(_session.Allies.Select((ally, i) => (Control)ViewFor(ally, $"Ally{i + 1}")).ToList());

			AddChild(Header());
			AddChild(Banner());
			AddChild(Pin(_order, LayoutPreset.TopLeft));
			_order.OffsetTop = _order.OffsetBottom = 104;
			AddChild(Pin(SigilButton.Of("pause", OpenPause, 56, SigilShape.Square), LayoutPreset.TopRight));
			AddChild(Pin(Controls(), LayoutPreset.BottomLeft));
			AddChild(Pin(_actions, LayoutPreset.BottomRight));
			AddChild(EffectsPanel());
			if (_coach != null)
			{
				// A placa do Mestre fica no quarto de cima à esquerda do campo, o único sempre vazio: os
				// aliados ficam embaixo à esquerda, e os inimigos, em cima à direita.
				_coach.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
				_coach.OffsetLeft = _coach.OffsetRight = ArenaLeft + 24;
				_coach.OffsetTop = _coach.OffsetBottom = 72;
				AddChild(_coach);
				_autoButton.Disabled = true;
			}

			RefreshAuto();
			_order.Show(_session.PredictOrder(TurnsShown));

			Run();
		}

		/// <summary>O tempo da luta na tela até o fim dela, sem a pausa (o <c>_Process</c> para junto).</summary>
		public double Elapsed { get; private set; }

		/// <summary>
		/// Abre o resultado (<see cref="BattleResultPanel"/>) por cima do campo, com o tempo desta luta. O
		/// motivo da derrota (tempo esgotado ou todos caídos) vem da própria luta.
		/// </summary>
		public void ShowResult(BattleOutcome outcome)
		{
			(string Icon, string Text)? defeat = outcome.Victory ? null
				: _session.Round > BattleRules.RoundLimit ? ("resolve", T("battle.timeout", BattleRules.RoundLimit))
				: ("retreat", T("battle.all_fell"));
			_banner.Text = "";
			// O painel de Efeitos aberto ficaria por baixo do resultado, atrapalhando a leitura.
			_effectsButton.ButtonPressed = false;
			AddChild(new BattleResultPanel(outcome, Elapsed, defeat, Close, Restart, rune => RuneSellRequested?.Invoke(rune), rune => RuneLockRequested?.Invoke(rune)));
		}

		public override void _Process(double delta)
		{
			if (!_finished)
				Elapsed += delta;
		}

		public override void _ExitTree() => _closed = true;

		/// <summary>Esc e o Voltar do celular pausam — depois que as janelas por cima (resumo, habilidade) fecharam.</summary>
		public override void _UnhandledInput(InputEvent @event)
		{
			if (!@event.IsActionPressed("ui_cancel"))
				return;
			GetViewport().SetInputAsHandled();
			OpenPause();
		}

		/// <summary>O canto de cima à esquerda: o nome da luta, a onda e a rodada.</summary>
		private Control Header()
		{
			var header = Layout.Row(12).Named("Header");
			var title = new Label { Name = "Title", Text = _title, VerticalAlignment = VerticalAlignment.Center };
			title.AddThemeFontOverride("font", GameTheme.Serif);
			title.AddThemeFontSizeOverride("font_size", 20);
			title.AddThemeColorOverride("font_color", Palette.Gold);
			header.AddChild(title);
			header.AddChild(_counters);
			return Pin(header, LayoutPreset.TopLeft);
		}

		/// <summary>O que acontece (a onda, quem usa o quê), escrito no meio do círculo.</summary>
		private Control Banner()
		{
			_banner.HorizontalAlignment = HorizontalAlignment.Center;
			_banner.VerticalAlignment = VerticalAlignment.Center;
			_banner.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_banner.MouseFilter = MouseFilterEnum.Ignore;
			_banner.AddThemeFontOverride("font", GameTheme.Serif);
			_banner.AddThemeFontSizeOverride("font_size", 22);
			_banner.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
			return OnArenaMiddle(_banner, new Vector2(300, 80));
		}

		/// <summary>O canto de baixo à esquerda: automático, velocidade (o número no sigilo) e Efeitos.</summary>
		private Control Controls()
		{
			var row = Layout.Row(10).Named("Controls");
			_autoButton.ButtonPressed = _auto;
			_autoButton.Toggled += SetAuto;
			row.AddChild(_autoButton);

			ShowSpeed();
			_speedButton.Pressed += () =>
			{
				_speedIndex = (_speedIndex + 1) % BattlePace.Speeds.Count;
				ShowSpeed();
			};
			row.AddChild(_speedButton);

			_effectsButton.Text = T("battle.effects");
			_effectsButton.Toggled += on =>
			{
				_effects.Visible = on;
				RefreshEffects();
			};
			row.AddChild(_effectsButton);
			return row;
		}

		/// <summary>A onda e a rodada, em cápsulas.</summary>
		private void RefreshCounters()
		{
			Layout.Clear(_counters);
			if (_wave.Length > 0)
				_counters.AddChild(Layout.Labeled("fight", _wave, T("battle.wave_tip")).Named("Wave"));
			if (_round.Length > 0)
				_counters.AddChild(Layout.Labeled("resolve", _round, T("battle.round_tip")).Named("Round"));
		}

		private void ShowSpeed()
		{
			var label = BattlePace.Speeds[_speedIndex].Label;
			_speedButton.Letters = $"{label}×";
		}

		/// <summary>Pausa a luta e abre o menu: continuar, recomeçar do começo ou sair.</summary>
		private void OpenPause()
		{
			if (_closed)
				return;
			PauseMenu.Open(this, Restart, Close);
		}

		private void Restart()
		{
			if (_closed)
				return;
			_closed = true;
			RestartRequested?.Invoke(_auto);
		}

		/// <summary>Prende um controle num canto da tela, a <see cref="Layout.ScreenMargin"/> das bordas, crescendo para dentro.</summary>
		private static Control Pin(Control control, LayoutPreset corner)
		{
			var margin = Layout.ScreenMargin;
			var right = corner is LayoutPreset.TopRight or LayoutPreset.BottomRight;
			var bottom = corner is LayoutPreset.BottomLeft or LayoutPreset.BottomRight;
			control.SetAnchorsAndOffsetsPreset(corner);
			control.GrowHorizontal = right ? GrowDirection.Begin : GrowDirection.End;
			control.GrowVertical = bottom ? GrowDirection.Begin : GrowDirection.End;
			control.OffsetLeft = control.OffsetRight = right ? -margin : margin;
			control.OffsetTop = control.OffsetBottom = bottom ? -margin : margin;
			return control;
		}

		/// <summary>Centra um controle de tamanho <paramref name="size"/> no meio do círculo da arena.</summary>
		private static Control OnArenaMiddle(Control control, Vector2 size)
		{
			// O campo começa depois da faixa da esquerda e acaba na margem da direita: o meio dele fica deslocado.
			var shift = (ArenaLeft - Layout.ScreenMargin) / 2;
			control.AnchorLeft = control.AnchorRight = 0.5f;
			control.AnchorTop = control.AnchorBottom = ArenaMiddle;
			control.OffsetLeft = shift - size.X / 2;
			control.OffsetRight = shift + size.X / 2;
			control.OffsetTop = -size.Y / 2;
			control.OffsetBottom = size.Y / 2;
			return control;
		}

        /// <summary>O painel de Efeitos: por cima do centro do campo, com o que está sobre cada unidade viva.</summary>
        private PanelContainer EffectsPanel()
        {
            _effects.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 14));

            OnArenaMiddle(_effects, new Vector2(640, 320));
            _effects.ZIndex = 21;

            var column = new VBoxContainer { Name = "Column" };
            var header = Layout.Row(8).Named("Header");
            header.AddChild(Doodle.Icon(Art.Icon("effects"), 30, Palette.Gold).Named("Icon"));
            header.AddChild(new Label { Name = "Title", Text = T("battle.effects"), ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            // O "?" ao lado do ✕, do mesmo tamanho: o que cada efeito em jogo faz, com o texto do Compêndio.
            var help = new SigilButton(null, 48) { Name = "Help", Letters = "?" };
            help.SetLetterSize(26);
            help.Pressed += () => ExplainEffects(help);
            header.AddChild(help);
            header.AddChild(SigilButton.Of("cancel", () => _effectsButton.ButtonPressed = false, 48).Named("Close"));
            column.AddChild(header);

            _effectsList.AddThemeConstantOverride("separation", 8);
            column.AddChild(Layout.Scroll(_effectsList));
            _effects.AddChild(column);

            return _effects;
        }

        /// <summary>
        /// A janela estreita do "?": os efeitos que estão em jogo agora (todos, se nenhum), um embaixo do
        /// outro, cada um com o símbolo, se é bom ou ruim e a explicação do Compêndio.
        /// </summary>
        private void ExplainEffects(Control anchor)
        {
            const float width = 440;
            var active = _session.Allies.Concat(_session.Enemies)
                .Where(u => u.IsAlive)
                .SelectMany(u => u.Statuses)
                .Select(status => status.Kind)
                .Distinct()
                .ToList();
            var kinds = active.Count > 0 ? active : Enum.GetValues<Core.Content.StatusKind>().ToList();

            var dialog = Dialog.Open(anchor, T("battle.effects_help"), width, anchor, "EffectsHelp");
            dialog.Body.AddChild(Layout.Text(T(active.Count > 0 ? "battle.effects_help_active" : "battle.effects_help_all"), GameTheme.Faded, width - 40).Named("Intro"));
            foreach (var kind in kinds)
            {
                var negative = BattleRules.IsNegative(kind);
                var row = Layout.Row(10).Named(kind.ToString());
                var icon = Doodle.Icon(Art.Effect(kind), 36, negative ? Palette.Negative : Palette.Positive).Named("Icon");
                icon.SizeFlagsVertical = SizeFlags.ShrinkBegin;
                row.AddChild(icon);
                var tag = negative ? T("compendium.effects.negative") : T("compendium.effects.positive");
                row.AddChild(RichText.Label($"{Texts.Term(kind)}  [color=#{Palette.TextFaded.ToHtml(false)}]{tag}[/color]\n{Texts.Explain(kind)}", width - 90).Named("Text"));
                dialog.Body.AddChild(row);
            }
        }

        private void RefreshEffects()
        {
            if (!_effects.Visible)
                return;

            Layout.Clear(_effectsList);

            var columnsContainer = new HBoxContainer { Name = "Columns", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            columnsContainer.AddThemeConstantOverride("separation", 16);

            var alliesColumn = new VBoxContainer { Name = "AlliesColumn", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            alliesColumn.AddThemeConstantOverride("separation", 8);

            var enemiesColumn = new VBoxContainer { Name = "EnemiesColumn", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            enemiesColumn.AddThemeConstantOverride("separation", 8);

            columnsContainer.AddChild(alliesColumn);
            columnsContainer.AddChild(enemiesColumn);
            _effectsList.AddChild(columnsContainer);

            var teams = new[]
            {
				new { Members = _session.Allies, Column = alliesColumn },
				new { Members = _session.Enemies, Column = enemiesColumn }
			};

            foreach (var team in teams)
            {
                var units = team.Members.Where(u => u.IsAlive).ToList();
                for (var i = 0; i < units.Count; i++)
                {
                    var unit = units[i];
                    var row = Layout.Row(8).Named($"Unit{i + 1}");
                    var frame = new PanelContainer { Name = "Portrait", MouseFilter = MouseFilterEnum.Stop };
                    var ring = unit.Side == Side.Allies ? Palette.Health : Palette.HealthLow;

                    frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, ring, 2, 18, 2));
                    frame.AddChild(Layout.Medal(Art.Creature(unit.Image), Palette.Of(unit.Element), 36));
                    var shown = unit;
                    Press.On(frame, () => MonsterSummary.Open(frame, shown), () => MonsterSummary.Open(frame, shown));
                    row.AddChild(frame);

                    var statuses = Layout.Flow(6).Named("Statuses");
                    statuses.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                    for (var k = 0; k < unit.Statuses.Count; k++)
                    {
                        var status = unit.Statuses[k];
                        var ink = BattleRules.IsNegative(status.Kind) ? Palette.Negative : Palette.Positive;
                        var value = status.Kind == Core.Content.StatusKind.Shield ? $" • {Math.Round(status.Value)}" : "";
                        statuses.AddChild(Layout.Labeled(Art.Effect(status.Kind), $"{status.Turns}{value}", Texts.Name(status.Kind), ink).Named($"{status.Kind}{k + 1}"));
                    }

                    row.AddChild(statuses);
                    team.Column.AddChild(row);
                }
            }
        }

        /// <summary>O cartão de <paramref name="unit"/>, de nome <paramref name="name"/> (<c>Ally2</c>, <c>Enemy1</c>).</summary>
        private UnitView ViewFor(BattleUnit unit, string name)
		{
			var view = new UnitView(unit) { Name = name };
			view.Pressed += v =>
			{
				if (_pickTarget != null)
				{
					_pickTarget(v.Unit);
					return;
				}

				MonsterSummary.Open(v, v.Unit);
			};
			view.LongPressed += v => MonsterSummary.Open(v, v.Unit);
			_views[unit] = view;
			return view;
		}

		private async void Run()
		{
			await Play(_session.Start());
			if (_coach != null)
				await _coach.Begin();
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

			_finished = true;
			if (!_closed)
				Finished?.Invoke(_session.Victory == true);
		}

		// Decisões ----------------------------------------------------------------------------------

		private async Task<UnitAction> DecideAlly(BattleUnit ally)
		{
			if (_coach != null)
			{
				await _coach.BeforeDecision(_session, ally);
				_autoButton.Disabled = !_coach.AutoAllowed;
			}

			if (_auto)
			{
				var chosen = AutoPilot.ForAlly(_session, ally);
				_coach?.Acted(chosen.Skill);
				return chosen;
			}

			var decision = new TaskCompletionSource<UnitAction>(TaskCreationOptions.RunContinuationsAsynchronously);
			void Decide(UnitAction action)
			{
				ClearActions();
				_coach?.Acted(action.Skill);
				decision.TrySetResult(action);
			}

			_decideAutomatically = () => Decide(AutoPilot.ForAlly(_session, ally));

			for (var i = 0; i < ally.Skills.Count; i++)
			{
				var index = i;
				var skill = ally.Skill(index);
				var ready = ally.IsReady(index);
				var slot = new VBoxContainer { Name = $"Skill{index + 1}" };
				slot.AddThemeConstantOverride("separation", 2);
				var button = new SigilButton(null, 74, SigilShape.Square)
				{
					Name = "Button",
					Disabled = !ready || _coach?.CanUse(index) == false,
					Badge = ready ? "" : T("battle.cooldown_badge", ally.Cooldown(index)),
				};
				button.SetSymbol(Art.Skill(skill));
				Press.OnButton(button, () =>
				{
					if (skill.NeedsTarget)
						PickTarget(ally, button, target => Decide(new UnitAction(index, target)));
					else
						Decide(new UnitAction(index, null));
				}, () => Dialog.Info(button, skill.Name, Texts.Describe(skill)));
				slot.AddChild(button);
				var name = new Label { Name = "Name", Text = skill.Name, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(92, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
				name.AddThemeFontSizeOverride("font_size", 14);
				name.AddThemeColorOverride("font_color", ready ? Palette.Text : Palette.TextFaded);
				name.AddThemeColorOverride("font_outline_color", Palette.Background);
				name.AddThemeConstantOverride("outline_size", 4);
				slot.AddChild(name);
				_actions.AddChild(slot);
			}

			return await decision.Task;
		}

		/// <summary>Os alvos possíveis acendem em azul; o sigilo da habilidade fica aceso até o toque no alvo.</summary>
		private void PickTarget(BattleUnit actor, SigilButton skill, Action<BattleUnit> onPicked)
		{
			foreach (var view in _views.Values)
				view.SetTargetable(false);
			foreach (var child in _actions.GetChildren().Select(c => c.GetNodeOrNull<SigilButton>("Button")).OfType<SigilButton>())
				child.Highlight = child == skill;
			_banner.Text = T("battle.pick_target");
			_banner.AddThemeColorOverride("font_color", Palette.Arcane);

			// Na luta de treino, o Mestre pode deixar só alguns alvos (os de Fogo, na lição dos elementos).
			var choosable = _session.ChoosableTargets(actor);
			if (_coach != null && choosable.Any(_coach.CanTarget))
				choosable = choosable.Where(_coach.CanTarget).ToList();
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
			Layout.Clear(_actions);
		}

		private void SetAuto(bool on)
		{
			_auto = on;
			RefreshAuto();
			if (on)
				_decideAutomatically?.Invoke();
		}

		private void RefreshAuto() => _autoButton.Text = _auto ? T("battle.auto_on") : T("battle.auto_off");

		private void Close()
		{
			if (_closed)
				return;
			_closed = true;
			Closed?.Invoke(_auto);
		}

		// Animação dos eventos ----------------------------------------------------------------------

		/// <summary>
		/// Toca os eventos em momentos (<see cref="BattlePace.Beats"/>): quem ataca corre até o alvo, cada golpe
		/// mostra de uma vez os acertos que caem juntos (com o tranco de quem bate e o respingo em cada alvo),
		/// e no fim da ação ele volta ao lugar. A espera entre momentos para com a pausa.
		/// </summary>
		private async Task Play(IReadOnlyList<BattleEvent> events)
		{
			foreach (var beat in BattlePace.Beats(events))
			{
				if (_closed || !IsInsideTree())
					return;

				var seconds = beat.Seconds / Speed;
				switch (beat.Kind)
				{
					case BeatKind.Approach when beat.Actor != null && _views.TryGetValue(beat.Actor, out var actor):
						_striker = actor;
						_arena.Approach(actor, (beat.Targets ?? Array.Empty<BattleUnit>()).Where(_views.ContainsKey).Select(unit => (Control)_views[unit]).ToList(), seconds);
						break;
					case BeatKind.Return when beat.Actor != null && _views.TryGetValue(beat.Actor, out var returning):
						_arena.Return(returning, seconds);
						_striker = null;
						break;
					case BeatKind.Volley when _striker != null && beat.Events.Select(HitTarget).FirstOrDefault(unit => unit != null) is { } first && _views.TryGetValue(first, out var struck):
						_arena.Bump(_striker, struck, seconds);
						break;
				}

				foreach (var battleEvent in beat.Events)
					Show(battleEvent);
				if (seconds > 0)
					await ToSignal(GetTree().CreateTimer(seconds, processAlways: false), SceneTreeTimer.SignalName.Timeout);
			}

			RefreshEffects();
		}

		private static BattleUnit? HitTarget(BattleEvent battleEvent) => battleEvent switch
		{
			Damaged damaged => damaged.Target,
			Missed missed => missed.Target,
			Protected _protected => _protected.Target,
			_ => null,
		};

		/// <summary>Aplica um evento na tela. Quanto esperar depois é do <see cref="BattlePace"/>.</summary>
		private void Show(BattleEvent battleEvent)
		{
			_coach?.Saw(battleEvent);
			switch (battleEvent)
			{
				case WaveStarted wave:
					foreach (var old in _views.Keys.Where(u => u.Side == Side.Enemies).ToList())
						_views.Remove(old);
					_arena.SetEnemies(wave.Enemies.Select((enemy, i) => (Control)ViewFor(enemy, $"Enemy{i + 1}")).ToList());
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
					_order.Show(_session.PredictOrder(TurnsShown));
					return;

				case SkillUsed used:
					_banner.Text = T("battle.uses", used.Actor.Name, used.Skill.Name);
					_banner.AddThemeColorOverride("font_color", Palette.Text);
					return;

				case ExtraTurn extra:
					_views[extra.Unit].Float(T("battle.extra_turn"), Palette.Gold, 14);
					return;

				case Counterattack counter:
					_views[counter.Unit].Float(T("battle.counterattack"), Palette.Gold, 14);
					return;

				case MaxHealthReduced reduced:
					_views[reduced.Target].Float(T("battle.max_hp", reduced.Amount), Palette.Negative);
					_views[reduced.Target].Refresh();
					return;

				case Damaged damaged:
					var hit = _views[damaged.Target];
					hit.Shake(Speed);
					_arena.Splash(hit, damaged.Crit ? Palette.Gold : Palette.Damage, BattlePace.Hit / Speed, damaged.Crit);
					hit.Float(damaged.Crit ? $"-{damaged.Amount}!" : $"-{damaged.Amount}", damaged.Crit ? Palette.Gold : Palette.Damage, damaged.Crit ? 18 : 16);
					hit.Refresh();
					return;

				case Missed missed:
					_arena.Splash(_views[missed.Target], Palette.TextFaded, BattlePace.Hit / Speed);
					_views[missed.Target].Float(T("battle.missed"), Palette.TextFaded);
					return;

				case Protected _protected:
					_arena.Splash(_views[_protected.Target], Palette.Shield, BattlePace.Hit / Speed);
					_views[_protected.Target].Float(T("battle.aegis"), Palette.Shield);
					_views[_protected.Target].Refresh();
					return;

				case Healed healed:
					_views[healed.Target].Float($"+{healed.Amount}", Palette.Heal, 14);
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
					_views[revived.Unit].Float(T("battle.revives"), Palette.Gold, 14);
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
