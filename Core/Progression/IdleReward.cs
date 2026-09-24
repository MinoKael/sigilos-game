namespace Sigilos.Core.Progression
{
	/// <summary>O que a ociosidade entrega numa coleta.</summary>
	public sealed record IdleReward(int Scrolls, int Essence, double Hours)
	{
		public bool IsEmpty => Scrolls == 0 && Essence == 0;
	}
}
