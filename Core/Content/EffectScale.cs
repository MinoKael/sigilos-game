namespace Sigilos.Core.Content
{
	/// <summary>Um termo somado à conta do efeito: <see cref="Power"/> × <see cref="Stat"/> (0,08 × Vida máxima).</summary>
	public sealed record ScaleTerm
	{
		public ScaleStat Stat { get; init; }
		public double Power { get; init; }
	}

	/// <summary>
	/// Multiplica a conta do efeito por (<see cref="Base"/> + <see cref="Slope"/> × a fração <see cref="By"/>):
	/// base 8,5 e slope −3 na Vida de quem lança dão ×8,5 sem Vida e ×5,5 com a Vida cheia.
	/// </summary>
	public sealed record ScaleFactor
	{
		public ScaleMeasure By { get; init; }
		public double Base { get; init; }
		public double Slope { get; init; }
	}

	/// <summary>
	/// Multiplica a conta do efeito por (Velocidade de quem lança + <see cref="Add"/>) / <see cref="Over"/>;
	/// com <see cref="OverTarget"/>, dividido pela Velocidade do alvo no lugar de <see cref="Over"/>.
	/// </summary>
	public sealed record SpeedFactor
	{
		public double Add { get; init; }
		public double Over { get; init; }
		public bool OverTarget { get; init; }
	}
}
