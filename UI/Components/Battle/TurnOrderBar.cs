using System.Collections.Generic;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;
using Side = Sigilos.Core.Battle.Side;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Os próximos a agir, de cima para baixo, com a palavra "Próximos" no alto. Moldura verde: aliado;
	/// vermelha: inimigo. Toque longo num retrato abre o resumo da unidade.
	/// </summary>
	public partial class TurnOrderBar : VBoxContainer
	{
		public TurnOrderBar()
		{
			Name = "TurnOrder";
			AddThemeConstantOverride("separation", Space.Small);
		}

		/// <summary>O lado de quem olha: o time dele tem o anel verde, o outro o vermelho (na Arena do Véu o segundo jogador luta pelos inimigos da sessão).</summary>
		public Side Mine { get; set; } = Side.Allies;

		/// <summary>A ficha do monstro do outro lado não abre (Arena do Véu).</summary>
		public bool HideRival { get; set; }

		public void Show(IReadOnlyList<BattleUnit> order)
		{
			Layout.Clear(this);
			var title = new Label { Name = "NextUp", Text = T("battle.next_up"), HorizontalAlignment = HorizontalAlignment.Center };
			title.AddThemeFontSizeOverride("font_size", FontSize.Detail);
			title.AddThemeColorOverride("font_color", Palette.GoldDark.Lightened(0.35f));
			AddChild(title);

			for (var i = 0; i < order.Count; i++)
			{
				var unit = order[i];
				var frame = new PanelContainer { Name = $"Turn{i + 1}", MouseFilter = MouseFilterEnum.Stop };
				frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, unit.Side == Mine ? Palette.Health : Palette.HealthLow, 2, 22, 2));
				frame.AddChild(Layout.Medal(Art.Creature(unit.Image), Palette.Of(unit.Element), 40));
				if (!HideRival || unit.Side == Mine)
					Press.On(frame, () => MonsterSummary.Open(frame, unit), () => MonsterSummary.Open(frame, unit));
				AddChild(frame);
			}
		}
	}
}
