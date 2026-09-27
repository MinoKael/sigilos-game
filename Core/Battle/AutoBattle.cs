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
		/// Batalha automática mede com eles quanto a luta levaria na tela.
		/// </summary>
		public static bool Run(BattleSession session, List<BattleEvent>? log = null)
		{
			var events = session.Start();
			log?.AddRange(events);
			while (!session.IsOver)
			{
				var turn = session.BeginTurn();
				log?.AddRange(turn.Events);
				if (turn.NeedsDecision)
				{
					var acted = session.Act(AutoPilot.For(session, turn.Actor));
					log?.AddRange(acted);
				}
			}

			return session.Victory == true;
		}
	}
}
