namespace Sigilos.UI.Audio
{
	/// <summary>Um som a tocar: o nome lógico do catálogo (<c>combat.critical_hit</c>) e quanto esperar antes, em segundos.</summary>
	public sealed record Cue(string Name, double Delay = 0);
}
