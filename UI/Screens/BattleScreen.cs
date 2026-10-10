using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Audio;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
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
	/// nome embaixo (segurar mostra o que ela faz). Segurar uma unidade abre o resumo dela, sem parar a
	/// luta; tocar num aliado também, e tocar num inimigo marca o foco (ver abaixo). O painel de Efeitos abre no centro do círculo. No
	/// fim avisa <see cref="Finished"/>; o GameRoot aplica a recompensa, grava o melhor tempo e chama
	/// <see cref="ShowResult"/>, que mostra o <see cref="BattleResultPanel"/>.
	///
	/// Numa onda com chefe, ele fica no meio dos inimigos, com o cartão maior, e a <see cref="BossBar"/>
	/// mostra a Vida dele no alto, no centro. Na pausa, o jogador escolhe se o automático foca o chefe.
	/// Tocar num inimigo (fora da escolha de alvo) marca o foco: a mira aparece nele e o automático ataca
	/// ele enquanto puder; tocar de novo desmarca. O resumo do inimigo fica no toque longo.
	///
	/// Na luta de treino, o <see cref="TutorialCoach"/> fala no alto: a tela espera as explicações dele
	/// antes de cada vez do jogador, acende só as habilidades e os alvos que ele deixa e lhe mostra cada
	/// evento; o Automático fica desligado até a última lição.
	///
	/// Para ver a Batalha automática (<see cref="AutoBattleFight"/>), a tela só assiste: não pede turno nem
	/// decide nada, mostra os momentos da luta conforme chegam no relógio dela. Pequena, na janela da
	/// Batalha automática, é só o campo e os monstros; na tela cheia, tem o cabeçalho, a ordem de turno,
	/// Efeitos e a seta de voltar no lugar da pausa.
	/// </summary>
	public partial class BattleScreen : Control
	{
		private readonly BattleSession _session;
		private readonly string _title;
		private readonly TutorialCoach? _coach;
		private readonly Dictionary<BattleUnit, UnitView> _views = new();

		/// <summary>A luta da Batalha automática que a tela assiste; nula na luta jogada.</summary>
		private readonly AutoBattleFight? _watch;

		/// <summary>Com o que fica em volta do campo (cabeçalho, ordem de turno, botões); só o campo na vista pequena.</summary>
		private readonly bool _hud = true;

		/// <summary>A seta de voltar da tela cheia que assiste.</summary>
		private readonly Action? _back;

		/// <summary>Os sons dos momentos; só a tela com o que fica em volta do campo toca (a vista pequena é muda).</summary>
		private readonly BattleSounds _sounds;

		/// <summary>O pedaço da luta assistida que já estava no campo quando a tela começou a olhar.</summary>
		private int _joined;

		/// <summary>Quantos próximos a ordem de turno mostra.</summary>
		private const int TurnsShown = 6;

		/// <summary>A faixa da ordem de turno, à esquerda do campo.</summary>
		private const float ArenaLeft = 96;

		/// <summary>Onde o centro do oval fica na altura da tela (o mesmo da <see cref="BattleArena"/>).</summary>
		private const float ArenaMiddle = 0.52f;

		private readonly BattleArena _arena = new() { Name = "Arena" };
		private readonly HBoxContainer _counters = Layout.Row(Space.Medium).Named("Counters");
		private string _wave = "";
		private string _round = "";
		private readonly Label _banner = new() { Name = "Banner" };
		private readonly HBoxContainer _actions = Layout.Row(Space.Large).Named("Skills");
		private readonly GameButton _autoButton = new("", ButtonKind.Secondary, "auto", 56) { Name = "Auto", ToggleMode = true };
		private readonly SigilButton _speedButton = new(null, 56, SigilShape.Square) { Name = "Speed" };
		private readonly GameButton _effectsButton = new("", ButtonKind.Secondary, "effects", 56) { Name = "Effects", ToggleMode = true };
		private readonly TurnOrderBar _order = new();
		private readonly BossBar _bossBar = new();
		private readonly PanelContainer _effects = new() { Name = "EffectsPanel", Visible = false };
		private readonly VBoxContainer _effectsList = new() { Name = "Units" };

		private bool _auto;
		private bool _focusBoss;

		/// <summary>O inimigo que o jogador marcou: o automático ataca ele primeiro. Some quando ele cai ou a onda acaba.</summary>
		private BattleUnit? _focus;
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
		/// <param name="focusBoss">O automático mira o chefe (a preferência salva; muda na pausa).</param>
		public BattleScreen(BattleSession session, string title, bool auto, TutorialCoach? coach = null, bool focusBoss = false)
		{
			_session = session;
			_title = title;
			_auto = auto && coach == null;
			_coach = coach;
			_focusBoss = focusBoss;
			_sounds = new BattleSounds();
		}

		/// <summary>
		/// Assiste a luta de agora da Batalha automática, na velocidade dela. Com <paramref name="back"/>, na
		/// tela cheia (com a seta de voltar); sem, só o campo e os monstros.
		/// </summary>
		public BattleScreen(AutoBattleFight fight, string title, Action? back)
		{
			_session = fight.Session;
			_title = title;
			_watch = fight;
			_auto = true;
			_hud = back != null;
			_back = back;
			_speedIndex = BattlePace.Speeds.Count - 1;
			_sounds = new BattleSounds();
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

		/// <summary>O jogador ligou ou desligou, na pausa, o foco do automático no chefe.</summary>
		public event Action<bool>? FocusBossChanged;

		/// <summary>Do resultado, direto para a luta seguinte (<see cref="ShowResult"/> com a Mana dela). O valor é a preferência de automático.</summary>
		public event Action<bool>? NextRequested;

		private float Speed => BattlePace.Speeds[_speedIndex].Factor;

		public override void _Ready()
		{
			// A vista pequena tem o tamanho e a escala de quem a mostra.
			if (_hud)
				SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background(_arena, ring: false));

			// O campo ocupa a tela toda, menos a faixa da ordem de turno à esquerda.
			_arena.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_arena.OffsetLeft = _hud ? ArenaLeft : Layout.ScreenMargin;
			_arena.OffsetRight = -Layout.ScreenMargin;
			_arena.OffsetTop = 16;
			_arena.OffsetBottom = -16;
			AddChild(_arena);
			_arena.SetAllies(_session.Allies.Select((ally, i) => (Control)ViewFor(ally, $"Ally{i + 1}")).ToList());

			AddChild(Header());
			AddChild(Banner());
			// A barra do chefe no alto, no centro: o nome da luta fica à esquerda, a pausa à direita, e ela
			// encosta no topo para não cobrir o inimigo do alto do arco.
			_bossBar.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
			_bossBar.GrowHorizontal = GrowDirection.Both;
			_bossBar.OffsetTop = _bossBar.OffsetBottom = 8;
			AddChild(_bossBar);
			AddChild(Pin(_order, LayoutPreset.TopLeft));
			_order.OffsetTop = _order.OffsetBottom = 104;
            AddChild(Pin(_back != null ? new BackButton(_back) { Name = "Back" } : SigilButton.Of("pause", OpenPause, 56, SigilShape.Square), LayoutPreset.TopRight));
			AddChild(Pin(Controls(), LayoutPreset.BottomLeft));
			AddChild(Pin(_actions, LayoutPreset.BottomRight));
			AddChild(EffectsPanel());
			if (_watch != null)
			{
				// A vista pequena é só o campo e os monstros.
				if (!_hud)
					foreach (var hud in GetChildren().OfType<Control>().Where(child => child != _arena && child is not Backdrop))
						hud.Visible = false;
				Join();
				return;
			}

			if (_coach != null)
			{
				// A placa do Mestre fica no quarto de cima à esquerda do campo, o único sempre vazio: os
				// aliados ficam embaixo à esquerda, e os inimigos, em cima à direita.
				_coach.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
				_coach.OffsetLeft = _coach.OffsetRight = ArenaLeft + 24;
				_coach.OffsetTop = _coach.OffsetBottom = 100;
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
		/// <param name="nextMana">A Mana da luta seguinte, quando dá para entrar nela agora: o Continuar vai para ela (<see cref="NextRequested"/>).</param>
		public void ShowResult(BattleOutcome outcome, int? nextMana = null)
		{
			(string Icon, string Text)? defeat = outcome.Victory ? null
				: _session.Round > BattleRules.RoundLimit ? ("resolve", T("battle.timeout", BattleRules.RoundLimit))
				: ("retreat", T("battle.all_fell"));
			_banner.Text = "";
			// O painel de Efeitos aberto ficaria por baixo do resultado, atrapalhando a leitura.
			_effectsButton.ButtonPressed = false;
			_bossBar.Clear();
			var next = nextMana is { } mana ? (mana, (Action)Next) : ((int, Action)?)null;
			AddChild(new BattleResultPanel(outcome, Elapsed, defeat, Close, Restart, rune => RuneSellRequested?.Invoke(rune), rune => RuneLockRequested?.Invoke(rune), next));
		}

		public override void _Process(double delta)
		{
			if (!_finished)
				Elapsed += delta;
		}

		public override void _ExitTree()
		{
			_closed = true;
			if (_watch != null)
				_watch.Played -= Watch;
		}

		/// <summary>Esc e o Voltar do celular pausam — depois que as janelas por cima (resumo, habilidade) fecharam.</summary>
		public override void _UnhandledInput(InputEvent @event)
		{
			// Assistindo, não há pausa: a tela cheia volta pela seta (que também atende o Esc).
			if (_watch != null || !@event.IsActionPressed("ui_cancel"))
				return;
			GetViewport().SetInputAsHandled();
			OpenPause();
		}

		/// <summary>
		/// O canto de cima à esquerda: o nome da luta e, embaixo dele, a onda e a rodada (em duas linhas, para
		/// sobrar o alto do centro para a barra do chefe).
		/// </summary>
		private Control Header()
		{
			var header = new VBoxContainer { Name = "Header" };
			header.AddThemeConstantOverride("separation", Space.Tight);
			var title = new Label { Name = "Title", Text = _title, VerticalAlignment = VerticalAlignment.Center };
			title.AddThemeFontOverride("font", GameTheme.Serif);
			title.AddThemeFontSizeOverride("font_size", 20);
			title.AddThemeColorOverride("font_color", Palette.Gold);
			header.AddChild(title);
			// O balão do chat fica ao lado da onda e da rodada (as cápsulas se refazem; o lugar dele não).
			var counters = Layout.Row(Space.Medium).Named("Status");
			counters.AddChild(_counters);
			counters.AddChild(ChatBubble.Slot());
			header.AddChild(counters);
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

		/// <summary>
		/// O canto de baixo à esquerda: automático, velocidade (o número no sigilo) e Efeitos, numa grade de
		/// três; o Automático tem largura para "Automático: desligado" caber numa linha.
		/// </summary>
		private Control Controls()
		{
			var row = Layout.Grid(4, 10).Named("Controls");
			row.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			_autoButton.Wide(150);
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
			// Assistindo a Batalha automática, o automático e a velocidade são os dela.
			_autoButton.Visible = _speedButton.Visible = _watch == null;

			_effectsButton.Text = T("battle.effects");
			_effectsButton.Toggled += on =>
			{
				_effects.Visible = on;
				RefreshEffects();
			};
			row.AddChild(_effectsButton);
            var help = new SigilButton(null, 48) { Name = "Help", Letters = "?" };
            help.SetLetterSize(26);
            help.Pressed += () => ExplainEffects(help);
            row.AddChild(help);
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
			// Foco no chefe só aparece em luta que tem chefe; a preferência vale para todas (e para a Batalha automática).
			PauseMenu.Open(this, Restart, Close, _session.HasBoss ? (_focusBoss, on =>
			{
				_focusBoss = on;
				FocusBossChanged?.Invoke(on);
			}) : null);
		}

		private void Restart()
		{
			if (_closed)
				return;
			_closed = true;
			RestartRequested?.Invoke(_auto);
		}

		private void Next()
		{
			if (_closed)
				return;
			_closed = true;
			NextRequested?.Invoke(_auto);
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
            var header = Layout.Row(Space.Medium).Named("Header");
            header.AddChild(Doodle.Icon(Art.Icon("effects"), 30, Palette.Gold).Named("Icon"));
            header.AddChild(new Label { Name = "Title", Text = T("battle.effects"), ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            header.AddChild(SigilButton.Of("cancel", () => _effectsButton.ButtonPressed = false, 48).Named("Close"));
            column.AddChild(header);

            _effectsList.AddThemeConstantOverride("separation", Space.Medium);
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
            var kinds = Enum.GetValues<Core.Content.StatusKind>().ToList();

            var dialog = Dialog.Open(anchor, T("battle.effects_help"), width, anchor, "EffectsHelp");
            dialog.Body.AddChild(Layout.Text(T("battle.effects_help_all"), GameTheme.Faded, width - 40).Named("Intro"));
            foreach (var kind in kinds)
            {
                var negative = BattleRules.IsNegative(kind);
                var row = Layout.Row(Space.Regular).Named(kind.ToString());
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
            columnsContainer.AddThemeConstantOverride("separation", Space.Loose);

            var alliesColumn = new VBoxContainer { Name = "AlliesColumn", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            alliesColumn.AddThemeConstantOverride("separation", Space.Medium);

            var enemiesColumn = new VBoxContainer { Name = "EnemiesColumn", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            enemiesColumn.AddThemeConstantOverride("separation", Space.Medium);

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
                    var row = Layout.Row(Space.Medium).Named($"Unit{i + 1}");
                    var frame = new PanelContainer { Name = "Portrait", MouseFilter = MouseFilterEnum.Stop };
                    var ring = unit.Side == Side.Allies ? Palette.Health : Palette.HealthLow;

                    frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, ring, 2, 18, 2));
                    frame.AddChild(Layout.Medal(Art.Creature(unit.Image), Palette.Of(unit.Element), 36));
                    var shown = unit;
                    Press.On(frame, () => MonsterSummary.Open(frame, shown), () => MonsterSummary.Open(frame, shown));
                    row.AddChild(frame);

                    var statuses = Layout.Flow(Space.Small).Named("Statuses");
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

				// Inimigo: marca (ou desmarca) o foco. Aliado (ou qualquer um, assistindo): o resumo, como no toque longo.
				if (v.Unit.Side == Side.Enemies && _watch == null)
					ToggleFocus(v);
				else
					MonsterSummary.Open(v, v.Unit);
			};
			view.LongPressed += v => MonsterSummary.Open(v, v.Unit);
			_views[unit] = view;
			return view;
		}

		/// <summary>Marca o inimigo tocado como foco do automático, ou desmarca se já era ele.</summary>
		private void ToggleFocus(UnitView view)
		{
			if (!view.Unit.IsAlive)
				return;

			var on = _focus != view.Unit;
			SetFocus(on ? view.Unit : null);
			view.Float(T(on ? "battle.focus_on" : "battle.focus_off"), Palette.Gold, 14);
		}

		private void SetFocus(BattleUnit? unit)
		{
			_focus = unit;
			foreach (var view in _views.Values)
				view.SetFocused(view.Unit == unit);
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
				var chosen = AutoPilot.ForAlly(_session, ally, _focusBoss, _focus);
				_coach?.Acted(chosen.Skill);
				return chosen;
			}

			Sfx.Play("progression.turn_start");
			var decision = new TaskCompletionSource<UnitAction>(TaskCreationOptions.RunContinuationsAsynchronously);
			void Decide(UnitAction action)
			{
				ClearActions();
				_coach?.Acted(action.Skill);
				decision.TrySetResult(action);
			}

			_decideAutomatically = () => Decide(AutoPilot.ForAlly(_session, ally, _focusBoss, _focus));

			for (var i = 0; i < ally.Skills.Count; i++)
			{
				var index = i;
				var skill = ally.Skill(index);
				var ready = ally.IsReady(index);
				var slot = new VBoxContainer { Name = $"Skill{index + 1}" };
				slot.AddThemeConstantOverride("separation", Space.Hair);
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
				ShowBeat(beat, seconds);
				if (seconds > 0)
					await ToSignal(GetTree().CreateTimer(seconds, processAlways: false), SceneTreeTimer.SignalName.Timeout);
			}

			RefreshEffects();
		}

		/// <summary>Um momento na tela: a corrida, o tranco ou a volta de quem ataca, e os eventos dele.</summary>
		private void ShowBeat(Beat beat, double seconds)
		{
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
			if (_hud)
				Sfx.Play(_sounds.Of(beat), Speed);
		}

		/// <summary>
		/// Começa a assistir a Batalha automática: o campo como a luta está agora (a onda, a Vida, os efeitos),
		/// e dali em diante os momentos que chegam. O que ainda falta mostrar do pedaço de agora já está no
		/// estado, então fica de fora (<see cref="AutoBattleFight.Steps"/>).
		/// </summary>
		private void Join()
		{
			_joined = _watch!.Steps;
			if (_joined > 0)
				ShowWave(_session.Wave, _session.WaveCount, _session.Enemies);
			foreach (var view in _views.Values)
				view.Refresh();
			if (_hud)
				_order.Show(_session.PredictOrder(TurnsShown));
			if (_session.IsOver)
				_banner.Text = _session.Victory == true ? T("battle.victory") : T("battle.defeat");
			_watch.Played += Watch;
		}

		private void Watch(Beat beat, int step)
		{
			if (step <= _joined || _closed || !IsInsideTree())
				return;
			ShowBeat(beat, AutoBattleFight.Seconds(beat));
			if (beat.Kind == BeatKind.Return)
				RefreshEffects();
		}

		private static BattleUnit? HitTarget(BattleEvent battleEvent) => battleEvent switch
		{
			Damaged damaged => damaged.Target,
			Missed missed => missed.Target,
			Protected _protected => _protected.Target,
			_ => null,
		};

		/// <summary>Os inimigos de uma onda no campo, com a barra e o anúncio do chefe, se ela tiver um.</summary>
		private void ShowWave(int number, int count, IReadOnlyList<BattleUnit> wave)
		{
			SetFocus(null);
			foreach (var old in _views.Keys.Where(u => u.Side == Side.Enemies).ToList())
				_views.Remove(old);
			var enemies = wave.Select((enemy, i) => (Control)ViewFor(enemy, $"Enemy{i + 1}")).ToList();
			var boss = wave.FirstOrDefault(enemy => enemy.IsBoss);
			// Os inimigos do alto do arco descem para os efeitos deles não ficarem embaixo da barra do chefe.
			_arena.Ceiling = boss != null && _hud ? _bossBar.OffsetTop + _bossBar.GetCombinedMinimumSize().Y + 4 - _arena.OffsetTop : 0;
			_arena.SetEnemies(enemies, boss != null ? _views[boss] : null);
			_wave = $"{number}/{count}";
			RefreshCounters();
			if (boss != null)
			{
				// A onda do chefe: a barra grande no alto e o anúncio em vermelho no meio do círculo.
				_bossBar.Track(_views[boss]);
				_banner.Text = T("battle.boss_wave", number, boss.Name);
				_banner.AddThemeColorOverride("font_color", Palette.Negative.Lightened(0.2f));
			}
			else
			{
				_bossBar.Clear();
				_banner.Text = T("battle.wave_banner", number);
				_banner.AddThemeColorOverride("font_color", Palette.Text);
			}
		}

		/// <summary>Aplica um evento na tela. Quanto esperar depois é do <see cref="BattlePace"/>.</summary>
		private void Show(BattleEvent battleEvent)
		{
			_coach?.Saw(battleEvent);
			switch (battleEvent)
			{
				case WaveStarted wave:
					ShowWave(wave.Wave, wave.WaveCount, wave.Enemies);
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

				case JointAttack joint:
					_views[joint.Unit].Float(T("battle.joint_attack"), Palette.Gold, 14);
					return;

				case StatusBlocked blocked:
					_views[blocked.Target].Float(T("battle.blocked", Texts.Name(blocked.Status)), Palette.TextFaded);
					return;

				case DurationChanged duration:
					_views[duration.Target].Float(T("battle.duration", Texts.Name(duration.Status), duration.Turns > 0 ? $"+{duration.Turns}" : $"−{-duration.Turns}"), BattleRules.IsNegative(duration.Status) == duration.Turns > 0 ? Palette.Negative : Palette.Positive);
					_views[duration.Target].Refresh();
					return;

				case HealthLeveled leveled:
					_views[leveled.Target].Float($"-{leveled.Amount}", Palette.TextFaded, 14);
					_views[leveled.Target].Refresh();
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
					_views[skipped.Unit].Float(skipped.Cause is { } cause ? Texts.Name(cause) : T("battle.stunned"), Palette.Negative);
					_banner.Text = T("battle.loses_turn", skipped.Unit.Name);
					return;

				case Died died:
					_views[died.Unit].Refresh();
					if (died.Unit == _focus)
						SetFocus(null);
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
