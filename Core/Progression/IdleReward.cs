namespace Sigilos.Core.Progression
{
	/// <summary>O que a ociosidade entrega numa coleta. A Mana já vem limitada ao que cabe até o máximo.</summary>
	public sealed record IdleReward(int Essence, int Gold, int Mana, double Hours)
	{
		public bool IsEmpty => Essence == 0 && Gold == 0 && Mana == 0;
	}
}
