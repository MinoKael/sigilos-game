namespace Sigilos.Core.Content
{
	/// <summary>
	/// Quando um efeito de habilidade acontece (<see cref="EffectDefinition.When"/>). O padrão é uma vez, na ordem da
	/// lista. Os outros três ligam o efeito a cada golpe do dano da mesma habilidade, no alvo que o golpe acertou:
	/// "ataca 3 vezes e, a cada golpe, ...".
	/// </summary>
	public enum HitTrigger
	{
		/// <summary>Uma vez por habilidade, na ordem da lista.</summary>
		Skill,

		/// <summary>Depois de cada golpe que acerta.</summary>
		Hit,

		/// <summary>Depois de cada golpe que acerta e é crítico.</summary>
		Crit,

		/// <summary>
		/// Para cada efeito de status que o golpe causou no alvo (o que a habilidade, a Passiva ou o conjunto de runas
		/// de quem ataca puseram nele naquele golpe, inclusive a renovação): só os de <see cref="EffectDefinition.WhenStatus"/>
		/// (ou, sem ele, os de <see cref="EffectDefinition.WhenScope"/>). O que este próprio efeito causa não o dispara de novo.
		/// </summary>
		Status,
	}
}
