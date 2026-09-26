using System;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Tests
{
	/// <summary>As Passivas das famílias que vieram dos inimigos: Limos, Goblins, Lobos, Bandidos, Trolls e Dragões.</summary>
	internal static class SignatureTests
	{
		private static PassiveDefinition Passive(PassiveKind kind, double value) => new() { Kind = kind, Value = value };

		private static double Hit(BattleUnit attacker, BattleUnit target) => DamageFormula.Compute(attacker, target, 1, 0, false);

		[Test]
		private static void SlimesTakeLessDamage()
		{
			var hero = TestData.Unit("herói", Side.Allies);
			var plain = TestData.Unit("comum", Side.Enemies);
			var slime = TestData.Unit("limo", Side.Enemies, passive: Passive(PassiveKind.DamageReduction, 0.2));
			Assert.Near(Hit(hero, plain) * 0.8, Hit(hero, slime), "20% a menos", 1);
		}

		[Test]
		private static void GoblinsHitDebuffedTargetsHarder()
		{
			var goblin = TestData.Unit("goblin", Side.Allies, passive: Passive(PassiveKind.BonusVsDebuffed, 0.3));
			var target = TestData.Unit("alvo", Side.Enemies);
			var clean = Hit(goblin, target);
			target.AddStatus(new StatusEffect(StatusKind.AttackDown, 2, 0, null));
			Assert.Near(clean * 1.3, Hit(goblin, target), "+30% com efeito negativo", 1);
		}

		[Test]
		private static void WolvesHitWoundedTargetsHarder()
		{
			var wolf = TestData.Unit("lobo", Side.Allies, passive: Passive(PassiveKind.BonusVsWounded, 0.25));
			var target = TestData.Unit("alvo", Side.Enemies, health: 1000);
			var healthy = Hit(wolf, target);
			target.Health = 400;
			Assert.Near(healthy * 1.25, Hit(wolf, target), "+25% abaixo da metade da Vida", 1);
		}

		[Test]
		private static void BanditsStartEachWaveWithImpeto()
		{
			var bandit = TestData.Unit("bandido", Side.Allies, speed: 1, passive: Passive(PassiveKind.ImpetoAtWaveStart, 0.3));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 1);
			var session = TestData.Session(new[] { bandit }, new[] { foe });
			session.Start();
			Assert.Near(0.3 * BattleRules.FullImpeto, bandit.Impeto, "começa com 30% de Ímpeto");
			Assert.Near(0, foe.Impeto, "quem não tem a Assinatura começa do zero");
		}

		[Test]
		private static void TrollsRegenerateAtTheStartOfTheirTurn()
		{
			var troll = TestData.Unit("troll", Side.Allies, speed: 300, health: 1000, passive: Passive(PassiveKind.RegenEachTurn, 0.05));
			var foe = TestData.Unit("inimigo", Side.Enemies, attack: 1);
			var session = TestData.Session(new[] { troll }, new[] { foe });
			session.Start();
			troll.Health = 500;

			var turn = session.BeginTurn();
			Assert.Equal(troll, turn.Actor, "o troll é o mais rápido");
			Assert.Near(550, troll.Health, "recupera 5% da Vida máxima");
		}

		[Test]
		private static void DragonsSetTargetsOnFire()
		{
			var burned = 0;
			for (var seed = 1; seed <= 20; seed++)
			{
				var dragon = TestData.Unit("dragão", Side.Allies, speed: 300, passive: Passive(PassiveKind.BurnOnHit, 1));
				var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
				var session = TestData.Session(new[] { dragon }, new[] { foe }, seed);
				session.Start();
				TestData.RunUntilTurnOf(session, dragon);
				var events = session.Act(new UnitAction(0, foe));
				Assert.True(events.OfType<StatusApplied>().Count(e => e.Status == StatusKind.Burn) + events.OfType<Resisted>().Count() == 1, "um sorteio por alvo");
				if (foe.Has(StatusKind.Burn))
					burned++;
			}

			Assert.True(burned >= 15, $"com 100% de chance quase sempre queima (só a Resistência mínima barra): {burned}/20");
		}

		[Test]
		private static void CommonEnemiesAreBuffedSummons()
		{
			var database = TestData.LoadReal();
			var troll = database.Summon("troll_water");
			var encounter = new Encounter(4, 20, new[] { new[] { new StageEnemy { Summon = troll.Id } } }, Scale: 1.5);
			var team = PlayerTeam.Build(TestData.PlayerWith("imp_fire"), database, Teams.Campaign);
			var session = BattleFactory.Create(database, team, encounter, 1);
			session.Start();

			var foe = session.Enemies.Single();
			var basis = Core.Progression.Growth.Stats(database.Roles[troll.Role], troll.Rarity, 4, 20);
			var (health, attack) = BattleFactory.FoeScale(troll.Rarity);
			Assert.Equal(troll.Name, foe.Name, "a mesma variante que o jogador invoca");
			Assert.Equal(troll.Element, foe.Element, "o elemento vem da variante");
			Assert.Equal(troll.Skills.Single(s => s.IsPassive).Passive, foe.Passive, "com a Passiva da variante");
			Assert.Near(basis.Health * health * 1.5, foe.MaxHealth, "Vida reforçada pelas estrelas e pelo encontro", 1e-6);
			Assert.Near(basis.Attack * attack * 1.5, foe.Stats.Attack, "Ataque também", 1e-6);
		}

		[Test]
		private static void OnlyBossesAreUniqueEnemies()
		{
			var database = TestData.LoadReal();
			foreach (var enemy in database.Enemies)
				Assert.False(database.Families.Any(f => f.Id == enemy.Id), $"{enemy.Id} é família de invocação: vira \"summon\" nas ondas");

			var commons = database.Stages.SelectMany(s => s.Waves)
				.Concat(database.Dungeons.SelectMany(d => d.Floors).SelectMany(f => f.Waves))
				.SelectMany(w => w)
				.Count(slot => slot.Summon != null);
			Assert.True(commons > 0, "as ondas usam invocações");
		}
	}
}
