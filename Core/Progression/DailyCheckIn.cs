using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O check-in diário: um ciclo de <see cref="CheckInDay.Cycle"/> dias contados por entradas. O dia avança na primeira
	/// entrada de cada dia UTC e o prêmio dele chega por carta (<see cref="Letters"/>, com prazo de sete dias); faltar
	/// dias não tira nada nem volta o ciclo, só adia o próximo prêmio. Os prêmios estão em Data/checkin.json, e quem
	/// executa é o servidor (<see cref="ServerActionKind.CheckIn"/>).
	/// </summary>
	public static class DailyCheckIn
	{
		/// <summary>O dia UTC de <paramref name="now"/> ainda não contou: dá para fazer o check-in.</summary>
		public static bool Due(PlayerState player, GameDatabase database, DateTime now) =>
			database.CheckIn.Count > 0 && player.CheckIn.LastDate != now.Date;

		/// <summary>O dia que o próximo check-in conta: depois do último, ou o primeiro depois de fechar o ciclo.</summary>
		public static int NextDay(PlayerState player) => player.CheckIn.Day % CheckInDay.Cycle + 1;

		/// <summary>
		/// Conta o dia de hoje e manda a carta dele; também tira do save as cartas vencidas (<see cref="Letters.Prune"/>: a conta
		/// no servidor só passa por <see cref="Account.Open"/> uma vez, então é a entrada do dia que mantém o correio em dia).
		/// Nulo, sem mudar nada, se hoje já contou ou não há prêmios nos dados.
		/// </summary>
		public static CheckInDay? Open(PlayerState player, GameDatabase database, DateTime now)
		{
			if (!Due(player, database, now) || Of(database, NextDay(player)) is not { } day)
				return null;

			Letters.Prune(player, now);
			player.CheckIn.Day = day.Day;
			player.CheckIn.LastDate = now.Date;
			Letters.Send(player, Letters.CheckIn, now, day.Rewards, day.Day.ToString());
			return day;
		}

		/// <summary>O dia do ciclo com este número.</summary>
		public static CheckInDay? Of(GameDatabase database, int day) => database.CheckIn.FirstOrDefault(d => d.Day == day);

		/// <summary>
		/// O próximo prêmio grande depois do dia em que a conta está (o do ciclo seguinte, se este já passou) e quantos
		/// check-ins faltam para ele. Nulo se os dados não têm dia grande.
		/// </summary>
		public static (CheckInDay Day, int Away)? NextBig(PlayerState player, GameDatabase database)
		{
			var current = player.CheckIn.Day;
			var big = database.CheckIn.Where(d => d.Big).OrderBy(d => d.Day).ToList();
			if (big.Count == 0)
				return null;

			var next = big.FirstOrDefault(d => d.Day > current) ?? big[0];
			return (next, next.Day > current ? next.Day - current : next.Day - current + CheckInDay.Cycle);
		}
	}
}
