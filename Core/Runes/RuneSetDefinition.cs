using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Um conjunto de runas: com <see cref="Pieces"/> runas do mesmo Glifo, ganha o bônus. Ou é
	/// atributo (<see cref="Stat"/>, com <see cref="Value"/> no estilo da Liderança), ou é efeito de
	/// combate (<see cref="Effect"/>, com <see cref="Value"/> de chance ou fração).
	/// </summary>
	public sealed record RuneSetDefinition(Glyph Set, int Pieces, Stat? Stat, double Value, RuneSetEffect Effect);
}
