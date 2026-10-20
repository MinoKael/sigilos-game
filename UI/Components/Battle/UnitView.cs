using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma unidade em campo, quase quadrada: o desenho, com o elemento e o nível nos cantos de cima e as
	/// recargas no de baixo; embaixo dele a barra de Vida (o escudo por cima, o número dentro) e a de
	/// Ímpeto. Os efeitos ficam em cima do cartão, um quadradinho por cópia, alinhados à esquerda, em até três fileiras de 4
	/// (<see cref="StatusChip"/>: vermelho o negativo, verde o positivo, com os turnos no canto; a primeira fileira junto
	/// ao cartão e as outras acima dela). Toque curto é <see cref="Pressed"/> (escolher o alvo); toque longo é
	/// <see cref="LongPressed"/> (o resumo da unidade, sem parar a luta). Lê o estado do
	/// <see cref="BattleUnit"/> em <see cref="Refresh"/>; o avanço de quem age é da
	/// <see cref="BattleArena"/>, o tremor daqui.
	///
	/// O chefe tem o cartão <see cref="BossScale"/> vezes maior (desenho, marcas e barras juntos) e a
	/// moldura vermelha acesa; a barra grande no alto da tela (<see cref="BossBar"/>) acompanha
	/// <see cref="Refreshed"/> e é ela que mostra os efeitos dele: o cartão do chefe não tem a fileira.
	/// </summary>
	public partial class UnitView : PanelContainer
	{
		public static readonly Vector2 CardSize = new(100, 116);

		/// <summary>Quanto o cartão do chefe é maior.</summary>
		public const float BossScale = 1.5f;

		private readonly StyleBoxFlat _box;
		private readonly ProgressBar _health;
		private readonly ProgressBar _shield;
		private readonly ProgressBar _impeto;
		private readonly Label _healthText;
		/// <summary>Os efeitos em fileiras de <see cref="PerRow"/>, alinhadas à esquerda do cartão: a primeira junto a ele, as outras acima.</summary>
		private readonly VBoxContainer _statuses = new() { Name = "Statuses", MouseFilter = MouseFilterEnum.Ignore, GrowHorizontal = GrowDirection.End, GrowVertical = GrowDirection.Begin };
		private readonly HBoxContainer[] _rows = Enumerable.Range(1, Rows)
			.Select(number => new HBoxContainer { Name = $"Row{number}", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Begin })
			.ToArray();
		private readonly Label _cooldown = new() { Name = "Cooldowns", MouseFilter = MouseFilterEnum.Ignore };
		private readonly Press _press = new() { Bounce = false };
		private readonly float _scale;
		private readonly Doodle _focusMark;
		private bool _active;
		private bool _targetable;

		public UnitView(BattleUnit unit)
		{
			Unit = unit;
			_scale = unit.IsBoss ? BossScale : 1;
			CustomMinimumSize = CardSize * _scale;
			MouseFilter = MouseFilterEnum.Stop;
			PivotOffset = CustomMinimumSize / 2;
			_press.Tapped += () => Pressed?.Invoke(this);
			_press.Held += () => LongPressed?.Invoke(this);

			_box = GameTheme.Box(Palette.Inset, Palette.GoldDark, 2, Radius.Button, 5);
			AddThemeStyleboxOverride("panel", _box);

			var column = new VBoxContainer { Name = "Column", MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 3); // Fora da escala: o cartão da luta é pequeno, cada pixel de altura conta.
			AddChild(column);

			// O desenho, com as quatro marcas nos cantos por cima dele.
			var art = new Control { Name = "Art", SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
			art.AddChild(Doodle.Masked(Art.Creature(unit.Image), Palette.Of(unit.Element), MaskShape.Rounded, 6, aura: unit.Awakened ? unit.Element : null));
			var element = Doodle.Icon(Art.Element(unit.Element), (int)(16 * _scale), Palette.Of(unit.Element)).Named("Element");
			element.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
			art.AddChild(element);
			var level = Corner(new Label { Name = "Level", Text = unit.Level.ToString(), ThemeTypeVariation = GameTheme.Number }, LayoutPreset.TopRight, (int)(13 * _scale));
			if (unit.Awakened)
				level.AddThemeColorOverride("font_color", Palette.Awakened);
			art.AddChild(level);
			// Os efeitos em cima do cartão, por fora da moldura, alinhados à esquerda dele e crescendo para cima: até 4 por fileira,
			// 10 no total (os do chefe ficam na barra dele). O selo dos turnos sai do canto de cada quadradinho, então
			// a fileira de cima fica a um selo de distância.
			foreach (var row in _rows)
				row.AddThemeConstantOverride("separation", RowGap);
			_statuses.AddThemeConstantOverride("separation", BadgeRoom);
			foreach (var row in _rows.Reverse())
				_statuses.AddChild(row);
			_statuses.SetAnchorsPreset(LayoutPreset.TopLeft);
			_statuses.OffsetTop = _statuses.OffsetBottom = -9;
			_statuses.OffsetLeft = _statuses.OffsetRight = -12;

			_statuses.Resized += () => _statuses.PivotOffset = new Vector2(0, _statuses.Size.Y);
			if (!unit.IsBoss)
				art.AddChild(_statuses);
			art.AddChild(Corner(_cooldown, LayoutPreset.BottomRight, (int)(11 * _scale)));

			// A mira do foco, no meio do desenho, por cima: só no inimigo que o jogador marcou.
			var mark = (int)(46 * _scale);
			_focusMark = Doodle.Icon(Art.Icon("target"), mark, Palette.Gold).Named("Focus");
			_focusMark.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
			_focusMark.OffsetLeft = _focusMark.OffsetTop = -mark / 2f;
			_focusMark.OffsetRight = _focusMark.OffsetBottom = mark / 2f;
			_focusMark.Visible = false;
			art.AddChild(_focusMark);
			column.AddChild(art);

			var bars = new Control { Name = "Bars", CustomMinimumSize = new Vector2(0, 12 * _scale), MouseFilter = MouseFilterEnum.Ignore };
			_health = Bar(Palette.Health, (int)(12 * _scale)).Named("Health");
			_shield = Bar(Palette.Shield, (int)(4 * _scale)).Named("Shield");
			_health.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_shield.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
			bars.AddChild(_health);
			bars.AddChild(_shield);
			_healthText = Corner(new Label { Name = "HealthText", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, LayoutPreset.FullRect, (int)(10 * _scale));
			bars.AddChild(_healthText);
			column.AddChild(bars);

			_impeto = Bar(Palette.Arcane, (int)(4 * _scale)).Named("Impetus");
			column.AddChild(_impeto);

			MouseEntered += Restyle;
			MouseExited += Restyle;
			Refresh();
			Restyle();
		}

		/// <summary>
		/// O cartão na profundidade da luta: a escala dele (menor ao fundo) e, em sentido contrário, a dos efeitos
		/// em cima dele, que seguem do tamanho de sempre para poder ler; presos pelo meio da borda de baixo.
		/// </summary>
		public void SetDepth(float scale)
		{
			_statuses.Scale = Vector2.One / scale;
			_statuses.PivotOffset = new Vector2(0, _statuses.Size.Y);
		}

		public event Action<UnitView>? Pressed;

		/// <summary>Toque longo (ou clique direito): o resumo da unidade.</summary>
		public event Action<UnitView>? LongPressed;

		/// <summary>O cartão releu a unidade (Vida, escudo, efeitos): a barra do chefe acompanha.</summary>
		public event Action<UnitView>? Refreshed;

		public BattleUnit Unit { get; }

		/// <summary>O lado de quem olha, para a cor do anel: o time dele em dourado, o outro em vermelho.</summary>
		public Core.Battle.Side Mine { get; set; } = Core.Battle.Side.Allies;

		/// <summary>
		/// Quanto a fileira dos efeitos (com o selo dos turnos) sobe acima da borda do cartão; zero no chefe,
		/// que não tem a fileira.
		/// </summary>
		public float Headroom => Unit.IsBoss ? 0 : Rows * ChipSide + (Rows - 1) * BadgeRoom + 9 + 8;

		/// <summary>Quadradinhos por fileira (três fileiras: o limite de 10 efeitos).</summary>
		public const int PerRow = 4;

		private const int Rows = 3;

		private const int ChipSide = 18;
		private const int RowGap = 4;

		/// <summary>O espaço entre as fileiras: o selo dos turnos da de baixo sobe por ele.</summary>
		private const int BadgeRoom = 8;

		// A Vida que a tela mostra agora. O simulador anda na frente da animação (a ação inteira já foi
		// calculada quando o primeiro golpe aparece): quem tem a Vida do momento é o evento (ShowHealth).
		private double? _shownHealth;
		private double? _shownMaxHealth;
		private double? _shownImpeto;
		private double? _shownShield;
		private IReadOnlyList<StatusMark>? _shownMarks;

		/// <summary>A Vida que o cartão mostra: a do último evento, ou a da unidade se nenhum a trouxe.</summary>
		public double ShownHealth => _shownHealth ?? Unit.Health;

		/// <summary>A Vida máxima que o cartão mostra.</summary>
		public double ShownMaxHealth => _shownMaxHealth ?? Unit.MaxHealth;

		/// <summary>O Ímpeto que o cartão mostra.</summary>
		public double ShownImpeto => _shownImpeto ?? Unit.Impeto;

		/// <summary>O que resta do escudo que o cartão mostra.</summary>
		public double ShownShield => _shownShield ?? Unit.ShieldValue;

		/// <summary>Os efeitos que o cartão mostra: os do último evento deles, ou os da unidade.</summary>
		public IReadOnlyList<StatusMark> ShownStatuses => _shownMarks ?? Unit.StatusMarks();

		/// <summary>Mostra os efeitos (e o escudo) de um evento.</summary>
		public void ShowStatuses(IReadOnlyList<StatusMark> marks, double shield)
		{
			_shownMarks = marks;
			_shownShield = shield;
			Refresh();
		}

		/// <summary>Mostra o Ímpeto de um evento.</summary>
		public void ShowImpeto(double impeto)
		{
			_shownImpeto = impeto;
			Refresh();
		}

		/// <summary>Mostra a Vida (e a máxima, se mudou) de um evento, sem pular para o fim da ação.</summary>
		public void ShowHealth(double health, double? maxHealth = null)
		{
			_shownHealth = health;
			if (maxHealth != null)
				_shownMaxHealth = maxHealth;
			Refresh();
		}

		/// <summary>Volta a seguir a unidade: o que faltava mostrar já foi mostrado.</summary>
		public void Sync()
		{
			if (_shownHealth == null && _shownMaxHealth == null && _shownImpeto == null && _shownShield == null && _shownMarks == null)
				return;
			_shownHealth = _shownMaxHealth = _shownImpeto = _shownShield = null;
			_shownMarks = null;
			Refresh();
		}

		public void Refresh()
		{
			var health = ShownHealth;
			var maxHealth = ShownMaxHealth;
			var alive = health > 0;
			_health.MaxValue = maxHealth;
			_health.Value = health;
			_health.AddThemeStyleboxOverride("fill", GameTheme.Energy(maxHealth > 0 && health / maxHealth < 0.3 ? Palette.HealthLow : Palette.Health));

			var shield = ShownShield;
			_shield.MaxValue = maxHealth;
			_shield.Value = shield;
			_shield.Visible = shield > 0;

			_healthText.Text = alive ? $"{health:0}" : Unit.Reviving ? T("battle.reviving") : T("battle.fallen");
			_impeto.Value = ShownImpeto;

			// Um quadradinho por efeito, uma cópia cada (o Veneno e a Aflição acumulam): os 4 primeiros na fileira junto
			// ao cartão e os outros na de cima, para os primeiros nunca mudarem de lugar.
			foreach (var row in _rows)
				Layout.Clear(row);
			if (!Unit.IsBoss)
			{
				var copies = new Dictionary<StatusKind, int>();
				var marks = ShownStatuses;
				for (var i = 0; i < marks.Count; i++)
				{
					var mark = marks[i];
					var number = copies[mark.Kind] = copies.GetValueOrDefault(mark.Kind) + 1;
					var chip = new StatusChip(mark.Kind, mark.Turns, ChipSide, mark.Unremovable) { Name = $"{mark.Kind}{number}" };
					_rows[Math.Min(i / PerRow, Rows - 1)].AddChild(chip);
				}

				for (var r = 1; r < Rows; r++)
					_rows[r].Visible = marks.Count > r * PerRow;
			}

			_cooldown.Text = string.Join(" ", Enumerable.Range(1, Math.Max(0, Unit.Skills.Count - 1))
				.Where(i => Unit.Cooldown(i) > 0)
				.Select(i => $"⟳{Unit.Cooldown(i)}"));
			Modulate = alive ? Colors.White : new Color(1, 1, 1, 0.35f);
			Refreshed?.Invoke(this);
		}

		/// <summary>Quem está agindo: moldura de ouro com aura.</summary>
		public void SetActive(bool active)
		{
			_active = active;
			Restyle();
		}

		/// <summary>O inimigo marcado como foco do automático: a mira dourada por cima do desenho.</summary>
		public void SetFocused(bool focused) => _focusMark.Visible = focused;

		/// <summary>Marca a unidade como alvo possível de um clique: aura arcana e a mãozinha.</summary>
		public void SetTargetable(bool targetable)
		{
			_targetable = targetable;
			MouseDefaultCursorShape = targetable ? CursorShape.PointingHand : CursorShape.Arrow;
			Restyle();
		}

		public void Shake(float speed)
		{
			var tween = CreateTween();
			for (var i = 0; i < 3; i++)
			{
				tween.TweenProperty(this, "rotation_degrees", 4f, 0.04 / speed);
				tween.TweenProperty(this, "rotation_degrees", -4f, 0.04 / speed);
			}

			tween.TweenProperty(this, "rotation_degrees", 0f, 0.04 / speed);
		}

		/// <summary>Um texto que sobe do cartão: o dano maior, os efeitos no tamanho padrão.</summary>
		public void Float(string text, Color color, int size = FloatingText.SmallSize) => FloatingText.Spawn(this, text, color, size);

		public override void _GuiInput(InputEvent @event) => _press.Feed(this, @event);

		private void Restyle()
		{
			var hover = IsInsideTree() && GetGlobalRect().HasPoint(GetGlobalMousePosition());
			var boss = Unit.IsBoss;
			var ring = _targetable ? Palette.Arcane : _active ? Palette.Gold : boss ? Palette.Negative : Unit.Side == Mine ? Palette.GoldDark : Palette.Negative.Darkened(0.4f);
			_box.BorderColor = hover && _targetable ? Palette.Arcane.Lightened(0.3f) : ring;
			_box.SetBorderWidthAll(_active || _targetable || boss ? 3 : 2);
			_box.BgColor = _targetable ? Palette.Inset.Lerp(Palette.Arcane, hover ? 0.2f : 0.08f) : Palette.Inset;
			// O chefe brilha em vermelho o tempo todo: é a luta grande.
			_box.ShadowColor = _targetable ? new Color(Palette.Arcane, 0.45f) : _active ? new Color(Palette.Gold, 0.4f) : boss ? new Color(Palette.Negative, 0.45f) : new Color(0, 0, 0, 0);
			_box.ShadowSize = _active || _targetable ? 8 : boss ? 12 : 0;
		}

		/// <summary>Um rótulo pequeno, contornado para ler sobre o desenho, preso num canto (ou no retângulo todo).</summary>
		private static Label Corner(Label label, LayoutPreset preset, int size)
		{
			label.MouseFilter = MouseFilterEnum.Ignore;
			label.AddThemeFontSizeOverride("font_size", size);
			label.AddThemeColorOverride("font_outline_color", Palette.Background);
			label.AddThemeConstantOverride("outline_size", 4);
			label.SetAnchorsAndOffsetsPreset(preset);
			if (preset is LayoutPreset.TopRight or LayoutPreset.BottomRight)
				label.GrowHorizontal = GrowDirection.Begin;
			if (preset is LayoutPreset.BottomRight)
				label.GrowVertical = GrowDirection.Begin;
			return label;
		}

		private static ProgressBar Bar(Color fill, int height)
		{
			var bar = Layout.Energy(fill, height);
			bar.MouseFilter = MouseFilterEnum.Ignore;
			bar.MaxValue = 100;
			return bar;
		}
	}
}
