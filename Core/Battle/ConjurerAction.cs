namespace Sigilos.Core.Battle
{
	/// <summary>A decisão do Conjurador no turno: lançar uma página ou canalizar Éter.</summary>
	public sealed record ConjurerAction(PageSlot? Page, BattleUnit? Target)
	{
		public static readonly ConjurerAction Channel = new(null, null);

		public bool IsChannel => Page == null;
	}
}
