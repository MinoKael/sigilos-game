namespace Sigilos.Core.Content
{
	/// <summary>Uma das três Explorações que se revezam a cada mês: o nome e o conjunto de desafios dela.</summary>
	public sealed record ExplorationVariation
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";
	}
}
