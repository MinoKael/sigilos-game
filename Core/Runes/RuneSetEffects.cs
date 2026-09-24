namespace Sigilos.Core.Runes
{
	/// <summary>
	/// O que os conjuntos completos fazem em combate além de atributo, já somados (três conjuntos de 2
	/// peças iguais valem três vezes). Zero quando o conjunto não está completo. Frações: 0,35 = 35%.
	/// </summary>
	/// <param name="Drain">Vampiro: fração do dano causado que vira cura.</param>
	/// <param name="StunChance">Desespero: chance de atordoar cada alvo atingido por habilidade.</param>
	/// <param name="ExtraTurnChance">Violento: chance de turno extra depois de agir.</param>
	/// <param name="AllyShield">Escudo: Vida do escudo que todos os aliados recebem (já em números).</param>
	/// <param name="ImmunityTurns">Vontade: turnos de Imunidade no começo de cada onda.</param>
	/// <param name="CounterChance">Vingança: chance de contra-atacar quando é atingido.</param>
	/// <param name="NemesisGauge">Nêmesis: fração de Ímpeto por 7% da Vida máxima perdida.</param>
	/// <param name="DestroyCap">Destruição: até que fração da Vida máxima do alvo cada habilidade tira.</param>
	public sealed record RuneSetEffects(
		double Drain,
		double StunChance,
		double ExtraTurnChance,
		double AllyShield,
		int ImmunityTurns,
		double CounterChance,
		double NemesisGauge,
		double DestroyCap)
	{
		public static readonly RuneSetEffects None = new(0, 0, 0, 0, 0, 0, 0, 0);
	}
}
