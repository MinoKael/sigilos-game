namespace Sigilos.Core.Content
{
	/// <summary>Os cinco elementos, mais o Neutro. Fogo vence Vento, Vento vence Água, Água vence Fogo; Luz e Trevas
	/// vencem uma à outra (GDD, seção 6). A tabela de vantagem mora em Battle/ElementChart.
	///
	/// O Neutro é o elemento dos homúnculos de 2★ naturais, que não têm variantes: uma só por família
	/// (<c>{família}_neutral</c>). Não tem vantagem nem desvantagem, não sai nos pergaminhos de Luz e Trevas
	/// e entra no Pergaminho Místico como os de Fogo, Água e Vento.</summary>
	public enum Element
	{
		Fire,
		Water,
		Wind,
		Light,
		Dark,
		Neutral,
	}
}
