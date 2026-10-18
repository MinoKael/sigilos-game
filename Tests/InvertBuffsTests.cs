using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>Inverter efeitos (EffectKind.InvertBuffs): positivo vira o equivalente negativo, sem equivalente vira Maldição, sem positivo tira Ímpeto.</summary>
	internal static class InvertBuffsTests
	{
		private static BattleUnit Cast(EffectDefinition invert, params StatusKind[] buffs)
		{
			var stats = new StatBlock { Health = 1000, Attack = 100, Defense = 0, Speed = 300, CritDamage = 0.5 };
			var skill = new SkillDefinition { Name = "troca", Cooldown = 3, Effects = new[] { invert } };
			var hero = new BattleUnit("herói", "herói", "", Side.Allies, Element.Fire, 1, false, stats, new[] { TestData.Strike, skill }, null, RuneSetEffects.None);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1, speed: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			var resolver = new EffectResolver(session);
			foreach (var buff in buffs)
				resolver.GiveStatus(foe, buff, 3);
			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(1, foe));
			return foe;
		}

		private static EffectDefinition Invert(bool fallback = false) => new() { Kind = EffectKind.InvertBuffs, Target = TargetKind.Target, Chance = 1, Fallback = fallback, Power = 20 };

		[Test]
		private static void EachBuffBecomesItsEquivalentAndTheRestBecomeCurse()
		{
			var foe = Cast(Invert(), StatusKind.AttackUp, StatusKind.Hidden, StatusKind.Foresight, StatusKind.Immunity);
			Assert.True(foe.Has(StatusKind.AttackDown), "Ataque+ virou Ataque−");
			Assert.True(foe.Has(StatusKind.Sleep), "Oculto virou Sono");
			Assert.True(foe.Has(StatusKind.Blind), "Presságio virou Cegueira");
			Assert.True(foe.Has(StatusKind.Curse), "Imunidade, sem equivalente, virou Maldição");
			Assert.False(foe.Has(StatusKind.AttackUp) || foe.Has(StatusKind.Immunity) || foe.Has(StatusKind.Foresight), "os positivos saíram");
		}

		[Test]
		private static void WithoutBuffsTheFallbackTakesImpeto()
		{
			var plain = Cast(Invert());
			var cut = Cast(Invert(fallback: true));
			Assert.True(cut.Impeto < plain.Impeto, "sem positivo, o Ímpeto cai só com o fallback");
		}
	}
}
