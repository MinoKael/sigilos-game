namespace Sigilos.Core.Content
{
	/// <summary>
	/// A Assinatura de uma família. <see cref="Value"/> é o número da regra (0,15 = 15%);
	/// <see cref="AwakenedValue"/> é o mesmo número depois do Despertar ("Assinatura melhorada").
	/// </summary>
	public sealed record PassiveDefinition
	{
		public string Name { get; init; } = "";
		public PassiveKind Kind { get; init; }
		public double Value { get; init; }
		public double AwakenedValue { get; init; }

		public double ValueFor(bool awakened) => awakened ? AwakenedValue : Value;
	}
}
