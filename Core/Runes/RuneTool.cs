using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Uma pedra para trabalhar runas.
	/// A Pedra de Afiar soma um bônus a um subatributo do mesmo <see cref="Stat"/>; a Gema Encantada
	/// troca um subatributo por <see cref="Stat"/>. <see cref="Grade"/> vai de Mágica a Lendária.
	/// </summary>
	public sealed record RuneTool(RuneToolKind Kind, RuneStat Stat, RuneRarity Grade);
}
