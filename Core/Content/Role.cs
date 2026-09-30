namespace Sigilos.Core.Content
{
	/// <summary>
	/// Papel da invocação: como o orçamento de atributos dela é repartido entre Vida, Ataque e Defesa, e a
	/// Velocidade de base (Data/stat_model.json, "role_profiles"). O papel não dá mais poder: só escolhe
	/// onde ele vai.
	/// </summary>
	public enum Role
	{
		HP,
		Attack,
		Defense,
		Support,
	}
}
