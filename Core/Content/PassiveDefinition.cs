namespace Sigilos.Core.Content
{
	/// <summary>
	/// A regra de uma habilidade passiva. <see cref="Value"/> é o número dela (0,15 = 15%);
	/// <see cref="AwakenedValue"/>, quando existe, é o número depois do Despertar.
	/// </summary>
	public sealed record PassiveDefinition
	{
		public PassiveKind Kind { get; init; }
		public double Value { get; init; }
		public double AwakenedValue { get; init; }
		public double ValueFor(bool awakened) => awakened && AwakenedValue > 0 ? AwakenedValue : Value;
	}
}
