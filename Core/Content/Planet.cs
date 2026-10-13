namespace Sigilos.Core.Content
{
	/// <summary>
	/// Os planetas que identificam os Sigilos (versão 0.1): o nome do conjunto é o do efeito, e o símbolo é o
	/// de um planeta. A associação efeito → planeta mora em <see cref="Runes.RuneSets.PlanetOf"/>; o desenho de
	/// cada um, em Assets/Planets (<c>earth.svg</c>, <c>mars.svg</c>...).
	/// </summary>
	public enum Planet
	{
		Earth,
		Mercury,
		Saturn,
		Neptune,
		Venus,
		Jupiter,
		Mars,
		Uranus,

		/// <summary>O Sol: a Defesa, que não tem conjunto na versão 0.1 (os oito conjuntos usam os outros oito planetas).</summary>
		Sun,
	}
}
