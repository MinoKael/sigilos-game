namespace Sigilos.Core.Progression
{
	/// <summary>O que a ociosidade entrega numa coleta.</summary>
	public sealed record IdleReward(int Scrolls, int Essence, int Dust, double Hours)
	{
		public bool IsEmpty => Scrolls == 0 && Essence == 0 && Dust == 0;
	}
}
