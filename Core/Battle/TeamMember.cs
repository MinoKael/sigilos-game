using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>Uma invocação escalada para a luta, com os Ecos que tem.</summary>
	public sealed record TeamMember(SummonDefinition Summon, int Echoes);
}
