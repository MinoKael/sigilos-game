using System.Text.Json.Serialization;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Quatro números do modelo de atributos (<see cref="StatModel"/>), um por atributo de base. No
	/// perfil de um papel são as fatias do orçamento para Vida, Ataque e Defesa e a Velocidade de base;
	/// num viés (de elemento, de família ou da variante) são os multiplicadores das fatias e a Velocidade
	/// somada. O padrão é o viés neutro: não muda nada.
	/// </summary>
	public sealed record StatWeights
	{
		public static readonly StatWeights Neutral = new();

		[JsonPropertyName("hp")]
		public double Health { get; init; } = 1;

		[JsonPropertyName("atk")]
		public double Attack { get; init; } = 1;

		[JsonPropertyName("def")]
		public double Defense { get; init; } = 1;

		[JsonPropertyName("spd")]
		public double Speed { get; init; }
	}
}
