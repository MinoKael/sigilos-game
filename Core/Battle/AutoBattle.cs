namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Roda uma luta inteira no automático, sem tela. É o botão Resolver (GDD, seção 7) e o
	/// simulador de balanceamento dos testes.
	/// </summary>
	public static class AutoBattle
	{
		/// <summary>Devolve verdadeiro na vitória.</summary>
		public static bool Run(BattleSession session)
		{
			session.Start();
			while (!session.IsOver)
			{
				var turn = session.BeginTurn();
				if (turn.NeedsDecision)
					session.Act(AutoPilot.For(session, turn.Actor));
			}

			return session.Victory == true;
		}
	}
}
