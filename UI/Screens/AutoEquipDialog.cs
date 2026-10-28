using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A janela do Auto-equipar: no alto, os conjuntos que o jogador quer (ligar um obriga a combinação a fechá-lo;
	/// nenhum ligado deixa a busca livre, <see cref="RuneAutoEquip"/>); no meio, à esquerda, os círculos com os 6 espaços
	/// de agora e os da combinação achada (os que não mudam ficam apagados) e, à direita, a ficha de agora ao lado da nova,
	/// com a diferença. Nada muda na conta antes do Equipar: a busca roda a cada conjunto ligado ou desligado.
	/// </summary>
	public static class AutoEquipDialog
	{
		private const float Width = 760;
		private const float TileScale = 0.75f;

		/// <summary>A largura do que fica ao lado dos círculos: o que sobra do corpo da janela (a largura menos as margens) depois das duas colunas deles.</summary>
		private static readonly float SideWidth = Width - 40 - 2 * RuneSwapStrip.ColumnWidth(TileScale) - Space.Regular - Space.Large;

		/// <param name="equip">Os Ids dos sigilos que entram no homúnculo (os que saem voltam ao inventário).</param>
		public static Dialog Open(Control from, GameDatabase database, PlayerState player, OwnedSummon monster, Action<IReadOnlyList<int>> equip)
		{
			var summon = database.Summon(monster.SummonId);
			var dialog = Dialog.Open(from, T("runes.auto_title", summon.NameFor(monster.Awakened)), Width, null, "AutoEquipDialog");
			var body = dialog.Body;
			var required = new List<RuneSet>();
			IReadOnlyList<Rune> changes = Array.Empty<Rune>();

			var heading = Layout.Row(Space.Medium).Named("SetsTitle");
			var title = new Label { Name = "Text", Text = T("runes.auto_sets"), ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
			heading.AddChild(title);
			SigilButton? help = null;
			help = SigilButton.Of("help", () => Dialog.Info(help!, T("runes.auto"), T("runes.auto_dialog_help"), 480), 40);
			heading.AddChild(help.Named("Help"));
			body.AddChild(heading);
			var sets = Layout.Grid(4, Space.Small).Named("Sets");
			body.AddChild(sets);

			var middle = Layout.Row(Space.Large).Named("Middle");
			body.AddChild(middle);
			var strip = new RuneSwapStrip(TileScale);
			strip.SlotPressed += tile => { if (tile.Rune != null) RuneDialog.Show(tile, tile.Rune); };
			middle.AddChild(strip);
			var side = new VBoxContainer { Name = "Side", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
			side.AddThemeConstantOverride("separation", Space.Large);
			middle.AddChild(side);
			var note = Layout.Text("", GameTheme.Faded, SideWidth).Named("Note");
			side.AddChild(note);
			var table = new StatTable { Name = "Stats", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
			side.AddChild(table);

			var confirm = GameButton.Of(T("runes.auto_equip"), () =>
			{
				dialog.Close();
				equip(changes.Select(r => r.Id).ToList());
			}, ButtonKind.Primary, "auto").Named("Equip");
			confirm.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
			dialog.AddFooter(confirm.Wide(240));

			void Refresh()
			{
				var current = player.RunesOn(monster.Id);
				var chosen = RuneAutoEquip.Choose(summon, monster, player.Runes, database.Summons, required);
				changes = chosen == null ? Array.Empty<Rune>() : RuneAutoEquip.Changes(chosen, monster);

				Layout.Clear(sets);
				var used = required.Sum(set => RuneSets.Active.First(d => d.Set == set).Pieces);
				foreach (var definition in RuneSets.Active)
				{
					var set = definition.Set;
					var on = required.Contains(set);
					var button = GameButton.Of(T("runes.auto_set", Texts.Name(set), definition.Pieces), () =>
					{
						if (!required.Remove(set))
							required.Add(set);
						Refresh();
					}, on ? ButtonKind.Primary : ButtonKind.Secondary, null, 44).Named(set.ToString());
					button.Block(!on && used + definition.Pieces > RuneRules.Slots ? T("runes.why_auto_full") : null, Texts.Name(set));
					sets.AddChild(button.FixedHeight());
				}

				strip.Show(current, chosen ?? current);

				note.Text = chosen == null ? T("runes.auto_impossible")
					: changes.Count == 0 ? T("runes.auto_none")
					: T("runes.auto_changes", changes.Count);
				var sheet = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, current);
				table.Compare(sheet, changes.Count == 0 ? null : SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, chosen!));
				confirm.Block(chosen == null ? T("runes.auto_impossible") : changes.Count == 0 ? T("runes.auto_none") : null, T("runes.auto_equip"));
			}

			Refresh();
			return dialog;
		}
	}
}
