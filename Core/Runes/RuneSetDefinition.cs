using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Um conjunto de runas: com <see cref="Pieces"/> runas do mesmo conjunto, ganha o bônus. Ou é
	/// atributo (<see cref="Stat"/>, com <see cref="Value"/> sobre a base), ou é efeito de combate
	/// (<see cref="Effect"/>, com <see cref="Value"/> de chance, fração ou turnos). <see cref="Glyph"/> é
	/// o Glifo do conjunto: o símbolo de runa que quer dizer o bônus dele.
	/// </summary>
	public sealed record RuneSetDefinition(RuneSet Set, Glyph Glyph, int Pieces, Stat? Stat, double Value, RuneSetEffect Effect);
}
