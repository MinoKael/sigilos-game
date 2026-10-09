namespace Sigilos.Core.Content
{
	/// <summary>A fração, de 0 a 1, que um <see cref="ScaleFactor"/> lê na hora do efeito.</summary>
	public enum ScaleMeasure
	{
		/// <summary>A Vida atual de quem lança, em fração da Vida máxima.</summary>
		HealthFraction,

		/// <summary>A Vida atual do alvo, em fração da Vida máxima dele.</summary>
		TargetHealthFraction,

		/// <summary>Os aliados de pé, em fração da equipe (quem lança conta).</summary>
		LivingAllies,
	}
}
