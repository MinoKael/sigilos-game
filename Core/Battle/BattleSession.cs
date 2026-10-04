using System;
using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Uma luta do começo ao fim: barra de Ímpeto, recargas, ondas, vitória e derrota (GDD, seção 7).
	/// Não sabe que existe tela nem quem decide: a cada turno devolve quem age, recebe a decisão
	/// (do jogador ou do <see cref="AutoPilot"/>) e devolve a lista de <see cref="BattleEvent"/>.
	///
	/// Também não sabe o que cada efeito, Passiva ou conjunto de runas faz: nos momentos da luta ela
	/// avisa as regras em vigor em cada unidade (<see cref="UnitBehavior"/>), e o que acontece dentro de
	/// uma habilidade é do <see cref="EffectResolver"/>.
	///
	/// Determinística: com a mesma semente e as mesmas decisões, a mesma luta. É isso que deixa o
	/// simulador rodar milhares de lutas sem gráfico para balancear.
	///
	/// Uso:
	///   session.Start();
	///   while (!session.IsOver) {
	///       var turn = session.BeginTurn();
	///       if (turn.NeedsDecision) session.Act(decisão);
	///   }
	/// </summary>
	public sealed class BattleSession
	{
		private readonly List<BattleUnit> _allies;
		private readonly IReadOnlyList<IReadOnlyList<BattleUnit>> _waves;
		private readonly EffectResolver _effects;
		private readonly List<BattleEvent> _pending = new();
		private int _waveIndex;

		public BattleSession(IReadOnlyList<BattleUnit> allies, IReadOnlyList<IReadOnlyList<BattleUnit>> waves, int seed)
		{
			_allies = allies.ToList();
			_waves = waves;
			Random = new Random(seed);
			_effects = new EffectResolver(this);
		}

		public IReadOnlyList<BattleUnit> Allies => _allies;

		/// <summary>Os inimigos da onda atual.</summary>
		public IReadOnlyList<BattleUnit> Enemies => _waves[_waveIndex];

		/// <summary>Alguma onda tem chefe (a pausa da luta oferece focar nele).</summary>
		public bool HasBoss => _waves.Any(wave => wave.Any(unit => unit.IsBoss));

		/// <summary>Onda atual, a partir de 1.</summary>
		public int Wave => _waveIndex + 1;

		public int WaveCount => _waves.Count;

		/// <summary>Tempo de luta em rodadas: Velocidade 100 enche a barra em 1,0.</summary>
		public double Time { get; private set; }

		/// <summary>
		/// A rodada 1 vai do tempo 0 ao 1,0, inclusive: quem tem Velocidade 100 age no fim dela. A folga
		/// de 1e-6 absorve o erro de soma de frações (4,0000001 ainda é a rodada 4).
		/// </summary>
		public int Round => Math.Max(1, (int)Math.Ceiling(Time - 1e-6));

		/// <summary>Quem está agindo agora.</summary>
		public BattleUnit? Current { get; private set; }

		public bool? Victory { get; private set; }
		public bool IsOver => Victory != null;

		internal Random Random { get; }

		/// <summary>O turno atual é um turno extra: ele não dá outro.</summary>
		internal bool IsExtraTurn { get; private set; }

		public IReadOnlyList<BattleEvent> Start()
		{
			StartWave();
			return Flush();
		}

		/// <summary>Avança a barra até o próximo a agir e resolve o que acontece antes da decisão.</summary>
		public TurnStart BeginTurn()
		{
			if (IsOver)
				throw new InvalidOperationException("A luta já acabou.");

			var unit = AdvanceToNextActor();
			Current = unit;

			if (Round > BattleRules.RoundLimit)
			{
				End(false);
				return new TurnStart(unit, false, Flush());
			}

			Emit(new TurnStarted(unit, Round));

			// O turno extra é gasto aqui, mesmo que a unidade não chegue a agir (atordoada).
			IsExtraTurn = unit.ExtraTurnPending;
			unit.ExtraTurnPending = false;

			if (unit.Reviving)
			{
				_effects.Revive(unit);
				FinishTurn(unit);
				return new TurnStart(unit, false, Flush());
			}

			// Queimadura, Veneno, Bomba, regeneração: tudo o que acontece no começo do turno, mesmo atordoado.
			foreach (var rule in unit.Rules())
			{
				rule.Behavior.OnTurnStart(rule, _effects);
				if (!unit.IsAlive)
				{
					CheckOutcome();
					return new TurnStart(unit, false, Flush());
				}
			}

			if (unit.Any(behavior => behavior.SkipsTurn))
			{
				Emit(new TurnSkipped(unit, unit.Statuses.FirstOrDefault(status => status.Behavior.SkipsTurn)?.Kind));
				FinishTurn(unit);
				return new TurnStart(unit, false, Flush());
			}

			return new TurnStart(unit, true, Flush());
		}

		/// <summary>
		/// A habilidade do turno e a recarga dela. Pedido impossível (habilidade em recarga ou que não
		/// existe) vira a básica.
		/// </summary>
		public IReadOnlyList<BattleEvent> Act(UnitAction action)
		{
			if (IsOver || Current is not { } unit)
				throw new InvalidOperationException("Não é turno de ninguém.");

			var index = unit.IsReady(action.Skill) ? action.Skill : 0;
			var skill = unit.Skill(index);

			Emit(new SkillUsed(unit, skill));
			var cast = _effects.Resolve(unit, skill.Effects, action.Target);
			unit.SetCooldown(index, Math.Max(0, skill.Cooldown - cast.CooldownReduction));

			foreach (var rule in unit.Rules())
				rule.Behavior.AfterAction(rule, _effects);

			FinishTurn(unit);
			return Flush();
		}

		/// <summary>Inimigos que <paramref name="actor"/> pode escolher como alvo agora.</summary>
		public IReadOnlyList<BattleUnit> ChoosableTargets(BattleUnit actor) =>
			Targeting.Choosable(actor, SideOf(actor.Side == Side.Allies ? Side.Enemies : Side.Allies));

		/// <summary>Os próximos a agir, sem mexer na luta. Serve à barra de ordem da tela.</summary>
		public IReadOnlyList<BattleUnit> PredictOrder(int count)
		{
			var takers = TurnTakers().ToList();
			var impeto = takers.ToDictionary(t => t, t => t.Impeto);
			var order = new List<BattleUnit>();

			while (order.Count < count && takers.Count > 0)
			{
				var (next, _) = Step(takers, t => impeto[t], (t, value) => impeto[t] = value);
				order.Add(next);
			}

			return order;
		}

		internal IReadOnlyList<BattleUnit> SideOf(Side side) => side == Side.Allies ? _allies : Enemies;

		internal void Emit(BattleEvent battleEvent) => _pending.Add(battleEvent);

		/// <summary>Em empate age primeiro quem vem antes: aliados, depois inimigos.</summary>
		private IEnumerable<BattleUnit> TurnTakers() => _allies.Concat(Enemies).Where(u => u.CanTakeTurn);

		private BattleUnit AdvanceToNextActor()
		{
			var takers = TurnTakers().ToList();
			var (next, elapsed) = Step(takers, t => t.Impeto, (t, value) => t.Impeto = value);
			Time += elapsed;
			return next;
		}

		/// <summary>
		/// Um passo da barra: acha quem chega a 100 primeiro (t = (100 − I) / VEL, GDD seção 15), avança
		/// todo mundo pelo mesmo tempo e zera o Ímpeto de quem vai agir.
		/// </summary>
		private static (BattleUnit Next, double Elapsed) Step(
			IReadOnlyList<BattleUnit> takers,
			Func<BattleUnit, double> getImpeto,
			Action<BattleUnit, double> setImpeto)
		{
			double TimeToAct(BattleUnit t) => Math.Max(0, (BattleRules.FullImpeto - getImpeto(t)) / t.TurnSpeed);

			var next = takers.MinBy(TimeToAct)!;
			var elapsed = TimeToAct(next);
			foreach (var taker in takers)
				setImpeto(taker, Math.Min(BattleRules.FullImpeto, getImpeto(taker) + taker.TurnSpeed * elapsed));

			setImpeto(next, 0);
			return (next, elapsed);
		}

		/// <summary>
		/// Começo de cada onda: as regras de quem está vivo são avisadas, aliados primeiro. É aqui que os
		/// conjuntos Tenacidade e Baluarte e a Passiva dos Bandidos valem de novo.
		/// </summary>
		private void StartWave()
		{
			Emit(new WaveStarted(Wave, WaveCount, Enemies));

			foreach (var unit in _allies.Where(u => u.IsAlive).Concat(Enemies).ToList())
			{
				foreach (var rule in unit.Rules())
					rule.Behavior.OnWaveStart(rule, _effects);
			}
		}

		private void FinishTurn(BattleUnit unit)
		{
			if (unit.IsAlive)
			{
				foreach (var expired in unit.TickStatuses())
					Emit(new StatusRemoved(unit, expired.Kind));
				unit.TickCooldowns();
			}

			CheckOutcome();
		}

		private void CheckOutcome()
		{
			if (IsOver)
				return;

			if (!_allies.Any(u => u.CanTakeTurn))
			{
				End(false);
				return;
			}

			// Quem caiu para voltar (o Rei Ossudo) segura a onda, como segura o time aliado.
			if (Enemies.Any(u => u.CanTakeTurn))
				return;

			if (_waveIndex + 1 < _waves.Count)
			{
				_waveIndex++;
				StartWave();
			}
			else
			{
				End(true);
			}
		}

		private void End(bool victory)
		{
			Victory = victory;
			Current = null;
			Emit(new BattleEnded(victory));
		}

		private IReadOnlyList<BattleEvent> Flush()
		{
			var events = _pending.ToList();
			_pending.Clear();
			return events;
		}
	}
}
