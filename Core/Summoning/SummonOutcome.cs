namespace Sigilos.Core.Summoning
{
	/// <summary>O que uma invocação virou ao entrar na coleção.</summary>
	public enum SummonOutcome
	{
		/// <summary>Primeira cópia: entra na coleção.</summary>
		New,

		/// <summary>Duplicata: +1 Eco.</summary>
		Echo,

		/// <summary>Duplicata com os 5 Ecos: vira Fragmentos.</summary>
		Fragments,
	}
}
