using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>
	/// A conta dos efeitos (Core/Content/EffectScaling, Core/Battle/EffectAmount): outro atributo, termos somados,
	/// os fatores de Vida, aliados e Velocidade, o dano fixo e o Revive. Os números são de unidades montadas à
	/// mão: Ataque 100, Vida 1000, Defesa 0, sem crítico.
	/// </summary>
	internal static class FormulaTests
	{
		private static SkillDefinition Skill(params EffectDefinition[] effects) => TestData.Strike with { Effects = effects };

		/// <summary>O dano de cada golpe da básica de <paramref name="hero"/> em <paramref name="foe"/>.</summary>
		private static List<int> Hits(BattleUnit hero, BattleUnit foe, params BattleUnit[] friends)
		{
			var session = TestData.Session(friends.Prepend(hero).ToList(), new[] { foe });
			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			return session.Act(new UnitAction(0, foe)).OfType<Damaged>().Where(d => d.Target == foe).Select(d => d.Amount).ToList();
		}

		private static BattleUnit Foe(double defense = 0, double health = 1_000_000, double speed = 100) =>
			TestData.Unit("inimigo", Side.Enemies, speed: speed, health: health, attack: 1, defense: defense);

		[Test]
		private static void PlusTermsAddOtherStats()
		{
			// 0,5 × Ataque + 0,08 × Vida máxima, em dois golpes: 50 + 80.
			var basic = Skill(new EffectDefinition
			{
				Kind = EffectKind.Damage, Power = 0.5, Hits = 2,
				Plus = new[] { new ScaleTerm { Stat = ScaleStat.MaxHealth, Power = 0.08 } },
			});
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: basic);
			Assert.Equal("130, 130", string.Join(", ", Hits(hero, Foe())), "a Vida máxima soma ao Ataque");
		}

		[Test]
		private static void StatReplacesTheAttack()
		{
			// 1,3 × Defesa: 130.
			var basic = Skill(new EffectDefinition { Kind = EffectKind.Damage, Power = 1.3, Stat = ScaleStat.Defense });
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, defense: 100, attack: 1, basic: basic);
			Assert.Equal(130, Hits(hero, Foe()).Single(), "escala na Defesa, não no Ataque");
		}

		[Test]
		private static void FactorReadsTheCastersHealth()
		{
			// Defesa × (8,5 − 3 × Vida atual em %).
			var basic = Skill(new EffectDefinition
			{
				Kind = EffectKind.Damage, Power = 1, Stat = ScaleStat.Defense,
				Factor = new ScaleFactor { By = ScaleMeasure.HealthFraction, Base = 8.5, Slope = -3 },
			});
			var full = TestData.Unit("cheio", Side.Allies, speed: 300, defense: 100, basic: basic);
			Assert.Equal(550, Hits(full, Foe()).Single(), "Vida cheia: ×5,5");

			var hurt = TestData.Unit("ferido", Side.Allies, speed: 300, defense: 100, basic: basic);
			hurt.Health = 500;
			Assert.Equal(700, Hits(hurt, Foe()).Single(), "metade da Vida: ×7");
		}

		[Test]
		private static void FactorReadsTheTargetsHealthAndTheLivingAllies()
		{
			// Ataque × (4,1 − 1,6 × Vida atual do alvo em %), com o alvo pela metade: ×3,3.
			var penalty = Skill(new EffectDefinition
			{
				Kind = EffectKind.Damage, Power = 1,
				Factor = new ScaleFactor { By = ScaleMeasure.TargetHealthFraction, Base = 4.1, Slope = -1.6 },
			});
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: penalty);
			var foe = Foe(health: 10_000);
			foe.Health = 5_000;
			Assert.Equal(330, Hits(hero, foe).Single(), "o alvo pela metade");

			// Ataque × (13,5 − 5,5 × aliados de pé em %), com um de dois de pé: ×10,75.
			var justice = Skill(new EffectDefinition
			{
				Kind = EffectKind.Damage, Power = 1,
				Factor = new ScaleFactor { By = ScaleMeasure.LivingAllies, Base = 13.5, Slope = -5.5 },
			});
			var judge = TestData.Unit("juiz", Side.Allies, speed: 300, basic: justice);
			var fallen = TestData.Unit("caído", Side.Allies, speed: 1);
			fallen.Health = 0;
			Assert.Equal(1075, Hits(judge, Foe(), fallen).Single(), "metade da equipe de pé");
		}

		[Test]
		private static void SpeedFactorDividesByANumberOrTheTargetsSpeed()
		{
			// Ataque × (Velocidade + 125) / 115, com Velocidade 120: 213.
			var wind = Skill(new EffectDefinition { Kind = EffectKind.Damage, Power = 1, Speed = new SpeedFactor { Add = 125, Over = 115 } });
			var hero = TestData.Unit("herói", Side.Allies, speed: 120, basic: wind);
			Assert.Equal(213, Hits(hero, Foe(speed: 1)).Single(), "dividido por 115");

			// 1,8 × Ataque × (Velocidade + 80) / Velocidade do alvo: 180 × 200 / 100.
			var silver = Skill(new EffectDefinition { Kind = EffectKind.Damage, Power = 1.8, Speed = new SpeedFactor { Add = 80, OverTarget = true } });
			var archer = TestData.Unit("arqueiro", Side.Allies, speed: 120, basic: silver);
			Assert.Equal(360, Hits(archer, Foe(speed: 100)).Single(), "dividido pela Velocidade do alvo");
		}

		[Test]
		private static void FixedDamageSkipsDefenseElementAndCrit()
		{
			var basic = Skill(new EffectDefinition { Kind = EffectKind.Damage, Power = 2, Fixed = true });
			var stats = new StatBlock { Health = 1000, Attack = 100, Speed = 300, Crit = 1, CritDamage = 0.5 };
			var hero = new BattleUnit("herói", "herói", "", Side.Allies, Element.Water, 1, false, stats, new[] { basic }, null, RuneSetEffects.None);
			var foe = Foe(defense: BattleRules.DefenseConstant);

			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			var hit = session.Act(new UnitAction(0, foe)).OfType<Damaged>().Single();
			Assert.Equal(200, hit.Amount, "200% do Ataque, sem cortar pela Defesa nem somar a vantagem de elemento");
			Assert.False(hit.Crit, "dano fixo não é crítico");
		}

		[Test]
		private static void ShieldScalesOnTheLevel()
		{
			var field = new SkillDefinition
			{
				Name = "Campo",
				Effects = new[] { new EffectDefinition { Kind = EffectKind.Shield, Target = TargetKind.AllAllies, Power = 110, Stat = ScaleStat.Level, Turns = 2 } },
			};
			var stats = new StatBlock { Health = 1000, Attack = 100, Speed = 300, CritDamage = 0.5 };
			var hero = new BattleUnit("herói", "herói", "", Side.Allies, Element.Fire, 35, false, stats, new[] { field }, null, RuneSetEffects.None);
			var friend = TestData.Unit("amigo", Side.Allies);
			var session = TestData.Session(new[] { hero, friend }, new[] { Foe() });
			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, null));
			Assert.Near(3850, friend.Find(StatusKind.Shield)?.Value ?? 0, "110 por nível, no nível 35");
		}

		[Test]
		private static void ReviveBringsBackFallenAlliesWithTheFormula()
		{
			// 0,5 × Vida atual em % × Vida máxima do alvo, com quem lança pela metade: 250 de 1000.
			var revival = Skill(new EffectDefinition
			{
				Kind = EffectKind.Revive, Target = TargetKind.AllAllies, Power = 0.5, Count = 1,
				Factor = new ScaleFactor { By = ScaleMeasure.HealthFraction, Slope = 1 },
			});
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: revival);
			var first = TestData.Unit("primeiro", Side.Allies, speed: 1);
			var second = TestData.Unit("segundo", Side.Allies, speed: 1);
			var session = TestData.Session(new[] { hero, first, second }, new[] { Foe() });
			session.Start();
			var resolver = new EffectResolver(session);
			resolver.KnockOut(first);
			resolver.KnockOut(second);
			hero.Health = 500;

			TestData.RunUntilTurnOf(session, hero);
			var events = session.Act(new UnitAction(0, null));
			var back = events.OfType<Revived>().Select(r => r.Unit).ToList();
			Assert.Equal(1, back.Count, "count 1: só um volta");
			Assert.Near(250, back.Single().Health, "metade da Vida máxima, vezes a Vida de quem lança");
			Assert.Equal(1, new[] { first, second }.Count(u => !u.IsAlive), "o outro fica caído");
		}

		[Test]
		private static void SkillLevelsScaleThePlusTerms()
		{
			var skill = new SkillDefinition
			{
				Name = "Teste",
				Effects = new[]
				{
					new EffectDefinition { Kind = EffectKind.Damage, Power = 0.5, Plus = new[] { new ScaleTerm { Stat = ScaleStat.MaxHealth, Power = 0.08 } } },
					new EffectDefinition { Kind = EffectKind.Heal, Power = 0.1, Plus = new[] { new ScaleTerm { Stat = ScaleStat.Attack, Power = 1 } } },
				},
				Levels = new[]
				{
					new SkillLevelUp { Kind = SkillLevelKind.Damage, Value = 0.5 },
					new SkillLevelUp { Kind = SkillLevelKind.Recovery, Value = 0.2 },
				},
			};
			var max = skill.At(3, false);
			Assert.Near(0.12, max.Effects[0].Plus.Single().Power, "+50% de dano no termo somado", 1e-9);
			Assert.Near(1.2, max.Effects[1].Plus.Single().Power, "+20% de cura no termo somado", 1e-9);
			Assert.Near(0.08, skill.Effects[0].Plus.Single().Power, "a habilidade de Data/ não muda", 1e-9);
		}

		[Test]
		private static void JsonReadsTheFormulaFields()
		{
			const string json = "{\"kind\": \"Damage\", \"power\": 1, \"stat\": \"Defense\", \"fixed\": true, " +
				"\"plus\": [{\"stat\": \"TargetMaxHealth\", \"power\": 0.06}], " +
				"\"factor\": {\"by\": \"LivingAllies\", \"base\": 13.5, \"slope\": -5.5}, " +
				"\"speed\": {\"add\": 80, \"overTarget\": true}}";
			var effect = JsonSerializer.Deserialize<EffectDefinition>(json, GameDatabase.JsonOptions)!;
			Assert.Equal(ScaleStat.Defense, effect.Stat, "stat");
			Assert.True(effect.Fixed, "fixed");
			Assert.Equal(ScaleStat.TargetMaxHealth, effect.Plus.Single().Stat, "plus");
			Assert.Equal(ScaleMeasure.LivingAllies, effect.Factor?.By, "factor");
			Assert.True(effect.Speed is { Add: 80, OverTarget: true }, "speed");

			var plain = JsonSerializer.Deserialize<EffectDefinition>("{\"kind\": \"Damage\", \"power\": 1}", GameDatabase.JsonOptions)!;
			Assert.False(EffectScaling.IsCustom(plain), "sem os campos, a conta é a de sempre");
			Assert.True(plain == new EffectDefinition { Kind = EffectKind.Damage, Power = 1 }, "e o efeito continua igual a outro sem eles");
		}

		[Test]
		private static void ValidationRejectsFormulaFieldsWhereTheyDoNotFit()
		{
			IReadOnlyList<string> Problems(EffectDefinition effect) => GameDatabase.ValidateSkill(Skill(effect)).ToList();

			Assert.True(Problems(new EffectDefinition { Kind = EffectKind.Status, Status = StatusKind.Stun, Chance = 1, Turns = 1, Stat = ScaleStat.Defense }).Count > 0, "Status não tem conta");
			Assert.True(Problems(new EffectDefinition { Kind = EffectKind.Heal, Power = 0.1, Fixed = true }).Count > 0, "só o dano é fixo");
			Assert.True(Problems(new EffectDefinition { Kind = EffectKind.Damage, Power = 1, Speed = new SpeedFactor { Add = 60 } }).Count > 0, "speed sem divisor");
			Assert.True(Problems(new EffectDefinition { Kind = EffectKind.Damage, Power = 1, Factor = new ScaleFactor { By = ScaleMeasure.HealthFraction } }).Count > 0, "factor que dá sempre 0");
			Assert.True(Problems(new EffectDefinition { Kind = EffectKind.Revive, Target = TargetKind.AllAllies }).Count > 0, "Revive sem Vida");
			Assert.Empty(Problems(new EffectDefinition { Kind = EffectKind.Revive, Target = TargetKind.AllAllies, Power = 0.3 }), "Revive de 30%");
			Assert.Empty(Problems(new EffectDefinition { Kind = EffectKind.Shield, Target = TargetKind.Self, Power = 110, Stat = ScaleStat.Level, Turns = 2 }), "escudo por nível");
		}

		[Test]
		private static void FiltersFindTheNewScalings()
		{
			var effects = new[]
			{
				new EffectDefinition { Kind = EffectKind.Damage, Power = 1, Stat = ScaleStat.Defense, Factor = new ScaleFactor { By = ScaleMeasure.HealthFraction, Base = 8.5, Slope = -3 } },
				new EffectDefinition { Kind = EffectKind.Damage, Power = 1, Speed = new SpeedFactor { Add = 80, OverTarget = true }, Fixed = true },
				new EffectDefinition { Kind = EffectKind.Revive, Target = TargetKind.AllAllies, Power = 0.3 },
			};
			var summon = TestData.Summon("phoenix_fire") with { Skills = new[] { Skill(effects) } };

			var scalings = SkillTraits.ScalingsOf(summon, false).ToList();
			foreach (var expected in new[] { SkillScaling.Attack, SkillScaling.Defense, SkillScaling.HealthFraction, SkillScaling.Speed, SkillScaling.TargetSpeed, SkillScaling.TargetMaxHealth })
				Assert.True(scalings.Contains(expected), $"escala em {expected}");
			Assert.False(scalings.Contains(SkillScaling.MaxHealth), "nada lê a Vida máxima de quem lança");

			var behaviors = SkillTraits.BehaviorsOf(summon, false).ToList();
			Assert.True(behaviors.Contains(SkillBehavior.FixedDamage), "dano fixo");
			Assert.True(behaviors.Contains(SkillBehavior.Revive), "revive");
		}
	}
}
