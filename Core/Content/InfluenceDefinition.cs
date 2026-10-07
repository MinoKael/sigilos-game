using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// A Influência de uma constelação: a mecânica que ela impõe à luta, a mesma nas três Explorações
	/// (o que gira a cada mês são os inimigos e o guardião). Um nome e as regras
	/// (<see cref="InfluenceRule"/>); a explicação escrita, com o que fazer contra ela, mora nos textos
	/// (<c>exploration.influence.{id da constelação}</c>).
	/// </summary>
	public sealed record InfluenceDefinition
	{
		public string Name { get; init; } = "";

		public IReadOnlyList<InfluenceRule> Rules { get; init; } = new List<InfluenceRule>();
	}
}
