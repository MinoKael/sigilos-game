using System;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A equipe de um conteúdo em miniatura: um medalhão por monstro (a Líder com a coroa), com estrelas
	/// e nível na dica, e o sigilo que abre a tela de Equipes. As vagas vazias aparecem como pedras.
	/// </summary>
	public partial class TeamStrip : HBoxContainer
	{
		private const float Medal = 44;

		public TeamStrip(GameDatabase database, PlayerState player, string content, Action onEdit)
		{
			AddThemeConstantOverride("separation", 6);
			var team = Teams.Of(player, content).Select(player.Monster).OfType<OwnedSummon>().Where(m => database.HasSummon(m.SummonId)).ToList();
			for (var i = 0; i < PlayerState.TeamSize; i++)
			{
				var slot = new PanelContainer { CustomMinimumSize = new Vector2(Medal, Medal), MouseFilter = MouseFilterEnum.Stop };
				var box = GameTheme.Box(Palette.Inset, Palette.GoldDark, 1, (int)(Medal / 2), 3);
				if (i < team.Count)
				{
					var monster = team[i];
					var summon = database.Summon(monster.SummonId);
					box.BorderColor = i == 0 ? Palette.Gold : Palette.Frame(summon.Rarity);
					box.SetBorderWidthAll(2);
					slot.TooltipText = $"{summon.NameFor(monster.Awakened)} · {T("common.stars_level", Texts.Stars(monster.Stars), monster.Level)}" + (i == 0 ? $"\n{T("teams.leader")}" : "");
					slot.AddChild(Doodle.Masked(Art.Creature(summon.ImageFor(monster.Awakened)), Palette.Of(summon.Element), MaskShape.Circle, boil: false));
				}
				else
				{
					slot.TooltipText = T("teams.empty");
				}

				slot.AddThemeStyleboxOverride("panel", box);
				box.SetContentMarginAll(2);
				AddChild(slot);
			}

			var edit = SigilButton.Of("team", T("common.team"), onEdit, 48, SigilShape.Square);
			edit.Highlight = team.Count == 0;
			AddChild(edit);
		}
	}
}
