namespace Sigilos.Core.Content
{
	/// <summary>
	/// O que vence uma luta. A derrota é sempre a mesma (o time do jogador inteiro caído); a vitória muda
	/// com o modo. Cada condição é uma estratégia em Core/Battle/Victory (<c>VictoryRules</c>).
	///
	/// Condição nova: o nome aqui, uma classe que herda de <c>VictoryRule</c> e a linha na tabela.
	/// </summary>
	public enum VictoryCondition
	{
		/// <summary>Vencer todas as ondas, até o último inimigo (a Campanha).</summary>
		AllWaves,

		/// <summary>
		/// Derrubar o chefe vence na hora, mesmo com lacaios em pé (as Masmorras). Sem chefe na luta, vale
		/// <see cref="AllWaves"/>.
		/// </summary>
		Boss,
	}
}
