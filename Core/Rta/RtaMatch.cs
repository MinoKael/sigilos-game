using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Core.Rta
{
	/// <summary>
	/// A escolha de um turno como viaja pela rede: a habilidade e o alvo por lado e posição nas listas da sessão
	/// (nunca por objeto). O primeiro a escolher é sempre o lado <c>Allies</c>, nas duas pontas.
	/// </summary>
	public sealed record RtaDecision(int Skill, int TargetSide = -1, int TargetIndex = -1);

	/// <summary>
	/// A luta da Arena do Véu: uma sessão de duelo comum e a pressão que cresce com os turnos jogados. O servidor
	/// e os dois jogadores montam a mesma partida (mesmas equipes, mesma semente) e aplicam as mesmas decisões
	/// na mesma ordem, então chegam ao mesmo <see cref="Hash"/> a cada turno. Quem decide é de fora: aqui só
	/// se avança a luta.
	/// </summary>
	public sealed class RtaMatch
	{
		public RtaMatch(GameDatabase database, BattleTeam first, BattleTeam second, int seed)
		{
			Session = BattleFactory.CreateDuel(database, first, second, seed);
		}

		public BattleSession Session { get; }

		/// <summary>Turnos decididos até agora (conta os extras).</summary>
		public int Turns { get; private set; }

		/// <summary>Quantas pressões já vieram.</summary>
		public int Pressure { get; private set; }

		public bool IsOver => Session.IsOver;

		/// <summary>Quem venceu; nulo enquanto a luta corre e no empate (<see cref="Draw"/>).</summary>
		public RtaSeat? Winner => !Session.IsOver || Session.TimedOut ? null : Session.Victory == true ? RtaSeat.First : RtaSeat.Second;

		/// <summary>As rodadas acabaram com gente de pé dos dois lados.</summary>
		public bool Draw => Session.IsOver && Session.TimedOut;

		public IReadOnlyList<BattleEvent> Start() => Session.Start();

		/// <summary>
		/// Avança até o próximo turno que pede decisão (ou o fim). Os turnos pulados e os eventos de caminho
		/// vêm juntos em <paramref name="events"/>; devolve o jogador que decide, ou nulo se a luta acabou.
		/// </summary>
		public BattleUnit? Next(List<BattleEvent> events)
		{
			while (!Session.IsOver)
			{
				ApplyPressure();
				var turn = Session.BeginTurn();
				events.AddRange(turn.Events);
				if (turn.NeedsDecision)
					return turn.Actor;
			}

			return null;
		}

		public static RtaSeat SeatOf(BattleUnit unit) => unit.Side == Side.Allies ? RtaSeat.First : RtaSeat.Second;

		/// <summary>Aplica a decisão do turno. Pedido que não existe (alvo fora da lista, habilidade em recarga) vira o que a sessão já faz com ele.</summary>
		public IReadOnlyList<BattleEvent> Act(RtaDecision decision)
		{
			var actor = Session.Current ?? throw new InvalidOperationException("Não é turno de ninguém.");
			var events = Session.Act(new UnitAction(decision.Skill, TargetOf(actor, decision)));
			Turns++;
			return events;
		}

		/// <summary>
		/// A decisão do automático para quem está agindo (o prazo estourou ou o jogador saiu). Sempre o piloto dos aliados,
		/// dos dois lados: o dos inimigos sorteia o alvo com a semente da luta, e isso só o servidor faria, desviando a
		/// cópia de cada jogador.
		/// </summary>
		public RtaDecision Automatic(BattleUnit actor) => Describe(AutoPilot.ForAlly(Session, actor));

		public RtaDecision Describe(UnitAction action) => action.Target is not { } target
			? new RtaDecision(action.Skill)
			: target.Side == Side.Allies
				? new RtaDecision(action.Skill, 0, Session.Allies.ToList().IndexOf(target))
				: new RtaDecision(action.Skill, 1, Session.Enemies.ToList().IndexOf(target));

		private BattleUnit? TargetOf(BattleUnit actor, RtaDecision decision)
		{
			if (decision.TargetSide is not (0 or 1))
				return null;

			var list = decision.TargetSide == 0 ? Session.Allies : Session.Enemies;
			if (decision.TargetIndex < 0 || decision.TargetIndex >= list.Count)
				return null;

			var unit = list[decision.TargetIndex];
			var allowed = unit.Side == actor.Side ? Session.ChoosableAllies(actor) : Session.ChoosableTargets(actor);
			return allowed.Contains(unit) ? unit : null;
		}

		/// <summary>
		/// Depois de 20 turnos, e a cada 15, todos ganham 10% de Ataque e perdem 10% da Vida máxima, empilhando.
		/// Ninguém cai por isso: a Vida mínima é 1. Os eventos saem junto do próximo turno.
		/// </summary>
		private void ApplyPressure()
		{
			while (Turns >= RtaRules.PressureStart + Pressure * RtaRules.PressureEvery)
			{
				Pressure++;
				foreach (var unit in Session.Allies.Concat(Session.Enemies).Where(u => u.IsAlive))
				{
					var amount = unit.MaxHealth * RtaRules.PressureStep;
					unit.HealthDestroyed += amount;
					unit.AttackPressure += RtaRules.PressureStep;
					unit.Health = Math.Clamp(unit.Health, 1, Math.Max(1, unit.MaxHealth));
					Session.Emit(new MaxHealthReduced(unit, (int)amount, unit.Health, unit.MaxHealth));
				}
			}
		}

		/// <summary>
		/// A impressão digital da luta neste ponto: relógio, Vida, Ímpeto, recargas e efeitos de cada unidade, em
		/// números inteiros (duas casas) para um arredondamento diferente entre máquinas não separar os dois lados.
		/// </summary>
		public string Hash()
		{
			ulong hash = 14695981039346656037UL;

			void Add(long value)
			{
				for (var i = 0; i < 8; i++)
				{
					hash ^= (byte)(value >> (i * 8));
					hash *= 1099511628211UL;
				}
			}

			Add(Turns);
			Add((long)Math.Round(Session.Time * 100));
			foreach (var unit in Session.Allies.Concat(Session.Enemies))
			{
				Add((long)Math.Round(unit.Health * 100));
				Add((long)Math.Round(unit.Impeto * 100));
				Add(unit.IsAlive ? 1 : 0);
				Add(unit.Statuses.Count);
				for (var i = 0; i < unit.Skills.Count; i++)
					Add(unit.Cooldown(i));
			}

			return hash.ToString("x16");
		}
	}
}
