using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>
	/// A reestruturação da versão 0.1: imunidades de chefe, Provocar como efeito positivo, as mecânicas das
	/// Masmorras (Núcleo Rúnico, Tesouro da Serpe, Afogamento), as condições de efeito, a exclusividade dos
	/// drops especiais e o elenco ativo.
	/// </summary>
	internal static class RestructureTests
	{
		private static SkillDefinition Skill(params EffectDefinition[] effects) => new() { Name = "teste", Effects = effects };

		private static EffectDefinition Inflict(StatusKind status, TargetKind target = TargetKind.Target, int turns = 2) =>
			new() { Kind = EffectKind.Status, Status = status, Target = target, Chance = 1, Turns = turns };

		// Chefes ---------------------------------------------------------------------------------------

		// Provocar -------------------------------------------------------------------------------------

		// Condições de efeito --------------------------------------------------------------------------

		// Mecânicas das Masmorras ----------------------------------------------------------------------

		// Recompensas ----------------------------------------------------------------------------------

		// Elenco, conjuntos e papéis -------------------------------------------------------------------

		[Test]
		private static void RetiredHomunculiStayInTheSaveButOutOfPlay()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("wolf_fire");
			var old = Roster.Add(player, database.Summon("imp_fire"));
			Assert.True(player.Monster(old.Id) != null, "o save guarda o monstro");
			var shown = new MonsterFilter().Apply(player.Monsters, database, player).Select(m => m.SummonId).ToList();
			Assert.False(shown.Contains("imp_fire"), "mas ele não aparece nas listas");
			Assert.True(shown.Contains("wolf_fire"), "e o ativo aparece");
		}

	}
}
