namespace Sigilos.Core.Runes
{
	/// <summary>
	/// O que uma runa pode dar. "Flat" soma o número; "Percent" soma uma fração
	/// do atributo de base (0,10 = +10%). Velocidade soma o número; Crítico, Dano crítico, Resistência
	/// e Precisão já são frações e somam direto.
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
		Accuracy,
	}
}
