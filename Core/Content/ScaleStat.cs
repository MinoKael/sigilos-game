namespace Sigilos.Core.Content
{
	/// <summary>
	/// O atributo em que um termo da conta do efeito escala (<see cref="EffectDefinition.Stat"/> e
	/// <see cref="ScaleTerm.Stat"/>). Os de quem lança são os de agora, com os efeitos (Ataque+, Quebra de
	/// Defesa...); os do alvo são de quem recebe o golpe, a cura ou o escudo.
	/// </summary>
	public enum ScaleStat
	{
		/// <summary>O Ataque de quem lança (o de sempre no dano).</summary>
		Attack,

		/// <summary>A Defesa de quem lança.</summary>
		Defense,

		/// <summary>A Vida máxima de quem lança (a de sempre no escudo).</summary>
		MaxHealth,

		/// <summary>A Velocidade de quem lança.</summary>
		Speed,

		/// <summary>A Vida máxima do alvo (a de sempre na cura).</summary>
		TargetMaxHealth,

		/// <summary>O nível de quem lança: o power é o valor por nível (110 = 110 por nível).</summary>
		Level,
	}
}
