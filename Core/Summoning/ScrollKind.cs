namespace Sigilos.Core.Summoning
{
	/// <summary>Os pergaminhos de invocação (GDD, seção 9). Cada um tem as suas taxas (<see cref="SummonRates"/>).</summary>
	public enum ScrollKind
	{
		/// <summary>Pergaminho Místico: 3★ a 5★ de Fogo, Água e Vento. O comum: fases, Masmorras, Loja.</summary>
		Mystic,

		/// <summary>Pergaminho de Luz e Trevas: 3★ a 5★, só Luz e Trevas. Só de marcos.</summary>
		LightDark,

		/// <summary>Pergaminho Lendário: 4★ ou 5★ de Fogo, Água e Vento. Só de marcos.</summary>
		Legendary,
	}
}
