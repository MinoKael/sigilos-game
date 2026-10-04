namespace Sigilos.Core.Content
{
	/// <summary>
	/// Núcleo de Infusão: o material de fusão que serve a qualquer família. Mora na coleção ou no Baú como
	/// um monstro (ocupa vaga), mas não luta, não entra em equipe, não usa runas, não sobe de nível, não
	/// evolui, não desperta e não se solta: só vira material de fusão e sobe uma habilidade sorteada de
	/// qualquer monstro (Core/Progression/Fusion). Vem dos marcos do jogo (Core/Progression/Milestones).
	///
	/// Não mora em Data/summons e não sai na invocação: o <see cref="GameDatabase"/> cria a definição dele,
	/// fora da lista de invocações, só para quem procura pelo id.
	/// </summary>
	public static class InfusionCore
	{
		public const string Id = "infusion_core";

		public static bool Is(string summonId) => summonId == Id;

		/// <summary>A definição de que o jogo precisa para mostrar e guardar o Núcleo como um monstro da conta.</summary>
		public static SummonDefinition Summon { get; } = Create();

		private static SummonDefinition Create()
		{
			var family = new FamilyDefinition { Id = Id, BaseName = "Núcleo de Infusão", Name = "Núcleos de Infusão", Rarity = 1, Image = Id };
			return new SummonDefinition { Id = Id, Name = family.BaseName, Element = Element.Light, Role = Role.Support, Family = family };
		}
	}
}
