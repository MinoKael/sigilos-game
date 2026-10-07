using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Core.Player
{
	/// <summary>O que uma habilidade faz, para os filtros de Monstros (<see cref="SkillTraits"/>).</summary>
	public enum SkillBehavior
	{
		/// <summary>Dano em todos os inimigos.</summary>
		AreaDamage,

		/// <summary>Dano em vários golpes (o "xN" da planilha de referência).</summary>
		MultiHit,

		IgnoreDefense,

		/// <summary>Cura uma parte do dano causado.</summary>
		Drain,

		/// <summary>Cura, de um aliado ou do time.</summary>
		Heal,

		Shield,

		/// <summary>Põe um efeito positivo nos aliados.</summary>
		Buff,

		/// <summary>Põe um efeito negativo nos inimigos.</summary>
		Debuff,

		/// <summary>Remove efeitos negativos dos aliados.</summary>
		Cleanse,

		/// <summary>Rouba efeitos positivos dos inimigos.</summary>
		StealBuff,

		/// <summary>Ímpeto a mais para os aliados.</summary>
		ImpetoBoost,

		/// <summary>Ímpeto a menos para os inimigos.</summary>
		ImpetoCut,

		/// <summary>Muda a duração dos efeitos.</summary>
		ChangeDuration,

		EqualizeHealth,
		JointAttack,
		ExtraTurn,

		/// <summary>A habilidade age sozinha (o "(Passive)" da planilha).</summary>
		Passive,
	}

	/// <summary>
	/// Em que atributo a habilidade escala: só os que o jogo tem (docs/COMBATE.md, "Em que atributo a
	/// habilidade escala"), cada um com o termo dele na planilha de referência (docs/allstats.xlsx). Os outros
	/// termos de lá (Defesa, Velocidade, Vida perdida...) não entram: escalar neles seria regra nova.
	/// </summary>
	public enum SkillScaling
	{
		/// <summary>{ATK}: o dano e a Bomba, no Ataque de quem lança.</summary>
		Attack,

		/// <summary>{MAX HP}: o escudo, na Vida máxima de quem lança.</summary>
		MaxHealth,

		/// <summary>{Target MAX HP}: a cura, na Vida máxima do alvo.</summary>
		TargetMaxHealth,
	}

	/// <summary>
	/// O que as habilidades de um monstro fazem e em que escalam, lido dos efeitos (o tipo, o alvo e os campos
	/// de <see cref="EffectDefinition"/>), nunca do texto. Efeito novo: uma linha nas tabelas.
	/// </summary>
	public static class SkillTraits
	{
		private static readonly (SkillBehavior Behavior, Func<EffectDefinition, bool> Has)[] Behaviors =
		{
			(SkillBehavior.AreaDamage, e => e.Kind == EffectKind.Damage && e.Target == TargetKind.AllEnemies),
			(SkillBehavior.MultiHit, e => e.Kind == EffectKind.Damage && e.Hits > 1),
			(SkillBehavior.IgnoreDefense, e => e.Kind == EffectKind.Damage && e.IgnoreDefense > 0),
			(SkillBehavior.Drain, e => e.Kind == EffectKind.Damage && e.Drain > 0),
			(SkillBehavior.Heal, e => e.Kind is EffectKind.Heal or EffectKind.HealTeam),
			(SkillBehavior.Shield, e => e.Kind == EffectKind.Shield || e is { Kind: EffectKind.Status, Status: StatusKind.Shield }),
			(SkillBehavior.Buff, e => e.Kind == EffectKind.Status && e.Status != StatusKind.Shield && !BattleRules.IsNegative(e.Status) && OnAllies(e.Target)),
			(SkillBehavior.Debuff, e => e.Kind == EffectKind.Status && BattleRules.IsNegative(e.Status) && !OnAllies(e.Target)),
			(SkillBehavior.Cleanse, e => e.Kind == EffectKind.Cleanse),
			(SkillBehavior.StealBuff, e => e.Kind == EffectKind.StealBuff),
			(SkillBehavior.ImpetoBoost, e => e.Kind == EffectKind.Impeto && e.Power > 0 && OnAllies(e.Target)),
			(SkillBehavior.ImpetoCut, e => e.Kind == EffectKind.Impeto && e.Power < 0 && !OnAllies(e.Target)),
			(SkillBehavior.ChangeDuration, e => e.Kind == EffectKind.ChangeDuration),
			(SkillBehavior.EqualizeHealth, e => e.Kind == EffectKind.EqualizeHealth),
			(SkillBehavior.JointAttack, e => e.Kind == EffectKind.JointAttack),
			(SkillBehavior.ExtraTurn, e => e.Kind == EffectKind.ExtraTurnOnKill),
		};

		/// <summary>A tabela de docs/COMBATE.md: que efeito escala em quê.</summary>
		private static readonly (SkillScaling Scaling, Func<EffectDefinition, bool> Has)[] Scalings =
		{
			(SkillScaling.Attack, e => e.Kind == EffectKind.Damage || e is { Kind: EffectKind.Status, Status: StatusKind.Bomb }),
			(SkillScaling.MaxHealth, e => e.Kind == EffectKind.Shield),
			(SkillScaling.TargetMaxHealth, e => e.Kind is EffectKind.Heal or EffectKind.HealTeam),
		};

		/// <summary>O que as habilidades do monstro fazem, na forma dele (desperto ou não).</summary>
		public static IEnumerable<SkillBehavior> BehaviorsOf(SummonDefinition summon, bool awakened)
		{
			var skills = summon.SkillsFor(awakened);
			var found = Behaviors.Where(rule => Effects(skills, awakened).Any(rule.Has)).Select(rule => rule.Behavior);
			return skills.Any(s => s.IsPassive) ? found.Append(SkillBehavior.Passive) : found;
		}

		/// <summary>Em que atributos as habilidades do monstro escalam, na forma dele.</summary>
		public static IEnumerable<SkillScaling> ScalingsOf(SummonDefinition summon, bool awakened) =>
			Scalings.Where(rule => Effects(summon.SkillsFor(awakened), awakened).Any(rule.Has)).Select(rule => rule.Scaling);

		/// <summary>Os efeitos de status que as habilidades do monstro põem, na forma dele.</summary>
		public static IEnumerable<StatusKind> StatusesOf(SummonDefinition summon, bool awakened) =>
			Effects(summon.SkillsFor(awakened), awakened).Where(e => e.Kind == EffectKind.Status).Select(e => e.Status).Distinct();

		/// <summary>Tudo o que aparece em alguma forma de algum monstro: as opções dos filtros.</summary>
		public static IReadOnlyList<SkillBehavior> BehaviorsIn(IEnumerable<SummonDefinition> summons) =>
			summons.SelectMany(s => BehaviorsOf(s, false).Concat(BehaviorsOf(s, true))).Distinct().OrderBy(b => b).ToList();

		public static IReadOnlyList<SkillScaling> ScalingsIn(IEnumerable<SummonDefinition> summons) =>
			summons.SelectMany(s => ScalingsOf(s, false).Concat(ScalingsOf(s, true))).Distinct().OrderBy(s => s).ToList();

		public static IReadOnlyList<StatusKind> StatusesIn(IEnumerable<SummonDefinition> summons) =>
			summons.SelectMany(s => StatusesOf(s, false).Concat(StatusesOf(s, true))).Distinct().OrderBy(s => s).ToList();

		private static IEnumerable<EffectDefinition> Effects(IEnumerable<SkillDefinition> skills, bool awakened) =>
			skills.SelectMany(s => awakened && s.AwakenedEffects.Count > 0 ? s.AwakenedEffects : s.Effects);

		private static bool OnAllies(TargetKind target) =>
			target is TargetKind.Self or TargetKind.LowestAlly or TargetKind.AllAllies or TargetKind.HighestAlly or TargetKind.RandomAlly;
	}
}
