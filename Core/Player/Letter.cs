using System;
using System.Collections.Generic;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// Uma carta que o próprio jogo manda à conta (prêmio da temporada da Arena, marco de nível, check-in): fica no save,
	/// então chega e é coletada pelas mesmas regras no servidor e no save de teste. Guarda o tipo e os valores do texto,
	/// não o texto, para a carta mudar de língua com o jogo. As regras estão em <see cref="Progression.Letters"/>.
	/// </summary>
	public sealed class Letter
	{
		public int Id { get; set; }

		/// <summary>De onde veio (<see cref="Progression.Letters"/>): escolhe o título e o texto.</summary>
		public string Kind { get; set; } = "";

		/// <summary>Os valores do texto (o grau da temporada, o nível alcançado...).</summary>
		public List<string> Args { get; set; } = new();

		/// <summary>O que ela traz, como nas cartas do servidor: moeda pelo nome ("gold"), presente pela chave ("rune:5").</summary>
		public Dictionary<string, int> Rewards { get; set; } = new();

		public DateTime SentAt { get; set; }

		public DateTime ExpiresAt { get; set; }
	}
}
