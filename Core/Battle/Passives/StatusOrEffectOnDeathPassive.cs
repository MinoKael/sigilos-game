using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Quando o dono cai: o escudo dos Cavaleiros. Dispara mesmo dentro de outra Passiva (cada um cai uma
	/// vez só, então não vira laço).
	/// </summary>
	internal sealed class StatusOrEffectOnDeathPassive : EffectPassive
	{
		public StatusOrEffectOnDeathPassive(PassiveDefinition passive) : base(passive) { }

		public override void OnDeath(UnitRule rule, EffectResolver resolver) => Fire(rule, resolver, null, force: true);
	}
}
