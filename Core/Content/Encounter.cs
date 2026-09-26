using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// O que a batalha precisa de uma fase ou de um andar de Masmorra: estrelas e nível dos inimigos, as
	/// ondas e um multiplicador de Vida e Ataque dos inimigos (os andares fundos passam do 6★ nível 40).
	/// </summary>
	public sealed record Encounter(int Stars, int Level, IReadOnlyList<IReadOnlyList<StageEnemy>> Waves, double Scale = 1);
}
