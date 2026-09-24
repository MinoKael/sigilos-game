using System.Collections.Generic;

namespace Sigilos.Core.Battle
{
	/// <summary>O lado do jogador numa luta: até 4 invocações. A primeira é a Líder.</summary>
	public sealed record BattleTeam(IReadOnlyList<TeamMember> Members);
}
