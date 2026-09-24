using Sigilos.Core.Content;

namespace Sigilos.Core.Summoning
{
	/// <summary>Uma invocação saída do ritual e o que ela virou na coleção.</summary>
	public sealed record SummonResult(SummonDefinition Summon, SummonOutcome Outcome, int Echoes, int Fragments);
}
