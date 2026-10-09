namespace Sigilos.Core.Runes
{
	/// <summary>A situação de uma runa, para o filtro do inventário.</summary>
	public enum RuneCondition
	{
		/// <summary>Bloqueada: não se vende.</summary>
		Locked,

		/// <summary>Desbloqueada: o que ainda pode ser vendido.</summary>
		Unlocked,
	}
}
