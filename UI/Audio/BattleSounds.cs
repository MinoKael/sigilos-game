using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.UI.Audio
{
	/// <summary>
	/// Os sons da luta na tela: poucos e macios, para uma luta longa de muitos monstros continuar agradável
	/// por baixo da música. Cada momento (<see cref="Beat"/>) vira no máximo <see cref="MaxCues"/> sons.
	///
	/// - A corrida até o alvo é calada.
	/// - O golpe: um acerto só para o golpe inteiro, mesmo em área ou crítico. O primeiro golpe da ação diz
	///   o que ela é: a básica, o acerto; a habilidade com recarga, o impacto do elemento de quem usa (a de
	///   recarga <see cref="GrandCooldown"/> ou mais, o grande feitiço); a do chefe, a voz dele, e a básica
	///   dele, o acerto pesado. Os golpes seguintes da mesma ação são o acerto simples.
	/// - A queda vem <see cref="FollowDelay"/> depois do golpe que a causou.
	/// - O que não é golpe: uma ação que só cura, fortalece ou enfraquece soa uma vez (o bem ou o mal), e a
	///   volta à vida soa como o bem. A Bomba que explode soa como o acerto pesado.
	/// - Calados: o caminho até o alvo, o erro e a Égide, o crítico, o efeito que o golpe põe, a resistência
	///   e a imunidade, o Ímpeto, o turno perdido, o dano de Veneno e o que acontece sozinho no começo e no
	///   fim do turno.
	/// - A onda (a do chefe tem a entrada dele), a vitória e a derrota.
	///
	/// Guarda a ação de agora (quem golpeia e com quê) entre os momentos: uma por tela de luta.
	/// </summary>
	public sealed class BattleSounds
	{
		/// <summary>O que um golpe dispara soa um pouco depois do acerto, em segundos na velocidade 1×.</summary>
		public const double FollowDelay = 0.12;

		/// <summary>A recarga a partir da qual a habilidade é um grande feitiço.</summary>
		public const int GrandCooldown = 5;

		private const int MaxCues = 2;

		private const string HitSound = "combat.hit";
		private const string HeavySound = "combat.hit_heavy";
		private const string BoonSound = "combat.status.boon";
		private const string BaneSound = "combat.status.bane";

		private static readonly IReadOnlyDictionary<Element, string> Impacts = new Dictionary<Element, string>
		{
			[Element.Fire] = "combat.elements.fire_impact",
			[Element.Water] = "combat.elements.water_impact",
			[Element.Wind] = "combat.elements.wind_impact",
			[Element.Light] = "combat.elements.light_impact",
			[Element.Dark] = "combat.elements.dark_impact",
		};

		private static readonly IReadOnlyDictionary<Element, string> Grand = new Dictionary<Element, string>
		{
			[Element.Fire] = "combat.elements.fire_grand",
			[Element.Water] = "combat.elements.water_grand",
			[Element.Wind] = "combat.elements.wind_grand",
			[Element.Light] = "combat.elements.light_grand",
			[Element.Dark] = "combat.elements.dark_grand",
		};

		/// <summary>Os sons que a ação de agora já tocou.</summary>
		private readonly HashSet<string> _sounded = new();

		/// <summary>Quem golpeia agora; nulo entre as ações (o dano de Veneno e Bomba vem sem ninguém).</summary>
		private BattleUnit? _actor;

		/// <summary>A habilidade da ação de agora; nula no contra-ataque e no ataque conjunto, que são a básica.</summary>
		private SkillDefinition? _skill;

		/// <summary>A ação de agora já acertou: os golpes seguintes são o acerto simples.</summary>
		private bool _struck;

		/// <summary>Os sons de um momento, com o atraso na velocidade 1× (quem toca divide pela aceleração).</summary>
		public IReadOnlyList<Cue> Of(Beat beat)
		{
			var cues = new List<Cue>();
			switch (beat.Kind)
			{
				case BeatKind.Approach:
					Begin(beat.Events[0]);
					break;
				case BeatKind.Volley:
					Volley(beat.Events, cues);
					break;
				case BeatKind.Return:
					_actor = null;
					_skill = null;
					_struck = false;
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

		/// <summary>Uma ação começa: guarda quem golpeia e com quê. A corrida é calada.</summary>
		private void Begin(BattleEvent battleEvent)
		{
			_sounded.Clear();
			_struck = false;
			(_actor, _skill) = battleEvent switch
			{
				SkillUsed used => (used.Actor, used.Skill),
				Counterattack counter => (counter.Unit, (SkillDefinition?)null),
				JointAttack joint => (joint.Unit, null),
				_ => (null, null),
			};
		}

		private void Volley(IReadOnlyList<BattleEvent> events, List<Cue> cues)
		{
			if (_actor != null && events.Any(e => e is Damaged))
			{
				cues.Add(new Cue(_struck ? HitSound : Strike(_actor, _skill)));
				_struck = true;
			}

			foreach (var died in events.OfType<Died>())
				cues.Add(new Cue(Fall(died.Unit), FollowDelay));
		}

		/// <summary>O primeiro acerto da ação: diz se foi a básica, a habilidade do elemento ou a do chefe.</summary>
		private static string Strike(BattleUnit actor, SkillDefinition? skill)
		{
			if (skill is not { Cooldown: > 0 })
				return actor.IsBoss ? HeavySound : HitSound;
			if (actor.IsBoss)
				return "combat.boss.special";
			return skill.Cooldown >= GrandCooldown ? Grand[actor.Element] : Impacts[actor.Element];
		}

		private void Plain(BattleEvent battleEvent, List<Cue> cues)
		{
			var name = battleEvent switch
			{
				WaveStarted wave => wave.Enemies.Any(enemy => enemy.IsBoss) ? "combat.boss.enter" : "progression.new_wave",
				Healed when _actor != null => BoonSound,
				StatusApplied applied when _actor != null => BattleRules.IsNegative(applied.Status) ? BaneSound : BoonSound,
				StatusRemoved { Status: StatusKind.Bomb } when _actor == null => HeavySound,
				Revived => BoonSound,
				Died died => Fall(died.Unit),
				BattleEnded ended => ended.Victory ? "progression.victory" : "progression.defeat",
				_ => null,
			};
			if (name == null || _actor != null && (_sounded.Contains(name) || IsStatus(name) && (_sounded.Contains(BoonSound) || _sounded.Contains(BaneSound))))
				return;
			cues.Add(new Cue(name));
		}

		private static bool IsStatus(string name) => name is BoonSound or BaneSound;

		private static string Fall(BattleUnit unit) => unit.IsBoss ? "combat.boss.defeated" : "combat.knockout";
	}
}
