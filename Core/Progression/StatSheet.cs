using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// A ficha de uma invocação como Summoners War mostra: a base (nível, raridade, Ecos, Despertar)
	/// e o que as runas somam, separados. A batalha usa o <see cref="Total"/>.
	/// </summary>
	public sealed record StatSheet(StatBlock Base, RuneBonus Runes)
	{
		public StatBlock Total => Base.Plus(Runes.Stats);
	}
}
