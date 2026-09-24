using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Tudo o que o lado do jogador leva para a luta: até 4 invocações (a primeira é a Líder), o nível
	/// compartilhado, o Conjurador e as páginas do Grimório em ordem de prioridade.
	/// </summary>
	public sealed record BattleTeam(
		IReadOnlyList<TeamMember> Members,
		int Level,
		ConjurerDefinition Conjurer,
		IReadOnlyList<PageDefinition> Pages);
}
