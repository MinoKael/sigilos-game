using System.Collections.Generic;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Uma invocação escalada para a luta, com o que ela cresceu e as runas que usa.
	/// <see cref="SkillLevels"/> segue a ordem de <see cref="SummonDefinition.AllSkills"/>.
	/// </summary>
	public sealed record TeamMember(SummonDefinition Summon, int Stars, int Level, bool Awakened, IReadOnlyList<int> SkillLevels, IReadOnlyList<Rune> Runes);
}
