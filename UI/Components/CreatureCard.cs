using System;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Cartão de monstro: estrelas de agora no alto à esquerda (douradas, roxas depois do Despertar), o
	/// elemento no alto à direita, o desenho na cor do elemento e o nível embaixo à direita. A moldura é
	/// pelas estrelas naturais (bronze, prata, ouro). Um símbolo pequeno embaixo à esquerda marca o que
	/// importa ali (na equipe, Líder), o cadeado ao lado dele diz que o monstro está bloqueado, o
	/// coração abaixo das estrelas marca o favorito, e uma faixa escrita embaixo diz o que o jogador
	/// precisa saber na hora ("Novo!", "Líder").
	///
	/// Toque curto é <see cref="Pressed"/> (escolher, marcar); toque longo abre o resumo do monstro
	/// (<see cref="MonsterSummary"/>), em qualquer tela. Escolhido, fica azul arcano; marcado na seleção
	/// de vários (fundir, soltar, mudar de lugar), ganha o ✓ verde. O toque passa para cima, então arrastar rola a lista.
	/// </summary>
	public partial class CreatureCard : PanelContainer
	{
		private readonly StyleBoxFlat _box;
		private readonly Color _frame;
		private readonly int _border;
		private readonly Doodle _check = new(Art.Icon("confirm"), Palette.Spirit, boil: false) { Name = "Check", Visible = false };
		private readonly Press _press = new();
		private bool _selected;
		private bool _marked;

		/// <param name="monster">Nulo quando não é um monstro da conta (Grimório): nível 1, sem Despertar.</param>
		/// <param name="marker">Símbolo de Assets/Icons embaixo à esquerda (leader, team).</param>
		/// <param name="tag">Faixa escrita embaixo do desenho ("Novo!", "Líder").</param>
		public CreatureCard(SummonDefinition summon, OwnedSummon? monster, float width = 104, string? marker = null, string? tag = null, bool awakenedPreview = false)
		{
			Summon = summon;
			Monster = monster;
			var awakened = monster?.Awakened ?? awakenedPreview;

			CustomMinimumSize = new Vector2(width, width * 1.25f);
			MouseFilter = MouseFilterEnum.Pass;
			MouseDefaultCursorShape = CursorShape.PointingHand;
			_press.Tapped += () => Pressed?.Invoke(this);
			_press.Held += () => MonsterSummary.Open(this, Summon, Monster);

			_frame = Palette.Frame(summon.Rarity);
			_border = summon.Rarity >= 3 ? 3 : 2;
			_box = GameTheme.Box(Palette.Inset, _frame, _border, 8, 4);
			_box.ShadowColor = new Color(0, 0, 0, 0.45f);
			_box.ShadowSize = 3;
			_box.ShadowOffset = new Vector2(0, 2);
			AddThemeStyleboxOverride("panel", _box);

			var layer = new Control { Name = "Layer", MouseFilter = MouseFilterEnum.Ignore };
			AddChild(layer);

			// O desenho cabe inteiro entre as estrelas e o nível, recortado no miolo arredondado do cartão.
			var frame = new ArtMask(MaskShape.Rounded, 6) { Name = "Frame" };
			frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			layer.AddChild(frame);
			var art = new Doodle(Art.Creature(summon.Image), Palette.Of(summon.Element), aura: awakened ? summon.Element : null) { Name = "Art" };
			art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			art.OffsetTop = width * 0.18f;
			art.OffsetBottom = -width * 0.14f;
			art.OffsetLeft = width * 0.05f;
			art.OffsetRight = -width * 0.05f;
			frame.AddChild(art);

			var stars = new Label { Name = "Stars", Text = Texts.Stars(monster?.Stars ?? summon.Rarity), MouseFilter = MouseFilterEnum.Ignore };
			stars.AddThemeColorOverride("font_color", Palette.Stars(awakened));
			stars.AddThemeFontSizeOverride("font_size", Math.Clamp((int)(width * 0.12f), 10, 16));
			stars.AddThemeColorOverride("font_outline_color", Palette.Background);
			stars.AddThemeConstantOverride("outline_size", 3);
			stars.Position = new Vector2(2, -2);
			layer.AddChild(stars);

			var element = Doodle.Icon(Art.Element(summon.Element), (int)(width * 0.17f), Palette.Of(summon.Element)).Named("Element");
			element.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
			element.OffsetLeft = -width * 0.17f;
			layer.AddChild(element);

			var level = new Label { Name = "Level", Text = (monster?.Level ?? 1).ToString(), ThemeTypeVariation = GameTheme.Number, MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Right };
			level.AddThemeFontSizeOverride("font_size", Math.Clamp((int)(width * 0.16f), 12, 22));
			level.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
			level.GrowHorizontal = GrowDirection.Begin;
			level.GrowVertical = GrowDirection.Begin;
			layer.AddChild(level);

			if (marker != null)
			{
				var icon = Doodle.Icon(Art.Icon(marker), (int)(width * 0.2f), Palette.Gold).Named("Marker");
				icon.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
				icon.OffsetTop = -width * 0.2f;
				layer.AddChild(icon);
			}

			if (monster is { Locked: true })
				layer.AddChild(LockBadge(width, marker != null));
			if (monster is { Favorite: true })
				layer.AddChild(FavoriteBadge(width));

			if (tag != null)
				layer.AddChild(Tag(tag, width));

			_check.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
			_check.OffsetLeft = _check.OffsetTop = -width * 0.25f;
			_check.OffsetRight = _check.OffsetBottom = width * 0.25f;
			layer.AddChild(_check);

			MouseEntered += Restyle;
			MouseExited += Restyle;
		}

		public event Action<CreatureCard>? Pressed;

		public SummonDefinition Summon { get; }
		public OwnedSummon? Monster { get; }

		/// <summary>Destaque de escolhido: moldura azul arcana com aura.</summary>
		public void SetSelected(bool selected)
		{
			_selected = selected;
			Restyle();
		}

		/// <summary>Marcado na seleção de vários (fundir, soltar, mudar de lugar): ✓ verde e fundo esverdeado.</summary>
		public void SetMarked(bool marked)
		{
			_marked = marked;
			_check.Visible = marked;
			Restyle();
		}

		public override void _GuiInput(InputEvent @event) => _press.Feed(this, @event);

		/// <summary>O cadeado do monstro bloqueado, embaixo à esquerda (ao lado do símbolo, se houver um).</summary>
		private static Control LockBadge(float width, bool besideMarker)
		{
			var size = Math.Max(16, width * 0.17f);
			var badge = new PanelContainer { Name = "Lock", MouseFilter = MouseFilterEnum.Ignore };
			badge.AddThemeStyleboxOverride("panel", GameTheme.Box(new Color(Palette.Inset, 0.92f), Palette.GoldDark, 1, (int)size, 2));
			badge.AddChild(Doodle.Icon(Art.Icon("lock"), (int)(size - 4), Palette.Gold).Named("Icon"));
			badge.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
			badge.GrowVertical = GrowDirection.Begin;
			badge.OffsetLeft = badge.OffsetRight = besideMarker ? width * 0.21f : 0;
			badge.OffsetTop = badge.OffsetBottom = 0;
			return badge;
		}

		/// <summary>O coração do favorito, logo abaixo das estrelas, à esquerda.</summary>
		private static Control FavoriteBadge(float width)
		{
			var size = (int)Math.Max(14, width * 0.16f);
			var heart = Doodle.Icon(Art.Icon("favorite"), size, Palette.Negative.Lightened(0.15f)).Named("Favorite");
			heart.Position = new Vector2(width * 0.04f, width * 0.17f);
			return heart;
		}

		/// <summary>A faixa escrita, em ouro sobre pedra, presa na borda de baixo do desenho.</summary>
		private static Control Tag(string text, float width)
		{
			var plate = new PanelContainer { Name = "Tag", MouseFilter = MouseFilterEnum.Ignore };
			var box = GameTheme.Box(new Color(Palette.Inset, 0.92f), Palette.Gold, 1, 6, 0);
			box.ContentMarginLeft = box.ContentMarginRight = 6;
			plate.AddThemeStyleboxOverride("panel", box);
			var label = new Label { Name = "Text", Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", Math.Clamp((int)(width * 0.13f), 12, 17));
			label.AddThemeColorOverride("font_color", Palette.Gold);
			plate.AddChild(label);
			plate.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
			plate.GrowHorizontal = GrowDirection.Both;
			plate.GrowVertical = GrowDirection.Begin;
			plate.OffsetBottom = -width * 0.2f;
			return plate;
		}

		private void Restyle()
		{
			var hover = IsInsideTree() && Pressed != null && GetGlobalRect().HasPoint(GetGlobalMousePosition());
			_box.BorderColor = _selected ? Palette.Arcane : hover ? _frame.Lightened(0.35f) : _frame;
			_box.SetBorderWidthAll(_selected ? _border + 1 : _border);
			_box.BgColor = _marked ? Palette.Inset.Lerp(Palette.Spirit, 0.18f) : hover ? Palette.Inset.Lightened(0.06f) : Palette.Inset;
			_box.ShadowColor = _selected ? new Color(Palette.Arcane, 0.4f) : hover ? new Color(Palette.Gold, 0.25f) : new Color(0, 0, 0, 0.45f);
			_box.ShadowSize = _selected || hover ? 7 : 3;
			_box.ShadowOffset = _selected || hover ? Vector2.Zero : new Vector2(0, 2);
		}
	}
}
