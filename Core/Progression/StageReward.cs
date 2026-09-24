namespace Sigilos.Core.Progression
{
	/// <summary>O que uma vitória de fase entregou.</summary>
	public sealed record StageReward(int Scrolls, int Essence, bool FirstClear);
}
