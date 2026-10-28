using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Battle
{
	/// <summary>O que um momento da luta é na tela: evento comum, avanço até o alvo, golpe ou volta ao lugar.</summary>
	public enum BeatKind
	{
		Plain,
		Approach,
		Volley,
		Return,
	}

	/// <summary>
	/// Um momento da luta na tela: os eventos mostrados juntos e quanto a tela espera depois, em segundos
	/// na velocidade 1×. <see cref="Actor"/> é quem avança ou volta; <see cref="Targets"/>, quem ele vai
	/// atingir (o avanço corre até o primeiro, ou até o meio do grupo num golpe em área).
	/// </summary>
	public sealed record Beat(BeatKind Kind, IReadOnlyList<BattleEvent> Events, double Seconds, BattleUnit? Actor = null, IReadOnlyList<BattleUnit>? Targets = null, IReadOnlyList<Attacker>? Group = null);

	/// <summary>Um dos que atacam juntos num ataque em conjunto: quem é e quem atinge neste momento.</summary>
	public sealed record Attacker(BattleUnit Actor, IReadOnlyList<BattleUnit> Targets);

	/// <summary>
	/// O ritmo da luta na tela: quem ataca corre até o alvo, golpeia e volta. A
	/// tela mostra a luta em <see cref="Beats"/>, e a Batalha automática usa a mesma conta para saber
	/// quanto uma luta levaria — assim as duas nunca discordam.
	///
	/// Um golpe (<see cref="BeatKind.Volley"/>) junta os acertos que caem ao mesmo tempo e o que eles
	/// disparam (efeitos, dreno, queda). O golpe seguinte começa quando um alvo se repete: um golpe de 2
	/// acertos num alvo e depois um em todos vira "A", "A", "A B C" — três momentos, o último em área.
	/// </summary>
	public static class BattlePace
	{
		/// <summary>
		/// As velocidades da tela, no manual e no automático: o que o botão mostra e o quanto acelera o tempo de base. O "1×" é
		/// também o ritmo do recorde e o "3×", o da Batalha automática; mexer nelas muda os melhores tempos e a duração da Batalha
		/// automática no servidor.
		/// </summary>
		public static readonly IReadOnlyList<(int Label, float Factor)> Speeds = new[] { (1, 2f), (2, 2.75f), (3, 3.5f) };

		/// <summary>A corrida até o alvo e a volta ao lugar.</summary>
		public const double Approach = 0.75;

		public const double Return = 0.6;

		/// <summary>Um golpe: o impacto, o número e o respingo.</summary>
		public const double Hit = 0.5;

		/// <summary>A Batalha automática leva o tempo da luta no automático, na velocidade mais rápida da tela.</summary>
		public static float AutoBattleFactor => Speeds[^1].Factor;

		/// <summary>Quanto uma luta inteira leva na tela, dividida pela aceleração.</summary>
		public static double Seconds(IEnumerable<BattleEvent> events, float factor) => Beats(events.ToList()).Sum(b => b.Seconds) / factor;

		/// <summary>Os eventos em momentos da tela, na ordem.</summary>
		public static IReadOnlyList<Beat> Beats(IReadOnlyList<BattleEvent> events)
		{
			var beats = new List<Beat>();
			BattleUnit? striker = null;
			var party = new List<BattleUnit>();
			var i = 0;
			while (i < events.Count)
			{
				var battleEvent = events[i];
				if (battleEvent is JointAttack)
				{
					AddJoint(beats, Chunks(events, ref i), striker, party);
					continue;
				}

				if (Strikes(battleEvent) is { } actor)
				{
					if (striker != null)
						beats.Add(ReturnBeat(striker, party));
					striker = actor;
					party.Clear();
					var targets = TargetsOf(events, i + 1);

					// Os aliados que vão atacar junto saem com quem lançou a habilidade: todos correm juntos.
					var joined = JoinedBy(events, i + 1, actor);
					party.AddRange(joined.Select(a => a.Actor));
					var group = joined.Count == 0 ? null : joined.Prepend(new Attacker(actor, targets)).ToList();
					beats.Add(new Beat(BeatKind.Approach, new[] { battleEvent }, Approach, actor, targets, group));
					i++;
					continue;
				}

				if (TargetOf(battleEvent) != null)
				{
					var volley = Volley(events, ref i);
					beats.Add(new Beat(BeatKind.Volley, volley, Hit + (volley.Any(e => e is Died) ? 0.25 : 0)));
					continue;
				}

				// Um turno novo encerra a ação de quem estava fora do lugar.
				if (striker != null && battleEvent is TurnStarted or ExtraTurn or TurnSkipped or WaveStarted or BattleEnded)
				{
					beats.Add(ReturnBeat(striker, party));
					striker = null;
					party.Clear();
				}

				beats.Add(new Beat(BeatKind.Plain, new[] { battleEvent }, Seconds(battleEvent)));
				i++;
			}

			if (striker != null)
				beats.Add(ReturnBeat(striker, party));
			return beats;
		}

		/// <summary>A volta de quem saiu do lugar, com os aliados que atacaram junto.</summary>
		private static Beat ReturnBeat(BattleUnit striker, IReadOnlyList<BattleUnit> party) => party.Count == 0
			? new Beat(BeatKind.Return, new BattleEvent[0], Return, striker)
			: new Beat(BeatKind.Return, new BattleEvent[0], Return, striker, Group: party.Prepend(striker).Select(unit => new Attacker(unit, new List<BattleUnit>())).ToList());

		/// <summary>Os aliados chamados por ataque conjunto até o fim da ação que começa em <paramref name="start"/>, com o alvo de cada um.</summary>
		private static List<Attacker> JoinedBy(IReadOnlyList<BattleEvent> events, int start, BattleUnit actor)
		{
			var joined = new List<Attacker>();
			for (var i = start; i < events.Count && events[i] is not (TurnStarted or ExtraTurn or TurnSkipped or WaveStarted or BattleEnded or SkillUsed or Counterattack); i++)
			{
				if (events[i] is JointAttack joint && joint.Unit != actor && joined.All(a => a.Actor != joint.Unit))
					joined.Add(new Attacker(joint.Unit, TargetsOf(events, i + 1)));
			}

			return joined;
		}

		/// <summary>O ataque de um aliado chamado: quem é, o aviso e os golpes dele (cada um é um grupo de acertos).</summary>
		private sealed record Chunk(BattleUnit Actor, BattleEvent Label, IReadOnlyList<IReadOnlyList<BattleEvent>> Volleys);

		/// <summary>Os ataques conjuntos seguidos que começam em <paramref name="i"/>: eles acontecem juntos.</summary>
		private static List<Chunk> Chunks(IReadOnlyList<BattleEvent> events, ref int i)
		{
			var chunks = new List<Chunk>();
			while (i < events.Count && events[i] is JointAttack joint)
			{
				var label = events[i++];
				var volleys = new List<IReadOnlyList<BattleEvent>>();
				while (i < events.Count && (TargetOf(events[i]) != null || FollowsHit(events[i])))
					volleys.Add(Volley(events, ref i));
				chunks.Add(new Chunk(joint.Unit, label, volleys));
			}

			return chunks;
		}

		private static IReadOnlyList<BattleUnit> Struck(IReadOnlyList<BattleEvent> volley) => volley.Select(TargetOf).OfType<BattleUnit>().Distinct().ToList();

		/// <summary>
		/// Os golpes do grupo caem juntos (o primeiro de cada um num momento, o segundo no seguinte...). Quem lançou a habilidade e os
		/// chamados já saíram juntos (<see cref="JoinedBy"/>) e voltam juntos; um chamado sem lançador à frente (uma Passiva) corre aqui.
		/// </summary>
		private static void AddJoint(List<Beat> beats, List<Chunk> chunks, BattleUnit? striker, List<BattleUnit> party)
		{
			var runners = chunks.Where(c => c.Actor != striker && !party.Contains(c.Actor)).ToList();
			var labels = chunks.Select(c => c.Label).ToList();
			if (runners.Count > 0)
			{
				var attackers = runners.Select(c => new Attacker(c.Actor, c.Volleys.Count > 0 ? Struck(c.Volleys[0]) : new List<BattleUnit>())).ToList();
				party.AddRange(runners.Select(c => c.Actor));
				beats.Add(new Beat(BeatKind.Approach, labels, Approach, attackers[0].Actor, attackers[0].Targets, attackers));
			}
			else
			{
				beats.Add(new Beat(BeatKind.Plain, labels, 0));
			}

			for (var k = 0; k < chunks.Max(c => c.Volleys.Count); k++)
			{
				var volley = chunks.Where(c => k < c.Volleys.Count).ToList();
				var events = volley.SelectMany(c => c.Volleys[k]).ToList();
				var group = volley.Select(c => new Attacker(c.Actor, Struck(c.Volleys[k]))).ToList();
				beats.Add(new Beat(BeatKind.Volley, events, Hit + (events.Any(e => e is Died) ? 0.25 : 0), Group: group));
			}
		}

		/// <summary>Quanto um evento fora de golpe e de corrida fica à mostra.</summary>
		private static double Seconds(BattleEvent battleEvent) => battleEvent switch
		{
			WaveStarted => 0.8,
			TurnStarted => 0.12,
			ExtraTurn => 0.3,
			MaxHealthReduced => 0.1,
			Healed => 0.12,
			StatusApplied => 0.1,
			Resisted => 0.08,
			Immune => 0.08,
			StatusBlocked => 0.08,
			DurationChanged => 0.08,
			HealthLeveled => 0.12,
			ImpetoChanged => 0.05,
			TurnSkipped => 0.4,
			Died => 0.3,
			Revived => 0.4,
			BattleEnded => 0.6,
			_ => 0,
		};

		/// <summary>Quem sai do lugar para golpear: quem usa uma habilidade ou contra-ataca.</summary>
		private static BattleUnit? Strikes(BattleEvent battleEvent) => battleEvent switch
		{
			SkillUsed used => used.Actor,
			Counterattack counter => counter.Unit,
			JointAttack joint => joint.Unit,
			_ => null,
		};

		/// <summary>O alvo de um acerto (dano, erro ou Égide); null se o evento não é acerto.</summary>
		private static BattleUnit? TargetOf(BattleEvent battleEvent) => battleEvent switch
		{
			Damaged damaged => damaged.Target,
			Missed missed => missed.Target,
			Protected _protected => _protected.Target,
			_ => null,
		};

		/// <summary>O que o golpe dispara e fica no mesmo momento que ele.</summary>
		private static bool FollowsHit(BattleEvent battleEvent) =>
			battleEvent is StatusApplied or Resisted or Immune or StatusRemoved or StatusesChanged or ImpetoChanged or Healed or MaxHealthReduced or Died;

		/// <summary>Um golpe: os acertos seguidos em alvos diferentes e o que eles disparam.</summary>
		private static IReadOnlyList<BattleEvent> Volley(IReadOnlyList<BattleEvent> events, ref int i)
		{
			var volley = new List<BattleEvent>();
			var struck = new HashSet<BattleUnit>();
			while (i < events.Count)
			{
				var battleEvent = events[i];
				if (TargetOf(battleEvent) is { } target)
				{
					if (!struck.Add(target))
						break;
				}
				else if (!FollowsHit(battleEvent))
				{
					break;
				}

				volley.Add(battleEvent);
				i++;
			}

			return volley;
		}

		/// <summary>Os alvos do primeiro golpe da ação que começa em <paramref name="start"/>: para onde a corrida vai.</summary>
		private static IReadOnlyList<BattleUnit> TargetsOf(IReadOnlyList<BattleEvent> events, int start)
		{
			for (var i = start; i < events.Count && Strikes(events[i]) == null && events[i] is not TurnStarted; i++)
			{
				if (TargetOf(events[i]) != null)
				{
					var index = i;
					return Volley(events, ref index).Select(TargetOf).OfType<BattleUnit>().ToList();
				}
			}

			return new BattleUnit[0];
		}
	}
}
