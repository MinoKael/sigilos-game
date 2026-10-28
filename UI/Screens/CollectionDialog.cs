using System;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// O Álbum: todos os homúnculos do jogo, uma aba por elemento (com quantos o jogador tem dele), em retratos
	/// quadrados por raridade. Os que a conta tem (na coleção ou no Baú) ficam acesos; os que faltam, apagados.
	/// Toque longo abre o resumo, como em qualquer cartão.
	/// </summary>
	public static class CollectionDialog
	{
		private const float Width = 920;
		private const float CardWidth = 78;
		private const int Columns = 10;

		public static Dialog Open(Control from, GameDatabase database, PlayerState player)
		{
			var owned = player.Monsters.Select(m => m.SummonId).ToHashSet();
			var summons = database.Summons.Where(s => !InfusionCore.Is(s.Id)).ToList();
			var dialog = Dialog.Open(from, T("monsters.album_title", summons.Count(s => owned.Contains(s.Id)), summons.Count), Width, null, "CollectionDialog");

			var tabs = new TextTabs(compact: true) { Name = "Elements" };
			var elements = Enum.GetValues<Element>().Where(e => summons.Any(s => s.Element == e)).ToList();
			foreach (var element in elements)
			{
				var of = summons.Where(s => s.Element == element).ToList();
				tabs.Add(Texts.Name(element), $"{of.Count(s => owned.Contains(s.Id))}/{of.Count}").Name = element.ToString();
			}

			dialog.Body.AddChild(tabs);
			var grid = Layout.Grid(Columns, Space.Small).Named("Summons");
			dialog.Body.AddChild(grid);

			void Show(Element element)
			{
				Layout.Clear(grid);
				foreach (var summon in summons.Where(s => s.Element == element).OrderByDescending(s => s.Rarity).ThenBy(s => s.Name, StringComparer.Ordinal))
				{
					var card = new CreatureCard(summon, null, CardWidth, aspect: CreatureCard.Square) { Name = summon.Id };
					if (!owned.Contains(summon.Id))
						card.Modulate = new Color(1, 1, 1, Fade.Disabled);
					grid.AddChild(card);
				}
			}

			tabs.Changed += index => Show(elements[index]);
			Show(elements[0]);
			return dialog;
		}
	}
}
