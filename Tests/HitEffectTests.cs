using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>Os efeitos ligados a cada golpe do dano (EffectDefinition.When): a cada golpe, a cada crítico, a cada efeito causado.</summary>
	internal static class HitEffectTests
	{
		private static EffectDefinition Strikes(int hits) => new() { Kind = EffectKind.Damage, Power = 1, Hits = hits };

		private static EffectDefinition Per(HitTrigger when, EffectKind kind, StatusKind status = StatusKind.Stigma, TargetKind target = TargetKind.Target, double power = 0, int turns = 2) =>
			new() { Kind = kind, When = when, Status = status, Target = target, Power = power, Turns = turns, Chance = 1 };

		private static BattleUnit Attacker(SkillDefinition special, double crit)
		{
			var stats = new StatBlock { Health = 1000, Attack = 100, Defense = 0, Speed = 300, CritDamage = 0.5, Crit = crit };
			return new BattleUnit("herói", "herói", "", Side.Allies, Element.Fire, 1, false, stats, new[] { TestData.Strike, special }, null, RuneSetEffects.None);
		}

		private static (BattleUnit Hero, BattleUnit Foe, System.Collections.Generic.IReadOnlyList<BattleEvent> Events) Cast(SkillDefinition special, double crit = 0, double foeHealth = 1_000_000)
		{
			var hero = Attacker(special, crit);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: foeHealth, attack: 1, speed: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			return (hero, foe, session.Act(new UnitAction(1, foe)));
		}

		[Test]
		private static void EachHitEffectRunsOnEveryHitAndTheDamageOnlyOnce()
		{
			var skill = new SkillDefinition { Name = "rajada", Cooldown = 3, Effects = new[] { Strikes(3), Per(HitTrigger.Hit, EffectKind.Impeto, power: 10) } };
			var (_, foe, events) = Cast(skill);
			Assert.Equal(3, events.OfType<Damaged>().Count(), "3 golpes");
			Assert.Equal(3, events.OfType<ImpetoChanged>().Count(e => ReferenceEquals(e.Target, foe)), "o efeito de golpe agiu uma vez por golpe, no alvo");

			var status = new SkillDefinition { Name = "rajada", Cooldown = 3, Effects = new[] { Strikes(3), Per(HitTrigger.Hit, EffectKind.Status, StatusKind.Blind) } };
			var (_, blinded, statusEvents) = Cast(status);
			Assert.Equal(3, statusEvents.OfType<StatusApplied>().Count(e => e.Status == StatusKind.Blind), "o status entrou (ou renovou) a cada golpe");
			Assert.True(blinded.Has(StatusKind.Blind), "e ficou");
		}

		[Test]
		private static void CritEffectsOnlyRunOnCrits()
		{
			var skill = new SkillDefinition { Name = "mira", Cooldown = 3, Effects = new[] { Strikes(2), Per(HitTrigger.Crit, EffectKind.Impeto, target: TargetKind.Self, power: 10) } };
			var always = Cast(skill, crit: 1);
			Assert.Equal(2, always.Events.OfType<ImpetoChanged>().Count(e => ReferenceEquals(e.Target, always.Hero)), "dois críticos, duas vezes");
			var never = Cast(skill, crit: 0);
			Assert.Equal(0, never.Events.OfType<ImpetoChanged>().Count(e => ReferenceEquals(e.Target, never.Hero)), "sem crítico, nada");
		}

		[Test]
		private static void StatusCausedEffectsFollowTheStatusAndDoNotChain()
		{
			var trigger = Per(HitTrigger.Status, EffectKind.Impeto, target: TargetKind.Self, power: 5) with { WhenScope = StatusScope.Debuffs };
			var skill = new SkillDefinition { Name = "marca", Cooldown = 3, Effects = new[] { Strikes(2), Per(HitTrigger.Hit, EffectKind.Status, StatusKind.Stigma), trigger } };
			var (hero, _, events) = Cast(skill);
			Assert.Equal(2, events.OfType<ImpetoChanged>().Count(e => ReferenceEquals(e.Target, hero)), "um por efeito negativo causado, e o que ele mesmo causa não o dispara de novo");

			var buff = new SkillDefinition { Name = "marca", Cooldown = 3, Effects = new[] { Strikes(2), Per(HitTrigger.Hit, EffectKind.Status, StatusKind.Stigma), trigger with { WhenScope = StatusScope.Buffs } } };
			var none = Cast(buff);
			Assert.Equal(0, none.Events.OfType<ImpetoChanged>().Count(e => ReferenceEquals(e.Target, none.Hero)), "o escopo filtra");
		}

		[Test]
		private static void ADeadTargetSkipsTargetEffectsButSelfEffectsStillRun()
		{
			var skill = new SkillDefinition
			{
				Name = "fim", Cooldown = 3,
				Effects = new[] { Strikes(1), Per(HitTrigger.Hit, EffectKind.Status, StatusKind.Stigma), Per(HitTrigger.Hit, EffectKind.Impeto, target: TargetKind.Self, power: 10) },
			};
			var (hero, foe, events) = Cast(skill, foeHealth: 1);
			Assert.False(foe.IsAlive, "o golpe derrubou o alvo");
			Assert.Equal(0, events.OfType<StatusApplied>().Count(e => ReferenceEquals(e.Target, foe)), "nada no alvo caído");
			Assert.Equal(1, events.OfType<ImpetoChanged>().Count(e => ReferenceEquals(e.Target, hero)), "mas o efeito em si age");
		}

		[Test]
		private static void HitEffectsNeedADamageAndNotAPassive()
		{
			var lone = new SkillDefinition { Name = "sozinho", Cooldown = 3, Effects = new[] { Per(HitTrigger.Hit, EffectKind.Status) } };
			Assert.True(GameDatabase.ValidateSkill(lone).Any(), "efeito de golpe sem dano é recusado");
			var fine = new SkillDefinition { Name = "ok", Cooldown = 3, Effects = new[] { Strikes(2), Per(HitTrigger.Hit, EffectKind.Status) } };
			Assert.False(GameDatabase.ValidateSkill(fine).Any(), "com dano, vale");
		}
	}
}
