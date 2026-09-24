using Godot;
using Sigilos.Core.Battle;
using Sigilos.UI.Style;

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
			_label = new Label { ThemeTypeVariation = GameTheme.OnStone, CustomMinimumSize = new Vector2(92, 0) };
			_label.AddThemeFontOverride("font", GameTheme.Serif);
			AddChild(_label);

			for (var i = 0; i < _pips.Length; i++)
			{
				_pips[i] = new Panel { CustomMinimumSize = new Vector2(20, 20), TooltipText = "Éter: recurso único do time (0 a 10)." };
				AddChild(_pips[i]);
			}

			SetValue(0);
		}

		public void SetValue(int ether)
		{
			_label.Text = $"Éter {ether}/{BattleRules.MaxEther}";
			for (var i = 0; i < _pips.Length; i++)
			{
				var lit = i < ether;
				_pips[i].AddThemeStyleboxOverride("panel", GameTheme.Box(lit ? Palette.Ether : Palette.StoneLight, lit ? Palette.Bone : Palette.InkFaded, 1, 10, 0));
			}
		}
	}
}
