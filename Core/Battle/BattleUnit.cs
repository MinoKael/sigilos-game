using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Uma invocação ou inimigo em campo. Guarda o estado que muda durante a luta (Vida, Ímpeto,
	/// efeitos, recarga) e calcula os atributos de agora a partir dos efeitos ativos.
	/// Quem decide o que acontece com ela é o <see cref="BattleSession"/>.
	/// </summary>
	public sealed class BattleUnit : ITurnTaker
	{
		private readonly List<StatusEffect> _statuses = new();

		public BattleUnit(
			string definitionId,
			string name,
			string image,
			Side side,
			Element element,
			Glyph? glyph,
			StatBlock stats,
			SkillDefinition basic,
			SkillDefinition? glyphSkill,
			PassiveDefinition? passive,
			double skillPower)
		{
			DefinitionId = definitionId;
			Name = name;
			Image = image;
			Side = side;
			Element = element;
			Glyph = glyph;
			Stats = stats;
			Basic = basic;
			GlyphSkill = glyphSkill;
			Passive = passive;
			SkillPower = skillPower;
			Health = stats.Health;
		}

		/// <summary>Id da invocação ou do inimigo em Data/.</summary>
		public string DefinitionId { get; }

		public string Name { get; }
		public string Image { get; }
		public Side Side { get; }
		public Element Element { get; }

		/// <summary>Só invocações têm Glifo; inimigos ficam nulos.</summary>
		public Glyph? Glyph { get; }

		/// <summary>Atributos de base da luta, já com nível, raridade, Ecos e Liderança.</summary>
		public StatBlock Stats { get; }

		public SkillDefinition Basic { get; }
		public SkillDefinition? GlyphSkill { get; }
		public PassiveDefinition? Passive { get; }

		/// <summary>Multiplica dano, cura e escudo das habilidades (Ecos).</summary>
		public double SkillPower { get; }

		public double Health { get; set; }
		public double MaxHealth => Stats.Health;
		public double HealthFraction => MaxHealth <= 0 ? 0 : Health / MaxHealth;
		public bool IsAlive => Health > 0;

		public double Impeto { get; set; }

		/// <summary>Turnos até a habilidade de Glifo voltar. 0 = pronta.</summary>
		public int GlyphCooldown { get; set; }

		public bool RebirthUsed { get; set; }

		/// <summary>Caída, mas renasce quando o Ímpeto dela encher (Assinatura da Fênix).</summary>
		public bool PendingRebirth { get; set; }

		/// <summary>O próprio time (inclui ela mesma). Ligado pelo <see cref="BattleFactory"/>.</summary>
		public IReadOnlyList<BattleUnit> Team { get; internal set; } = Array.Empty<BattleUnit>();

		public IReadOnlyList<StatusEffect> Statuses => _statuses;

		public bool CanTakeTurn => IsAlive || PendingRebirth;

		public bool IsGlyphReady => GlyphSkill != null && GlyphCooldown == 0;

		public double TurnSpeed
		{
			get
			{
				var speed = Stats.Speed;
				if (Has(StatusKind.SpeedUp))
					speed *= 1 + BattleRules.SpeedUpBonus;
				if (Passive?.Kind == PassiveKind.SpeedWhenLowest && IsLowestInTeam())
					speed *= 1 + Passive.Value;
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

		public SkillDefinition Skill(SkillSlot slot) => slot == SkillSlot.Glyph && GlyphSkill != null ? GlyphSkill : Basic;

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

		private bool IsLowestInTeam()
		{
			var alive = Team.Where(u => u.IsAlive).ToList();
			return alive.Count > 1 && alive.MinBy(u => u.HealthFraction) == this;
		}
	}
}
