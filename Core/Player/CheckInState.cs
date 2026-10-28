using System;

namespace Sigilos.Core.Player
{
	/// <summary>Onde a conta está no ciclo do check-in (<see cref="Progression.DailyCheckIn"/>).</summary>
	public sealed class CheckInState
	{
		/// <summary>O último dia do ciclo que já virou carta, de 1 a <see cref="Content.CheckInDay.Cycle"/>; 0 = nenhum ainda.</summary>
		public int Day { get; set; }

		/// <summary>O dia (UTC, sem a hora) em que <see cref="Day"/> chegou; nulo antes do primeiro.</summary>
		public DateTime? LastDate { get; set; }
	}
}
