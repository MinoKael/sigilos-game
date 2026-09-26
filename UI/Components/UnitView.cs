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
	/// Uma unidade em campo, só símbolos: elemento e nível no alto, o desenho, as barras de energia de
	/// Vida (com o escudo por cima) e de Ímpeto, e embaixo os efeitos como Glifos e as recargas. O nome
	/// vem na dica. Lê o estado do <see cref="BattleUnit"/> em <see cref="Refresh"/> e faz as animações
	/// de interpolação do GDD (avançar, recuar, tremer) — nunca quadro a quadro.
	/// </summary>
	public partial class UnitView : PanelContainer
	{
		public static readonly Vector2 CardSize = new(128, 160);

		private readonly StyleBoxFlat _box;
		private readonly ProgressBar _health;
		private readonly ProgressBar _shield;
		private readonly ProgressBar _impeto;
		private readonly Label _healthText;
		private readonly HBoxContainer _statuses = new() { MouseFilter = MouseFilterEnum.Stop, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		private readonly Label _cooldown = new() { ThemeTypeVariation = GameTheme.Faded };
		private bool _active;
		private bool _targetable;

		public UnitView(BattleUnit unit)
		{
			Unit = unit;
			CustomMinimumSize = CardSize;
			MouseFilter = MouseFilterEnum.Stop;
			PivotOffset = CardSize / 2;
			TooltipText = T("battle.unit_tip", unit.Name, unit.Level);

			_box = GameTheme.Box(Palette.Inset, Palette.GoldDark, 2, 10, 6);
			AddThemeStyleboxOverride("panel", _box);

			var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 3);
			AddChild(column);

			var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			top.AddChild(Doodle.Icon(Art.Element(unit.Element), 18, Palette.Of(unit.Element)));
			top.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
			var level = new Label { Text = unit.Level.ToString(), ThemeTypeVariation = GameTheme.Number, MouseFilter = MouseFilterEnum.Ignore };
			level.AddThemeFontSizeOverride("font_size", 14);
			if (unit.Awakened)
				level.AddThemeColorOverride("font_color", Palette.Awakened);
			top.AddChild(level);
			column.AddChild(top);

			var art = new Control { CustomMinimumSize = new Vector2(0, 70), SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
			art.AddChild(Doodle.Masked(Art.Creature(unit.Image), Palette.Of(unit.Element), MaskShape.Rounded, 6));
			column.AddChild(art);

			var bars = new Control { CustomMinimumSize = new Vector2(0, 12), MouseFilter = MouseFilterEnum.Ignore };
			_health = Bar(Palette.Health, 12);
			_shield = Bar(Palette.Shield, 5);
			_health.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_shield.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
			bars.AddChild(_health);
			bars.AddChild(_shield);
			column.AddChild(bars);

			_healthText = new Label { HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = GameTheme.Faded, MouseFilter = MouseFilterEnum.Ignore };
			_healthText.AddThemeFontSizeOverride("font_size", 12);
			column.AddChild(_healthText);

			_impeto = Bar(Palette.Arcane, 5);
			_impeto.TooltipText = T("battle.impetus_tip");
			_impeto.MouseFilter = MouseFilterEnum.Stop;
			column.AddChild(_impeto);

			var bottom = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 18) };
			_statuses.AddThemeConstantOverride("separation", 2);
			bottom.AddChild(_statuses);
			bottom.AddChild(_cooldown);
			column.AddChild(bottom);

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

			_healthText.Text = Unit.IsAlive ? $"{Unit.Health:0}" : Unit.PendingRebirth ? T("battle.reviving") : T("battle.fallen");
			_impeto.Value = Unit.Impeto;

			Layout.Clear(_statuses);
			foreach (var kind in Unit.Statuses.Where(s => s.Kind != StatusKind.Shield).Select(s => s.Kind).Distinct())
			{
				var ink = BattleRules.IsNegative(kind) ? Palette.Negative : Palette.Positive;
				_statuses.AddChild(Doodle.Icon(Art.Effect(kind), 17, ink));
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

		/// <summary>Avança na direção do inimigo e volta.</summary>
		public void Lunge(float direction, float speed)
		{
			var tween = CreateTween();
			tween.TweenProperty(this, "position:x", Position.X + 24 * direction, 0.12 / speed);
			tween.TweenProperty(this, "position:x", Position.X, 0.18 / speed);
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

		public void Float(string text, Color color) => FloatingText.Spawn(this, text, color);

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

		private static ProgressBar Bar(Color fill, int height)
		{
			var bar = Layout.Energy(fill, height);
			bar.MouseFilter = MouseFilterEnum.Ignore;
			bar.MaxValue = 100;
			return bar;
		}
	}
}
