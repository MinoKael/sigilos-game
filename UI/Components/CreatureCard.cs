using System;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Cartão de monstro: moldura pela raridade, estrelas douradas (roxas depois do Despertar), nível
	/// no canto, desenho na cor do elemento e nome. Usado na coleção, nas equipes e no resultado do
	/// ritual. Clicável quando alguém assina <see cref="Pressed"/>.
	/// </summary>
	public partial class CreatureCard : PanelContainer
	{
		private readonly StyleBoxFlat _box;
		private readonly Color _frame;
		private readonly Label _mark = new()
		{
			Text = "✓",
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};

		/// <param name="monster">Nulo quando não é um monstro da conta (Grimório): mostra nível 1, sem Despertar.</param>
		/// <param name="badge">Linha de baixo, no lugar dos Ecos: "Líder", "Nova!", "Baú".</param>
		public CreatureCard(SummonDefinition summon, OwnedSummon? monster, string? badge = null, float width = 150, bool awakenedPreview = false)
		{
			Summon = summon;
			Monster = monster;
			var awakened = monster?.Awakened ?? awakenedPreview;
			var name = summon.NameFor(awakened);

			CustomMinimumSize = new Vector2(width, width * 1.3f);
			MouseFilter = MouseFilterEnum.Stop;
			TooltipText = T("carta.dica", name, Texts.Name(summon.Element), Texts.Name(summon.Role));

			_frame = Palette.Frame(summon.Rarity);
			_box = GameTheme.Box(Palette.Inset, _frame, summon.Rarity >= 3 ? 3 : 2, 6, 6);
			AddThemeStyleboxOverride("panel", _box);

			var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 2);
			AddChild(column);

			var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			top.AddChild(Doodle.Icon(Art.Element(summon.Element), 18, Palette.Of(summon.Element)));
			var stars = new Label { Text = Texts.Stars(summon.Rarity), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center };
			stars.AddThemeColorOverride("font_color", Palette.Stars(awakened));
			stars.AddThemeFontSizeOverride("font_size", 14);
			top.AddChild(stars);
			column.AddChild(top);

			var portrait = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, width * 0.6f), MouseFilter = MouseFilterEnum.Ignore };
			var art = new Doodle(Art.Creature(summon.ImageFor(awakened)), Palette.Of(summon.Element));
			art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			portrait.AddChild(art);
			var level = new Label { Text = T("carta.nivel", monster?.Level ?? 1), Position = new Vector2(2, 0) };
			level.AddThemeFontSizeOverride("font_size", 12);
			level.AddThemeColorOverride("font_outline_color", Palette.Background);
			level.AddThemeConstantOverride("outline_size", 4);
			portrait.AddChild(level);
			column.AddChild(portrait);

			var label = new Label
			{
				Text = name,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(width - 16, 0),
			};
			label.AddThemeFontSizeOverride("font_size", 13);
			if (awakened)
				label.AddThemeColorOverride("font_color", Palette.Awakened);
			column.AddChild(label);

			var footer = badge ?? (monster is { Echoes: > 0 } ? T("carta.ecos", monster.Echoes) : "");
			if (footer.Length > 0)
				column.AddChild(new Label { Text = footer, ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center });

			_mark.AddThemeColorOverride("font_color", Palette.Positive);
			_mark.AddThemeColorOverride("font_outline_color", Palette.Background);
			_mark.AddThemeConstantOverride("outline_size", 6);
			_mark.AddThemeFontSizeOverride("font_size", 40);
			AddChild(_mark);
		}

		public event Action<CreatureCard>? Pressed;

		public SummonDefinition Summon { get; }
		public OwnedSummon? Monster { get; }

		/// <summary>Destaque de seleção: borda clara grossa.</summary>
		public void SetSelected(bool selected)
		{
			_box.BorderColor = selected ? Palette.Text : _frame;
			_box.SetBorderWidthAll(selected ? 5 : Summon.Rarity >= 3 ? 3 : 2);
		}

		/// <summary>Marca para fundir ou liberar em massa: um ✓ por cima e fundo esverdeado.</summary>
		public void SetMarked(bool marked)
		{
			_mark.Visible = marked;
			_box.BgColor = marked ? Palette.Inset.Lerp(Palette.Positive, 0.2f) : Palette.Inset;
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				Pressed?.Invoke(this);
		}
	}
}
