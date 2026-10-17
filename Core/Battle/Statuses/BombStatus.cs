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

		public override bool TriggersWhenCutShort => true;

		public override void OnTurnStart(UnitRule rule, EffectResolver resolver)
		{
			if (rule is StatusEffect { Turns: <= 1 })
				Explode(rule, resolver, atTurnStart: true);
		}

		/// <summary>Contagem encurtada até o fim (Ignição Maestral): explode na hora, no meio do turno de quem encurtou.</summary>
		public override void OnCutShort(UnitRule rule, EffectResolver resolver) => Explode(rule, resolver, atTurnStart: false);

		private static void Explode(UnitRule rule, EffectResolver resolver, bool atTurnStart)
		{
			var target = rule.Owner;
			var attack = rule.Source?.Attack ?? 0;
			resolver.Remove(rule);
			resolver.Wound(target, attack * BattleRules.BombDamageMultiplier * target.DamageTaken(), shielded: true);
			if (!target.IsAlive)
				return;

			resolver.ApplyStatus(rule.Source ?? target, target, Content.StatusKind.Stun, 1, BattleRules.BombStunTurns, resistible: false);
			// No começo do turno do alvo, o atordoamento já tira este turno e acaba no fim dele; no meio do turno de
			// outro, ele pega o próximo turno do alvo.
			if (atTurnStart && target.Find(Content.StatusKind.Stun) is { } stun)
				stun.Fresh = false;
		}
	}
}
