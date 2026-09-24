namespace Sigilos.Core.Content
{
	/// <summary>
	/// O Despertar de uma variante (GDD, seções 8 e 10): o nome próprio que ela ganha, como os heróis de
	/// Epic Seven, e o atributo extra que vem junto, como em Summoners War. O aumento geral de
	/// atributos é o mesmo para todos (Core/Progression/Awakening.cs).
	/// </summary>
	public sealed record AwakeningDefinition
	{
		public string Name { get; init; } = "";
		public Stat Stat { get; init; }
		public double Value { get; init; }
	}
}
