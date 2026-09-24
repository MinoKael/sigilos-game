namespace Sigilos.Core.Runes
{
	/// <summary>Um subatributo de runa. O valor cresce quando a runa chega a +3, +6 e +9.</summary>
	public sealed class RuneSubstat
	{
		public RuneStat Stat { get; set; }
		public double Value { get; set; }
	}
}
