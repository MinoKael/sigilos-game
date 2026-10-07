using System.Collections.Generic;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Roda uma luta inteira no automático, sem tela. É a Batalha automática (de 1 a
	/// <see cref="MaxRuns"/> lutas seguidas, <see cref="RepeatRuns"/> de início) e o simulador de
	/// balanceamento dos testes.
	/// </summary>
	public static class AutoBattle
	{
		/// <summary>Quantas lutas a Batalha automática sugere de uma vez; o jogador escolhe outro número.</summary>
		public const int RepeatRuns = 30;

		/// <summary>O máximo de lutas seguidas numa Batalha automática.</summary>
		public const int MaxRuns = 30;

		/// <summary>
		/// Devolve verdadeiro na vitória. Com <paramref name="log"/>, guarda todos os eventos da luta: a
		/// Batalha automática mede com eles quanto a luta levaria na tela. Com <paramref name="focusBoss"/>,
		/// a equipe mira o chefe (a escolha do jogador na pausa da luta); o simulador luta sem.
		/// </summary>
		public static bool Run(BattleSession session, List<BattleEvent>? log = null, bool focusBoss = false)
		{
			var events = session.Start();
			log?.AddRange(events);
			while (!session.IsOver)
				Turn(session, log, focusBoss);

			return session.Victory == true;
		}

		/// <summary>
		/// Um turno no automático: o começo dele e, se a unidade age, a ação. A luta vista da Batalha
		/// automática anda por aqui, um turno de cada vez, e decide igual a <see cref="Run"/>.
		/// </summary>
		public static void Turn(BattleSession session, List<BattleEvent>? log, bool focusBoss)
		{
			var turn = session.BeginTurn();
			log?.AddRange(turn.Events);
			if (turn.NeedsDecision)
			{
				var acted = session.Act(AutoPilot.For(session, turn.Actor, focusBoss));
				log?.AddRange(acted);
			}
		}
	}
}
