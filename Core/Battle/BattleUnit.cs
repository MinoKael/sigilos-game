using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle.Passives;
using Sigilos.Core.Battle.Sets;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Uma invocação ou inimigo em campo. Guarda o estado que muda durante a luta (Vida, Ímpeto,
	/// efeitos, recarga) e as regras em vigor nela: os efeitos de status, os conjuntos de runas e a
	/// Passiva (<see cref="Rules"/>). Os atributos de agora saem da ficha passada por essas regras.
	/// Quem decide o que acontece com ela é o <see cref="BattleSession"/>.
	/// </summary>
	public sealed class BattleUnit
	{
		private readonly List<StatusEffect> _statuses = new();

		/// <summary>As regras que valem a luta inteira: os conjuntos de runas e, por último, a Passiva.</summary>
		private readonly List<UnitRule> _innate = new();

		private readonly int[] _cooldowns;

		public BattleUnit(
			string definitionId,
			string name,
			string image,
			Side side,
			Element element,
			int level,
			bool awakened,
			StatBlock stats,
			IReadOnlyList<SkillDefinition> skills,
			PassiveDefinition? passive,
			RuneSetEffects runeEffects)
		{
			DefinitionId = definitionId;
			Name = name;
			Image = image;
			Side = side;
			Element = element;
			Level = level;
			Awakened = awakened;
			Stats = stats;
			Skills = skills;
			_cooldowns = new int[skills.Count];
			Passive = passive;
			RuneEffects = runeEffects;
			Health = stats.Health;

			_innate.AddRange(SetBehaviors.RulesFor(runeEffects));
			if (passive != null)
				_innate.Add(new UnitRule(PassiveBehaviors.Of(passive.Kind), PassiveValue));
			foreach (var rule in _innate)
				rule.Owner = this;
		}

		/// <summary>Id da invocação ou do inimigo em Data/.</summary>
		public string DefinitionId { get; }

		public string Name { get; }
		public string Image { get; }
		public Side Side { get; }
		public Element Element { get; }

		public int Level { get; }
		public bool Awakened { get; }

		/// <summary>Atributos da luta, já com estrelas, nível, Despertar, runas e Liderança.</summary>
		public StatBlock Stats { get; }

		/// <summary>As habilidades ativas, já no nível e na versão (desperta ou não) com que lutam. A 0 é a básica.</summary>
		public IReadOnlyList<SkillDefinition> Skills { get; }

		/// <summary>A Passiva, se a unidade tem uma.</summary>
		public PassiveDefinition? Passive { get; }

		/// <summary>O número da Passiva, já melhorado se a invocação despertou.</summary>
		public double PassiveValue => Passive?.ValueFor(Awakened) ?? 0;

		/// <summary>O que os conjuntos de runas completos fazem em combate, já somado.</summary>
		public RuneSetEffects RuneEffects { get; }

		public double Health { get; set; }

		/// <summary>Vida máxima que o conjunto Oblívio de um inimigo já tirou.</summary>
		public double HealthDestroyed { get; set; }

		public double MaxHealth => Stats.Health - HealthDestroyed;
		public double HealthFraction => MaxHealth <= 0 ? 0 : Health / MaxHealth;
		public bool IsAlive => Health > 0;

		/// <summary>De 0 a 100: a unidade age quando chega a 100.</summary>
		public double Impeto { get; set; }

		/// <summary>Turnos até a habilidade <paramref name="index"/> voltar. 0 = pronta.</summary>
		public int Cooldown(int index) => index > 0 && index < _cooldowns.Length ? _cooldowns[index] : 0;

		public void SetCooldown(int index, int turns)
		{
			if (index > 0 && index < _cooldowns.Length)
				_cooldowns[index] = turns;
		}

		/// <summary>A básica está sempre pronta; as outras, fora da recarga.</summary>
		public bool IsReady(int index) => index >= 0 && index < Skills.Count && Cooldown(index) == 0;

		/// <summary>Ganhou um turno extra: o próximo turno desta unidade é ele.</summary>
		public bool ExtraTurnPending { get; set; }

		/// <summary>Com que fração da Vida máxima a unidade caída volta quando o Ímpeto dela encher. 0: não volta.</summary>
		public double RevivalHealth { get; internal set; }

		/// <summary>Caída, mas volta no próximo turno dela.</summary>
		public bool Reviving => !IsAlive && RevivalHealth > 0;

		/// <summary>O próprio time (inclui ela mesma). Ligado pelo <see cref="BattleFactory"/>.</summary>
		public IReadOnlyList<BattleUnit> Team { get; internal set; } = Array.Empty<BattleUnit>();

		public IReadOnlyList<StatusEffect> Statuses => _statuses;

		/// <summary>Está na barra de Ímpeto: viva, ou caída esperando voltar.</summary>
		public bool CanTakeTurn => IsAlive || Reviving;

		/// <summary>Velocidade de agora. A barra enche em proporção a ela.</summary>
		public double TurnSpeed => Math.Max(1, Current(Stat.Speed));

		public double Attack => Current(Stat.Attack);

		public double Defense => Current(Stat.Defense);

		/// <summary>O atributo de agora: o da ficha, passado por cada regra em vigor (Ataque+, Quebra de Defesa...).</summary>
		public double Current(Stat stat)
		{
			var value = Stats.Get(stat);
			foreach (var status in _statuses)
				value = status.Behavior.Modify(status, stat, value);
			foreach (var rule in _innate)
				value = rule.Behavior.Modify(rule, stat, value);
			return value;
		}

		public SkillDefinition Skill(int index) => Skills[index >= 0 && index < Skills.Count ? index : 0];

		/// <summary>Fim do turno do dono: cada recarga perde um turno.</summary>
		internal void TickCooldowns()
		{
			for (var i = 1; i < _cooldowns.Length; i++)
			{
				if (_cooldowns[i] > 0)
					_cooldowns[i]--;
			}
		}

		public bool Has(StatusKind kind) => _statuses.Any(s => s.Kind == kind);

		public StatusEffect? Find(StatusKind kind) => _statuses.FirstOrDefault(s => s.Kind == kind);

		public int Count(StatusKind kind) => _statuses.Count(s => s.Kind == kind);

		// Regras --------------------------------------------------------------------------------------

		/// <summary>
		/// As regras em vigor, na ordem em que são avisadas: os efeitos de status como chegaram, depois os
		/// conjuntos de runas e a Passiva. Percorre uma cópia, porque uma regra pode tirar outra (ou a si
		/// mesma) no meio de um momento da luta; a que saiu antes da vez dela não é avisada.
		/// </summary>
		internal IEnumerable<UnitRule> Rules()
		{
			var rules = new List<UnitRule>(_statuses.Count + _innate.Count);
			rules.AddRange(_statuses);
			rules.AddRange(_innate);

			foreach (var rule in rules)
			{
				if (Holds(rule))
					yield return rule;
			}
		}

		/// <summary>A regra ainda está na unidade.</summary>
		internal bool Holds(UnitRule rule) => rule is StatusEffect status ? _statuses.Contains(status) : _innate.Contains(rule);

		/// <summary>Alguma regra em vigor tem esta característica (perde o turno, está oculta...).</summary>
		internal bool Any(Func<UnitBehavior, bool> trait)
		{
			foreach (var status in _statuses)
			{
				if (trait(status.Behavior))
					return true;
			}

			foreach (var rule in _innate)
			{
				if (trait(rule.Behavior))
					return true;
			}

			return false;
		}

		/// <summary>A regra desta estratégia que vale a luta inteira (conjunto de runas ou Passiva), se a unidade tem.</summary>
		internal UnitRule? Innate(UnitBehavior behavior) => _innate.FirstOrDefault(rule => rule.Behavior == behavior);

		/// <summary>Quanto as regras da unidade multiplicam o dano que ela recebe.</summary>
		internal double DamageTaken()
		{
			double factor = 1;
			foreach (var status in _statuses)
				factor *= status.Behavior.DamageTaken(status);
			foreach (var rule in _innate)
				factor *= rule.Behavior.DamageTaken(rule);
			return factor;
		}

		/// <summary>Quanto as regras da unidade multiplicam o dano dos golpes dela em <paramref name="target"/>.</summary>
		internal double DamageDealt(BattleUnit target)
		{
			double factor = 1;
			foreach (var status in _statuses)
				factor *= status.Behavior.DamageDealt(status, target);
			foreach (var rule in _innate)
				factor *= rule.Behavior.DamageDealt(rule, target);
			return factor;
		}

		internal void AddStatus(StatusEffect status)
		{
			status.Owner = this;
			_statuses.Add(status);
		}

		internal void Remove(UnitRule rule)
		{
			if (rule is StatusEffect status)
				_statuses.Remove(status);
			else
				_innate.Remove(rule);
		}

		internal void ClearStatuses() => _statuses.Clear();

		/// <summary>
		/// Fim do turno do dono: cada efeito perde um turno, menos os recebidos neste mesmo turno.
		/// Devolve os que acabaram.
		/// </summary>
		internal List<StatusEffect> TickStatuses()
		{
			foreach (var status in _statuses)
			{
				if (status.Fresh)
					status.Fresh = false;
				else
					status.Turns--;
			}

			var expired = _statuses.Where(s => s.Turns <= 0).ToList();
			foreach (var status in expired)
				_statuses.Remove(status);
			return expired;
		}
	}
}
