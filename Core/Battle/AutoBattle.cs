namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Roda uma luta inteira no automático, sem tela. É o botão Resolver (GDD, seção 7) e o
	/// simulador de balanceamento dos testes.
	/// </summary>
	public static class AutoBattle
	{
		/// <summary>Devolve verdadeiro na vitória.</summary>
		public static bool Run(BattleSession session, Posture posture)
		{
			session.Start();
			while (!session.IsOver)
			{
				var turn = session.BeginTurn();
				if (!turn.NeedsDecision)
					continue;

				switch (turn.Actor)
				{
					case ConjurerSeat:
						session.Act(AutoPilot.ForConjurer(session, posture));
						break;
					case BattleUnit { Side: Side.Allies } ally:
						session.Act(AutoPilot.ForAlly(session, ally, posture));
						break;
					case BattleUnit enemy:
						session.Act(AutoPilot.ForEnemy(session, enemy));
						break;
				}
			}

			return session.Victory == true;
		}
	}
}
