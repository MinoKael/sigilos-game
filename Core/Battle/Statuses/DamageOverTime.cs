namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Dano a cada turno: no começo do turno do dono, cada cópia tira uma fração da Vida máxima dele,
	/// sem passar por Defesa nem escudo (no chefe, só <see cref="BattleRules.BossAfflictionShare"/> disso). É
	/// a Aflição, que acumula até o limite de efeitos do monstro.
	/// </summary>
	internal sealed class DamageOverTime : StatusBehavior
	{
		private readonly double _fraction;

		public DamageOverTime(double fraction, int maxStacks)
		{
			_fraction = fraction;
			MaxStacks = maxStacks;
		}

		public override bool Harmful => true;

		public override int MaxStacks { get; }

		public override void OnTurnStart(UnitRule rule, EffectResolver resolver) =>
			resolver.Wound(rule.Owner, rule.Owner.MaxHealth * _fraction * (rule.Owner.IsBoss ? BattleRules.BossAfflictionShare : 1));
	}
}
