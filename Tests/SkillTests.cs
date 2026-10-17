using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Battle;
using Sigilos.Core.Battle.Effects;
using Sigilos.Core.Battle.Passives;
using Sigilos.Core.Battle.Statuses;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>Habilidades: recarga de cada uma, níveis (cópias), versão desperta e o automático.</summary>
	internal static class SkillTests
	{
		private static readonly SkillDefinition Special = TestData.Strike with { Name = "Especial", Cooldown = 3, Effects = new[] { new EffectDefinition { Kind = EffectKind.Damage, Power = 2 } } };

		[Test]
		private static void EveryKindHasItsStrategy()
		{
			foreach (var kind in Enum.GetValues<EffectKind>())
				Assert.True(Has(() => SkillEffects.Of(kind)), $"falta a estratégia do efeito {kind} em Core/Battle/Effects/SkillEffects.cs");
			foreach (var kind in Enum.GetValues<StatusKind>())
				Assert.True(Has(() => StatusBehaviors.Of(kind)), $"falta a estratégia do status {kind} em Core/Battle/Statuses/StatusBehaviors.cs");
			foreach (var kind in Enum.GetValues<PassiveKind>())
				Assert.True(Has(() => PassiveBehaviors.Of(new PassiveDefinition { Kind = kind })), $"falta a estratégia da Passiva {kind} em Core/Battle/Passives/PassiveBehaviors.cs");

			static bool Has(Func<object> find)
			{
				try
				{
					return find() != null;
				}
				catch (KeyNotFoundException)
				{
					return false;
				}
			}
		}

		[Test]
		private static void RealSummonsFightWithTheirLeveledSkills()
		{
			var database = TestData.Database;
			var knight = database.Summon("knight_fire");
			var levels = knight.AllSkills.Select(s => s.MaxLevel).ToList();
			var (actives, passive) = BattleFactory.Prepare(knight.SkillsFor(true), levels, true);
			Assert.Equal(3, actives.Count, "o Cavaleiro de Fogo tem três ativas");
			Assert.Equal(null, passive, "e nenhuma Passiva");
			var cut = (int)knight.Skills[1].Bonus(SkillLevelKind.Cooldown, knight.Skills[1].MaxLevel);
			Assert.True(cut > 0, "a especial tem nível de recarga");
			Assert.Equal(knight.Skills[1].Cooldown - cut, actives[1].Cooldown, "no máximo, a especial perde os turnos de recarga dos níveis");
			Assert.True(actives[1].Effects.Any(e => e.IgnoreDefense > 0), "desperto, a especial usa a versão do Despertar");
		}

	}
}
