namespace Sigilos.Core.Content
{
	/// <summary>
	/// O Despertar de uma variante (GDD, seções 8 e 10): o nome próprio que ela ganha, como os bônus (Velocidade, Crítico, Resistência ou
	/// Precisão). Os números são os mesmos para todos (Core/Progression/Awakening.cs).
	/// </summary>
	public sealed record AwakeningDefinition
	{
		public string Name { get; init; } = "";
		public Stat Stat { get; init; }
	}
}
