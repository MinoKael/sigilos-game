using System.Collections.Generic;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O que uma vitória custou (<see cref="Mana"/>) e entregou, na Campanha ou numa Masmorra. <see cref="LevelUps"/> lista os
	/// monstros da equipe (pelo id) que subiram de nível com a experiência da luta;
	/// <see cref="AccountLevels"/> é quantos níveis a conta subiu (cada um com o Ouro dele, fora de
	/// <see cref="Gold"/>).
	/// </summary>
	public sealed record VictoryReward(
		int Mana,
		int Scrolls,
		int Gold,
		int Essence,
		int Experience,
		bool FirstClear,
		Rune? Rune,
		IReadOnlyList<RuneTool> Tools,
		IReadOnlyList<int> LevelUps,
		int AccountLevels);
}
