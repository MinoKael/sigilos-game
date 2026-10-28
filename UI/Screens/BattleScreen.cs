using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.Core.Rta;
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
	/// O campo é a <see cref="BattleArena"/>, visto em diagonal: os aliados embaixo à esquerda, perto, e os
	/// inimigos em cima à direita, ao fundo e menores, e o que acontece escrito no meio dele. Em volta: no canto de cima à
	/// esquerda o nome da luta, a onda e a rodada, escritas, e embaixo, em pé, quem age a seguir; no de
	/// cima à direita a pausa (<see cref="PauseMenu"/>: continuar, recomeçar, sair); embaixo à esquerda
	/// os botões Automático (dourado quando ligado), a velocidade, Efeitos e a ajuda, só com o símbolo; embaixo à
	/// direita uma habilidade por botão, com o nome embaixo (segurar mostra o que ela faz) e, no fim, o losango da
	/// Passiva, que só serve para ler o texto dela. Segurar uma unidade abre o resumo dela, sem parar a
	/// luta; tocar num aliado também, e tocar num inimigo marca o foco (ver abaixo). O painel de Efeitos abre no meio do campo. No
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

		/// <summary>Contra o time de outro jogador (defesa da Arena), a ficha dos monstros dele não abre.</summary>
		private readonly bool _hideRival;
		private readonly Dictionary<BattleUnit, UnitView> _views = new();

		/// <summary>A luta da Batalha automática que a tela assiste; nula na luta jogada.</summary>
		private readonly AutoBattleFight? _watch;

		/// <summary>Com o que fica em volta do campo (cabeçalho, ordem de turno, botões); só o campo na vista pequena.</summary>
		private readonly bool _hud = true;

		/// <summary>A luta da Arena do Véu contra outro jogador; nulo nas outras. Nela as jogadas vêm do servidor.</summary>
		private readonly OnlineBattle? _online;

		/// <summary>O lado do jogador: os <c>Allies</c> da sessão, ou os <c>Enemies</c> para quem escolheu em segundo na Arena do Véu.</summary>
		private readonly Side _mine = Side.Allies;

		private readonly Label _clock = new() { Name = "TurnClock", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
		private readonly Godot.Timer _clockTick = new() { Name = "ClockTick", WaitTime = 0.25, Autostart = false };
		private double _deadline;

		/// <summary>Cancela a espera da decisão do jogador (o prazo estourou e o servidor já jogou por ele).</summary>
		private Action? _cancelDecision;

		private IReadOnlyList<BattleUnit> MyUnits => _mine == Side.Allies ? _session.Allies : _session.Enemies;

		private IReadOnlyList<BattleUnit> TheirUnits => _mine == Side.Allies ? _session.Enemies : _session.Allies;

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

		/// <summary>Onde o texto da ação e o painel de Efeitos ficam, na altura da tela: o meio do campo, um pouco acima do <see cref="BattleArena.Middle"/>.</summary>
		private const float ArenaMiddle = 0.5f;

		private readonly BattleArena _arena = new() { Name = "Arena" };
		private readonly HBoxContainer _counters = Layout.Row(Space.Medium).Named("Counters");
		private string _wave = "";
		private string _round = "";
		private readonly Label _banner = new() { Name = "Banner" };
		private readonly HBoxContainer _actions = Layout.Row(Space.Large).Named("Skills");
		private readonly SigilButton _autoButton = new(Art.Icon("play"), 56, SigilShape.Square) { Name = "Auto" };
		private readonly SigilButton _speedButton = new(null, 56, SigilShape.Square) { Name = "Speed" };
		private readonly SigilButton _configButton = new(null, 56, SigilShape.Square) { Name = "Config" };
		private readonly SigilButton _effectsButton = new(Art.Icon("effects"), 56, SigilShape.Square) { Name = "Effects", ToggleMode = true };
		private readonly TurnOrderBar _order = new();
		private readonly BossBar _bossBar = new();
		private readonly PanelContainer _effects = new() { Name = "EffectsPanel", Visible = false };
		private readonly VBoxContainer _effectsList = new() { Name = "Units" };

		private bool _auto;
		private bool _focusBoss;

		/// <summary>O inimigo que o jogador marcou: o automático ataca ele primeiro. Some quando ele cai ou a onda acaba.</summary>
		private BattleUnit? _focus;

		/// <summary>A velocidade da tela (o índice em <see cref="BattlePace.Speeds"/>): a do aparelho, no manual e no automático.</summary>
		private int _speedIndex = Math.Clamp(UiSession.BattleSpeed, 0, BattlePace.Speeds.Count - 1);
		private bool _closed;
		private bool _finished;

		/// <summary>Ataca o inimigo tocado com a 1ª habilidade, no manual; nulo quando ninguém espera ou ela não pode.</summary>
		private Action<BattleUnit>? _quickAttack;

		/// <summary>Decide no automático a vez que está esperando o jogador; nulo quando ninguém espera.</summary>
		private Action? _decideAutomatically;

		/// <summary>O que um toque em unidade faz agora; nulo fora da escolha de alvo.</summary>
		private Action<BattleUnit>? _pickTarget;

		/// <summary>Quem está fora do lugar, na frente do alvo: dá o tranco a cada golpe até voltar.</summary>
		private UnitView? _striker;

		/// <param name="coach">O Mestre da luta de treino; nulo nas outras lutas.</param>
		/// <param name="focusBoss">O automático mira o chefe (a preferência salva; muda na pausa).</param>
		public BattleScreen(BattleSession session, string title, bool auto, TutorialCoach? coach = null, bool focusBoss = false, bool hideRival = false)
		{
			_hideRival = hideRival;
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

		/// <summary>
		/// A luta da Arena do Véu contra outro jogador (<see cref="OnlineBattle"/>): o time dele fica embaixo, de qualquer
		/// lado que seja na sessão, e as jogadas dos dois vêm do servidor.
		/// </summary>
		public BattleScreen(OnlineBattle online, string title)
		{
			_online = online;
			_session = online.Replica.Match.Session;
			_mine = online.Seat == RtaSeat.First ? Side.Allies : Side.Enemies;
			_title = title;
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
			_arena.SetAllies(MyUnits.Select((ally, i) => (Control)ViewFor(ally, $"Ally{i + 1}")).ToList());

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
            AddChild(Pin(_back != null ? new BackButton(_back) { Name = "Back" } : Help(), LayoutPreset.TopRight));
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

			if (_online != null)
			{
				StartOnline();
				return;
			}

			if (_coach != null)
			{
				// A placa do Mestre fica no canto de cima à esquerda do campo, sempre vazio: o time fica embaixo
				// e os inimigos, em cima à direita.
				_coach.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
				_coach.OffsetLeft = _coach.OffsetRight = ArenaLeft + 24;
				_coach.OffsetTop = _coach.OffsetBottom = 100;
				AddChild(_coach);
				_autoButton.Disabled = true;
			}

			if (_hideRival)
			{
				_order.Mine = _mine;
				_order.HideRival = true;
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
		public void ShowResult(BattleOutcome outcome, int? nextMana = null, double? seconds = null)
		{
			(string Icon, string Text)? defeat = outcome.Victory ? null
				: _session.Round > BattleRules.RoundLimit ? ("resolve", T("battle.timeout", BattleRules.RoundLimit))
				: ("retreat", T("battle.all_fell"));
			_banner.Text = "";
			// O painel de Efeitos aberto ficaria por baixo do resultado, atrapalhando a leitura.
			_effectsButton.ButtonPressed = false;
			_bossBar.Clear();
			var next = nextMana is { } mana ? (mana, (Action)Next) : ((int, Action)?)null;
			AddChild(new BattleResultPanel(outcome, seconds ?? Elapsed, defeat, Close, Restart, rune => RuneSellRequested?.Invoke(rune), rune => RuneLockRequested?.Invoke(rune), next));
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
			title.AddThemeFontSizeOverride("font_size", FontSize.Strong);
			title.AddThemeColorOverride("font_color", Palette.Gold);
			header.AddChild(title);
			// O balão do chat fica ao lado da onda e da rodada (as cápsulas se refazem; o lugar dele não).
			var counters = Layout.Row(Space.Medium).Named("Status");
			counters.AddChild(_counters);
			counters.AddChild(ChatBubble.Slot());
			header.AddChild(counters);
			return Pin(header, LayoutPreset.TopLeft);
		}

		/// <summary>O que acontece (a onda, quem usa o quê), escrito no meio do campo.</summary>
		private Control Banner()
		{
			_banner.HorizontalAlignment = HorizontalAlignment.Center;
			_banner.VerticalAlignment = VerticalAlignment.Center;
			_banner.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_banner.MouseFilter = MouseFilterEnum.Ignore;
			_banner.AddThemeFontOverride("font", GameTheme.Serif);
			_banner.AddThemeFontSizeOverride("font_size", FontSize.Large);
			_banner.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
			return OnArenaMiddle(_banner, new Vector2(300, 80));
		}

		/// <summary>
		/// O canto de baixo à esquerda: pausa, automático (aceso quando ligado), velocidade (1×, 2×, 3×, no manual e no
		/// automático), Efeitos e a ajuda, só com o símbolo ou o número e sem texto.
		/// </summary>
		private Control Controls()
		{
			var row = Layout.Grid(5, 10).Named("Controls");
			row.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			_configButton.SetIcon(Art.Icon("gears"));
			_configButton.Pressed += OpenPause;
			row.AddChild(_configButton);

			_autoButton.Pressed += () => SetAuto(!_auto);
			row.AddChild(_autoButton);
			_speedButton.Pressed += CycleSpeed;
			row.AddChild(_speedButton);
			RefreshSpeed();
			// Assistindo a Batalha automática, o automático e a velocidade são os dela; online, a pausa é a desistência e o
			// automático é o do prazo: o botão fica apagado no lugar dele, para a velocidade não mudar de lugar.
			_autoButton.Visible = _watch == null;
			_autoButton.Disabled = _online != null;
			_speedButton.Visible = _watch == null;
			_configButton.Visible = _watch == null;

			_effectsButton.Toggled += on =>
			{
				_effects.Visible = on;
				RefreshEffects();
			};
			row.AddChild(_effectsButton);
			if (_back != null)
				row.AddChild(Help());
			return row;
		}

		/// <summary>O "?" dos Efeitos: no canto de cima à direita (na tela cheia da Batalha automática, ao lado dos botões).</summary>
		private SigilButton Help()
		{
			SigilButton help = null!;
			help = SigilButton.Of("help", () => ExplainEffects(help), 48);
			return help;
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

		/// <summary>Pausa a luta e abre o menu: continuar, recomeçar do começo ou sair.</summary>
		private void OpenPause()
		{
			if (_closed)
				return;
			if (_online != null)
			{
				if (!_finished)
					OpenLeave();
				return;
			}

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

		/// <summary>Centra um controle de tamanho <paramref name="size"/> no meio do campo da arena.</summary>
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
				new { Members = MyUnits, Column = alliesColumn },
				new { Members = TheirUnits, Column = enemiesColumn }
			};

            foreach (var team in teams)
            {
                var units = team.Members.Where(u => u.IsAlive).ToList();
                for (var i = 0; i < units.Count; i++)
                {
                    var unit = units[i];
                    var row = Layout.Row(Space.Medium).Named($"Unit{i + 1}");
                    var frame = new PanelContainer { Name = "Portrait", MouseFilter = MouseFilterEnum.Stop };
                    var ring = unit.Side == _mine ? Palette.Health : Palette.HealthLow;

                    frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, ring, 2, 18, 2));
                    frame.AddChild(Layout.Medal(Art.Creature(unit.Image), Palette.Of(unit.Element), 36));
                    var shown = unit;
                    if (!HidesFromMe(shown))
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
			var view = new UnitView(unit) { Name = name, Mine = _mine };
			view.Pressed += v =>
			{
				if (_pickTarget != null)
				{
					_pickTarget(v.Unit);
					return;
				}

				// Inimigo: marca (ou desmarca) o foco. Aliado (ou qualquer um, assistindo): o resumo, como no toque longo.
				if (v.Unit.Side != _mine && _watch == null)
				{
					if (_auto)
						ToggleFocus(v);
					else
						_quickAttack?.Invoke(v.Unit);
				}
				else if (!HidesFromMe(v.Unit))
					MonsterSummary.Open(v, v.Unit);
			};
			view.LongPressed += v =>
			{
				if (!HidesFromMe(v.Unit))
					MonsterSummary.Open(v, v.Unit);
			};
			_views[unit] = view;
			return view;
		}

		/// <summary>Na Arena do Véu, o monstro do rival não mostra a ficha (atributos e habilidades) durante a luta.</summary>
		private bool HidesFromMe(BattleUnit unit) => (_online != null || _hideRival) && unit.Side != _mine;

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

		/// <summary>As decisões do lado do jogador, na ordem: o servidor refaz a luta com elas para decidir o resultado.</summary>
		public List<LoggedAction> Actions { get; } = new();

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
				if (turn.Actor.Side == Side.Allies)
					Actions.Add(BattleReplay.Log(_session, action));
				await Play(_session.Act(action));
			}

			_finished = true;
			if (!_closed)
				Finished?.Invoke(_session.Victory == true);
		}

		// Arena do Véu ------------------------------------------------------------------------------

		/// <summary>Monta o campo da luta contra outro jogador: o time dele embaixo, o do rival em cima, e o relógio do turno.</summary>
		private void StartOnline()
		{
			_order.Mine = _mine;
			_order.HideRival = true;
			_arena.SetEnemies(TheirUnits.Select((unit, i) => (Control)ViewFor(unit, $"Enemy{i + 1}")).ToList());
			_clock.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
			_clock.GrowHorizontal = GrowDirection.Both;
			_clock.OffsetTop = 8;
			_clock.OffsetBottom = 44;
			_clock.OffsetLeft = -200;
			_clock.OffsetRight = 200;
			_clock.AddThemeFontOverride("font", GameTheme.Serif);
			_clock.AddThemeFontSizeOverride("font_size", FontSize.Strong);
			_clock.AddThemeColorOverride("font_outline_color", Palette.Background);
			_clock.AddThemeConstantOverride("outline_size", 6);
			AddChild(_clock);
			_clockTick.Timeout += ShowClock;
			AddChild(_clockTick);
			_clockTick.Start();
			RunOnline();
		}

		/// <summary>
		/// Segue o servidor: cada turno chega como <c>turn</c> (de quem é e quanto tempo tem) e <c>acted</c> (o que foi
		/// decidido, por quem foi e a impressão digital da luta). A cópia local avança com a decisão e a tela toca o
		/// resultado. Na vez do jogador, os botões das habilidades abrem e a escolha vai ao servidor; se o prazo
		/// estourar antes, o servidor já jogou e os botões fecham.
		/// </summary>
		private async void RunOnline()
		{
			var online = _online!;
			var replica = online.Replica;
			var warned = false;
			RtaMessage? last = null;

			await Play(replica.Start());
			_order.Show(_session.PredictOrder(TurnsShown));
			while (!_closed)
			{
				var (actor, events) = replica.Advance();
				await Play(events);
				if (actor == null || _closed)
					break;

				var turn = await Message(online, "turn");
				if (turn.Type != "turn")
				{
					last = turn;
					break;
				}

				var mine = RtaMatch.SeatOf(actor) == online.Seat;
				SetClock(turn.Seconds, mine);
				var acted = Message(online, "acted");
				if (mine)
				{
					var decided = DecideMine(actor);
					await Task.WhenAny(decided, acted);
					if (decided.IsCompletedSuccessfully && decided.Result is { } action)
					{
						var wire = replica.Match.Describe(action);
						_ = online.Send(new RtaMessage { Type = "act", Turn = turn.Turn, Skill = wire.Skill, Side = wire.TargetSide, Index = wire.TargetIndex });
					}
					else
					{
						_cancelDecision?.Invoke();
						ClearActions();
					}
				}

				var reply = await acted;
				ClearClock();
				if (reply.Type != "acted" || _closed)
				{
					last = reply;
					break;
				}

				await Play(replica.Apply(reply));
				if (!replica.InSync && !warned)
				{
					warned = true;
					Dialog.Info(this, T("veil.desync_title"), T("veil.desync"));
				}
			}

			ClearClock();
			ClearActions();
			if (_closed)
				return;

			last ??= await Message(online, "end");
			_finished = true;
			ShowVeilResult(last);
		}

		/// <summary>Espera a mensagem do tipo pedido; o fim da partida e o erro (a conexão caiu) valem no lugar de qualquer uma.</summary>
		private static async Task<RtaMessage> Message(OnlineBattle online, string type)
		{
			while (true)
			{
				var message = await online.Next();
				if (message.Type == type || message.Type is "end" or "error")
					return message;
			}
		}

		private async Task<UnitAction?> DecideMine(BattleUnit actor)
		{
			try
			{
				return await DecideAlly(actor);
			}
			catch (OperationCanceledException)
			{
				return null;
			}
		}

		private bool _clockMine;

		private void SetClock(int seconds, bool mine)
		{
			_deadline = Time.GetTicksMsec() / 1000.0 + seconds;
			_clockMine = mine;
			ShowClock();
		}

		private void ClearClock()
		{
			_deadline = 0;
			_clock.Text = "";
		}

		private void ShowClock()
		{
			if (_deadline <= 0)
				return;

			var left = Math.Max(0, (int)Math.Ceiling(_deadline - Time.GetTicksMsec() / 1000.0));
			_clock.Text = T(_clockMine ? "veil.your_turn" : "veil.rival_turn", left);
			_clock.AddThemeColorOverride("font_color", !_clockMine ? Palette.TextFaded : left <= 5 ? Palette.Negative : Palette.Gold);
		}

		/// <summary>O jogador desiste: o servidor dá a vitória ao rival e manda o fim, que fecha a luta.</summary>
		private void OpenLeave() => Dialog.Confirm(this, T("veil.leave_title"), T("veil.leave_text"), T("veil.leave"), () => _ = _online!.Send(new RtaMessage { Type = "leave" }), ButtonKind.Danger);

		/// <summary>O resultado da partida; sem o <c>end</c> (a conexão caiu), o aviso de que o servidor termina a luta sozinho.</summary>
		private void ShowVeilResult(RtaMessage end)
		{
			_banner.Text = "";
			_effectsButton.ButtonPressed = false;
			var database = UiSession.Database;
			var before = database?.Arena.GradeOf(end.NewAster - end.Delta).Name ?? "";
			var after = database?.Arena.GradeOf(end.NewAster).Name ?? "";
			AddChild(new VeilResultPanel(end, _online!.Seat, before, after, Close));
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
			_cancelDecision = () => decision.TrySetCanceled();
			void Decide(UnitAction action)
			{
				ClearActions();
				_coach?.Acted(action.Skill);
				decision.TrySetResult(action);
			}

			_decideAutomatically = () => Decide(AutoPilot.ForAlly(_session, ally, _focusBoss, _focus));
			_quickAttack = target =>
			{
				var first = ally.Skill(0);
				if (ally.IsReady(0) && !first.NeedsAlly && _coach?.CanUse(0) != false && _session.ChoosableTargets(ally).Contains(target) && (_coach == null || _coach.CanTarget(target)))
					Decide(new UnitAction(0, target));
			};

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
					Disabled = !ready || _coach?.CanUse(index) == false || skill.NeedsAlly && _session.ChoosableAllies(ally).Count == 0,
					Badge = ready ? "" : T("battle.cooldown_badge", ally.Cooldown(index)),
				};
				button.SetSkill(skill);
				Press.OnButton(button, () =>
				{
					if (skill.NeedsAlly)
						PickTarget(ally, button, target => Decide(new UnitAction(index, target)), _session.ChoosableAllies(ally), "battle.pick_ally");
					else if (skill.NeedsTarget)
						PickTarget(ally, button, target => Decide(new UnitAction(index, target)));
					else
						Decide(new UnitAction(index, null));
				}, () => Dialog.Info(button, skill.Name, Texts.Describe(skill)));
				slot.AddChild(button);
				var name = new Label { Name = "Name", Text = skill.Name, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(92, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
				name.AddThemeFontSizeOverride("font_size", FontSize.Detail);
				name.AddThemeColorOverride("font_color", ready ? Palette.Text : Palette.TextFaded);
				name.AddThemeColorOverride("font_outline_color", Palette.Background);
				name.AddThemeConstantOverride("outline_size", 4);
				slot.AddChild(name);
				_actions.AddChild(slot);
			}

			// A Passiva, só para conferir o texto: um losango no fim da fileira, que não age (toque ou toque longo abrem a descrição).
			if (PassiveOf(ally) is { } passive)
				_actions.AddChild(PassiveSlot(passive, ally.Awakened));

			return await decision.Task;
		}

		/// <summary>A habilidade de Passiva de um aliado (a luta guarda só o efeito dela, sem o nome e o desenho).</summary>
		private SkillDefinition? PassiveOf(BattleUnit unit) =>
			UiSession.Database?.AllSummons.FirstOrDefault(summon => summon.Id == unit.DefinitionId)?.SkillsFor(unit.Awakened).FirstOrDefault(skill => skill.IsPassive);

		/// <summary>O losango da Passiva com o nome embaixo, como as habilidades, mas sem agir: serve para ler o que ela faz.</summary>
		private static Control PassiveSlot(SkillDefinition passive, bool awakened)
		{
			var slot = new VBoxContainer { Name = "Passive" };
			slot.AddThemeConstantOverride("separation", Space.Hair);
			var button = new SigilButton(null, 74, SigilShape.Diamond) { Name = "Button" };
			button.SetSkill(passive);
			void Read() => Dialog.Info(button, passive.Name, Texts.Describe(passive, awakened));
			Press.OnButton(button, Read, Read);
			slot.AddChild(button);
			var name = new Label { Name = "Name", Text = passive.Name, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(92, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
			name.AddThemeFontSizeOverride("font_size", FontSize.Detail);
			name.AddThemeColorOverride("font_color", Palette.Text);
			name.AddThemeColorOverride("font_outline_color", Palette.Background);
			name.AddThemeConstantOverride("outline_size", 4);
			slot.AddChild(name);
			return slot;
		}

		/// <summary>
		/// Os alvos possíveis acendem em azul; o sigilo da habilidade fica aceso até o toque no alvo. Sem
		/// <paramref name="options"/>, são os inimigos que podem ser escolhidos; com ela (os aliados das habilidades de
		/// ChosenAlly), a lista dada e o texto <paramref name="bannerKey"/>.
		/// </summary>
		private void PickTarget(BattleUnit actor, SigilButton skill, Action<BattleUnit> onPicked, IReadOnlyList<BattleUnit>? options = null, string bannerKey = "battle.pick_target")
		{
			foreach (var view in _views.Values)
				view.SetTargetable(false);
			foreach (var child in _actions.GetChildren().Select(c => c.GetNodeOrNull<SigilButton>("Button")).OfType<SigilButton>())
				child.Highlight = child == skill;
			_banner.Text = T(bannerKey);
			_banner.AddThemeColorOverride("font_color", Palette.Arcane);

			// Na luta de treino, o Mestre pode deixar só alguns alvos (os de Fogo, na lição dos elementos).
			var choosable = options ?? _session.ChoosableTargets(actor);
			if (options == null && _coach != null && choosable.Any(_coach.CanTarget))
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
			_quickAttack = null;
			_cancelDecision = null;
			_pickTarget = null;
			foreach (var view in _views.Values)
				view.SetTargetable(false);
			Layout.Clear(_actions);
		}

		private void SetAuto(bool on)
		{
			_auto = on;
			if (!on)
				SetFocus(null);
			RefreshAuto();
			if (on)
				_decideAutomatically?.Invoke();
		}

		/// <summary>1×, 2× e 3×, nessa ordem; o 3× volta ao 1×. Vale do próximo momento da luta em diante e fica guardada no aparelho.</summary>
		private void CycleSpeed()
		{
			_speedIndex = (_speedIndex + 1) % BattlePace.Speeds.Count;
			UiSession.ChooseBattleSpeed(_speedIndex);
			RefreshSpeed();
		}

		private void RefreshSpeed() => _speedButton.Letters = $"{BattlePace.Speeds[_speedIndex].Label}×";

		/// <summary>Sem texto no botão: no manual, o "play" liga o automático; ligado, ele pulsa aceso e o "pause" volta ao manual.</summary>
		private void RefreshAuto()
		{
			_autoButton.SetIcon(Art.Icon(_auto ? "pause" : "play"));
			_autoButton.Highlight = _auto;
		}

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

			// Tudo o que a ação trouxe já passou: os cartões voltam a seguir a Vida das unidades.
			foreach (var view in _views.Values)
				view.Sync();
			RefreshEffects();
		}

		/// <summary>
		/// Atualiza o cartão com a Vida que o evento traz (a do momento); sem ela, com a da unidade.
		/// </summary>
		private static void Refresh(UnitView view, double? health, double? maxHealth = null)
		{
			if (health is { } shown)
				view.ShowHealth(shown, maxHealth);
			else
				view.Refresh();
		}

		/// <summary>Um momento na tela: a corrida, o tranco ou a volta de quem ataca, e os eventos dele.</summary>
		private void ShowBeat(Beat beat, double seconds)
		{
			switch (beat.Kind)
			{
				case BeatKind.Approach when beat.Group != null:
					_striker = beat.Actor != null && _views.TryGetValue(beat.Actor, out var lead) ? lead : _striker;
					foreach (var runner in beat.Group.Where(a => _views.ContainsKey(a.Actor)))
						_arena.Approach(_views[runner.Actor], runner.Targets.Where(_views.ContainsKey).Select(unit => (Control)_views[unit]).ToList(), seconds);
					break;
				case BeatKind.Return when beat.Group != null:
					foreach (var runner in beat.Group.Where(a => _views.ContainsKey(a.Actor)))
						_arena.Return(_views[runner.Actor], seconds);
					break;
				case BeatKind.Volley when beat.Group != null:
					foreach (var attacker in beat.Group)
					{
						if (_views.TryGetValue(attacker.Actor, out var from) && attacker.Targets.FirstOrDefault(_views.ContainsKey) is { } first)
							_arena.Bump(from, _views[first], seconds);
					}

					break;
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
			{
				view.Sync();
				view.Refresh();
			}

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
				// A onda do chefe: a barra grande no alto e o anúncio em vermelho no meio do campo.
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
				case WaveStarted when _online != null:
					return;

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
					// O que a unidade usa aparece nos próprios golpes (números e respingos), sem texto no meio da tela.
					_banner.Text = "";
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
					Refresh(_views[leveled.Target], leveled.Health);
					return;

				case MaxHealthReduced reduced:
					_views[reduced.Target].Float(T("battle.max_hp", reduced.Amount), Palette.Negative);
					Refresh(_views[reduced.Target], reduced.Health, reduced.MaxHealth);
					return;

				case Damaged damaged:
					var hit = _views[damaged.Target];
					hit.Shake(Speed);
					_arena.Splash(hit, damaged.Crit ? Palette.Gold : Palette.Damage, BattlePace.Hit / Speed, damaged.Crit);
					hit.Float(damaged.Crit ? $"-{damaged.Amount}!" : $"-{damaged.Amount}", damaged.Crit ? Palette.Gold : Palette.Damage, damaged.Crit ? 18 : 16);
					Refresh(hit, damaged.Health);
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
					Refresh(_views[healed.Target], healed.Health);
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

				case PassiveStacked stacked:
					// Núcleo Rúnico: o aumento de Defesa a cada rodada fica à vista ("Defesa ×3").
					_views[stacked.Unit].Float(T("battle.passive_stacked", Texts.Name(stacked.Stat), stacked.Stacks), Palette.Positive);
					_views[stacked.Unit].Refresh();
					return;

				case StatusRemoved removed:
					_views[removed.Target].Refresh();
					return;

				case ImpetoChanged changed:
					if (changed.Impeto is { } impeto)
						_views[changed.Target].ShowImpeto(impeto);
					else
						_views[changed.Target].Refresh();
					return;

				case StatusesChanged statuses:
					_views[statuses.Unit].ShowStatuses(statuses.Marks, statuses.Shield);
					return;

				case TurnSkipped skipped:
					_views[skipped.Unit].Float(skipped.Cause is { } cause ? Texts.Name(cause) : T("battle.stunned"), Palette.Negative);
					_banner.Text = T("battle.loses_turn", skipped.Unit.Name);
					return;

				case Died died:
					_views[died.Unit].ShowHealth(0);
					_views[died.Unit].ShowImpeto(0);
					if (died.Unit == _focus)
						SetFocus(null);
					return;

				case Revived revived:
					_views[revived.Unit].Float(T("battle.revives"), Palette.Gold, 14);
					Refresh(_views[revived.Unit], revived.Health);
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
