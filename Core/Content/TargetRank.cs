namespace Sigilos.Core.Content
{
	/// <summary>
	/// O valor que <see cref="TargetKind.LowestAlly"/> e <see cref="TargetKind.HighestAlly"/> comparam para
	/// escolher o aliado (<see cref="EffectDefinition.By"/>). Os atributos são os de agora, com os efeitos.
	/// </summary>
	public enum TargetRank
	{
		/// <summary>A fração de Vida (o padrão: LowestAlly é o mais ferido).</summary>
		Health,

		Attack,
		Defense,
		Speed,
		Crit,
		CritDamage,
		Resistance,
		Accuracy,

		/// <summary>O Ímpeto acumulado (quem está mais perto do turno).</summary>
		Impeto,
	}
}
