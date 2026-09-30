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
	/// Uma unidade em campo, quase quadrada e só de símbolos: o desenho, com o elemento e o nível nos
	/// cantos de cima e os efeitos e as recargas nos de baixo; embaixo dele a barra de Vida (o escudo por
	/// cima, o número dentro) e a de Ímpeto. O nome vem na dica. Lê o estado do <see cref="BattleUnit"/>
	/// em <see cref="Refresh"/>; o avanço de quem age é da <see cref="BattleArena"/>, o tremor daqui.
	/// </summary>
	public partial class UnitView : PanelContainer
	{
		public static readonly Vector2 CardSize = new(100, 116);

		private readonly StyleBoxFlat _box;
		private readonly ProgressBar _health;
		private readonly ProgressBar _shield;
		private readonly ProgressBar _impeto;
		private readonly Label _healthText;
		private readonly HBoxContainer _statuses = new() { Name = "Statuses", MouseFilter = MouseFilterEnum.Stop };
		private readonly Label _cooldown = new() { Name = "Cooldowns", MouseFilter = MouseFilterEnum.Ignore };
		private bool _active;
		private bool _targetable;

		public UnitView(BattleUnit unit)
		{
			Unit = unit;
			CustomMinimumSize = CardSize;
			MouseFilter = MouseFilterEnum.Stop;
			PivotOffset = CardSize / 2;
			TooltipText = T("battle.unit_tip", unit.Name, unit.Level);

			_box = GameTheme.Box(Palette.Inset, Palette.GoldDark, 2, 10, 5);
			AddThemeStyleboxOverride("panel", _box);

			var column = new VBoxContainer { Name = "Column", MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 3);
			AddChild(column);

			// O desenho, com as quatro marcas nos cantos por cima dele.
			var art = new Control { Name = "Art", SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
			art.AddChild(Doodle.Masked(Art.Creature(unit.Image), Palette.Of(unit.Element), MaskShape.Rounded, 6));
			var element = Doodle.Icon(Art.Element(unit.Element), 16, Palette.Of(unit.Element)).Named("Element");
			element.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
			art.AddChild(element);
			var level = Corner(new Label { Name = "Level", Text = unit.Level.ToString(), ThemeTypeVariation = GameTheme.Number }, LayoutPreset.TopRight, 13);
			if (unit.Awakened)
				level.AddThemeColorOverride("font_color", Palette.Awakened);
			art.AddChild(level);
			_statuses.AddThemeConstantOverride("separation", 1);
			_statuses.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
			_statuses.GrowVertical = GrowDirection.Begin;
			art.AddChild(_statuses);
			art.AddChild(Corner(_cooldown, LayoutPreset.BottomRight, 11));
			column.AddChild(art);

			var bars = new Control { Name = "Bars", CustomMinimumSize = new Vector2(0, 12), MouseFilter = MouseFilterEnum.Ignore };
			_health = Bar(Palette.Health, 12).Named("Health");
			_shield = Bar(Palette.Shield, 4).Named("Shield");
			_health.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_shield.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
			bars.AddChild(_health);
			bars.AddChild(_shield);
			_healthText = Corner(new Label { Name = "HealthText", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, LayoutPreset.FullRect, 10);
			bars.AddChild(_healthText);
			column.AddChild(bars);

			_impeto = Bar(Palette.Arcane, 4).Named("Impetus");
			_impeto.TooltipText = T("battle.impetus_tip");
			_impeto.MouseFilter = MouseFilterEnum.Stop;
			column.AddChild(_impeto);

			MouseEntered += Restyle;
			MouseExited += Restyle;
			Refresh();
		}

		public event Action<UnitView>? Pressed;

		public BattleUnit Unit { get; }

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

			Layout.Clear(_statuses);
			foreach (var kind in Unit.Statuses.Where(s => s.Kind != StatusKind.Shield).Select(s => s.Kind).Distinct())
			{
				var ink = BattleRules.IsNegative(kind) ? Palette.Negative : Palette.Positive;
				_statuses.AddChild(Doodle.Icon(Art.Effect(kind), 14, ink));
			}

			_statuses.TooltipText = string.Join("\n", Unit.Statuses.Select(s => T("battle.effect_tip", Texts.Name(s.Kind), Texts.Turns(s.Turns))));
			_cooldown.Text = string.Join(" ", Enumerable.Range(1, Math.Max(0, Unit.Skills.Count - 1))
				.Where(i => Unit.Cooldown(i) > 0)
				.Select(i => $"⟳{Unit.Cooldown(i)}"));
			Modulate = Unit.IsAlive ? Colors.White : new Color(1, 1, 1, 0.35f);
		}

		/// <summary>Quem está agindo: moldura de ouro com aura.</summary>
		public void SetActive(bool active)
		{
			_active = active;
			Restyle();
		}

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

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				Pressed?.Invoke(this);
		}

		private void Restyle()
		{
			var hover = IsInsideTree() && GetGlobalRect().HasPoint(GetGlobalMousePosition());
			var ring = _targetable ? Palette.Arcane : _active ? Palette.Gold : Unit.Side == Core.Battle.Side.Allies ? Palette.GoldDark : Palette.Negative.Darkened(0.4f);
			_box.BorderColor = hover && _targetable ? Palette.Arcane.Lightened(0.3f) : ring;
			_box.SetBorderWidthAll(_active || _targetable ? 3 : 2);
			_box.BgColor = _targetable ? Palette.Inset.Lerp(Palette.Arcane, hover ? 0.2f : 0.08f) : Palette.Inset;
			_box.ShadowColor = _targetable ? new Color(Palette.Arcane, 0.45f) : _active ? new Color(Palette.Gold, 0.4f) : new Color(0, 0, 0, 0);
			_box.ShadowSize = _active || _targetable ? 8 : 0;
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
