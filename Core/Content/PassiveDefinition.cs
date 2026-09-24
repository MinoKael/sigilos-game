namespace Sigilos.Core.Content
{
	/// <summary>A Assinatura de uma família. <see cref="Value"/> é o número da regra (0,15 = 15%).</summary>
	public sealed record PassiveDefinition
	{
		public string Name { get; init; } = "";
		public PassiveKind Kind { get; init; }
		public double Value { get; init; }
	}
}
