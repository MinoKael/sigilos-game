namespace Sigilos.Core.Battle
{
	/// <summary>A decisão de uma invocação ou inimigo no turno: qual habilidade, se paga o aprimoramento
	/// e em quem mira. <see cref="Target"/> fica nulo quando a habilidade não pede alvo.</summary>
	public sealed record UnitAction(SkillSlot Slot, bool Enhance, BattleUnit? Target);
}
