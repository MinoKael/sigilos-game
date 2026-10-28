using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Um dia do ciclo do check-in (Data/checkin.json): o que a carta daquele dia traz. As chaves são as moedas do correio
	/// (<see cref="Progression.MailItem"/>: "gold", "mana", "scrolls"...). Os dias <see cref="Big"/> são os de prêmio maior,
	/// que o painel do check-in anuncia com antecedência; quem os escolhe são os dados, não o número do dia.
	/// </summary>
	public sealed record CheckInDay
	{
		/// <summary>Quantos dias tem o ciclo: terminado o último, o primeiro volta.</summary>
		public const int Cycle = 28;

		/// <summary>De 1 a <see cref="Cycle"/>.</summary>
		public int Day { get; init; }

		public bool Big { get; init; }

		public Dictionary<string, int> Rewards { get; init; } = new();
	}
}
