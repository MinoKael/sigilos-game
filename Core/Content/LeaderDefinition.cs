namespace Sigilos.Core.Content
{
	/// <summary>
	/// Liderança: vale para o time inteiro quando a invocação é a primeira da lista. Como em Summoners
	/// War, age sobre o atributo de base: em Vida, Ataque, Defesa e Velocidade <see cref="Value"/> é a
	/// fração da base somada (0,15 = +15%); em Crítico, Resistência, Precisão... soma direto.
	/// </summary>
	public sealed record LeaderDefinition
	{
		public Stat Stat { get; init; }
		public double Value { get; init; }
	}
}
