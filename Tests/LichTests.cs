using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Battle.Statuses;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>
	/// As peças novas das Liches: o Filactério (quem pôs não morre), o teto do bônus por efeitos, a Purificação que
	/// também tira os positivos, o dreno de Vida e de Ímpeto da Passiva e a cura ao derrubar da Investida de Ossos.
	/// </summary>
	internal static class LichTests
	{
		private static SkillDefinition Skill(string name, int cooldown, params EffectDefinition[] effects) =>
			new() { Name = name, Cooldown = cooldown, Effects = effects };

		/// <summary>Um herói e um parceiro ferido (o "aliado mais ferido"), contra um inimigo parado.</summary>
		private static (BattleSession Session, BattleUnit Hero, BattleUnit Buddy, BattleUnit Foe, EffectResolver Resolver) Scene(SkillDefinition special, int extraAllies = 0)
		{
			var hero = TestData.Unit("lich", Side.Allies, speed: 300, special: special);
			var buddy = TestData.Unit("parceiro", Side.Allies, speed: 1);
			var allies = new[] { hero, buddy }.Concat(Enumerable.Range(1, extraAllies).Select(i => TestData.Unit($"extra{i}", Side.Allies, speed: 1))).ToList();
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1, speed: 1);
			var session = TestData.Session(allies, new[] { foe });
			session.Start();
			buddy.Health = 400;
			return (session, hero, buddy, foe, new EffectResolver(session));
		}

		private static void Cast(BattleSession session, BattleUnit hero, BattleUnit foe)
		{
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(1, foe));
		}

		private static EffectDefinition Phylactery() => new() { Kind = EffectKind.Status, Target = TargetKind.ChosenAlly, Status = StatusKind.Phylactery, Turns = 3, Unremovable = true };

		[Test]
		private static void PhylacteryKeepsTheCasterAtOneHealthWhileTheHolderStands()
		{
			var (session, hero, buddy, foe, resolver) = Scene(Skill("filactério", 5, Phylactery()));
			Cast(session, hero, foe);
			Assert.True(buddy.Has(StatusKind.Phylactery), "o parceiro ficou com o Filactério");
			resolver.Wound(hero, 1_000_000);
			Assert.True(hero.IsAlive, "o dano que não é golpe não derruba quem tem a alma guardada");
			Assert.Equal(1.0, hero.Health, "a Vida para em 1");

			resolver.Wound(buddy, 1_000_000);
			Assert.False(buddy.IsAlive, "o dono do Filactério cai");
			resolver.Wound(hero, 1);
			Assert.False(hero.IsAlive, "sem o Filactério em pé, o Lich cai");
		}

		[Test]
		private static void PhylacteryGoesToTheChosenAllyAndIsUniquePerCaster()
		{
			var (session, hero, buddy, foe, _) = Scene(Skill("filactério", 1, Phylactery()), extraAllies: 1);
			var third = session.Allies.Single(unit => unit.Name == "extra1");
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(1, third));
			Assert.True(third.Has(StatusKind.Phylactery) && !buddy.Has(StatusKind.Phylactery), "foi no aliado escolhido, não no mais ferido");
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(1, buddy));
			Assert.True(buddy.Has(StatusKind.Phylactery) && !third.Has(StatusKind.Phylactery), "escolher outro tira o anterior: só um por quem pôs");
			Assert.False(hero.Has(StatusKind.Phylactery), "o Lich nunca é o alvo");
		}

		[Test]
		private static void WithoutAValidChoiceThePhylacteryGoesToTheMostHurtOtherAlly()
		{
			foreach (var pick in new Func<BattleUnit, BattleUnit, BattleUnit?>[] { (hero, _) => hero, (_, foe) => foe, (_, _) => null })
			{
				var (session, hero, buddy, foe, _) = Scene(Skill("filactério", 5, Phylactery()), extraAllies: 1);
				TestData.RunUntilTurnOf(session, hero);
				session.Act(new UnitAction(1, pick(hero, foe)));
				Assert.True(buddy.Has(StatusKind.Phylactery), "o parceiro, o mais ferido dos outros");
				Assert.False(hero.Has(StatusKind.Phylactery), "nunca o Lich");
			}
		}

		[Test]
		private static void ALoneLichCannotUseAnAllySkill()
		{
			var hero = TestData.Unit("lich", Side.Allies, speed: 300, special: Skill("filactério", 5, Phylactery()));
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1, speed: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			Assert.Equal(0, AutoPilot.For(session, hero).Skill, "sem outro aliado, o automático fica na básica");
			session.Act(new UnitAction(1, null));
			Assert.False(hero.Has(StatusKind.Phylactery), "sem aliado para escolher, nada é posto");
		}

		[Test]
		private static void AnAutoPilotLichPicksTheMostHurtOtherAlly()
		{
			var (session, hero, buddy, _, _) = Scene(Skill("filactério", 5, Phylactery()), extraAllies: 1);
			TestData.RunUntilTurnOf(session, hero);
			var action = AutoPilot.For(session, hero);
			Assert.Equal(1, action.Skill, "o automático usa a habilidade de aliado");
			Assert.True(action.Target == buddy, "e mira o mais ferido dos outros");
		}

		[Test]
		private static void AnAllyTargetCannotMixWithAnEnemyTarget()
		{
			var mixed = Skill("mista", 3, Phylactery(), new EffectDefinition { Kind = EffectKind.Damage, Target = TargetKind.Target, Power = 1 });
			Assert.True(TestData.Database.Validate().All(problem => !problem.Contains("mistura Target")), "o banco de verdade não mistura");
			var problems = GameDatabase.ValidateSkills(new[] { TestData.Strike, mixed });
			Assert.True(problems.Any(problem => problem.Contains("mistura Target")), "a mistura é erro");
		}

		private static EffectDefinition Bonus(TargetKind from, double max) =>
			new() { Kind = EffectKind.BonusPerStatus, Target = TargetKind.Self, From = from, Power = 0.2, Bonus = BonusKind.Damage, Max = max };

		private static double DamageOf(double max, int buffsOnAllies, int debuffsOnFoe)
		{
			var special = Skill("bônus", 3, Bonus(TargetKind.AllAllies, max), Bonus(TargetKind.AllEnemies, max),
				new EffectDefinition { Kind = EffectKind.Damage, Target = TargetKind.Target, Power = 1 });
			var (session, hero, _, foe, resolver) = Scene(special);
			foreach (var buff in new[] { StatusKind.Immunity, StatusKind.Blessing, StatusKind.Counter }.Take(buffsOnAllies))
				resolver.GiveStatus(hero, buff, 5);
			foreach (var debuff in new[] { StatusKind.AttackDown, StatusKind.SpeedDown, StatusKind.Silence, StatusKind.Unrecoverable }.Take(debuffsOnFoe))
				resolver.GiveStatus(foe, debuff, 5);
			var before = foe.Health;
			Cast(session, hero, foe);
			return before - foe.Health;
		}

		[Test]
		private static void TheBonusPerStatusCapIsSharedByTheEffects()
		{
			var plain = DamageOf(0, 0, 0);
			// 3 positivos nos aliados + 4 negativos no inimigo = 7 efeitos: +140% sem teto, +100% com teto.
			Assert.Near(plain * 2.4, DamageOf(0, 3, 4), "sem teto: 7 × 20%", 1e-6 * plain);
			Assert.Near(plain * 2.0, DamageOf(1.0, 3, 4), "teto de +100% para os dois efeitos juntos", 1e-6 * plain);
			Assert.Near(plain * 1.6, DamageOf(1.0, 1, 2), "abaixo do teto, vale a conta", 1e-6 * plain);
		}

		[Test]
		private static void CleanseCanRemoveBeneficialEffectsToo()
		{
			EffectDefinition Purge(StatusScope? scope) => new() { Kind = EffectKind.Cleanse, Target = TargetKind.Self, Count = 0, Removes = scope };
			(int Buffs, int Debuffs, bool Kept) Run(StatusScope? scope)
			{
				var (session, hero, _, foe, resolver) = Scene(Skill("purificar", 3, Purge(scope)));
				resolver.GiveStatus(hero, StatusKind.AttackUp, 5);
				resolver.GiveStatus(hero, StatusKind.DefenseUp, 5);
				resolver.GiveStatus(hero, StatusKind.SpeedDown, 5);
				resolver.GiveShield(hero, 100, 5, unremovable: true);
				Cast(session, hero, foe);
				var kept = hero.Has(StatusKind.Shield);
				return (hero.Statuses.Count(s => !StatusBehaviors.Of(s.Kind).Harmful && s.Kind != StatusKind.Shield), hero.Statuses.Count(s => StatusBehaviors.Of(s.Kind).Harmful), kept);
			}

			var legacy = Run(null);
			Assert.Equal(2, legacy.Buffs, "sem removes, os positivos ficam");
			Assert.Equal(0, legacy.Debuffs, "sem removes, só os negativos saem");
			var all = Run(StatusScope.All);
			Assert.Equal(0, all.Buffs, "All tira os positivos");
			Assert.Equal(0, all.Debuffs, "All tira os negativos");
			Assert.True(all.Kept, "o irremovível fica");
		}

		[Test]
		private static void SoulDrainTakesLifeAndImpetoOncePerSkill()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.SoulDrain, Value = 0.15 };
			var multi = TestData.Strike with { Effects = new[] { new EffectDefinition { Kind = EffectKind.Damage, Target = TargetKind.Target, Power = 1, Hits = 4 } } };
			var hero = TestData.Unit("lich", Side.Allies, speed: 300, basic: multi, passive: passive);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1, speed: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			hero.Health = 500;
			TestData.RunUntilTurnOf(session, hero);
			foe.Impeto = 40;
			hero.Impeto = 0;
			session.Act(new UnitAction(0, foe));
			Assert.Near(25, foe.Impeto, "o alvo perdeu 15 pontos, uma vez só (não por golpe)");
			Assert.True(hero.Health > 500, "e o dano drenou Vida");
		}

		[Test]
		private static void BoneChargeHealsOnlyWhenItKills()
		{
			var charge = TestData.Summon("lich_fire").Skills[0];
			double Heal(double foeHealth)
			{
				var hero = TestData.Unit("lich", Side.Allies, speed: 300, basic: charge, attack: 1000);
				var foe = TestData.Unit("inimigo", Side.Enemies, health: foeHealth, attack: 1, speed: 1);
				var session = TestData.Session(new[] { hero }, new[] { foe });
				session.Start();
				hero.Health = 500;
				TestData.RunUntilTurnOf(session, hero);
				var before = hero.Health;
				session.Act(new UnitAction(0, foe));
				return hero.Health - before;
			}

			Assert.Near(150, Heal(10), "derrubou: cura 15% da Vida máxima (1000)");
			Assert.Equal(0.0, Heal(1_000_000), "não derrubou: nada");
		}

		[Test]
		private static void TheLichSkillSetsAreTheSpecified()
		{
			foreach (var element in new[] { "fire", "water", "wind", "light", "dark" })
			{
				var lich = TestData.Summon($"lich_{element}");
				Assert.Equal(3, lich.Skills.Count, $"{lich.Id}: três habilidades");
				var attack = lich.Skills[0].Effects[0];
				Assert.True(attack.Kind == EffectKind.Damage && attack.Hits == 4 && attack.Power == 0.9, $"{lich.Id}: a Investida ataca 4 vezes com 90%");
				var passive = lich.Skills[1].Passive;
				var expected = element is "water" or "light" ? PassiveKind.DamageReduction : PassiveKind.SoulDrain;
				Assert.True(passive != null && passive.Kind == expected, $"{lich.Id}: a segunda é a Passiva {expected}");
			}

			var dark = TestData.Summon("lich_dark").Skills[2];
			Assert.True(dark.Effects.Any(e => e.Status == StatusKind.Phylactery && e.Unremovable && e.Turns == 3), "o Filactério é irremovível por 3 turnos");
			Assert.Equal(8, dark.Cooldown, "recarga 8");
		}
		[Test]
		private static void EveryLichFightsAndUsesItsThirdSkill()
		{
			var database = TestData.LoadReal();
			var encounter = new Encounter(4, 20, new IReadOnlyList<StageEnemy>[]
			{
				new[] { new StageEnemy { Summon = "wolf_wind" }, new StageEnemy { Summon = "knight_fire" }, new StageEnemy { Summon = "pirate_water" } },
			});
			foreach (var element in new[] { "fire", "water", "wind", "light", "dark" })
			{
				var team = PlayerTeam.Build(TestData.PlayerWith($"lich_{element}", "knight_fire", "pirate_water"), database, Teams.Campaign);
				var used = new HashSet<string>();
				var session = BattleFactory.Create(database, team, encounter, 3);
				session.Start();
				for (var turns = 0; !session.IsOver && turns < 400; turns++)
				{
					var turn = session.BeginTurn();
					if (!turn.NeedsDecision)
						continue;
					var events = session.Act(turn.Actor.Side == Side.Allies ? AutoPilot.For(session, turn.Actor) : AutoPilot.ForEnemy(session, turn.Actor));
					foreach (var used3 in events.OfType<SkillUsed>().Where(e => e.Actor.Name == database.Summon($"lich_{element}").Name))
						used.Add(used3.Skill.Name);
				}

				Assert.True(session.IsOver, $"lich_{element}: a luta acaba");
				Assert.True(used.Contains(database.Summon($"lich_{element}").Skills[2].Name), $"lich_{element}: usou a terceira habilidade (usou: {string.Join(", ", used)})");
			}
		}
		[Test]
		private static void CondemnationAppliesThreeAfflictionsPerEnemyAndHealsPerEffect()
		{
			var condemnation = TestData.Summon("lich_fire").Skills[2];
			var hero = TestData.Unit("lich", Side.Allies, speed: 300, special: condemnation);
			var foes = Enumerable.Range(1, 3).Select(i => TestData.Unit($"inimigo{i}", Side.Enemies, health: 1_000_000, attack: 1, speed: 1)).ToList();
			var session = TestData.Session(new[] { hero }, foes);
			session.Start();
			hero.Health = 100;
			TestData.RunUntilTurnOf(session, hero);
			var applied = 0;
			foreach (var battleEvent in session.Act(new UnitAction(1, foes[0])))
			{
				if (battleEvent is StatusApplied { Status: StatusKind.Affliction })
					applied++;
			}

			foreach (var foe in foes)
				Assert.True(foe.Count(StatusKind.Affliction) <= 3, $"{foe.Name}: no máximo 3 Aflições");
			Assert.True(applied >= 3, "algumas Aflições pegaram");
			Assert.Near(100 + applied * 0.05 * hero.MaxHealth, hero.Health, "cura de 5% da Vida máxima por Aflição causada", 1e-6);
		}
	}
}
