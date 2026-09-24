namespace Sigilos.Core.Runes
{
	/// <summary>
	/// O que uma runa pode dar. "Flat" soma o número; "Percent" soma uma fração do atributo de base
	/// (0,10 = +10%). Crítico, Dano crítico, Resistência e Foco já são frações e somam direto.
	/// </summary>
	public enum RuneStat
	{
		HealthFlat,
		HealthPercent,
		AttackFlat,
		AttackPercent,
		DefenseFlat,
		DefensePercent,
		Speed,
		Crit,
		CritDamage,
		Resistance,
		Focus,
	}
}
