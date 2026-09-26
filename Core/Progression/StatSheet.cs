using System;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// A ficha de uma invocação mostra: a base (estrelas, nível, Despertar)
	/// e o que as runas somam, separados. A batalha usa o <see cref="TotalWith"/>.
	/// </summary>
	public sealed record StatSheet(StatBlock Base, RuneBonus Runes)
	{
		public StatBlock Total => Base.Plus(Runes.Stats);

		/// <summary>
		/// O total com a Liderança, pela fórmula de: atributo = runas + base × (1 + líder).
		/// A Liderança nunca multiplica o que veio das runas.
		/// </summary>
		public StatBlock TotalWith(LeaderDefinition? leader)
		{
			if (leader == null)
				return Total;

			var bonus = StatBlock.IsAbsolute(leader.Stat)
				? Math.Ceiling(Base.Get(leader.Stat) * leader.Value - 1e-9)
				: leader.Value;
			return Total.With(leader.Stat, Total.Get(leader.Stat) + bonus);
		}
	}
}
