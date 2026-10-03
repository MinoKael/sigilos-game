using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;

namespace Sigilos.UI
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
	public sealed record Beat(BeatKind Kind, IReadOnlyList<BattleEvent> Events, double Seconds, BattleUnit? Actor = null, IReadOnlyList<BattleUnit>? Targets = null);

	/// <summary>
	/// O ritmo da luta na tela, como em Summoners War: quem ataca corre até o alvo, golpeia e volta. A
	/// tela mostra a luta em <see cref="Beats"/>, e a Batalha automática usa a mesma conta para saber
	/// quanto uma luta levaria — assim as duas nunca discordam.
	///
	/// Um golpe (<see cref="BeatKind.Volley"/>) junta os acertos que caem ao mesmo tempo e o que eles
	/// disparam (efeitos, dreno, queda). O golpe seguinte começa quando um alvo se repete: um golpe de 2
	/// acertos num alvo e depois um em todos vira "A", "A", "A B C" — três momentos, o último em área.
	/// </summary>
	public static class BattlePace
	{
		/// <summary>As velocidades da tela: o que o botão mostra e o quanto acelera. O "2×" acelera 3 vezes.</summary>
		public static readonly IReadOnlyList<(int Label, float Factor)> Speeds = new[] { (1, 1f), (2, 3f) };

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
			var i = 0;
			while (i < events.Count)
			{
				var battleEvent = events[i];
				if (Strikes(battleEvent) is { } actor)
				{
					if (striker != null)
						beats.Add(new Beat(BeatKind.Return, new BattleEvent[0], Return, striker));
					striker = actor;
					beats.Add(new Beat(BeatKind.Approach, new[] { battleEvent }, Approach, actor, TargetsOf(events, i + 1)));
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
					beats.Add(new Beat(BeatKind.Return, new BattleEvent[0], Return, striker));
					striker = null;
				}

				beats.Add(new Beat(BeatKind.Plain, new[] { battleEvent }, Seconds(battleEvent)));
				i++;
			}

			if (striker != null)
				beats.Add(new Beat(BeatKind.Return, new BattleEvent[0], Return, striker));
			return beats;
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
			battleEvent is StatusApplied or Resisted or Immune or StatusRemoved or ImpetoChanged or Healed or MaxHealthReduced or Died;

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
