using System.Collections.Generic;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// O começo de um turno. Quando <see cref="NeedsDecision"/> é falso o turno já acabou sozinho
	/// (atordoado, queimado até cair, renascendo), e <see cref="Events"/> conta o que houve.
	/// </summary>
	public sealed record TurnStart(ITurnTaker Actor, bool NeedsDecision, IReadOnlyList<BattleEvent> Events);
}
