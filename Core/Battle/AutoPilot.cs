using System;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// O automático (GDD, seção 7). O jogador não controla a IA passo a passo: escolhe a Postura de
	/// Éter e a ordem do Grimório, e estas regras simples fazem o resto.
	///
	/// - Invocação: usa o Glifo quando está pronto; aprimora se o Éter que sobra cobre a reserva da
	///   postura; mira com vantagem elemental e, no empate, no mais ferido.
	/// - Conjurador: lança a primeira página da ordem que puder pagar; na postura Econômica, guarda
	///   para o Círculo III e só gasta abaixo disso quando canalizar desperdiçaria Éter.
	/// - Inimigo: usa o Glifo quando está pronto e mira ao acaso (com a semente da luta).
	/// </summary>
	public static class AutoPilot
	{
		public static UnitAction ForAlly(BattleSession session, BattleUnit actor, Posture posture)
		{
			var slot = actor.IsGlyphReady ? SkillSlot.Glyph : SkillSlot.Basic;
			var skill = actor.Skill(slot);
			var enhance = session.CanEnhance(actor, skill) && session.Ether - skill.EnhanceCost >= Reserve(session, posture);
			var target = skill.NeedsTarget ? BestTarget(session, actor) : null;
			return new UnitAction(slot, enhance, target);
		}

		public static UnitAction ForEnemy(BattleSession session, BattleUnit actor)
		{
			var slot = actor.IsGlyphReady ? SkillSlot.Glyph : SkillSlot.Basic;
			var choosable = session.ChoosableTargets(actor);
			var target = choosable.Count == 0 ? null : choosable[session.Random.Next(choosable.Count)];
			return new UnitAction(slot, false, target);
		}

		public static ConjurerAction ForConjurer(BattleSession session, Posture posture)
		{
			var castable = session.Conjurer.Pages.Where(session.CanCast).ToList();
			if (castable.Count == 0)
				return ConjurerAction.Channel;

			var page = castable[0];
			if (posture == Posture.Economic)
			{
				var savingFor = session.Conjurer.Pages.Any(p => p.Resonant && p.Page.Circle == 3);
				var channelWouldOverflow = session.Ether + session.Conjurer.Definition.ChannelGain > BattleRules.MaxEther;
				var third = castable.FirstOrDefault(p => p.Page.Circle == 3);

				if (third != null)
					page = third;
				else if (savingFor && !channelWouldOverflow)
					return ConjurerAction.Channel;
			}

			var target = page.NeedsTarget ? BestTarget(session, session.Conjurer) : null;
			return new ConjurerAction(page, target);
		}

		/// <summary>Éter que a postura quer ver sobrando depois de um aprimoramento.</summary>
		private static int Reserve(BattleSession session, Posture posture)
		{
			var costliest = session.Conjurer.Pages.Where(p => p.Resonant).Select(p => p.Cost).DefaultIfEmpty(0).Max();
			return posture switch
			{
				Posture.Aggressive => 0,
				Posture.Balanced => (int)Math.Ceiling(costliest / 2.0),
				_ => costliest,
			};
		}

		private static BattleUnit? BestTarget(BattleSession session, ITurnTaker actor)
		{
			var element = (actor as BattleUnit)?.Element;
			return session.ChoosableTargets(actor)
				.OrderByDescending(u => element is { } e ? ElementChart.Multiplier(e, u.Element) : 1)
				.ThenBy(u => u.HealthFraction)
				.FirstOrDefault();
		}
	}
}
