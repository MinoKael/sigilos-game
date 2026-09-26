namespace Sigilos.Core.Battle
{
	/// <summary>
	/// A decisão de uma invocação ou inimigo no turno: qual habilidade (o índice em
	/// <see cref="BattleUnit.Skills"/>; 0 é a básica) e em quem mira. <see cref="Target"/> fica nulo
	/// quando a habilidade não pede alvo.
	/// </summary>
	public sealed record UnitAction(int Skill, BattleUnit? Target);
}
