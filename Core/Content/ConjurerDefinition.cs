using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Um Conjurador (Data/conjurers.json). Fica fora de campo, não tem Vida e entra na barra de
	/// Ímpeto como qualquer unidade. O MVP só tem o Erudito.
	/// </summary>
	public sealed record ConjurerDefinition
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";
		public string Image { get; init; } = "";
		public double Speed { get; init; }

		/// <summary>Poder no nível 60. Escala as páginas como o Ataque escala as habilidades.</summary>
		public double Power { get; init; }

		public int PageSlots { get; init; }

		/// <summary>Éter ganho ao canalizar.</summary>
		public int ChannelGain { get; init; }

		/// <summary>Desconto de custo por Círculo: {"2": 1} deixa o Círculo II 1 Éter mais barato.</summary>
		public IReadOnlyDictionary<int, int> CircleDiscounts { get; init; } = new Dictionary<int, int>();
	}
}
