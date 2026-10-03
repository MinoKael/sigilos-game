namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Silêncio: o dono só usa habilidades sem recarga (a básica, em geral).</summary>
	internal sealed class SilenceStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override bool BlocksCooldownSkills => true;
	}
}
