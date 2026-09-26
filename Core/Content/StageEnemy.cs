namespace Sigilos.Core.Content
{
	/// <summary>
	/// Um inimigo numa onda. Quase sempre uma invocação (<see cref="Summon"/>, id de Data/summons: a
	/// variante já traz o elemento), com os atributos reforçados para a luta. Criaturas que não existem
	/// como invocação — os chefes — vêm de Data/enemies.json (<see cref="Enemy"/>), com o elemento
	/// escolhido aqui.
	/// </summary>
	public sealed record StageEnemy
	{
		public string? Summon { get; init; }
		public string? Enemy { get; init; }

		/// <summary>Só para <see cref="Enemy"/>.</summary>
		public Element Element { get; init; }
	}
}
