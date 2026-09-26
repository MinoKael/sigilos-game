namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Um sorteio que um subatributo recebeu: o de origem (no nível em que ele entrou na runa) ou o de
	/// uma melhora em +3, +6, +9 ou +12. A lista de sorteios é a história da runa: "+5% de Ataque em
	/// +3, +20 de Defesa em +6".
	/// </summary>
	public sealed record RuneRoll(int Level, double Amount);
}
