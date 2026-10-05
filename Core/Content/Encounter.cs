using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// O que a batalha precisa de uma fase, de um andar de Masmorra ou de uma constelação: estrelas e nível
	/// dos inimigos, as ondas, um multiplicador de Vida e Ataque dos inimigos (os andares fundos passam do
	/// 6★ nível 40) e as regras que valem a luta inteira (a Influência de uma constelação; nulo: nenhuma).
	/// </summary>
	public sealed record Encounter(int Stars, int Level, IReadOnlyList<IReadOnlyList<StageEnemy>> Waves, double Scale = 1, IReadOnlyList<InfluenceRule>? Rules = null);
}
