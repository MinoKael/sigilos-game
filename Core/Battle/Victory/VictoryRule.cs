namespace Sigilos.Core.Battle.Victory
{
	/// <summary>
	/// Quando uma luta está ganha: uma estratégia por <see cref="Content.VictoryCondition"/> (a tabela está
	/// em <see cref="VictoryRules"/>). A <see cref="BattleSession"/> pergunta depois de cada queda e de
	/// cada fim de turno, antes de passar de onda; a derrota ela mesma confere antes.
	/// </summary>
	internal abstract class VictoryRule
	{
		public abstract bool Won(BattleSession session);
	}
}
