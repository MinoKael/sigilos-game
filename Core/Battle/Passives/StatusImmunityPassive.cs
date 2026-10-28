using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Imunidade a efeitos de status: os de <see cref="PassiveDefinition.Statuses"/> nunca pegam no dono, de quem vierem,
	/// e a tela mostra "Imune". Vale a luta inteira, até o Esquecimento calar a Passiva.
	/// </summary>
	internal sealed class StatusImmunityPassive : EffectPassive
	{
		public StatusImmunityPassive(PassiveDefinition passive) : base(passive) { }

		public override bool BlocksStatus(StatusKind status) => Passive.Statuses.Contains(status);
	}
}
