using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// A Passiva só de condição e ações: não dispara efeitos. O que ela faz (obrigar a mirar, revidar, não acordar
	/// quem dorme) está na base (<see cref="EffectPassive"/>), guiado por <see cref="PassiveDefinition.Condition"/>,
	/// <see cref="PassiveDefinition.Actions"/> e a camada do parceiro.
	/// </summary>
	internal sealed class ConditionalPassive : EffectPassive
	{
		public ConditionalPassive(PassiveDefinition passive) : base(passive) { }
	}
}
