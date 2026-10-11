using Godot;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// As caixas de cápsula (as pontas redondas), que cada lugar montava à mão:
	///
	/// - <see cref="Carved"/>: pedra entalhada no cabeçalho (as moedas, o correio) e as cápsulas de número
	///   (<c>Layout.Chip</c>, <c>Layout.Labeled</c>);
	/// - <see cref="Lit"/>: a mesma pedra com o filete de ouro do toque;
	/// - <see cref="Floating"/>: a que flutua por cima de qualquer tela (o balão do chat, o aviso da Batalha
	///   automática), com sombra, meio transparente pelo <see cref="Fade.Floating"/> do nó.
	///
	/// Cada chamada devolve uma caixa nova: quem muda a cor da borda depois (o estado do aviso) muda só a dele.
	/// </summary>
	public static class Pills
	{
		/// <summary>Pedra entalhada (<see cref="GameTheme.Carved"/>) com as pontas redondas e o conteúdo afastado das bordas.</summary>
		public static StyleBoxFlat Carved(int radius, int left, int right)
		{
			var box = GameTheme.Carved(Palette.Inset, 4);
			box.SetCornerRadiusAll(radius);
			box.ContentMarginLeft = left;
			box.ContentMarginRight = right;
			return box;
		}

		/// <summary>A cápsula <paramref name="rest"/> acesa: filete de ouro fino em volta (sob o dedo, apertada).</summary>
		public static StyleBoxFlat Lit(StyleBoxFlat rest)
		{
			var lit = (StyleBoxFlat)rest.Duplicate();
			lit.BorderColor = Palette.Gold;
			lit.SetBorderWidthAll(1);
			return lit;
		}

		/// <summary>
		/// A cápsula flutuante, da altura <paramref name="height"/> (pontas de meia altura), com a borda dada. Sem
		/// <paramref name="shadow"/> para o que já está colado numa flutuante (a faixa da última linha do chat).
		/// </summary>
		public static StyleBoxFlat Floating(Color border, float height, int width = 2, bool shadow = true)
		{
			var box = GameTheme.Box(new Color(Palette.Inset, 0.94f), border, width, (int)(height / 2), 0);
			if (shadow)
			{
				box.ShadowColor = new Color(0, 0, 0, 0.5f);
				box.ShadowSize = 6;
			}

			return box;
		}
	}
}
