using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Escolher um monstro numa janela: a grade dos cartões (coleção primeiro, depois o Baú, cada um
	/// com a faixa "Baú"; em cada parte, os favoritos na frente), o escolhido de agora aceso, e, se <c>allowNone</c>, o botão Nenhum. Tocar
	/// escolhe e fecha; segurar abre o resumo, como em todo lugar. A mesma janela serve a outra lista, com
	/// outro título (o alvo da fusão das cópias do Baú).
	/// </summary>
	public static class MonsterPicker
	{
		public static Dialog Open(Control from, GameDatabase database, PlayerState player, int? current, bool allowNone, Action<int?> chosen)
		{
			var monsters = player.Monsters
				.Where(m => database.HasSummon(m.SummonId) && !m.IsInfusionCore)
				.OrderBy(m => m.Stored)
				.ThenByDescending(m => m.Favorite)
				.ThenByDescending(m => m.Stars)
				.ThenByDescending(m => m.Level);
			var dialog = Open(from, database, T("picker.title"), T("picker.hint"), monsters, current, id => chosen(id));
			if (allowNone)
				dialog.AddAction(T("picker.none"), () => chosen(null)).Named("None");
			return dialog;
		}

		/// <param name="monsters">Os que aparecem, já filtrados e na ordem.</param>
		public static Dialog Open(Control from, GameDatabase database, string title, string hint, IEnumerable<OwnedSummon> monsters, int? current, Action<int> chosen)
		{
			var dialog = Dialog.Open(from, title, 820, null, "MonsterPicker");
			dialog.Body.AddChild(Layout.Text(hint, Style.GameTheme.Faded).Named("Hint"));
			var grid = new GridContainer { Name = "Monsters", Columns = 6 };
			grid.AddThemeConstantOverride("h_separation", Space.Regular);
			grid.AddThemeConstantOverride("v_separation", Space.Regular);
			foreach (var monster in monsters)
			{
				var card = new CreatureCard(database.Summon(monster.SummonId), monster, 112, null, monster.Stored ? T("monsters.vault") : null) { Name = $"Monster{monster.Id}" };
				card.SetSelected(monster.Id == current);
				card.Pressed += c =>
				{
					dialog.Close();
					chosen(c.Monster!.Id);
				};
				grid.AddChild(card);
			}

			dialog.Body.AddChild(grid);
			return dialog;
		}
	}
}
