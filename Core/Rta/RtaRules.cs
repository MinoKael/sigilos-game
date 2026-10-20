using System.Collections.Generic;

namespace Sigilos.Core.Rta
{
	/// <summary>Quem é quem numa partida da Arena do Véu: o primeiro a escolher luta como <c>Allies</c> da sessão, o segundo como <c>Enemies</c>.</summary>
	public enum RtaSeat
	{
		First,
		Second,
	}

	/// <summary>
	/// As regras fixas da Arena do Véu, o duelo em tempo real entre dois jogadores: a ordem das escolhas, os
	/// relógios e a pressão que cresce com a luta. Os pontos (Áster) e a loja moram em Data/arena.json.
	/// </summary>
	public static class RtaRules
	{
		/// <summary>Quantos cada um leva para o banimento, e quantos lutam depois dele.</summary>
		public const int DraftSize = 5;

		public const int TeamSize = 4;

		/// <summary>Invocações diferentes que a conta precisa ter para entrar na fila: o suficiente para nunca faltar escolha, mesmo com o rival pegando as dele.</summary>
		public const int MinPool = DraftSize * 2;

		/// <summary>O máximo de invocações que a conta manda ao entrar (as mais fortes): limita o tamanho da mensagem.</summary>
		public const int MaxPool = 40;

		/// <summary>A ordem das escolhas: o primeiro pega 1, o segundo 2, o primeiro 2, o segundo 2, o primeiro 2, o segundo 1.</summary>
		public static readonly IReadOnlyList<(RtaSeat Seat, int Count)> PickOrder = new List<(RtaSeat, int)>
		{
			(RtaSeat.First, 1),
			(RtaSeat.Second, 2),
			(RtaSeat.First, 2),
			(RtaSeat.Second, 2),
			(RtaSeat.First, 2),
			(RtaSeat.Second, 1),
		};

		/// <summary>Segundos para escolher, banir ou indicar a Líder; estourou, o automático escolhe e o próximo prazo cai pela metade.</summary>
		public const int DraftSeconds = 30;

		/// <summary>Segundos para decidir um turno de luta.</summary>
		public const int TurnSeconds = 15;

		/// <summary>O menor prazo a que a metade chega.</summary>
		public const int MinSeconds = 3;

		/// <summary>Turnos jogados até a primeira pressão, e a cada quantos vem a seguinte.</summary>
		public const int PressureStart = 20;

		public const int PressureEvery = 15;

		/// <summary>O que cada pressão soma de Ataque e tira da Vida máxima (fração).</summary>
		public const double PressureStep = 0.10;
	}
}
