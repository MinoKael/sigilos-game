namespace Sigilos.Core.Runes
{
	/// <summary>
	/// O que os conjuntos de 4 peças fazem em combate além de atributo. Zero quando o conjunto não está
	/// completo. Frações: 0,35 = 35%.
	/// </summary>
	public sealed record RuneSetEffects(double Drain, double StunOnHit, double ExtraTurn)
	{
		public static readonly RuneSetEffects None = new(0, 0, 0);
	}
}
