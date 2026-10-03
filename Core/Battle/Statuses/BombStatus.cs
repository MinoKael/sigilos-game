namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Bomba: espera a contagem e explode no começo do último turno dela — com 1 turno, no próximo turno
	/// do alvo; com 2, no seguinte. A explosão vale o Ataque de quem pôs vezes
	/// <see cref="BattleRules.BombDamageMultiplier"/>: ignora a Defesa e não tem elemento nem crítico,
	/// mas o escudo absorve e o que mexe no dano recebido pelo alvo (Maldição) conta. Quem sobrevive à
	/// explosão fica atordoado (<see cref="BattleRules.BombStunTurns"/>): a Imunidade barra, a Resistência não.
	/// </summary>
	internal sealed class BombStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override void OnTurnStart(UnitRule rule, EffectResolver resolver)
		{
			if (rule is not StatusEffect { Turns: <= 1 })
				return;

			var target = rule.Owner;
			var attack = rule.Source?.Attack ?? 0;
			resolver.Remove(rule);
			resolver.Wound(target, attack * BattleRules.BombDamageMultiplier * target.DamageTaken(), shielded: true);
			if (!target.IsAlive)
				return;

			resolver.ApplyStatus(rule.Source ?? target, target, Content.StatusKind.Stun, 1, BattleRules.BombStunTurns, resistible: false);
			// A explosão é no começo do turno do alvo: o atordoamento já tira este turno e acaba no fim dele.
			if (target.Find(Content.StatusKind.Stun) is { } stun)
				stun.Fresh = false;
		}
	}
}
