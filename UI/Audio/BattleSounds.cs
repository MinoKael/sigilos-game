using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.UI.Audio
{
	/// <summary>
	/// Os sons da luta na tela: cada momento (<see cref="Beat"/>) vira no máximo <see cref="MaxCues"/> sons,
	/// para a luta em 2× não virar barulho.
	///
	/// - A corrida até o alvo: a básica corre calada; a habilidade com recarga soa o feitiço do elemento de
	///   quem usa (a de recarga <see cref="GrandCooldown"/> ou mais, o grande feitiço), e a do chefe, a voz dele.
	/// - O golpe: um acerto só, mesmo em área — o crítico, o pesado do chefe, a onda do golpe em área, o
	///   impacto do elemento na habilidade ou o golpe médio da básica — e, por cima, o escudo que absorveu ou
	///   a vantagem (e a desvantagem) de elemento. O dano que não é golpe (Veneno, Bomba) tem o som dele.
	/// - O que o golpe dispara vem <see cref="FollowDelay"/> depois: a queda primeiro, depois o efeito.
	/// - Fora do golpe: a onda (a do chefe tem a entrada dele), cura, efeitos, Ímpeto, turno perdido, volta
	///   à vida, vitória e derrota. O efeito que vence sozinho no fim do turno não soa; o tirado por uma
	///   habilidade (purificar, roubar) e a Bomba que explode, sim. Dentro de uma ação, o que já soou
	///   não soa de novo fora do golpe: a cura em cinco aliados é uma cura só.
	///
	/// Guarda a ação de agora (quem golpeia e com quê) entre os momentos: uma por tela de luta. Com
	/// <c>live</c>, a luta jogada: a Vida das unidades é a de agora, e o chefe perto de cair avisa uma vez.
	/// </summary>
	public sealed class BattleSounds
	{
		/// <summary>O que um golpe dispara soa um pouco depois do acerto, em segundos na velocidade 1×.</summary>
		public const double FollowDelay = 0.12;

		/// <summary>A recarga a partir da qual a habilidade é um grande feitiço.</summary>
		public const int GrandCooldown = 5;

		/// <summary>A Vida (fração) abaixo da qual o chefe avisa que está perto de cair.</summary>
		public const double BossNearDefeat = 0.25;

		private const int MaxCues = 3;

		private static readonly IReadOnlyDictionary<Element, string> Casts = new Dictionary<Element, string>
		{
			[Element.Fire] = "combat.elements.fire_flame",
			[Element.Water] = "combat.elements.water_flow",
			[Element.Wind] = "combat.elements.wind_gust",
			[Element.Light] = "combat.elements.light_glint",
			[Element.Dark] = "combat.elements.dark_whisper",
		};

		private static readonly IReadOnlyDictionary<Element, string> Grand = new Dictionary<Element, string>
		{
			[Element.Fire] = "combat.elements.fire_grand",
			[Element.Water] = "combat.elements.water_grand",
			[Element.Wind] = "combat.elements.wind_grand",
			[Element.Light] = "combat.elements.light_grand",
			[Element.Dark] = "combat.elements.dark_grand",
		};

		private static readonly IReadOnlyDictionary<Element, string> Impacts = new Dictionary<Element, string>
		{
			[Element.Fire] = "combat.elements.fire_burst",
			[Element.Water] = "combat.elements.water_impact",
			[Element.Wind] = "combat.elements.wind_cut",
			[Element.Light] = "combat.elements.light_burst",
			[Element.Dark] = "combat.elements.dark_impact",
		};

		/// <summary>O som de cada efeito que pega; o que não está aqui soa como fortalecer ou enfraquecer.</summary>
		private static readonly IReadOnlyDictionary<StatusKind, string> Applied = new Dictionary<StatusKind, string>
		{
			[StatusKind.Shield] = "combat.status.shield",
			[StatusKind.Affliction] = "combat.status.poison",
			[StatusKind.Stun] = "combat.status.stun",
			[StatusKind.Bomb] = "combat.status.bomb",
			[StatusKind.Unrecoverable] = "combat.status.wound",
			[StatusKind.Sleep] = "combat.status.sleep",
			[StatusKind.Silence] = "combat.status.silence",
			[StatusKind.Oblivion] = "combat.status.oblivion",
			[StatusKind.Blessing] = "combat.status.blessing",
			[StatusKind.Counter] = "combat.status.counter",
			[StatusKind.Karma] = "combat.status.karma",
			[StatusKind.Immunity] = "combat.status.immunity",
		};

		private readonly bool _live;
		private readonly HashSet<BattleUnit> _warned = new();

		/// <summary>Os sons que a ação de agora já tocou.</summary>
		private readonly HashSet<string> _sounded = new();

		/// <summary>Quem golpeia agora; nulo entre as ações (o dano de Veneno e Bomba vem sem ninguém).</summary>
		private BattleUnit? _actor;

		/// <summary>A habilidade da ação de agora; nula no contra-ataque e no ataque conjunto, que são a básica.</summary>
		private SkillDefinition? _skill;

		/// <summary>A Bomba acabou de explodir: o dano que vem dela já soou.</summary>
		private bool _detonated;

		public BattleSounds(bool live)
		{
			_live = live;
		}

		/// <summary>Os sons de um momento, com o atraso na velocidade 1× (quem toca divide pela aceleração).</summary>
		public IReadOnlyList<Cue> Of(Beat beat)
		{
			var cues = new List<Cue>();
			switch (beat.Kind)
			{
				case BeatKind.Approach:
					_sounded.Clear();
					Approach(beat.Events[0], cues);
					break;
				case BeatKind.Volley:
					Volley(beat.Events, cues);
					break;
				case BeatKind.Return:
					_actor = null;
					_skill = null;
					_sounded.Clear();
					break;
				default:
					foreach (var battleEvent in beat.Events)
						Plain(battleEvent, cues);
					break;
			}

			var played = cues.GroupBy(cue => cue.Name).Select(same => same.First()).Take(MaxCues).ToList();
			if (_actor != null)
				_sounded.UnionWith(played.Select(cue => cue.Name));
			return played;
		}

		private void Approach(BattleEvent battleEvent, List<Cue> cues)
		{
			switch (battleEvent)
			{
				case SkillUsed used:
					_actor = used.Actor;
					_skill = used.Skill;
					if (used.Skill.Cooldown == 0)
						return;
					cues.Add(new Cue(used.Actor.IsBoss ? "combat.boss.special"
						: used.Skill.Cooldown >= GrandCooldown ? Grand[used.Actor.Element]
						: Casts[used.Actor.Element]));
					return;
				case Counterattack counter:
					_actor = counter.Unit;
					_skill = null;
					cues.Add(new Cue("combat.status.counter"));
					return;
				case JointAttack joint:
					_actor = joint.Unit;
					_skill = null;
					cues.Add(new Cue("progression.skill_activate"));
					return;
			}
		}

		private void Volley(IReadOnlyList<BattleEvent> events, List<Cue> cues)
		{
			var hits = events.OfType<Damaged>().ToList();
			if (hits.Count == 0)
			{
				if (events.Any(e => e is Missed))
					cues.Add(new Cue("combat.attack_miss"));
				else if (events.Any(e => e is Protected))
					cues.Add(new Cue("combat.attack_blocked"));
			}
			else if (_actor == null)
			{
				if (!_detonated)
					cues.Add(new Cue("combat.damage.poison"));
				_detonated = false;
			}
			else
			{
				cues.Add(new Cue(Hit(_actor, _skill, hits)));
			}

			foreach (var died in events.OfType<Died>())
				cues.Add(new Cue(Fall(died.Unit), FollowDelay));
			if (_live && hits.Select(hit => hit.Target).FirstOrDefault(unit => unit.IsBoss && unit.IsAlive && unit.HealthFraction < BossNearDefeat) is { } boss && _warned.Add(boss))
				cues.Add(new Cue("combat.boss.near_defeat", FollowDelay));

			if (hits.Any(hit => hit.Absorbed > 0))
				cues.Add(new Cue("combat.damage.shield_absorb"));
			else if (_actor != null && hits.Any(hit => hit.ElementMultiplier > 1))
				cues.Add(new Cue("combat.damage.elemental"));
			else if (_actor != null && hits.Any(hit => hit.ElementMultiplier < 1))
				cues.Add(new Cue("combat.damage.reduced"));

			foreach (var battleEvent in events)
			{
				var name = battleEvent switch
				{
					StatusApplied applied => Status(applied.Status),
					Resisted => "combat.status.resist",
					Immune => "combat.status.immunity",
					StatusRemoved { Status: StatusKind.Shield } removed when hits.Any(hit => hit.Target == removed.Target && hit.Absorbed > 0) => "combat.status.shield_break",
					ImpetoChanged changed => Impeto(changed),
					_ => null,
				};
				if (name != null)
					cues.Add(new Cue(name, FollowDelay));
			}
		}

		/// <summary>O acerto: um som só para o golpe inteiro.</summary>
		private static string Hit(BattleUnit actor, SkillDefinition? skill, IReadOnlyList<Damaged> hits)
		{
			var special = skill is { Cooldown: > 0 };
			if (hits.Any(hit => hit.Crit))
				return "combat.critical_hit";
			if (actor.IsBoss)
				return special ? "combat.very_heavy_hit" : "combat.attack_heavy";
			if (hits.Select(hit => hit.Target).Distinct().Count() > 1)
				return "combat.area_attack";
			return special ? Impacts[actor.Element] : "combat.attack_medium";
		}

		private void Plain(BattleEvent battleEvent, List<Cue> cues)
		{
			var name = battleEvent switch
			{
				WaveStarted wave => wave.Enemies.Any(enemy => enemy.IsBoss) ? "combat.boss.enter" : "progression.new_wave",
				Healed => "combat.status.heal",
				StatusApplied applied => Status(applied.Status),
				StatusRemoved removed => Removed(removed),
				Resisted => "combat.status.resist",
				Immune => "combat.status.immunity",
				StatusBlocked => "combat.damage.nullified",
				ImpetoChanged changed => Impeto(changed),
				TurnSkipped skipped => skipped.Cause == StatusKind.Sleep ? "combat.status.sleep" : "combat.status.stun",
				ExtraTurn => "progression.impetus_full",
				Died died => Fall(died.Unit),
				Revived => "combat.status.revive",
				BattleEnded ended => ended.Victory ? "progression.victory" : "progression.defeat",
				_ => null,
			};
			if (battleEvent is TurnStarted)
				_detonated = false;
			if (name != null && (_actor == null || !_sounded.Contains(name)))
				cues.Add(new Cue(name));
		}

		/// <summary>
		/// O efeito que sai fora do golpe: a Bomba que explode no turno do dono, o negativo que uma habilidade
		/// purifica e o positivo que ela rouba. O que vence sozinho fica calado.
		/// </summary>
		private string? Removed(StatusRemoved removed)
		{
			if (_skill?.Effects is not { } effects)
			{
				if (removed.Status != StatusKind.Bomb || _actor != null)
					return null;
				_detonated = true;
				return "combat.explosion";
			}

			if (BattleRules.IsNegative(removed.Status) && effects.Any(effect => effect.Kind == EffectKind.Cleanse))
				return "combat.status.purify";
			if (!BattleRules.IsNegative(removed.Status) && effects.Any(effect => effect.Kind == EffectKind.StealBuff))
				return "combat.status.buff_remove";
			return null;
		}

		private static string Status(StatusKind status) =>
			Applied.TryGetValue(status, out var name) ? name
			: BattleRules.IsNegative(status) ? "combat.status.debuff_apply"
			: "combat.status.buff_apply";

		private static string Impeto(ImpetoChanged changed) => changed.Amount >= 0 ? "progression.impetus_gain" : "progression.impetus_loss";

		private static string Fall(BattleUnit unit) => unit.IsBoss ? "combat.boss.defeated" : "combat.knockout";
	}
}
