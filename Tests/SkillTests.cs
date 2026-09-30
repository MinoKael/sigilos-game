using System;
using System.Collections.Generic;
using System.Linq;
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
		private static void EachSkillHasItsOwnCooldown()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, special: Special);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(1, foe));
			Assert.Equal(2, hero.Cooldown(1), "recarga 3 conta o turno de uso");
			Assert.False(hero.IsReady(1), "em recarga");
			Assert.True(hero.IsReady(0), "a básica está sempre pronta");

			TestData.RunUntilTurnOf(session, hero);
			var before = foe.Health;
			session.Act(new UnitAction(1, foe));
			Assert.Near(before - 100, foe.Health, "em recarga, vira a básica");
		}

		[Test]
		private static void SkillLevelsRaiseDamageChanceAndCutCooldown()
		{
			var skill = new SkillDefinition
			{
				Name = "Teste",
				Cooldown = 4,
				Effects = new[]
				{
					new EffectDefinition { Kind = EffectKind.Damage, Power = 2 },
					new EffectDefinition { Kind = EffectKind.Status, Status = StatusKind.Stun, Chance = 0.3 },
					new EffectDefinition { Kind = EffectKind.Heal, Power = 0.1 },
				},
				Levels = new[]
				{
					new SkillLevelUp { Kind = SkillLevelKind.Damage, Value = 0.1 },
					new SkillLevelUp { Kind = SkillLevelKind.EffectRate, Value = 0.2 },
					new SkillLevelUp { Kind = SkillLevelKind.Recovery, Value = 0.5 },
					new SkillLevelUp { Kind = SkillLevelKind.Cooldown, Value = 1 },
				},
			};

			Assert.Equal(5, skill.MaxLevel, "4 melhorias: nível máximo 5");
			var base1 = skill.At(1, false);
			Assert.Near(2, base1.Effects[0].Power, "nível 1 sem bônus");
			var max = skill.At(5, false);
			Assert.Near(2.2, max.Effects[0].Power, "+10% de dano", 1e-9);
			Assert.Near(0.5, max.Effects[1].Chance, "+20 pontos de chance", 1e-9);
			Assert.Near(0.15, max.Effects[2].Power, "+50% de cura", 1e-9);
			Assert.Equal(3, max.Cooldown, "recarga −1");
		}

		[Test]
		private static void AwakenedVersionReplacesTheEffects()
		{
			var skill = Special with { AwakenedEffects = new[] { new EffectDefinition { Kind = EffectKind.Damage, Power = 5 } } };
			Assert.Near(2, skill.At(1, false).Effects.Single().Power, "sem despertar");
			Assert.Near(5, skill.At(1, true).Effects.Single().Power, "desperta, a lista troca");
			Assert.True(skill.ChangesOnAwakening, "muda no Despertar");
		}

		[Test]
		private static void ASkillMixesPlainHitsWithOneThatIgnoresDefense()
		{
			// "Quatro golpes: três comuns e o último ignorando a Defesa" são dois efeitos de dano em
			// Data/, um depois do outro. Nenhum código novo.
			var flurry = TestData.Strike with
			{
				Effects = new[]
				{
					new EffectDefinition { Kind = EffectKind.Damage, Power = 1, Hits = 3 },
					new EffectDefinition { Kind = EffectKind.Damage, Power = 1, IgnoreDefense = 1 },
				},
			};
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, element: Element.Light, basic: flurry);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 10_000, defense: BattleRules.DefenseConstant, element: Element.Light);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			var hits = session.Act(new UnitAction(0, foe)).OfType<Damaged>().Select(d => d.Amount).ToList();
			Assert.Equal("50, 50, 50, 100", string.Join(", ", hits), "três golpes pela metade (a Defesa corta) e o último cheio");
		}

		[Test]
		private static void EveryKindHasItsStrategy()
		{
			foreach (var kind in Enum.GetValues<EffectKind>())
				Assert.True(Has(() => SkillEffects.Of(kind)), $"falta a estratégia do efeito {kind} em Core/Battle/Effects/SkillEffects.cs");
			foreach (var kind in Enum.GetValues<StatusKind>())
				Assert.True(Has(() => StatusBehaviors.Of(kind)), $"falta a estratégia do status {kind} em Core/Battle/Statuses/StatusBehaviors.cs");
			foreach (var kind in Enum.GetValues<PassiveKind>())
				Assert.True(Has(() => PassiveBehaviors.Of(kind)), $"falta a estratégia da Passiva {kind} em Core/Battle/Passives/PassiveBehaviors.cs");

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
		private static void AutoPilotUsesTheStrongestReadySkill()
		{
			var third = Special with { Name = "Terceira", Cooldown = 5 };
			var hero = new BattleUnit("herói", "herói", "", Side.Allies, Element.Fire, 1, false,
				new StatBlock { Health = 1000, Attack = 100, Speed = 300, CritDamage = 0.5 },
				new[] { TestData.Strike, Special, third }, null, RuneSetEffects.None);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			Assert.Equal(2, AutoPilot.ForAlly(session, hero).Skill, "a de maior número pronta");
			session.Act(AutoPilot.ForAlly(session, hero));
			TestData.RunUntilTurnOf(session, hero);
			Assert.Equal(1, AutoPilot.ForAlly(session, hero).Skill, "a terceira em recarga: usa a segunda");
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
			Assert.Equal(knight.Skills[1].Cooldown - 1, actives[1].Cooldown, "no máximo, a especial perde um turno de recarga");
			Assert.True(actives[1].Effects.Any(e => e.IgnoreDefense > 0), "desperto, a especial usa a versão do Despertar");
		}
	}
}
