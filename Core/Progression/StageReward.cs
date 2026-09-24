using System.Collections.Generic;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O que uma vitória de fase entregou. <see cref="LevelUps"/> lista as invocações do time que
	/// subiram de nível com a experiência da luta.
	/// </summary>
	public sealed record StageReward(
		int Scrolls,
		int Essence,
		int Dust,
		int Experience,
		bool FirstClear,
		Rune? Rune,
		IReadOnlyList<string> LevelUps);
}
