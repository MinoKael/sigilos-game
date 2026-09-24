using System;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A ficha de atributos: nome, valor de base e, em verde, o que as runas
	/// somam. Passar o mouse num atributo explica o que ele faz.
	/// </summary>
	public partial class StatTable : GridContainer
	{
		public StatTable()
		{
			Columns = 3;
			AddThemeConstantOverride("h_separation", 16);
			AddThemeConstantOverride("v_separation", 2);
		}

		public void Show(StatSheet sheet)
		{
			Layout.Clear(this);
			foreach (var stat in Enum.GetValues<Stat>())
			{
				AddChild(new Label { Text = Texts.Name(stat), TooltipText = Explain(stat), MouseFilter = MouseFilterEnum.Stop });

				var baseValue = new Label { Text = Texts.Value(stat, sheet.Base.Get(stat)), HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(64, 0) };
				AddChild(baseValue);

				var bonus = sheet.Runes.Stats.Get(stat);
				var bonusLabel = new Label { Text = bonus > 0.0005 ? $"+{Texts.Value(stat, bonus)}" : "", CustomMinimumSize = new Vector2(64, 0) };
				bonusLabel.AddThemeColorOverride("font_color", Palette.Positive);
				AddChild(bonusLabel);
			}
		}

		private static string Explain(Stat stat) => stat switch
		{
			Stat.Health => "Quanto dano aguenta antes de cair.",
			Stat.Attack => "Base do dano: o multiplicador de cada habilidade é sobre ele.",
			Stat.Defense => $"Reduz o dano recebido: Defesa {BattleRules.DefenseConstant:0} corta o dano pela metade.",
			Stat.Speed => "Quão rápido a barra de Ímpeto enche. Decide quem age primeiro.",
			Stat.Crit => "Chance de crítico. O crítico multiplica o dano por 1 + Dano crítico.",
			Stat.CritDamage => "Quanto o crítico soma ao dano: 50% de base.",
			Stat.Accuracy => "Precisão: desconta da Resistência do alvo aos seus efeitos negativos.",
			Stat.Resistance => "Chance de barrar efeitos negativos, menos a Precisão de quem lança (nunca abaixo de 15%).",
			_ => "",
		};
	}
}
