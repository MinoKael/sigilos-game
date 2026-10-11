namespace Sigilos.Core.Content
{
	/// <summary>
	/// O que a constelação paga na primeira vitória de cada mês. É sempre o mesmo, qualquer que seja a
	/// Exploração do mês: a dificuldade gira, a recompensa não.
	/// </summary>
	public sealed record ConstellationReward
	{
		public int Essence { get; init; }
		public int Gold { get; init; }

		/// <summary>Pergaminhos Místicos.</summary>
		public int Scrolls { get; init; }

		/// <summary>Experiência de cada monstro da equipe (e da conta).</summary>
		public int Experience { get; init; }

		/// <summary>Marco: Pergaminhos Lendários.</summary>
		public int Legendary { get; init; }

		/// <summary>Marco: Pergaminhos de Luz e Trevas.</summary>
		public int LightDark { get; init; }

		/// <summary>Marco: Núcleos de Infusão.</summary>
		public int Cores { get; init; }
	}
}
