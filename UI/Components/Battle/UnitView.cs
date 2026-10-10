using System;
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
	/// Ímpeto. Os efeitos ficam numa fileira de quadradinhos em cima do cartão (<see cref="StatusChip"/>:
	/// vermelho o negativo, verde o positivo, com os turnos no canto). Toque curto é <see cref="Pressed"/> (escolher o alvo); toque longo é
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
		private readonly HBoxContainer _statuses = new() { Name = "Statuses", MouseFilter = MouseFilterEnum.Ignore };
		private readonly Label _cooldown = new() { Name = "Cooldowns", MouseFilter = MouseFilterEnum.Ignore };
		private readonly Press _press = new();
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
			// Os efeitos em cima do cartão, por fora da moldura, crescendo para a direita (os do chefe ficam na barra dele).
			// O selo dos turnos sai um pouco do canto de cada quadradinho: o espaço entre eles não deixa cobrir o vizinho.
			_statuses.AddThemeConstantOverride("separation", 9);
			_statuses.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
			_statuses.OffsetTop = _statuses.OffsetBottom = -(StatusChip.Side + 10);
			_statuses.OffsetLeft = _statuses.OffsetRight = -4;
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

		public event Action<UnitView>? Pressed;

		/// <summary>Toque longo (ou clique direito): o resumo da unidade.</summary>
		public event Action<UnitView>? LongPressed;

		/// <summary>O cartão releu a unidade (Vida, escudo, efeitos): a barra do chefe acompanha.</summary>
		public event Action<UnitView>? Refreshed;

		public BattleUnit Unit { get; }

		/// <summary>
		/// Quanto a fileira dos efeitos (com o selo dos turnos) sobe acima da borda do cartão; zero no chefe,
		/// que não tem a fileira.
		/// </summary>
		public float Headroom => Unit.IsBoss ? 0 : StatusChip.Side + 14;

		public void Refresh()
		{
			_health.MaxValue = Unit.MaxHealth;
			_health.Value = Unit.Health;
			_health.AddThemeStyleboxOverride("fill", GameTheme.Energy(Unit.HealthFraction < 0.3 ? Palette.HealthLow : Palette.Health));

			var shield = Unit.Find(StatusKind.Shield)?.Value ?? 0;
			_shield.MaxValue = Unit.MaxHealth;
			_shield.Value = shield;
			_shield.Visible = shield > 0;

			_healthText.Text = Unit.IsAlive ? $"{Unit.Health:0}" : Unit.Reviving ? T("battle.reviving") : T("battle.fallen");
			_impeto.Value = Unit.Impeto;

			// Um quadradinho por efeito; vários do mesmo (o Veneno acumula) mostram o prazo mais longo.
			Layout.Clear(_statuses);
			if (!Unit.IsBoss)
			{
				foreach (var group in Unit.Statuses.Where(s => s.Kind != StatusKind.Shield).GroupBy(s => s.Kind))
					_statuses.AddChild(new StatusChip(group.Key, group.Max(s => s.Turns)));
			}

			_cooldown.Text = string.Join(" ", Enumerable.Range(1, Math.Max(0, Unit.Skills.Count - 1))
				.Where(i => Unit.Cooldown(i) > 0)
				.Select(i => $"⟳{Unit.Cooldown(i)}"));
			Modulate = Unit.IsAlive ? Colors.White : new Color(1, 1, 1, 0.35f);
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
			var ring = _targetable ? Palette.Arcane : _active ? Palette.Gold : boss ? Palette.Negative : Unit.Side == Core.Battle.Side.Allies ? Palette.GoldDark : Palette.Negative.Darkened(0.4f);
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
