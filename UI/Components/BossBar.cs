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
	/// A barra do chefe, no alto da tela, enquanto ele está em campo: "Chefe", o elemento, o nome e o
	/// nível numa linha; embaixo, a Vida grande, com o escudo por cima, o número dentro e um rastro dourado
	/// que desce devagar atrás do dano; à direita, os efeitos dele. Segue o cartão do chefe
	/// (<see cref="UnitView.Refreshed"/>), então desce junto com o golpe na tela, não antes.
	/// </summary>
	public partial class BossBar : PanelContainer
	{
		private const float BarWidth = 560;
		private const int BarHeight = 18;

		/// <summary>Quanto do rastro some por segundo, em fração da Vida máxima.</summary>
		private const double TrailSpeed = 0.35;

		private readonly Doodle _element = new(null, Palette.Gold, boil: false) { Name = "Element", CustomMinimumSize = new Vector2(22, 22) };
		private readonly Label _name = new() { Name = "Name", VerticalAlignment = VerticalAlignment.Center };
		private readonly Label _level = new() { Name = "Level", ThemeTypeVariation = GameTheme.Number, VerticalAlignment = VerticalAlignment.Center };
		private readonly HBoxContainer _statuses = new() { Name = "Statuses", MouseFilter = MouseFilterEnum.Ignore };
		private readonly ProgressBar _trail = Layout.Energy(Palette.Gold, BarHeight).Named("Trail");
		private readonly ProgressBar _health = Layout.Energy(Palette.Negative, BarHeight).Named("Health");
		private readonly ProgressBar _shield = Layout.Energy(Palette.Shield, 6).Named("Shield");
		private readonly Label _healthText = new() { Name = "HealthText", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		private UnitView? _view;

		public BossBar()
		{
			Name = "BossBar";
			Visible = false;
			MouseFilter = MouseFilterEnum.Ignore;
			AddThemeStyleboxOverride("panel", Ornament.Panel(new Color(Palette.Panel, 0.92f), Palette.Negative, 6));

			var column = new VBoxContainer { Name = "Column", MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 2);
			AddChild(column);

			var top = Layout.Row(8).Named("Top");
			top.MouseFilter = MouseFilterEnum.Ignore;
			var tag = new Label { Name = "Tag", Text = T("battle.boss"), VerticalAlignment = VerticalAlignment.Center };
			tag.AddThemeColorOverride("font_color", Palette.Negative.Lightened(0.25f));
			tag.AddThemeFontSizeOverride("font_size", 14);
			top.AddChild(tag);
			top.AddChild(_element);
			_name.AddThemeFontOverride("font", GameTheme.Serif);
			_name.AddThemeFontSizeOverride("font_size", 20);
			_name.AddThemeColorOverride("font_color", Palette.Gold);
			_name.AddThemeColorOverride("font_outline_color", Palette.Background);
			_name.AddThemeConstantOverride("outline_size", 4);
			top.AddChild(_name);
			top.AddChild(_level);
			top.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
			_statuses.AddThemeConstantOverride("separation", 10);
			top.AddChild(_statuses);
			column.AddChild(top);

			// O rastro fica atrás da Vida: a Vida tem fundo transparente, então o que ela perdeu aparece em ouro
			// até o rastro alcançar.
			var bars = new Control { Name = "Bars", CustomMinimumSize = new Vector2(BarWidth, BarHeight), MouseFilter = MouseFilterEnum.Ignore };
			foreach (var bar in new[] { _trail, _health })
			{
				bar.MouseFilter = MouseFilterEnum.Ignore;
				bar.Step = 0;
				bar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				bars.AddChild(bar);
			}

			_trail.AddThemeStyleboxOverride("fill", GameTheme.Energy(new Color(Palette.Gold, 0.8f)));
			_health.AddThemeStyleboxOverride("background", new StyleBoxEmpty());
			_shield.MouseFilter = MouseFilterEnum.Ignore;
			_shield.Step = 0;
			_shield.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
			bars.AddChild(_shield);
			_healthText.MouseFilter = MouseFilterEnum.Ignore;
			_healthText.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_healthText.AddThemeFontSizeOverride("font_size", 13);
			_healthText.AddThemeColorOverride("font_outline_color", Palette.Background);
			_healthText.AddThemeConstantOverride("outline_size", 4);
			bars.AddChild(_healthText);
			column.AddChild(bars);
		}

		/// <summary>O chefe entrou em campo: a barra aparece e passa a seguir o cartão dele.</summary>
		public void Track(UnitView view)
		{
			Release();
			_view = view;
			view.Refreshed += Follow;
			var unit = view.Unit;
			_element.SetArt(Art.Element(unit.Element));
			_element.SetInk(Palette.Of(unit.Element));
			_name.Text = unit.Name;
			_level.Text = T("battle.boss_level", unit.Level);
			_trail.MaxValue = _health.MaxValue = _shield.MaxValue = unit.MaxHealth;
			_trail.Value = unit.Health;
			Follow(view);
			Visible = true;
		}

		/// <summary>Onda sem chefe: a barra some.</summary>
		public void Clear()
		{
			Release();
			Visible = false;
		}

		public override void _Process(double delta)
		{
			// O rastro desce até a Vida; nunca fica abaixo dela (a cura sobe os dois juntos).
			if (_trail.Value > _health.Value)
				_trail.Value = Math.Max(_health.Value, _trail.Value - _trail.MaxValue * TrailSpeed * delta);
			else
				_trail.Value = _health.Value;
		}

		public override void _ExitTree() => Release();

		private void Follow(UnitView view)
		{
			var unit = view.Unit;
			_health.MaxValue = _trail.MaxValue = _shield.MaxValue = unit.MaxHealth;
			_health.Value = unit.Health;
			var shield = unit.Find(StatusKind.Shield)?.Value ?? 0;
			_shield.Value = shield;
			_shield.Visible = shield > 0;
			_healthText.Text = unit.IsAlive
				? T("battle.boss_health", Texts.Short((int)Math.Ceiling(unit.Health)), Texts.Short((int)Math.Ceiling(unit.MaxHealth)))
				: T("battle.fallen");

			Layout.Clear(_statuses);
			foreach (var group in unit.Statuses.Where(s => s.Kind != StatusKind.Shield).GroupBy(s => s.Kind))
				_statuses.AddChild(new StatusChip(group.Key, group.Max(s => s.Turns), 28));
		}

		private void Release()
		{
			if (_view != null && IsInstanceValid(_view))
				_view.Refreshed -= Follow;
			_view = null;
		}
	}
}
