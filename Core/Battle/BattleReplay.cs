using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Battle
{
	/// <summary>Uma decisão do jogador numa luta jogada: a habilidade e o alvo (a posição entre aliados e inimigos; -1 sem alvo).</summary>
	public sealed record LoggedAction(int Skill, int Target);

	/// <summary>
	/// Refaz uma luta jogada: o servidor sorteia a semente, o jogo joga e manda as decisões, e o servidor roda a luta de novo
	/// com elas. Qualquer lista de decisões é uma partida legal (decisão inválida ou faltando vira o piloto automático), então
	/// o jogador só escolhe como jogar; a semente, a equipe e o resultado são do servidor.
	/// </summary>
	public static class BattleReplay
	{
		public static LoggedAction Log(BattleSession session, UnitAction action) =>
			new(action.Skill, action.Target == null ? -1 : Units(session).IndexOf(action.Target));

		public static bool Run(BattleSession session, IReadOnlyList<LoggedAction>? log, bool focusBoss, List<BattleEvent>? events = null)
		{
			events?.AddRange(session.Start());
			var next = 0;
			while (!session.IsOver)
			{
				var turn = session.BeginTurn();
				events?.AddRange(turn.Events);
				if (!turn.NeedsDecision)
					continue;

				var actor = turn.Actor;
				var action = actor.Side == Side.Allies && log != null && next < log.Count
					? Resolve(session, actor, log[next++], focusBoss)
					: AutoPilot.For(session, actor, focusBoss);
				var acted = session.Act(action);
				events?.AddRange(acted);
			}

			return session.Victory == true;
		}

		private static UnitAction Resolve(BattleSession session, BattleUnit actor, LoggedAction entry, bool focusBoss)
		{
			var fallback = AutoPilot.For(session, actor, focusBoss);
			if (entry.Skill < 0 || entry.Skill >= actor.Skills.Count || !actor.IsReady(entry.Skill))
				return fallback;

			var skill = actor.Skill(entry.Skill);
			var target = entry.Target >= 0 && entry.Target < Units(session).Count ? Units(session)[entry.Target] : null;
			var allowed = skill.NeedsAlly ? session.ChoosableAllies(actor) : session.ChoosableTargets(actor);
			if (skill.NeedsAlly && !allowed.Any())
				return fallback;

			if (target != null && !allowed.Contains(target))
				return new UnitAction(entry.Skill, AutoPilot.For(session, actor, focusBoss).Target);

			return new UnitAction(entry.Skill, target ?? (skill.NeedsTarget || skill.NeedsAlly ? fallback.Target : null));
		}

		private static List<BattleUnit> Units(BattleSession session) => session.Allies.Concat(session.Enemies).ToList();
	}
}
