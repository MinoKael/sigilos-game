using System.Collections.Generic;

namespace Sigilos.Sounds.Synth
{
	/// <summary>
	/// Um corpo que vibra, por síntese modal: cada parcial é um seno na razão <see cref="Ratios"/> da nota, com
	/// o volume <see cref="Gains"/> e o tempo de queda <see cref="Decays"/> (fração do tempo da fundamental).
	/// As razões não harmônicas dão o material: madeira, sino, vidro, metal, pedra.
	/// </summary>
	public sealed record Modes(IReadOnlyList<double> Ratios, IReadOnlyList<double> Gains, IReadOnlyList<double> Decays)
	{
		/// <summary>Tecla de marimba: fundamental redonda e o quarto parcial da barra afinada.</summary>
		public static readonly Modes Marimba = new(new[] { 1, 3.93, 9.2 }, new[] { 1, 0.3, 0.07 }, new[] { 1, 0.25, 0.08 });

		/// <summary>Bloco de madeira: o "toc" curto dos botões e da capa do livro.</summary>
		public static readonly Modes Wood = new(new[] { 1, 2.76, 5.40, 8.93 }, new[] { 1, 0.5, 0.25, 0.1 }, new[] { 1, 0.45, 0.22, 0.12 });

		/// <summary>Celesta: quase só a fundamental, a assinatura delicada do grimório.</summary>
		public static readonly Modes Celesta = new(new[] { 1, 2, 3, 4.02 }, new[] { 1, 0.22, 0.08, 0.04 }, new[] { 1, 0.4, 0.25, 0.15 });

		/// <summary>Sino pequeno, de parciais quase harmônicos.</summary>
		public static readonly Modes Bell = new(new[] { 1, 2.0, 2.99, 4.15, 5.43, 6.8 }, new[] { 1, 0.45, 0.3, 0.16, 0.1, 0.05 }, new[] { 1, 0.7, 0.55, 0.4, 0.3, 0.2 });

		/// <summary>Cristal: a estrela, o brilho astral.</summary>
		public static readonly Modes Glass = new(new[] { 1, 2.32, 4.25, 6.63 }, new[] { 1, 0.35, 0.14, 0.06 }, new[] { 1, 0.55, 0.3, 0.18 });

		/// <summary>Taça cantante: pares levemente desafinados que batem devagar. O círculo de invocação.</summary>
		public static readonly Modes Bowl = new(new[] { 1, 1.005, 2.71, 2.72, 5.03, 8.4 }, new[] { 1, 0.8, 0.45, 0.35, 0.15, 0.05 }, new[] { 1, 1, 0.75, 0.75, 0.45, 0.25 });

		/// <summary>Metal estilizado: fivelas, travas, escudos, sem o realismo de uma espada.</summary>
		public static readonly Modes Metal = new(new[] { 1, 1.47, 2.09, 2.56, 3.18, 3.83 }, new[] { 1, 0.7, 0.55, 0.4, 0.3, 0.2 }, new[] { 1, 0.8, 0.6, 0.5, 0.35, 0.25 });

		/// <summary>Moeda: parciais altos e curtos.</summary>
		public static readonly Modes Coin = new(new[] { 1, 1.56, 2.34, 2.98, 3.72 }, new[] { 1, 0.6, 0.45, 0.3, 0.2 }, new[] { 1, 0.7, 0.5, 0.35, 0.25 });

		/// <summary>Pedra gravada: a runa.</summary>
		public static readonly Modes Stone = new(new[] { 1, 1.72, 2.85, 3.94, 5.3 }, new[] { 1, 0.6, 0.35, 0.2, 0.1 }, new[] { 1, 0.6, 0.4, 0.25, 0.15 });
	}
}
