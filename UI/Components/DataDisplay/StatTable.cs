using System;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A ficha de atributos, de dois jeitos:
	///
	/// - <see cref="Show"/>: Glifo e nome, o total (base mais runas e conjuntos) em branco e, em verde e um
	///   pouco menor, quanto disso as runas somaram.
	/// - <see cref="Compare"/>: a ficha de agora ao lado da que o monstro teria com outras runas, e a
	///   diferença já feita (+ em verde, − em vermelho; o que não muda fica apagado). Sem troca para
	///   mostrar, as mesmas colunas voltam a ser o total e o bônus das runas. As células são montadas uma
	///   vez e só reescritas, para a ficha acompanhar cada toque na lista sem piscar.
	///
	/// O que cada atributo faz está no Compêndio.
	/// </summary>
	public partial class StatTable : GridContainer
	{
		/// <summary>O bônus e a diferença ficam um pouco menores que o total: o total é o número que importa.</summary>
		private const int BonusSize = GameTheme.BodySize - 3;

		/// <summary>Largura das colunas de números da comparação: 5 algarismos sem empurrar a ficha.</summary>
		private const float NumberWidth = 48;

		/// <summary>As três colunas de números da comparação, com o cabeçalho na linha 0.</summary>
		private Label[,]? _cells;

		public StatTable()
		{
			Columns = 4;
			AddThemeConstantOverride("h_separation", Space.Large);
			AddThemeConstantOverride("v_separation", Space.None);
		}

		public void Show(StatSheet sheet)
		{
			Layout.Clear(this);
			_cells = null;
			Columns = 4;
			AddThemeConstantOverride("h_separation", Space.Large);
			foreach (var stat in Enum.GetValues<Stat>())
			{
				// Quatro células por atributo, com o nome dele na frente: HpGlyph, HpName, HpTotal, HpBonus.
				AddChild(new RuneGlyph(Texts.GlyphOf(stat), 20, Palette.Gold) { Name = $"{stat}Glyph" });
				AddChild(new Label { Name = $"{stat}Name", Text = Texts.Name(stat) });

				var total = new Label { Name = $"{stat}Total", Text = Texts.Value(stat, sheet.Total.Get(stat)), HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(64, 0) };
				AddChild(total);

				var bonus = sheet.Runes.Stats.Get(stat);
				var bonusLabel = new Label { Name = $"{stat}Bonus", Text = bonus > 0.0005 ? $"+{Texts.Value(stat, bonus)}" : "", CustomMinimumSize = new Vector2(64, 0), VerticalAlignment = VerticalAlignment.Center };
				bonusLabel.AddThemeColorOverride("font_color", Palette.Positive);
				bonusLabel.AddThemeFontSizeOverride("font_size", BonusSize);
				AddChild(bonusLabel);
			}
		}

		/// <summary>
		/// Agora e com a troca, lado a lado, e a diferença do que aparece na tela (nada de "+0%" por causa
		/// de uma casa escondida). Sem <paramref name="next"/>, mostra o total e o bônus das runas.
		/// </summary>
		public void Compare(StatSheet now, StatSheet? next)
		{
			var cells = _cells ?? Build();
			cells[0, 0].Text = next == null ? T("runes.stats_total") : T("runes.stats_now");
			cells[0, 1].Text = next == null ? T("runes.stats_runes") : T("runes.stats_next");
			var stats = Enum.GetValues<Stat>();
			for (var i = 0; i < stats.Length; i++)
			{
				var stat = stats[i];
				var (current, bonus, change) = (cells[i + 1, 0], cells[i + 1, 1], cells[i + 1, 2]);
				var before = now.Total.Get(stat);
				current.Text = Texts.Value(stat, before);
				if (next == null)
				{
					var runes = now.Runes.Stats.Get(stat);
					Paint(bonus, runes > 0.0005 ? $"+{Texts.Value(stat, runes)}" : "", Palette.Positive, BonusSize);
					change.Text = "";
					continue;
				}

				var after = next.Total.Get(stat);
				var difference = Shown(stat, after) - Shown(stat, before);
				var tone = difference > 0 ? Palette.Positive : difference < 0 ? Palette.Negative : new Color(Palette.Text, 0.45f);
				Paint(bonus, Texts.Value(stat, after), tone, GameTheme.BodySize);
				Paint(change, difference == 0 ? "" : (difference > 0 ? "+" : "−") + Texts.Value(stat, Math.Abs(difference)), tone, BonusSize);
			}
		}

		/// <summary>As células da comparação: cabeçalho (só as colunas de números têm título) e uma linha por atributo.</summary>
		private Label[,] Build()
		{
			Layout.Clear(this);
			Columns = 5;
			AddThemeConstantOverride("h_separation", Space.Medium);
			var stats = Enum.GetValues<Stat>();
			var cells = new Label[stats.Length + 1, 3];
			AddChild(new Control { Name = "HeadGlyph", MouseFilter = MouseFilterEnum.Ignore });
			AddChild(new Control { Name = "HeadName", MouseFilter = MouseFilterEnum.Ignore });
			string[] heads = { "HeadNow", "HeadNext", "HeadChange" };
			for (var column = 0; column < 3; column++)
			{
				var head = new Label { Name = heads[column], ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Right };
				cells[0, column] = head;
				AddChild(head);
			}

			for (var i = 0; i < stats.Length; i++)
			{
				var stat = stats[i];
				// Cinco células por atributo, com o nome dele na frente: HealthGlyph, HealthName, HealthNow, HealthNext, HealthChange.
				AddChild(new RuneGlyph(Texts.GlyphOf(stat), 20, Palette.Gold) { Name = $"{stat}Glyph" });
				AddChild(new Label { Name = $"{stat}Name", Text = Texts.Name(stat), SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis });
				string[] names = { "Now", "Next", "Change" };
				for (var column = 0; column < 3; column++)
				{
					var cell = new Label { Name = $"{stat}{names[column]}", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(column == 2 ? NumberWidth - 4 : NumberWidth, 0) };
					cells[i + 1, column] = cell;
					AddChild(cell);
				}
			}

			_cells = cells;
			return cells;
		}

		private static void Paint(Label label, string text, Color color, int size)
		{
			label.Text = text;
			label.AddThemeColorOverride("font_color", color);
			label.AddThemeFontSizeOverride("font_size", size);
		}

		/// <summary>O valor como a ficha escreve (inteiro, ou décimo de %), para a diferença bater com os números à vista.</summary>
		private static double Shown(Stat stat, double value) => StatBlock.IsAbsolute(stat)
			? Math.Round(value, MidpointRounding.AwayFromZero)
			: Math.Round(value * 1000, MidpointRounding.AwayFromZero) / 1000;
	}
}
