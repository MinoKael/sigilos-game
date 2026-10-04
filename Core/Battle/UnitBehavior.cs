using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// O que uma regra faz na luta: a estratégia de um efeito de status, de uma Passiva ou de um conjunto
	/// de runas (padrão Strategy). A luta não sabe o que é Veneno nem Contragolpe: a cada momento — começo
	/// de onda, começo de turno, golpe, queda — ela avisa as regras em vigor na unidade
	/// (<see cref="BattleUnit.Rules"/>), e cada estratégia responde só aos momentos dela. Todo método
	/// daqui vem vazio: a estratégia sobrescreve os que usa.
	///
	/// Uma estratégia não guarda estado: é uma só para todas as lutas. Os números de cada aplicação (o
	/// valor, quem pôs, os turnos que faltam) vêm na <see cref="UnitRule"/> que todo método recebe.
	///
	/// Regra nova: uma classe que herda daqui (efeito de status: de <see cref="Statuses.StatusBehavior"/>)
	/// e a linha dela na tabela — Statuses/StatusBehaviors, Passives/PassiveBehaviors ou Sets/SetBehaviors.
	/// Momento novo: um método vazio aqui e a chamada no ponto da luta em que ele acontece
	/// (<see cref="BattleSession"/> ou <see cref="EffectResolver"/>).
	/// </summary>
	internal abstract class UnitBehavior
	{
		// O que a regra muda no dono ----------------------------------------------------------------

		/// <summary>Um atributo de agora: recebe o valor até aqui e devolve o novo (Ataque+, Quebra de Defesa).</summary>
		public virtual double Modify(UnitRule rule, Stat stat, double value) => value;

		/// <summary>O dono perde o turno (Atordoamento).</summary>
		public virtual bool SkipsTurn => false;

		/// <summary>O dono não pode ser alvo único enquanto sobrar outro alvo (Oculto).</summary>
		public virtual bool HidesOwner => false;

		/// <summary>Nenhum efeito negativo pega no dono (Imunidade).</summary>
		public virtual bool BlocksHarmful => false;

		/// <summary>O dono só pode mirar nesta unidade enquanto ela estiver viva (Provocação).</summary>
		public virtual BattleUnit? ForcedTarget(UnitRule rule) => null;

		/// <summary>Nenhum efeito positivo pega no dono (Karma).</summary>
		public virtual bool BlocksBeneficial => false;

		/// <summary>O dono não recebe cura (Ferida).</summary>
		public virtual bool BlocksHealing => false;

		/// <summary>O dono só usa habilidades sem recarga (Silêncio).</summary>
		public virtual bool BlocksCooldownSkills => false;

		/// <summary>A Passiva do dono para de funcionar (Esquecimento).</summary>
		public virtual bool SuppressesPassive => false;

		/// <summary>Multiplica a chance de Crítico dos golpes que o dono recebe (1 = não muda; Resistir Crítico).</summary>
		public virtual double CritTaken(UnitRule rule) => 1;

		// Momentos da luta --------------------------------------------------------------------------

		/// <summary>Começou uma onda e o dono está vivo.</summary>
		public virtual void OnWaveStart(UnitRule rule, EffectResolver resolver) { }

		/// <summary>Começou o turno do dono, antes de ele decidir (e mesmo que vá perder o turno).</summary>
		public virtual void OnTurnStart(UnitRule rule, EffectResolver resolver) { }

		/// <summary>O dono acabou de usar a habilidade do turno.</summary>
		public virtual void AfterAction(UnitRule rule, EffectResolver resolver) { }

		/// <summary>O dono caiu. Os efeitos de status dele já saíram, mas ainda são avisados.</summary>
		public virtual void OnDeath(UnitRule rule, EffectResolver resolver) { }

		/// <summary>Um efeito de status que o dono pôs acabou de pegar (novo ou renovado) em <paramref name="target"/>.</summary>
		public virtual void OnStatusGiven(UnitRule rule, EffectResolver resolver, BattleUnit target, StatusKind status) { }

		/// <summary>Um efeito de status acabou de pegar no dono (novo ou renovado); <paramref name="source"/> é quem pôs (nulo no escudo).</summary>
		public virtual void OnStatusReceived(UnitRule rule, EffectResolver resolver, BattleUnit? source, StatusKind status) { }

		// Momentos de um golpe, na ordem em que acontecem -------------------------------------------

		/// <summary>O dono vai dar um golpe: pode errar, garantir o crítico, somar dreno.</summary>
		public virtual void OnAttack(UnitRule rule, Strike strike) { }

		/// <summary>O dono vai levar um golpe que não errou: pode esquivar (o golpe erra) ou anular.</summary>
		public virtual void OnDefend(UnitRule rule, Strike strike) { }

		/// <summary>Multiplica o dano dos golpes do dono neste alvo (1 = não muda).</summary>
		public virtual double DamageDealt(UnitRule rule, BattleUnit target) => 1;

		/// <summary>Multiplica o dano que o dono recebe (1 = não muda).</summary>
		public virtual double DamageTaken(UnitRule rule) => 1;

		/// <summary>Quanto deste dano a regra segura antes de chegar à Vida do dono (escudo).</summary>
		public virtual double Absorb(UnitRule rule, double amount) => 0;

		/// <summary>O dono levou o golpe e sobreviveu: pode revidar em quem atacou.</summary>
		public virtual void AfterHurt(UnitRule rule, Strike strike) { }

		/// <summary>O dono acertou o golpe e o alvo sobreviveu.</summary>
		public virtual void AfterHit(UnitRule rule, Strike strike) { }

		// Momentos de uma habilidade ----------------------------------------------------------------

		/// <summary>Um efeito da habilidade do dono acabou de acontecer.</summary>
		public virtual void AfterEffect(UnitRule rule, Cast cast, EffectDefinition effect) { }

		/// <summary>A habilidade do dono acabou: <see cref="Cast.Dealt"/> tem o dano de cada alvo.</summary>
		public virtual void AfterSkill(UnitRule rule, Cast cast) { }

		/// <summary>Uma habilidade de outra unidade acabou e atingiu o dono (não vale para contra-ataque).</summary>
		public virtual void AfterStruck(UnitRule rule, Cast cast) { }
	}
}
