using Godot;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using System.Linq;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A comparação: o toque num sigilo abre a ficha dele numa janela e, se o homúnculo usa outro no mesmo
	/// espaço, a ficha desse ao lado ("Equipado agora" e "Escolhido"). Cada lado tem os seus botões, sempre
	/// nas mesmas células: Equipar ou Remover, Provar, Aprimorar, Bloquear e Vender.
	/// </summary>
	public partial class RuneScreen
	{
		private const float CardWidth = 330;

		/// <summary>Um lado da comparação: a ficha mais a folga da moldura em volta dela.</summary>
		private const float SideWidth = CardWidth + 2 * Space.Large;

		private const float ActionHeight = 44;

		/// <summary>O que a janela gasta em volta do conteúdo: a moldura (18 de cada lado) e a barra de rolagem reservada.</summary>
		private const float DialogInset = 48;

		/// <summary>A janela da comparação, se aberta, o sigilo dela e se ela tem dois lados.</summary>
		private Dialog? _compare;
		private int? _compareRune;
		private bool _compareTwo;

		private void OpenCompare(Rune rune)
		{
			_compare?.Close();
			var two = Rival(rune) != null;
			var width = (two ? 2 * SideWidth + Space.Loose : SideWidth) + DialogInset;
			var dialog = Dialog.Open(this, T("runes.dialog_title"), width, null, "CompareDialog");
			dialog.Closed += () =>
			{
				if (_compare != dialog)
					return;
				_compare = null;
				_compareRune = null;
			};
			_compare = dialog;
			_compareRune = rune.Id;
			_compareTwo = two;
			RefreshCompare();
		}

		/// <summary>O sigilo que o homúnculo usa no espaço de <paramref name="rune"/>, quando não é o próprio.</summary>
		private Rune? Rival(Rune rune) =>
			Monster is { } monster && rune.EquippedOn != monster.Id
				? _player.RunesOn(monster.Id).FirstOrDefault(r => r.Slot == rune.Slot)
				: null;

		/// <summary>
		/// Refaz a comparação com os sigilos como estão agora. Fecha se o sigilo sumiu (vendido) ou se a
		/// comparação mudou de forma (o espaço ganhou ou perdeu o sigilo do homúnculo).
		/// </summary>
		private void RefreshCompare()
		{
			if (_compare == null)
				return;
			if (_player.Runes.FirstOrDefault(r => r.Id == _compareRune) is not { } rune || (Rival(rune) != null) != _compareTwo)
			{
				_compare.Close();
				return;
			}

			Layout.Clear(_compare.Body);
			var row = Layout.Row(Space.Loose).Named("Sides");
			if (Rival(rune) is { } rival)
			{
				row.AddChild(CompareSide("Equipped", T("runes.compare_equipped"), rival));
				row.AddChild(CompareSide("Chosen", T("runes.compare_chosen"), rune));
			}
			else
			{
				row.AddChild(CompareSide("Chosen", null, rune));
			}

			_compare.Body.AddChild(row);
		}

		/// <summary>Um lado: o nome do lado, a ficha (e quem usa o sigilo) numa moldura e os botões embaixo.</summary>
		private Control CompareSide(string name, string? caption, Rune rune)
		{
			var side = new VBoxContainer { Name = name, CustomMinimumSize = new Vector2(SideWidth, 0) };
			side.AddThemeConstantOverride("separation", Space.Medium);
			if (caption != null)
				side.AddChild(new Label { Name = "Caption", Text = caption, ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center });

			// A moldura estica: nos dois lados os botões ficam na mesma altura, por maior que seja uma das fichas.
			var panel = new PanelContainer { Name = "Panel", ThemeTypeVariation = GameTheme.InsetPanel, SizeFlagsVertical = SizeFlags.ExpandFill };
			var content = new VBoxContainer { Name = "Content" };
			content.AddThemeConstantOverride("separation", Space.Medium);
			content.AddChild(new RuneCard(rune, CardWidth, Opened(rune)) { Name = "Card" });
			if (rune.EquippedOn is { } owner && _player.Monster(owner) is { } holder)
				content.AddChild(OwnerRow(holder, CardWidth));
			panel.AddChild(content);
			side.AddChild(panel);
			side.AddChild(RuneActions(rune));
			return side;
		}

		/// <summary>
		/// Os botões de um sigilo, cada um na sua célula, todos da mesma altura: Equipar ou Remover | Provar,
		/// Aprimorar | Bloquear, Vender | (vazia). O que não vale agora fica no lugar, apagado, e o toque diz o motivo.
		/// </summary>
		private Control RuneActions(Rune rune)
		{
			var grid = Layout.Grid(2, Space.Medium).Named("Actions");
			var monster = Monster;
			var here = rune.EquippedOn != null && (rune.EquippedOn == _monsterId || monster == null);
			if (here)
			{
				var full = RuneInventory.IsFull(_player);
				grid.AddChild(GameButton.Of(T("runes.remove"), () =>
					{
						_compare?.Close();
						UnequipRequested?.Invoke(rune.Id);
					}, ButtonKind.Secondary, "cancel", ActionHeight)
					.Block(full ? T("runes.inventory_full", RuneInventory.Capacity) : null, T("runes.remove")).FixedHeight().Named("Unequip"));
			}
			else
			{
				grid.AddChild(GameButton.Of(T("runes.equip_on"), () =>
					{
						_compare?.Close();
						_trial.Remove(rune.Slot);
						EquipRequested?.Invoke(rune.Id, monster!.Id);
					}, ButtonKind.Primary, "confirm", ActionHeight)
					.Block(monster == null ? T("runes.why_no_monster") : null, T("runes.equip_on")).FixedHeight().Named("Equip"));
			}

			var trying = _trial.TryGetValue(rune.Slot, out var tried) && tried == rune.Id;
			grid.AddChild(GameButton.Of(trying ? T("runes.untry") : T("runes.try"), () =>
				{
					if (trying)
						_trial.Remove(rune.Slot);
					else
						_trial[rune.Slot] = rune.Id;
					_compare?.Close();
					RefreshLeft();
				}, ButtonKind.Secondary, trying ? "cancel" : "stats", ActionHeight)
				.Block(monster == null ? T("runes.why_no_monster") : here ? T("runes.why_try_equipped") : null, T("runes.try")).FixedHeight().Named("Try"));

			grid.AddChild(GameButton.Of(T("runes.improve"), () => OpenUpgrade(rune), ButtonKind.Primary, "level_max", ActionHeight).FixedHeight().Named("Improve"));
			grid.AddChild(GameButton.Of(rune.Locked ? T("lock.unlock") : T("lock.lock"), () => LockRequested?.Invoke(rune.Id), ButtonKind.Secondary, rune.Locked ? "unlock" : "lock", ActionHeight)
				.FixedHeight().Named("Lock"));

			var value = RuneRules.SellValue(rune);
			grid.AddChild(GameButton.Of(T("runes.sell_button_short"), () => Dialog.Confirm(this, T("runes.sell_title"), T("runes.sell_confirm", value), T("runes.sell_button_short"),
					() => SellRequested?.Invoke(rune.Id), ButtonKind.Danger), ButtonKind.Danger, "dismantle", ActionHeight)
				.Block(rune.Locked ? T("runes.locked_note") : rune.EquippedOn != null ? T("runes.why_sell_equipped") : null, T("runes.sell_button_short")).FixedHeight().Named("Sell"));
			grid.AddChild(new Control { Name = "Empty", MouseFilter = MouseFilterEnum.Ignore });
			return grid;
		}
	}
}
