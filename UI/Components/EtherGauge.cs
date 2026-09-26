using Godot;
using Sigilos.Core.Battle;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>O Éter do time: 10 contas, acesas até o valor atual.</summary>
	public partial class EtherGauge : HBoxContainer
	{
		private readonly Panel[] _pips = new Panel[BattleRules.MaxEther];
		private readonly Label _label;

		public EtherGauge()
		{
			AddThemeConstantOverride("separation", 4);
			TooltipText = T("batalha.eter_dica", BattleRules.MaxEther);
			MouseFilter = MouseFilterEnum.Stop;
			_label = new Label { CustomMinimumSize = new Vector2(92, 0), MouseFilter = MouseFilterEnum.Ignore };
			_label.AddThemeFontOverride("font", GameTheme.Serif);
			AddChild(_label);

			for (var i = 0; i < _pips.Length; i++)
			{
				_pips[i] = new Panel { CustomMinimumSize = new Vector2(20, 20), MouseFilter = MouseFilterEnum.Ignore };
				AddChild(_pips[i]);
			}

			SetValue(0);
		}

		public void SetValue(int ether)
		{
			_label.Text = T("batalha.eter", ether, BattleRules.MaxEther);
			for (var i = 0; i < _pips.Length; i++)
			{
				var lit = i < ether;
				_pips[i].AddThemeStyleboxOverride("panel", GameTheme.Box(lit ? Palette.Ether : Palette.Inset, lit ? Palette.Text : Palette.GoldDark, 1, 10, 0));
			}
		}
	}
}
