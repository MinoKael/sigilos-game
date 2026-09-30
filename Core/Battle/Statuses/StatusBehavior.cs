using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// A estratégia de um efeito de status: o que o <see cref="StatusKind"/> faz enquanto está na
	/// unidade. Responde aos momentos de toda regra (<see cref="UnitBehavior"/>) e diz se o efeito é
	/// negativo e quantas cópias cabem no mesmo alvo. A duração, o valor e quem pôs ficam no
	/// <see cref="StatusEffect"/> que cada método recebe.
	///
	/// Efeito novo: o nome em <see cref="StatusKind"/>, a estratégia (uma das prontas — <see cref="StatChange"/>
	/// e <see cref="DamageOverTime"/> — ou uma classe nova), a linha em <see cref="StatusBehaviors"/>, os
	/// textos em Data/texts ("effect.Nome") e o símbolo em Assets/Effects.
	/// </summary>
	internal abstract class StatusBehavior : UnitBehavior
	{
		/// <summary>Efeito negativo: a Resistência do alvo pode barrar, a Imunidade barra sempre e a Purificação remove.</summary>
		public virtual bool Harmful => false;

		/// <summary>
		/// Quantas cópias cabem no mesmo alvo. O comum é uma só: aplicar de novo renova a duração. Com
		/// mais (Veneno), cada aplicação entra como uma cópia até o limite, e daí em diante renova a primeira.
		/// </summary>
		public virtual int MaxStacks => 1;
	}
}
