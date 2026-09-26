namespace Sigilos.Core.Content
{
	/// <summary>
	/// O Despertar de uma variante (GDD, seções 8 e 10): o nome próprio e um bônus. Todo Despertar dá
	/// mais Vida, Ataque e Defesa (Core/Progression/Awakening.cs); além disso, a variante ganha um
	/// destes: um atributo (<see cref="Stat"/>, quase sempre as 5★), uma habilidade nova
	/// (<see cref="Skill"/>, quase sempre as 3★) ou uma habilidade melhorada
	/// (<see cref="SkillDefinition.AwakenedEffects"/>, quase sempre as 4★).
	/// </summary>
	public sealed record AwakeningDefinition
	{
		public string Name { get; init; } = "";

		/// <summary>Velocidade, Crítico, Resistência ou Precisão; nulo quando o bônus é outro.</summary>
		public Stat? Stat { get; init; }

		/// <summary>A habilidade que só existe desperta.</summary>
		public SkillDefinition? Skill { get; init; }
	}
}
