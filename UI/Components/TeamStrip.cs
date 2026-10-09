using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A última equipe usada num conteúdo, em miniatura: um medalhão por monstro (a Líder com moldura de
	/// ouro e a palavra "Líder" embaixo) e as vagas vazias como pedras. Só mostra: a equipe se monta na
	/// preparação da luta (o Lutar). Toque longo num medalhão abre o resumo do monstro.
	/// </summary>
	public partial class TeamStrip : HBoxContainer
	{
		private const float Medal = 58;

		public TeamStrip(GameDatabase database, PlayerState player, string content)
		{
			Name = "Team";
			AddThemeConstantOverride("separation", 8);
			var team = Teams.Of(player, content).Select(player.Monster).OfType<OwnedSummon>().Where(m => database.HasSummon(m.SummonId)).ToList();
			for (var i = 0; i < PlayerState.TeamSize; i++)
			{
				var column = new VBoxContainer { Name = $"Slot{i + 1}", MouseFilter = MouseFilterEnum.Ignore };
				column.AddThemeConstantOverride("separation", 0);
				var slot = new PanelContainer { Name = "Medal", CustomMinimumSize = new Vector2(Medal, Medal), MouseFilter = MouseFilterEnum.Pass };
				var box = GameTheme.Box(Palette.Inset, Palette.GoldDark, 1, (int)(Medal / 2), 3);
				if (i < team.Count)
				{
					var monster = team[i];
					var summon = database.Summon(monster.SummonId);
					box.BorderColor = i == 0 ? Palette.Gold : Palette.Frame(summon.Rarity);
					box.SetBorderWidthAll(2);
					slot.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Circle, boil: false, aura: monster.Awakened ? summon.Element : null));
					Press.On(slot, null, () => MonsterSummary.Open(slot, summon, monster));
				}

				slot.AddThemeStyleboxOverride("panel", box);
				box.SetContentMarginAll(3);
				column.AddChild(slot);
				var caption = new Label { Name = "Caption", Text = i == 0 && team.Count > 0 ? T("teams.leader") : "", HorizontalAlignment = HorizontalAlignment.Center };
				caption.AddThemeFontSizeOverride("font_size", 13);
				caption.AddThemeColorOverride("font_color", Palette.Gold);
				column.AddChild(caption);
				AddChild(column);
			}
		}
	}
}
