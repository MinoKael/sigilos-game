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
	/// Uma unidade em campo: desenho, nível, Vida, escudo, barra de Ímpeto e
	/// efeitos. Lê o estado do <see cref="BattleUnit"/> em <see cref="Refresh"/> e faz as animações de
	/// interpolação do GDD (avançar, recuar, tremer) — nunca quadro a quadro.
	/// </summary>
	public partial class UnitView : PanelContainer
	{
		public static readonly Vector2 CardSize = new(132, 172);

		private readonly StyleBoxFlat _box;
		private readonly ProgressBar _health;
		private readonly ProgressBar _shield;
		private readonly ProgressBar _impeto;
		private readonly Label _healthText;
		private readonly Label _statuses;
		private readonly Label _cooldown;

		public UnitView(BattleUnit unit)
		{
			Unit = unit;
			CustomMinimumSize = CardSize;
			MouseFilter = MouseFilterEnum.Stop;
			PivotOffset = CardSize / 2;
			TooltipText = T("battle.unit_tip", unit.Name, unit.Level);

			_box = GameTheme.Box(Palette.Inset, Palette.GoldDark, 2, 6, 6);
			AddThemeStyleboxOverride("panel", _box);

			var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 3);
			AddChild(column);

			var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			top.AddChild(Doodle.Icon(Art.Element(unit.Element), 16, Palette.Of(unit.Element)));
			var name = new Label { Text = unit.Name, ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			name.AddThemeFontSizeOverride("font_size", 12);
			if (unit.Awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			top.AddChild(name);
			column.AddChild(top);

			var portrait = new Control { CustomMinimumSize = new Vector2(0, 76), SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
			var art = new Doodle(Art.Creature(unit.Image), Palette.Of(unit.Element));
			art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			portrait.AddChild(art);
			var level = new Label { Text = T("card.level", unit.Level) };
			level.AddThemeFontSizeOverride("font_size", 12);
			level.AddThemeColorOverride("font_outline_color", Palette.Background);
			level.AddThemeConstantOverride("outline_size", 4);
			portrait.AddChild(level);
			column.AddChild(portrait);

			var bars = new Control { CustomMinimumSize = new Vector2(0, 12), MouseFilter = MouseFilterEnum.Ignore };
			_health = Bar(Palette.Health, 12);
			_shield = Bar(Palette.Shield, 5);
			_health.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_shield.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
			bars.AddChild(_health);
			bars.AddChild(_shield);
			column.AddChild(bars);

			_healthText = new Label { HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = GameTheme.Faded };
			column.AddChild(_healthText);

			_impeto = Bar(Palette.Gold, 5);
			_impeto.TooltipText = T("battle.impetus_tip");
			column.AddChild(_impeto);

			var bottom = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			_statuses = new Label { ThemeTypeVariation = GameTheme.Faded, ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Stop };
			_cooldown = new Label { ThemeTypeVariation = GameTheme.Faded };
			bottom.AddChild(_statuses);
			bottom.AddChild(_cooldown);
			column.AddChild(bottom);

			Refresh();
		}

		public event Action<UnitView>? Pressed;

		public BattleUnit Unit { get; }

		public void Refresh()
		{
			_health.MaxValue = Unit.MaxHealth;
			_health.Value = Unit.Health;
			_health.AddThemeStyleboxOverride("fill", GameTheme.Box(Unit.HealthFraction < 0.3 ? Palette.HealthLow : Palette.Health, Palette.Background, 0, 3, 0));

			var shield = Unit.Find(StatusKind.Shield)?.Value ?? 0;
			_shield.MaxValue = Unit.MaxHealth;
			_shield.Value = shield;
			_shield.Visible = shield > 0;

			_healthText.Text = Unit.IsAlive ? $"{Unit.Health:0}/{Unit.MaxHealth:0}" : Unit.PendingRebirth ? T("battle.reviving") : T("battle.fallen");
			_impeto.Value = Unit.Impeto;
			_statuses.Text = string.Join(" ", Unit.Statuses
				.Where(s => s.Kind != StatusKind.Shield)
				.Select(s => Texts.Short(s.Kind))
				.Distinct());
			_statuses.TooltipText = string.Join("\n", Unit.Statuses.Select(s => T("battle.effect_tip", Texts.Name(s.Kind), s.Turns, Texts.Plain(Texts.Explain(s.Kind)))));
			_cooldown.Text = string.Join(" ", Enumerable.Range(1, Math.Max(0, Unit.Skills.Count - 1))
				.Where(i => Unit.Cooldown(i) > 0)
				.Select(i => $"⟳{Unit.Cooldown(i)}"));
			Modulate = Unit.IsAlive ? Colors.White : new Color(1, 1, 1, 0.35f);
		}

		/// <summary>Borda de destaque: quem está agindo.</summary>
		public void SetActive(bool active)
		{
			_box.BorderColor = active ? Palette.Gold : Palette.GoldDark;
			_box.SetBorderWidthAll(active ? 4 : 2);
		}

		/// <summary>Marca a unidade como alvo possível de um clique.</summary>
		public void SetTargetable(bool targetable)
		{
			_box.BgColor = targetable ? Palette.PanelLight.Lerp(Palette.Gold, 0.25f) : Palette.Inset;
			MouseDefaultCursorShape = targetable ? CursorShape.PointingHand : CursorShape.Arrow;
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

		private static ProgressBar Bar(Color fill, int height)
		{
			var bar = new ProgressBar
			{
				ShowPercentage = false,
				CustomMinimumSize = new Vector2(0, height),
				MouseFilter = MouseFilterEnum.Ignore,
				MaxValue = 100,
			};
			bar.AddThemeStyleboxOverride("fill", GameTheme.Box(fill, fill, 0, 3, 0));
			return bar;
		}
	}
}
