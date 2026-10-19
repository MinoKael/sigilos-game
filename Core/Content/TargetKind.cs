namespace Sigilos.Core.Content
{
	/// <summary>
	/// Em quem o efeito cai, sempre do ponto de vista de quem lança: "Ally" é do lado de quem lança,
	/// "Enemy" é do outro lado. <see cref="Target"/> é o inimigo escolhido na hora. Os ao acaso sorteiam
	/// entre os de pé, uma vez por habilidade (o efeito seguinte cai no mesmo sorteado).
	/// </summary>
	public enum TargetKind
	{
		Target,
		AllEnemies,
		Self,

		/// <summary>O aliado com o menor valor de <see cref="EffectDefinition.By"/> (padrão: a fração de Vida).</summary>
		LowestAlly,

		AllAllies,

		/// <summary>O aliado com o maior valor de <see cref="EffectDefinition.By"/>.</summary>
		HighestAlly,

		RandomAlly,
		RandomEnemy,

		/// <summary>
		/// Um aliado que o jogador escolhe, menos quem lança (o Filactério). Sem escolha válida (o automático, um
		/// inimigo), o aliado de menor fração de Vida (<see cref="Battle.Targeting.DefaultAlly"/>).
		/// </summary>
		ChosenAlly,
	}
}
