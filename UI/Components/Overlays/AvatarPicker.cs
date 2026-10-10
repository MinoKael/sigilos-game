using System;
using System.Collections.Generic;
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
	/// se é o desperto), ou o ícone de um retrato especial (<see cref="SpecialAvatars"/>: o padrão, enquanto
	/// o jogador não escolhe, e os de recompensa). A janela de escolha mostra só os que a conta pode usar
	/// (<see cref="Account.Avatars"/>), em duas abas pelo tipo de cada um (<see cref="AvatarKind"/>): Monstros
	/// (cada variante que ela tem e, de quem tem uma cópia desperta, também o desperto; os do correio
	/// também) e Especiais. Abre na aba do retrato de agora. Tocar escolhe e fecha.
	/// </summary>
	public static class AvatarPicker
	{
		private const float Side = 84;
		private const float CellWidth = 104;
		private const int Columns = 5;

		private static readonly AvatarKind[] Kinds = { AvatarKind.Summon, AvatarKind.Special };

		/// <summary>O retrato de agora, para encher uma moldura redonda.</summary>
		public static Control Portrait(GameDatabase database, PlayerState player)
		{
			var current = Account.Current(player);
			if (current.Kind == AvatarKind.Summon && database.HasSummon(current.Id))
			{
				var summon = database.Summon(current.Id);
				return Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Circle, boil: false, aura: current.Awakened ? summon.Element : null);
			}

			return Doodle.Masked(Art.SpecialAvatar(current.Id), Palette.Gold, MaskShape.Circle, boil: false);
		}

		/// <summary>As abas dos retratos que a conta pode usar, o de agora aceso; <paramref name="chosen"/> recebe o id e se é a forma desperta.</summary>
		public static Dialog Open(Control from, GameDatabase database, PlayerState player, Action<string, bool> chosen)
		{
			var dialog = Dialog.Open(from, T("avatar.title"), 640, null, "AvatarPicker");
			var current = Account.Current(player);
			var avatars = Account.Avatars(player)
				.Where(a => a.Kind == AvatarKind.Special || database.HasSummon(a.Id))
				.ToList();

			var tabs = new TextTabs(height: 46, compact: true) { Name = "Kinds", Alignment = BoxContainer.AlignmentMode.Center };
			foreach (var kind in Kinds)
				tabs.Add(T($"avatar.kind.{kind}"), avatars.Count(a => a.Kind == kind).ToString()).Name = kind.ToString();
			dialog.Body.AddChild(tabs);
			var page = new VBoxContainer { Name = "Page" };
			page.AddThemeConstantOverride("separation", Space.Regular);
			dialog.Body.AddChild(page);

			void Show(int index)
			{
				Layout.Clear(page);
				var kind = Kinds[index];
				var shown = avatars.Where(a => a.Kind == kind).ToList();
				if (shown.Count == 0)
				{
					page.AddChild(Layout.Text(T("avatar.empty"), GameTheme.Faded, 580).Named("Empty"));
					return;
				}

				page.AddChild(Layout.Text(T($"avatar.hint.{kind}"), GameTheme.Faded, 580).Named("Hint"));
				var grid = new GridContainer { Name = "Avatars", Columns = Columns };
				grid.AddThemeConstantOverride("h_separation", Space.Regular);
				grid.AddThemeConstantOverride("v_separation", Space.Regular);
				foreach (var avatar in Ordered(database, kind, shown))
				{
					var cell = Cell(database, avatar, avatar == current);
					cell.Pressed += () =>
					{
						dialog.Close();
						chosen(avatar.Id, avatar.Awakened);
					};
					grid.AddChild(cell);
				}

				page.AddChild(grid);
			}

			tabs.Changed += Show;
			var open = Array.IndexOf(Kinds, current.Kind);
			tabs.Select(open);
			Show(open);
			return dialog;
		}

		/// <summary>Os de monstro pela raridade, família, elemento e o desperto depois; os especiais na ordem do catálogo.</summary>
		private static IEnumerable<AccountAvatar> Ordered(GameDatabase database, AvatarKind kind, List<AccountAvatar> avatars) => kind == AvatarKind.Special
			? avatars.OrderBy(a => SpecialAvatars.All.ToList().IndexOf(a.Id))
			: avatars
				.OrderByDescending(a => database.Summon(a.Id).Rarity)
				.ThenBy(a => database.Summon(a.Id).FamilyId)
				.ThenBy(a => database.Summon(a.Id).Element)
				.ThenBy(a => a.Awakened);

		/// <summary>Um retrato na grade: o círculo na moldura da raridade (de ouro no especial, arcana no de agora) e o nome embaixo.</summary>
		private static SurfaceButton Cell(GameDatabase database, AccountAvatar avatar, bool current)
		{
			var summon = avatar.Kind == AvatarKind.Summon ? database.Summon(avatar.Id) : null;
			var cell = new SurfaceButton
			{
				Name = avatar.Awakened ? $"{avatar.Id}_awakened" : avatar.Id,
				CustomMinimumSize = new Vector2(CellWidth, Side + 46),
			}.Boxes(null, GameTheme.Box(new Color(Palette.Gold, 0.12f), new Color(Palette.Gold, 0), 0, Radius.Button, 0));

			var column = new VBoxContainer { Name = "Column", MouseFilter = Control.MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", Space.Tight);
			var frame = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(Side, Side), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
			var border = current ? Palette.Arcane : summon != null ? Palette.Frame(summon.Rarity) : Palette.GoldDark;
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Panel, border, current ? 3 : 2, (int)(Side / 2), 4));
			frame.AddChild(summon != null
				? Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Circle, boil: false, aura: avatar.Awakened ? summon.Element : null)
				: Doodle.Masked(Art.SpecialAvatar(avatar.Id), Palette.Gold, MaskShape.Circle, boil: false));
			column.AddChild(frame);
			var name = new Label
			{
				Name = "Name",
				Text = summon != null ? summon.NameFor(avatar.Awakened) : T($"avatar.special.{avatar.Id}"),
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(CellWidth, 0),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			name.AddThemeFontSizeOverride("font_size", 13);
			name.AddThemeColorOverride("font_color", avatar.Awakened ? Palette.Awakened : Palette.Text);
			column.AddChild(name);
			cell.Body.AddChild(column);
			return cell;
		}
	}
}
