using System;
using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Cartão de invocação: desenho na cor do elemento, nome, estrelas e o Glifo. Usado na coleção
	/// e no resultado do ritual. Clicável quando alguém assina <see cref="Pressed"/>.
	/// </summary>
	public partial class CreatureCard : PanelContainer
	{
		private readonly StyleBoxFlat _box;

		public CreatureCard(SummonDefinition summon, int echoes, string? badge = null, float width = 150)
		{
			Summon = summon;
			CustomMinimumSize = new Vector2(width, width * 1.3f);
			MouseFilter = MouseFilterEnum.Stop;
			TooltipText = $"{summon.Name}\n{Texts.Name(summon.Role)} · {Texts.Name(summon.Glyph)}";

			_box = GameTheme.Box(Palette.Parchment, Palette.Rarity(summon.Rarity), summon.Rarity >= 3 ? 3 : 2, 6, 6);
			AddThemeStyleboxOverride("panel", _box);

			var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 2);
			AddChild(column);

			var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			top.AddChild(Doodle.Icon(Art.Element(summon.Element), 18, Palette.Of(summon.Element)));
			var stars = new Label { Text = Texts.Stars(summon.Rarity), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center };
			stars.AddThemeColorOverride("font_color", Palette.Rarity(summon.Rarity));
			top.AddChild(stars);
			top.AddChild(Doodle.Icon(Art.Glyph(summon.Glyph), 18));
			column.AddChild(top);

			var portrait = new Doodle(Art.Creature(summon.Family.Image), Palette.Of(summon.Element))
			{
				SizeFlagsVertical = SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, width * 0.62f),
			};
			column.AddChild(portrait);

			var name = new Label
			{
				Text = summon.Name,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(width - 16, 0),
			};
			name.AddThemeFontSizeOverride("font_size", 13);
			column.AddChild(name);

			var footer = badge ?? (echoes > 0 ? $"Eco {echoes}" : "");
			if (footer.Length > 0)
			{
				var label = new Label { Text = footer, ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center };
				column.AddChild(label);
			}
		}

		public event Action<CreatureCard>? Pressed;

		public SummonDefinition Summon { get; }

		/// <summary>Destaque de seleção: borda de ouro grossa.</summary>
		public void SetSelected(bool selected)
		{
			_box.BorderColor = selected ? Palette.Gold : Palette.Rarity(Summon.Rarity);
			_box.SetBorderWidthAll(selected ? 5 : Summon.Rarity >= 3 ? 3 : 2);
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				Pressed?.Invoke(this);
		}
	}
}
