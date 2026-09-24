namespace Sigilos.Core.Content
{
	/// <summary>Um inimigo numa onda: quem é e de que elemento.</summary>
	public sealed record StageEnemy
	{
		public string Enemy { get; init; } = "";
		public Element Element { get; init; }
	}
}
