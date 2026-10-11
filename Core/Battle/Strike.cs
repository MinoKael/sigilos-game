using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Um golpe de uma habilidade num alvo, do pedido ao resultado. Quem monta é o efeito de dano
	/// (Effects/DamageEffect): um golpe diferente — mais forte, ignorando a Defesa, drenando — é só um
	/// <see cref="Strike"/> com outros números. As regras de quem ataca e de quem apanha mexem nele antes
	/// do dano e leem o resultado depois (a ordem está em <see cref="EffectResolver.Land"/>).
	/// </summary>
	internal sealed class Strike
	{
		public Strike(Cast cast, BattleUnit target, EffectDefinition effect)
		{
			Cast = cast;
			Target = target;
			Effect = effect;
			Power = effect.Power * cast.Scale;
			IgnoreDefense = effect.IgnoreDefense;
			Drain = effect.Drain;
		}

		/// <summary>A habilidade de que o golpe faz parte.</summary>
		public Cast Cast { get; }

		public EffectResolver Resolver => Cast.Resolver;
		public BattleUnit Attacker => Cast.Caster;
		public BattleUnit Target { get; }

		/// <summary>O efeito de dano: a conta do golpe (<see cref="EffectAmount"/>).</summary>
		public EffectDefinition Effect { get; }

		/// <summary>Multiplicador sobre o atributo do efeito (0,8 = 80% do Ataque), já com os bônus da habilidade.</summary>
		public double Power { get; set; }

		/// <summary>Dano fixo: não passa pela Defesa, pelo elemento nem pelo crítico.</summary>
		public bool Fixed => Effect.Fixed;

		/// <summary>Fração da Defesa do alvo que o golpe ignora (1 = toda).</summary>
		public double IgnoreDefense { get; set; }

		/// <summary>Fração do dano que volta como Vida para quem ataca.</summary>
		public double Drain { get; set; }

		/// <summary>Nulo: sorteia pela chance de Crítico de quem ataca. Uma regra pode decidir antes.</summary>
		public bool? Crit { get; set; }

		/// <summary>O golpe errou (quem ataca estava cego, ou o alvo esquivou): não acontece mais nada.</summary>
		public bool Missed { get; set; }

		/// <summary>O alvo anulou o golpe: não acontece mais nada.</summary>
		public bool Blocked { get; set; }

		/// <summary>O dano do golpe, antes do escudo. Preenchido quando o golpe acerta.</summary>
		public double Amount { get; set; }

		/// <summary>A parte do dano que o escudo segurou.</summary>
		public double Absorbed { get; set; }

		/// <summary>O que o alvo perdeu de Vida.</summary>
		public double Dealt => Amount - Absorbed;
	}
}
