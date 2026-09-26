using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Summoning
{
	/// <summary>
	/// Uma invocação saída do ritual: a cópia nova (sempre nível 1 e sem Despertar) e se é a primeira
	/// daquela variante na conta. Se a coleção estava cheia, <see cref="OwnedSummon.Stored"/> diz que
	/// ela foi para o Baú.
	/// </summary>
	public sealed record SummonResult(SummonDefinition Summon, OwnedSummon Monster, bool FirstCopy);
}
