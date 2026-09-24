using System.Collections.Generic;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle
{
	/// <summary>Uma invocação escalada para a luta, com o que ela cresceu e as runas que usa.</summary>
	public sealed record TeamMember(SummonDefinition Summon, int Level, int Echoes, bool Awakened, IReadOnlyList<Rune> Runes);
}
