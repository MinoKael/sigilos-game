using System.Collections.Generic;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;

namespace Sigilos.Core.Rta
{
	/// <summary>
	/// A cópia da luta que cada jogador roda no próprio aparelho: o servidor manda as equipes e a semente (<c>battle</c>)
	/// e depois só as decisões (<c>acted</c>); aqui a mesma <see cref="RtaMatch"/> avança com elas e dá os eventos
	/// para a tela. A impressão digital de cada turno é conferida com a do servidor: se divergir
	/// (<see cref="InSync"/> falso), a tela mostra o aviso e espera o resultado do servidor, que é o que vale.
	/// </summary>
	public sealed class RtaReplica
	{
		private RtaReplica(RtaMatch match) => Match = match;

		public RtaMatch Match { get; }

		/// <summary>Todos os turnos conferidos até agora batem com o servidor.</summary>
		public bool InSync { get; private set; } = true;

		/// <summary>Monta a luta da mensagem <c>battle</c>; nulo se alguma equipe veio sem nenhum integrante válido.</summary>
		public static RtaReplica? From(GameDatabase database, RtaMessage battle)
		{
			var first = battle.TeamFirst == null ? null : ArenaWire.ToBattle(database, battle.TeamFirst);
			var second = battle.TeamSecond == null ? null : ArenaWire.ToBattle(database, battle.TeamSecond);
			return first == null || second == null ? null : new RtaReplica(new RtaMatch(database, first, second, battle.Seed));
		}

		public IReadOnlyList<BattleEvent> Start() => Match.Start();

		/// <summary>Avança até o próximo turno com decisão; devolve quem decide (nulo: acabou) e o que aconteceu no caminho.</summary>
		public (BattleUnit? Actor, IReadOnlyList<BattleEvent> Events) Advance()
		{
			var events = new List<BattleEvent>();
			var actor = Match.Next(events);
			return (actor, events);
		}

		/// <summary>Aplica a decisão que o servidor mandou e confere a impressão digital.</summary>
		public IReadOnlyList<BattleEvent> Apply(RtaMessage acted)
		{
			// Uma decisão que esta cópia não esperava é divergência, não erro: o aviso aparece e vale o resultado do servidor.
			try
			{
				var events = Match.Act(acted.Decision);
				if (acted.Hash != null && acted.Hash != Match.Hash())
					InSync = false;
				return events;
			}
			catch (System.Exception exception) when (exception is System.InvalidOperationException or System.ArgumentException or System.ArgumentOutOfRangeException)
			{
				InSync = false;
				return new List<BattleEvent>();
			}
		}
	}
}
