namespace Sigilos.Core.Content
{
	/// <summary>
	/// Liderança: vale para o time inteiro quando a invocação é a primeira da lista. Em atributo
	/// absoluto (Vida, Ataque, Defesa, Velocidade) <see cref="Value"/> multiplica; em fração (Crítico,
	/// Foco...) soma.
	/// </summary>
	public sealed record LeaderDefinition
	{
		public Stat Stat { get; init; }
		public double Value { get; init; }
	}
}
