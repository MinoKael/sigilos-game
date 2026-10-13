using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Um conjunto de runas: com <see cref="Pieces"/> runas do mesmo conjunto, ganha o bônus. Ou é
	/// atributo (<see cref="Stat"/>, com <see cref="Value"/> sobre a base), ou é efeito de combate
	/// (<see cref="Effect"/>, com <see cref="Value"/> de chance, fração ou turnos). <see cref="Planet"/> é
	/// o planeta do Sigilo, o símbolo que identifica o conjunto (nulo nos conjuntos fora do jogo).
	/// </summary>
	public sealed record RuneSetDefinition(RuneSet Set, Planet? Planet, int Pieces, Stat? Stat, double Value, RuneSetEffect Effect);
}
