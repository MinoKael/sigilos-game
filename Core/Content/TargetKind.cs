namespace Sigilos.Core.Content
{
	/// <summary>
	/// Em quem o efeito cai, sempre do ponto de vista de quem lança: "Ally" é do lado de quem lança,
	/// "Enemy" é do outro lado. <see cref="Target"/> é o inimigo escolhido na hora.
	/// </summary>
	public enum TargetKind
	{
		Target,
		TwoEnemies,
		AllEnemies,
		Self,
		LowestAlly,
		TwoAllies,
		AllAllies,
		DeadAlly,
	}
}
