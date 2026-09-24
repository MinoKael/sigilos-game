using System.Linq;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// O automático (GDD, seção 7): usa o Glifo quando está pronto e mira com vantagem elemental e, no
	/// empate, no mais ferido. <b>Nunca gasta Éter</b>: o aprimoramento é exclusivo do manual, para que
	/// o automático não fique mais forte com um poder que existe para premiar a decisão do jogador.
	///
	/// Inimigos usam o Glifo quando está pronto e miram ao acaso (com a semente da luta).
	/// </summary>
	public static class AutoPilot
	{
		public static UnitAction ForAlly(BattleSession session, BattleUnit actor)
		{
			var slot = actor.IsGlyphReady ? SkillSlot.Glyph : SkillSlot.Basic;
			var target = actor.Skill(slot).NeedsTarget ? BestTarget(session, actor) : null;
			return new UnitAction(slot, false, target);
		}

		public static UnitAction ForEnemy(BattleSession session, BattleUnit actor)
		{
			var slot = actor.IsGlyphReady ? SkillSlot.Glyph : SkillSlot.Basic;
			var choosable = session.ChoosableTargets(actor);
			var target = choosable.Count == 0 ? null : choosable[session.Random.Next(choosable.Count)];
			return new UnitAction(slot, false, target);
		}

		public static UnitAction For(BattleSession session, BattleUnit actor) =>
			actor.Side == Side.Allies ? ForAlly(session, actor) : ForEnemy(session, actor);

		private static BattleUnit? BestTarget(BattleSession session, BattleUnit actor) => session
			.ChoosableTargets(actor)
			.OrderByDescending(u => ElementChart.Multiplier(actor.Element, u.Element))
			.ThenBy(u => u.HealthFraction)
			.FirstOrDefault();
	}
}
