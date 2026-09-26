using System;
using System.Collections.Generic;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// Um monstro da conta: uma cópia de uma variante de Data/summons. Cada invocação cria uma cópia
	/// nova, nas estrelas naturais, no nível 1, com as habilidades no nível 1 e sem Despertar, mesmo que
	/// o jogador já tenha outra da mesma variante.
	/// </summary>
	public sealed class OwnedSummon
	{
		/// <summary>Único na conta: runas e equipes apontam para ele.</summary>
		public int Id { get; set; }

		/// <summary>A variante, pelo id de Data/summons.</summary>
		public string SummonId { get; set; } = "";

		/// <summary>Estrelas de agora, das naturais até 6 (Core/Progression/Evolution).</summary>
		public int Stars { get; set; } = 1;

		/// <summary>De 1 ao máximo das estrelas (Core/Progression/Leveling).</summary>
		public int Level { get; set; } = 1;

		/// <summary>Experiência acumulada dentro do nível atual.</summary>
		public int Experience { get; set; }

		/// <summary>
		/// Nível de cada habilidade, na ordem de <see cref="Content.SummonDefinition.AllSkills"/>. Falta
		/// na lista = nível 1. Sobe fundindo cópias (Core/Progression/Fusion).
		/// </summary>
		public List<int> SkillLevels { get; set; } = new();

		public int SkillLevel(int index) => index >= 0 && index < SkillLevels.Count ? Math.Max(1, SkillLevels[index]) : 1;

		public void RaiseSkill(int index)
		{
			while (SkillLevels.Count <= index)
				SkillLevels.Add(1);
			SkillLevels[index]++;
		}

		public bool Awakened { get; set; }

		/// <summary>Guardado no Baú: fora da coleção e sem equipe; as runas ficam com ele.</summary>
		public bool Stored { get; set; }
	}
}
