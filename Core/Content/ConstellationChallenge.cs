using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// O desafio de uma constelação numa das três Explorações: as ondas, com o guardião na última
	/// (<see cref="StageEnemy.Guardian"/> faz de uma invocação o chefe, desperta e com as habilidades no
	/// máximo: as habilidades dela são o desafio).
	/// </summary>
	public sealed record ConstellationChallenge
	{
		public IReadOnlyList<IReadOnlyList<StageEnemy>> Waves { get; init; } = new List<IReadOnlyList<StageEnemy>>();

		/// <summary>Ajuste fino deste desafio sobre a força da constelação (multiplica Vida e Ataque).</summary>
		public double Scale { get; init; } = 1;
	}
}
