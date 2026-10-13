namespace Sigilos.Core.Progression
{
	/// <summary>Um selo do Grimório do Alquimista (<see cref="Seals"/>): quanto o jogador já fez do marco e quanto ele pede.</summary>
	public readonly record struct Seal(string Id, int Progress, int Goal)
	{
		public bool Done => Progress >= Goal;
	}
}
