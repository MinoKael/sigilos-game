using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Uma invocação ou inimigo em campo. Guarda o estado que muda durante a luta (Vida, Ímpeto,
	/// efeitos, recarga) e calcula os atributos de agora a partir dos efeitos ativos.
	/// Quem decide o que acontece com ela é o <see cref="BattleSession"/>.
	/// </summary>
	public sealed class BattleUnit
	{
		private readonly List<StatusEffect> _statuses = new();
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

		/// <summary>O que os conjuntos de runas fazem em combate (Vampiro, Desespero, Violento...).</summary>
		public RuneSetEffects RuneEffects { get; }

		public double Health { get; set; }

		/// <summary>Vida máxima que o conjunto Destruição de um inimigo já tirou.</summary>
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

		public bool RebirthUsed { get; set; }

		/// <summary>O Violento deu um turno extra: o próximo turno desta unidade é ele.</summary>
		public bool ExtraTurnPending { get; set; }

        /// <summary>Caída, mas renasce quando o Ímpeto dela encher (Passiva da Fênix).</summary>
        public bool PendingRebirth { get; set; }

		/// <summary>O próprio time (inclui ela mesma). Ligado pelo <see cref="BattleFactory"/>.</summary>
		public IReadOnlyList<BattleUnit> Team { get; internal set; } = Array.Empty<BattleUnit>();

		public IReadOnlyList<StatusEffect> Statuses => _statuses;

		/// <summary>Está na barra de Ímpeto: viva, ou caída esperando renascer.</summary>
		public bool CanTakeTurn => IsAlive || PendingRebirth;

		/// <summary>Velocidade de agora, com efeitos e Passiva. A barra enche em proporção a ela.</summary>
		public double TurnSpeed
		{
			get
			{
				var speed = Stats.Speed;
				if (Has(StatusKind.SpeedUp))
					speed *= 1 + BattleRules.SpeedUpBonus;
				if (Passive?.Kind == PassiveKind.SpeedWhenLowest && IsLowestInTeam())
					speed *= 1 + PassiveValue;
				return Math.Max(1, speed);
			}
		}

		public double Attack
		{
			get
			{
				var attack = Stats.Attack;
				if (Has(StatusKind.AttackUp))
					attack *= 1 + BattleRules.AttackUpBonus;
				if (Has(StatusKind.AttackDown))
					attack *= 1 - BattleRules.AttackDownPenalty;
				return attack;
			}
		}

		public double Defense => Has(StatusKind.DefenseUp) ? Stats.Defense * (1 + BattleRules.DefenseUpBonus) : Stats.Defense;

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

		internal void AddStatus(StatusEffect status) => _statuses.Add(status);

		internal void RemoveStatus(StatusEffect status) => _statuses.Remove(status);

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

			var expired = _statuses.Where(s => s.Turns <= 0 || (s.Kind == StatusKind.Shield && s.Value <= 0)).ToList();
			foreach (var status in expired)
				_statuses.Remove(status);
			return expired;
		}

		/// <summary>Estritamente a menos Vida: empate (todos cheios no começo da luta) não conta.</summary>
		private bool IsLowestInTeam()
		{
			var others = Team.Where(u => u.IsAlive && u != this).ToList();
			return others.Count > 0 && others.All(u => u.HealthFraction > HealthFraction);
		}
	}
}
