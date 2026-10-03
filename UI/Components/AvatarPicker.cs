using System;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O retrato da conta: o desenho de um monstro que a conta tem, num círculo (com a aura do elemento,
	/// se é o desperto), ou o sigilo padrão enquanto o jogador não escolhe. A janela de escolha mostra só
	/// os que a conta pode usar (<see cref="Account.Avatars"/>): cada variante que ela tem e, de quem tem
	/// uma cópia desperta, também o desperto. Tocar escolhe e fecha.
	/// </summary>
	public static class AvatarPicker
	{
		private const float Side = 84;
		private const float CellWidth = 104;

		/// <summary>O retrato de agora, para encher uma moldura redonda.</summary>
		public static Control Portrait(GameDatabase database, PlayerState player)
		{
			if (player.Avatar is not { } id || !database.HasSummon(id))
				return Doodle.Masked(Art.Icon("avatar"), Palette.Gold, MaskShape.Circle, boil: false);
			var summon = database.Summon(id);
			return Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Circle, boil: false, aura: player.AvatarAwakened ? summon.Element : null);
		}

		/// <summary>A grade dos retratos que a conta pode usar, o de agora aceso; <paramref name="chosen"/> recebe a variante e se é a desperta.</summary>
		public static Dialog Open(Control from, GameDatabase database, PlayerState player, Action<string, bool> chosen)
		{
			var dialog = Dialog.Open(from, T("avatar.title"), 640, null, "AvatarPicker");
			var avatars = Account.Avatars(player)
				.Where(a => database.HasSummon(a.Summon))
				.Select(a => (Summon: database.Summon(a.Summon), a.Awakened))
				.OrderByDescending(a => a.Summon.Rarity)
				.ThenBy(a => a.Summon.FamilyId)
				.ThenBy(a => a.Summon.Element)
				.ThenBy(a => a.Awakened)
				.ToList();
			if (avatars.Count == 0)
			{
				dialog.Body.AddChild(Layout.Text(T("avatar.empty"), GameTheme.Faded, 580).Named("Empty"));
				return dialog;
			}

			dialog.Body.AddChild(Layout.Text(T("avatar.hint"), GameTheme.Faded, 580).Named("Hint"));
			var grid = new GridContainer { Name = "Avatars", Columns = 5 };
			grid.AddThemeConstantOverride("h_separation", 10);
			grid.AddThemeConstantOverride("v_separation", 10);
			foreach (var (summon, awakened) in avatars)
			{
				var current = player.Avatar == summon.Id && player.AvatarAwakened == awakened;
				var cell = Cell(summon, awakened, current);
				cell.Pressed += () =>
				{
					dialog.Close();
					chosen(summon.Id, awakened);
				};
				grid.AddChild(cell);
			}

			dialog.Body.AddChild(grid);
			return dialog;
		}

		/// <summary>Um retrato na grade: o círculo na moldura da raridade (arcana no de agora) e o nome embaixo.</summary>
		private static Button Cell(SummonDefinition summon, bool awakened, bool current)
		{
			var cell = new Button
			{
				Name = awakened ? $"{summon.Id}_awakened" : summon.Id,
				Flat = true,
				FocusMode = Control.FocusModeEnum.None,
				MouseDefaultCursorShape = Control.CursorShape.PointingHand,
				CustomMinimumSize = new Vector2(CellWidth, Side + 46),
			};
			var lit = GameTheme.Box(new Color(Palette.Gold, 0.12f), new Color(Palette.Gold, 0), 0, 10, 0);
			cell.AddThemeStyleboxOverride("hover", lit);
			cell.AddThemeStyleboxOverride("pressed", lit);
			cell.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

			var column = new VBoxContainer { Name = "Column", MouseFilter = Control.MouseFilterEnum.Ignore };
			column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			column.AddThemeConstantOverride("separation", 4);
			var frame = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(Side, Side), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Panel, current ? Palette.Arcane : Palette.Frame(summon.Rarity), current ? 3 : 2, (int)(Side / 2), 4));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Circle, boil: false, aura: awakened ? summon.Element : null));
			column.AddChild(frame);
			var name = new Label
			{
				Name = "Name",
				Text = summon.NameFor(awakened),
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(CellWidth, 0),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			name.AddThemeFontSizeOverride("font_size", 13);
			name.AddThemeColorOverride("font_color", awakened ? Palette.Awakened : Palette.Text);
			column.AddChild(name);
			cell.AddChild(column);
			return cell;
		}
	}
}
