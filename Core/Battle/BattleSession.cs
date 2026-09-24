using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Uma luta do começo ao fim: barra de Ímpeto, Éter, ondas, vitória e derrota (GDD, seção 7).
	/// Não sabe que existe tela nem quem decide: a cada turno devolve quem age, recebe a decisão
	/// (do jogador ou do <see cref="AutoPilot"/>) e devolve a lista de <see cref="BattleEvent"/>.
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

		/// <summary>Onda atual, a partir de 1.</summary>
		public int Wave => _waveIndex + 1;

		public int WaveCount => _waves.Count;

		public int Ether { get; private set; }

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

		public IReadOnlyList<BattleEvent> Start()
		{
			Emit(new WaveStarted(Wave, WaveCount, Enemies));
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

			if (unit.PendingRebirth)
			{
				unit.PendingRebirth = false;
				unit.Health = Math.Round(unit.MaxHealth * unit.PassiveValue);
				Emit(new Revived(unit));
				FinishTurn(unit);
				return new TurnStart(unit, false, Flush());
			}

			BurnTick(unit);
			if (!unit.IsAlive)
			{
				CheckOutcome();
				return new TurnStart(unit, false, Flush());
			}

			if (unit.Has(StatusKind.Stun))
			{
				Emit(new TurnSkipped(unit));
				FinishTurn(unit);
				return new TurnStart(unit, false, Flush());
			}

			return new TurnStart(unit, true, Flush());
		}

		/// <summary>
		/// A habilidade do turno, o aprimoramento pago com Éter e o ganho de Éter. Pedido impossível
		/// (Glifo em recarga, Éter curto) vira o básico sem aprimoramento.
		/// </summary>
		public IReadOnlyList<BattleEvent> Act(UnitAction action)
		{
			if (IsOver || Current is not { } unit)
				throw new InvalidOperationException("Não é turno de ninguém.");

			var slot = action.Slot == SkillSlot.Glyph && unit.IsGlyphReady ? SkillSlot.Glyph : SkillSlot.Basic;
			var skill = unit.Skill(slot);
			var enhance = action.Enhance && CanEnhance(unit, skill);

			if (enhance)
				ChangeEther(-skill.EnhanceCost);

			Emit(new SkillUsed(unit, skill, enhance));
			_effects.Resolve(unit, skill.EffectsFor(enhance), action.Target);

			if (unit.Side == Side.Allies)
				ChangeEther(slot == SkillSlot.Glyph ? BattleRules.GlyphEtherGain : BattleRules.BasicEtherGain);
			if (slot == SkillSlot.Glyph)
				unit.GlyphCooldown = skill.Cooldown;

			// Conjunto da Porta: chance de agir de novo logo em seguida.
			if (unit.IsAlive && unit.RuneEffects.ExtraTurn > 0 && Random.NextDouble() < unit.RuneEffects.ExtraTurn)
			{
				unit.Impeto = BattleRules.FullImpeto;
				Emit(new ExtraTurn(unit));
			}

			FinishTurn(unit);
			return Flush();
		}

		/// <summary>Só aliados aprimoram: inimigos não usam Éter.</summary>
		public bool CanEnhance(BattleUnit unit, SkillDefinition skill) =>
			unit.Side == Side.Allies && skill.CanEnhance && Ether >= skill.EnhanceCost;

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

		/// <summary>Uma unidade caiu: Éter pelo inimigo, Assinaturas de queda.</summary>
		internal void KnockOut(BattleUnit unit)
		{
			unit.Health = 0;
			unit.Impeto = 0;
			unit.ClearStatuses();
			Emit(new Died(unit));

			if (unit.Side == Side.Enemies)
				ChangeEther(BattleRules.KillEtherGain);

			switch (unit.Passive)
			{
				case { Kind: PassiveKind.ShieldOnDeath }:
					foreach (var ally in unit.Team.Where(u => u.IsAlive))
						_effects.GiveShield(ally, unit.PassiveValue * unit.MaxHealth, BattleRules.DeathShieldTurns);
					break;

				case { Kind: PassiveKind.RebirthOnce } when !unit.RebirthUsed:
					unit.RebirthUsed = true;
					unit.PendingRebirth = true;
					break;
			}
		}

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

		private void BurnTick(BattleUnit unit)
		{
			foreach (var _ in unit.Statuses.Where(s => s.Kind == StatusKind.Burn).ToList())
			{
				var amount = Math.Max(1, Math.Round(unit.MaxHealth * BattleRules.BurnFraction));
				unit.Health = Math.Max(0, unit.Health - amount);
				Emit(new Damaged(unit, (int)amount, 0, false, 1));
				if (!unit.IsAlive)
				{
					KnockOut(unit);
					return;
				}
			}
		}

		private void FinishTurn(BattleUnit unit)
		{
			if (unit.IsAlive)
			{
				foreach (var expired in unit.TickStatuses())
					Emit(new StatusRemoved(unit, expired.Kind));
				if (unit.GlyphCooldown > 0)
					unit.GlyphCooldown--;
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

			if (Enemies.Any(u => u.IsAlive))
				return;

			if (_waveIndex + 1 < _waves.Count)
			{
				_waveIndex++;
				Emit(new WaveStarted(Wave, WaveCount, Enemies));
			}
			else
			{
				End(true);
			}
		}

		private void ChangeEther(int delta)
		{
			var before = Ether;
			Ether = Math.Clamp(Ether + delta, 0, BattleRules.MaxEther);
			if (Ether != before)
				Emit(new EtherChanged(Ether, Ether - before));
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
