using System.Linq;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// O automático (GDD, seção 7): usa a habilidade pronta mais forte (a de maior número, fora da
	/// recarga) e mira com vantagem elemental e, no empate, no mais ferido.
	///
	/// Inimigos escolhem a habilidade do mesmo jeito e miram ao acaso (com a semente da luta).
	/// </summary>
	public static class AutoPilot
	{
		public static UnitAction ForAlly(BattleSession session, BattleUnit actor)
		{
			var skill = Strongest(actor);
			var target = actor.Skill(skill).NeedsTarget ? BestTarget(session, actor) : null;
			return new UnitAction(skill, target);
		}

		public static UnitAction ForEnemy(BattleSession session, BattleUnit actor)
		{
			var skill = Strongest(actor);
			var choosable = session.ChoosableTargets(actor);
			var target = choosable.Count == 0 ? null : choosable[session.Random.Next(choosable.Count)];
			return new UnitAction(skill, target);
		}

		public static UnitAction For(BattleSession session, BattleUnit actor) =>
			actor.Side == Side.Allies ? ForAlly(session, actor) : ForEnemy(session, actor);

		/// <summary>A habilidade pronta de maior índice; a básica está sempre pronta.</summary>
		private static int Strongest(BattleUnit actor) =>
			Enumerable.Range(0, actor.Skills.Count).Reverse().First(actor.IsReady);

		private static BattleUnit? BestTarget(BattleSession session, BattleUnit actor) => session
			.ChoosableTargets(actor)
			.OrderByDescending(u => ElementChart.Multiplier(actor.Element, u.Element))
			.ThenBy(u => u.HealthFraction)
			.FirstOrDefault();
	}
}
