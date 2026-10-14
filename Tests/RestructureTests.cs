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

		[Test]
		private static void BossIgnoresTheCentralImmunityListFromEverySource()
		{
			Assert.True(BattleRules.BossImmunities.SetEquals(new[] { StatusKind.Stun, StatusKind.Sleep, StatusKind.Oblivion, StatusKind.Silence, StatusKind.Bomb }), "a lista central tem os cinco efeitos");
			Assert.False(BattleRules.BossImmunities.Contains(StatusKind.Affliction), "a Aflição continua pegando em chefe");

			var every = BattleRules.BossImmunities.Concat(new[] { StatusKind.Affliction }).Select(s => Inflict(s, turns: 3)).ToArray();
			var hero = TestData.Unit("herói", Side.Allies, speed: 200, basic: Skill(every));
			var boss = TestData.Unit("chefe", Side.Enemies, health: 1e9, attack: 1, boss: true);
			var minion = TestData.Unit("lacaio", Side.Enemies, health: 1e9, attack: 1);
			var session = TestData.Session(new[] { hero }, new[] { boss, minion });
			session.Start();

			var sawAffliction = false;
			var minionAffected = false;
			for (var i = 0; i < 12; i++)
			{
				TestData.RunUntilTurnOf(session, hero);
				session.Act(new UnitAction(0, boss));
				TestData.RunUntilTurnOf(session, hero);
				session.Act(new UnitAction(0, minion));
				sawAffliction |= boss.Has(StatusKind.Affliction);
				minionAffected |= minion.Has(StatusKind.Stun) || minion.Has(StatusKind.Silence) || minion.Has(StatusKind.Sleep);
				foreach (var immune in BattleRules.BossImmunities)
					Assert.False(boss.Has(immune), $"o chefe é imune a {immune}");
			}

			Assert.True(sawAffliction, "a Aflição pega no chefe");
			Assert.True(minionAffected, "o lacaio (que não é chefe) não é imune");
		}

		// Provocar -------------------------------------------------------------------------------------

		[Test]
		private static void TauntIsABuffThatDrawsSingleTargetAttacksButNotAreaOnes()
		{
			Assert.False(BattleRules.IsNegative(StatusKind.Taunt), "Provocar é um efeito positivo");

			var area = Skill(new EffectDefinition { Kind = EffectKind.Damage, Power = 1, Target = TargetKind.AllEnemies });
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: TestData.Strike, special: area);
			var a = TestData.Unit("a", Side.Enemies, health: 1e6, attack: 1);
			var b = TestData.Unit("b", Side.Enemies, health: 1e6, attack: 1);
			var session = TestData.Session(new[] { hero }, new[] { a, b });
			session.Start();

			Assert.Equal(2, session.ChoosableTargets(hero).Count, "sem Provocar, escolhe livremente");
			b.AddStatus(new StatusEffect(StatusKind.Taunt, 2, 0, b));
			Assert.Equal(1, session.ChoosableTargets(hero).Count, "com Provocar, só o provocador");
			Assert.Equal(b, session.ChoosableTargets(hero)[0], "o portador do buff");

			a.AddStatus(new StatusEffect(StatusKind.Taunt, 2, 0, a));
			Assert.Equal(a, session.ChoosableTargets(hero)[0], "dois provocadores: o primeiro da fila, sempre");

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(1, null));
			Assert.True(a.Health < 1e6 && b.Health < 1e6, "o ataque em área continua atingindo todos");

			foreach (var unit in new[] { a, b })
				unit.Remove(unit.Statuses.First(s => s.Kind == StatusKind.Taunt));
			Assert.Equal(2, session.ChoosableTargets(hero).Count, "sem o buff, a escolha volta ao normal");
		}

		// Condições de efeito --------------------------------------------------------------------------

		[Test]
		private static void EffectConditionsLookAtAlliesAndDefense()
		{
			var give = new EffectDefinition { Kind = EffectKind.Status, Status = StatusKind.Counter, Target = TargetKind.Self, Chance = 1, Turns = 1, IfAlly = "parceiro" };
			var tank = new EffectDefinition { Kind = EffectKind.Status, Status = StatusKind.Taunt, Target = TargetKind.Self, Chance = 1, Turns = 1, IfDefenseAbove = true };
			var hero = TestData.Unit("herói", Side.Allies, defense: 200, basic: Skill(new EffectDefinition { Kind = EffectKind.Damage, Power = 1 }, give, tank));
			var partner = TestData.Unit("parceiro", Side.Allies, speed: 1);
			var soft = TestData.Unit("mole", Side.Enemies, health: 1e6, defense: 10);
			var session = TestData.Session(new[] { hero, partner }, new[] { soft });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, soft));
			Assert.True(hero.Has(StatusKind.Counter), "o parceiro está vivo: o efeito condicionado acontece");
			Assert.True(hero.Has(StatusKind.Taunt), "a Defesa dele passa a do alvo: o efeito acontece");

			var hard = TestData.Unit("duro", Side.Enemies, health: 1e6, defense: 900);
			var loner = TestData.Unit("sozinho", Side.Allies, defense: 200, basic: Skill(give, tank));
			var alone = TestData.Session(new[] { loner }, new[] { hard });
			alone.Start();
			TestData.RunUntilTurnOf(alone, loner);
			alone.Act(new UnitAction(0, hard));
			Assert.False(loner.Has(StatusKind.Counter), "sem o parceiro vivo, não acontece");
			Assert.False(loner.Has(StatusKind.Taunt), "com Defesa menor que a do alvo, não acontece");
		}

		[Test]
		private static void BonusPerStatusScalesWithTheDebuffsOnTheTarget()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.BonusPerStatusOrEffect, Value = 0.1, Scope = StatusScope.Debuffs };
			var raven = TestData.Unit("corvo", Side.Allies, speed: 200, passive: passive);
			var foe = TestData.Unit("alvo", Side.Enemies, health: 1e6, attack: 1);
			var session = TestData.Session(new[] { raven }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, raven);
			var before = foe.Health;
			session.Act(new UnitAction(0, foe));
			var plain = before - foe.Health;

			foe.AddStatus(new StatusEffect(StatusKind.SpeedDown, 3, 0, raven));
			foe.AddStatus(new StatusEffect(StatusKind.Curse, 3, 0, raven));
			TestData.RunUntilTurnOf(session, raven);
			before = foe.Health;
			session.Act(new UnitAction(0, foe));
			var boosted = before - foe.Health;

			// Maldição também aumenta o dano (BattleRules.CurseBonus): o piso é o bônus por efeito sozinho.
			Assert.True(boosted >= plain * 1.2 - 1, $"+10% por debuff no alvo ({plain} → {boosted})");
		}

		// Mecânicas das Masmorras ----------------------------------------------------------------------

		[Test]
		private static void DrowningTicksOncePerRoundWhateverTheTurnCount()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.Drowning, Value = 0.1 };
			var fast = TestData.Unit("rápido", Side.Allies, speed: 300, health: 1000, passive: passive);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 10, health: 1e9, attack: 1);
			var session = TestData.Session(new[] { fast }, new[] { foe });
			session.Start();

			var rounds = new Dictionary<int, double>();
			for (var i = 0; i < 60 && session.Round < 5 && !session.IsOver; i++)
			{
				var turn = session.BeginTurn();
				rounds.TryAdd(session.Round, fast.Health);
				if (turn.NeedsDecision)
					session.Act(new UnitAction(0, foe));
			}

			Assert.Near(1000, rounds[1], "na rodada 1 ninguém se afoga", 1);
			Assert.Near(900, rounds[2], "10% da Vida máxima na rodada 2", 1);
			Assert.Near(800, rounds[3], "e mais 10% na rodada 3, mesmo com vários turnos nela", 1);
			Assert.Near(700, rounds[4], "e na 4", 1);
			Assert.True(fast.Has(StatusKind.SpeedDown), "e fica com Velocidade−");
			Assert.Near(0.10, BattleRules.DrowningFraction, "10% é o valor inicial, configurável", 1e-12);
		}

		[Test]
		private static void RunicCoreGrowsDefenseOncePerRoundUpToTheCap()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.RunicCore, Value = 0.15 };
			var golem = TestData.Unit("golem", Side.Enemies, speed: 300, health: 1e9, attack: 1, defense: 100, passive: passive, boss: true);
			var hero = TestData.Unit("herói", Side.Allies, speed: 10, health: 1e9);
			var session = TestData.Session(new[] { hero }, new[] { golem });
			session.Start();

			var defense = new Dictionary<int, double>();
			for (var i = 0; i < 400 && session.Round <= 30 && !session.IsOver; i++)
			{
				var turn = session.BeginTurn();
				defense[session.Round] = golem.Defense;
				if (turn.NeedsDecision)
					session.Act(new UnitAction(0, turn.Actor == golem ? hero : golem));
			}

			Assert.Near(100, defense[1], "rodada 1: a Defesa de base", 1e-6);
			Assert.Near(115, defense[2], "rodada 2: +15%", 1e-6);
			Assert.Near(130, defense[3], "rodada 3: +30%, sem acumular por turno extra", 1e-6);
			var cap = 100 * (1 + BattleRules.RunicCoreStep * BattleRules.RunicCoreMaxSteps);
			Assert.Near(cap, defense[defense.Keys.Max()], "e para no teto", 1e-6);
		}

		[Test]
		private static void DungeonMechanicsAreDataOnTheRightFloors()
		{
			var database = TestData.LoadReal();
			var golem = database.Enemy("runic_golem");
			Assert.True(golem.Skills.Any(s => s.Passive?.Kind == PassiveKind.RunicCore), "o Golem tem o Núcleo Rúnico");
			Assert.True(golem.HealthScale < 9, "com menos Vida que a configuração anterior (9)");

			// A Vida que o jogador enfrenta é healthScale × a escala do andar: tem de ficar abaixo da de antes (9 × as
			// escalas de então) em todo andar, não só no número do chefe.
			var before = new[] { 0.7, 0.6, 0.95, 1.3, 2.2 };
			var golemFloors = database.Dungeon("golem").Floors;
			for (var i = 0; i < golemFloors.Count; i++)
				Assert.True(golem.HealthScale * golemFloors[i].Scale < 9 * before[i], $"golem {i + 1}: o Golem tem menos Vida que antes");

			var wyvern = database.Dungeon("wyvern");
			Assert.True(wyvern.Rules.Any(r => r.Effects.Any(e => e.Kind == EffectKind.StealBuff)), "a Serpe rouba buffs");
			Assert.True(wyvern.Rules.Any(r => r.Effects.Any(e => e.Status == StatusKind.SpeedDown)), "e aplica Slow");
			Assert.True(wyvern.Rules.Any(r => r.Passive.Kind == PassiveKind.BonusPerStatusOrEffect && r.Passive.Scope == StatusScope.Buffs), "e bate mais em quem tem buff");

			var sanctum = database.Dungeon("sanctum");
			Assert.True(sanctum.Rules.Any(r => r.Passive.Kind == PassiveKind.Drowning && Math.Abs(r.Passive.Value - 0.1) < 1e-9), "o Santuário afoga em 10%");
			Assert.Equal(DungeonKind.Tools, sanctum.Kind, "e é a forja");
			Assert.True(sanctum.Floor(1).Encounter.Rules is { Count: > 0 }, "as regras chegam à luta");

			Assert.False(database.Dungeons.Any(d => d.Id is "crypt" or "forge"), "Cripta e Forja Rachada saem do jogo");
			Assert.Equal(5, database.Dungeon("crypt").Floors.Count, "mas ficam nos dados");
		}

		[Test]
		private static void EveryBossAttackIsAnAreaAttack()
		{
			var database = TestData.LoadReal();
			foreach (var boss in database.Enemies.Where(e => !e.Minion))
			{
				foreach (var skill in boss.Skills.Where(s => !s.IsPassive))
				{
					foreach (var effect in skill.Effects.Where(e => e.Kind == EffectKind.Damage))
						Assert.Equal(TargetKind.AllEnemies, effect.Target, $"{boss.Id}/{skill.Name}: o golpe é em área");
				}
			}
		}

		/// <summary>O guardião de uma constelação usa as habilidades da família (de alvo único): ainda assim, chefe bate em área.</summary>
		[Test]
		private static void AnyBossHitIsAnAreaAttackEvenWithSingleTargetSkills()
		{
			var boss = TestData.Unit("guardião", Side.Enemies, speed: 200, attack: 100, boss: true);
			var a = TestData.Unit("a", Side.Allies, speed: 1, health: 1e6);
			var b = TestData.Unit("b", Side.Allies, speed: 1, health: 1e6);
			var session = TestData.Session(new[] { a, b }, new[] { boss });
			session.Start();
			TestData.RunUntilTurnOf(session, boss);
			session.Act(new UnitAction(0, a));
			Assert.True(a.Health < 1e6 && b.Health < 1e6, "o golpe de alvo único do chefe atingiu os dois");

			var minion = TestData.Unit("lacaio", Side.Enemies, speed: 200, attack: 100);
			var c = TestData.Unit("c", Side.Allies, speed: 1, health: 1e6);
			var d = TestData.Unit("d", Side.Allies, speed: 1, health: 1e6);
			var other = TestData.Session(new[] { c, d }, new[] { minion });
			other.Start();
			TestData.RunUntilTurnOf(other, minion);
			other.Act(new UnitAction(0, c));
			Assert.True(c.Health < 1e6 && d.Health == 1e6, "o lacaio segue com o alvo único");
		}

		[Test]
		private static void PartnerExtraTurnsAreNotStackedByReapplication()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.BonusPerStatusOrEffect, Value = 0.1, Scope = StatusScope.Debuffs, Partner = "luz", PartnerTurns = 1 };
			var inflict = Skill(Inflict(StatusKind.SpeedDown, turns: 2));
			var raven = TestData.Unit("corvo", Side.Allies, speed: 200, basic: inflict, passive: passive);
			var light = TestData.Unit("luz", Side.Allies, speed: 1);
			var foe = TestData.Unit("alvo", Side.Enemies, health: 1e9, attack: 1, speed: 1, resistance: 0);
			var session = TestData.Session(new[] { raven, light }, new[] { foe });
			session.Start();

			for (var i = 0; i < 6; i++)
			{
				TestData.RunUntilTurnOf(session, raven);
				session.Act(new UnitAction(0, foe));
			}

			var applied = foe.Find(StatusKind.SpeedDown);
			Assert.True(applied == null || applied.Turns <= 3, "reaplicar não soma o turno do parceiro sem fim (dois da habilidade + um do parceiro)");
		}

		[Test]
		private static void NoSkillPutsTauntOnAnEnemy()
		{
			var database = TestData.LoadReal();
			var skills = database.AllSummons.SelectMany(s => s.AllSkills).Concat(database.Enemies.SelectMany(e => e.Skills));
			foreach (var effect in skills.SelectMany(s => s.Effects.Concat(s.AwakenedEffects)).Where(e => e.Kind == EffectKind.Status && e.Status == StatusKind.Taunt))
				Assert.True(effect.Target is TargetKind.Self or TargetKind.AllAllies or TargetKind.LowestAlly or TargetKind.HighestAlly or TargetKind.RandomAlly, "Provocar vai para um aliado");
		}

		[Test]
		private static void BuffsAreWeakerThanTheirDebuffs()
		{
			Assert.Near(0.5, BattleRules.AttackUpBonus, "Ataque+ vale 50%", 1e-12);
			Assert.Near(0.7, BattleRules.AttackDownPenalty, "Ataque− vale 70%", 1e-12);
			Assert.Near(0.5, BattleRules.DefenseUpBonus, "Defesa+ vale 50%", 1e-12);
			Assert.Near(0.7, BattleRules.DefenseDownPenalty, "Defesa− vale 70%", 1e-12);

			var unit = TestData.Unit("a", Side.Allies, attack: 100, defense: 100);
			unit.AddStatus(new StatusEffect(StatusKind.AttackUp, 2));
			Assert.Near(150, unit.Current(Stat.Attack), "o buff é aplicado uma vez só", 1e-9);
		}

		// Recompensas ----------------------------------------------------------------------------------

		[Test]
		private static void SpecialDropsAreMutuallyExclusiveInOneResolution()
		{
			var random = new Random(3);
			for (var i = 0; i < 1000; i++)
			{
				Assert.Equal(DropCategory.InfusionCore, RewardDrops.Roll(random, 100, 0, 0), "com só o Núcleo a 100%, sai o Núcleo");
				Assert.True(RewardDrops.Roll(random, 100, 100, 100) != DropCategory.None, "somando mais de 100%, as faixas se reduzem e sempre sai uma só");
			}

			var counts = new Dictionary<DropCategory, int>();
			const int n = 200_000;
			for (var i = 0; i < n; i++)
			{
				var category = RewardDrops.Roll(random, 5, 10, 20);
				counts[category] = counts.GetValueOrDefault(category) + 1;
			}

			Assert.Near(0.05, counts.GetValueOrDefault(DropCategory.InfusionCore) / (double)n, "Núcleo de Infusão: a chance individual segue", 0.004);
			Assert.Near(0.10, counts.GetValueOrDefault(DropCategory.Scroll) / (double)n, "Transmutação Alquímica: idem", 0.004);
			Assert.Near(0.20, counts.GetValueOrDefault(DropCategory.FragmentCore) / (double)n, "Núcleo de Fragmento: idem", 0.004);
			Assert.Equal(30, RewardDrops.FragmentCoreValue, "o Núcleo de Fragmento vale 30");
		}

		[Test]
		private static void DungeonVictoryNeverGrantsTwoSpecialCategories()
		{
			var database = TestData.LoadReal();
			var real = database.Dungeon("golem");
			var floors = real.Floors.Select(f => f with { CoreChance = 30, ScrollChance = 30, FragmentChance = 30, FirstClearCores = 0, FirstClearLegendary = 0, FirstClearLightDark = 0 }).ToList();
			var dungeon = real with { Floors = floors };
			var random = new Random(11);
			var withFragments = 0;
			for (var i = 0; i < 300; i++)
			{
				var player = TestData.PlayerWith(TestData.TypicalTeam);
				player.Mana = 999;
				player.DungeonFloors[dungeon.Id] = 5;
				var cores = player.Monsters.Count(m => m.IsInfusionCore);
				var scrolls = player.Scrolls;
				var fragments = player.Fragments;
				Dungeons.ApplyVictory(random, player, dungeon, 1);
				var granted = (player.Monsters.Count(m => m.IsInfusionCore) - cores > 0 ? 1 : 0)
					+ (player.Scrolls - scrolls > 0 ? 1 : 0)
					+ (player.Fragments - fragments > 0 ? 1 : 0);
				Assert.True(granted <= 1, "no máximo uma das três categorias por vitória");
				if (player.Fragments - fragments == RewardDrops.FragmentCoreValue)
					withFragments++;
			}

			Assert.True(withFragments > 0, "o Núcleo de Fragmento cai, com 30 Fragmentos");
		}

		// Elenco, conjuntos e papéis -------------------------------------------------------------------

		[Test]
		private static void OnlyTheListedHomunculiAreActive()
		{
			var database = TestData.LoadReal();
			var active = database.Families.ToDictionary(f => f.Id, f => f.Rarity);
			var expected = new Dictionary<string, int>
			{
				["pixie"] = 2, ["mushroom"] = 2, ["carnivorous_plant"] = 2, ["slime"] = 2,
				["fox"] = 3, ["goat"] = 3, ["dolphin"] = 3, ["wolf"] = 3, ["crow"] = 3,
				["bandit"] = 4, ["pirate"] = 4, ["miner"] = 4, ["knight"] = 4,
				["lich"] = 5, ["wizard"] = 5, ["druid"] = 5,
			};
			Assert.Equal(string.Join(",", expected.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}")), string.Join(",", active.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}")), "o elenco da 0.1");
			Assert.True(database.Summons.All(s => s.Family.Active), "as listas só trazem ativos");
			Assert.False(database.HasSummon("imp_fire"), "o Diabrete saiu do jogo");
			Assert.True(database.Knows("imp_fire") && database.Summon("imp_fire").Name.Length > 0, "mas o id ainda resolve (saves, inimigos)");
			Assert.Equal("Raposa de Fogo", database.Summon("fox_fire").Name, "a Raposa entra");
			Assert.Equal("Bode de Fogo", database.Summon("goat_fire").Name, "e o Bode");
			Assert.Equal("Golfinho de Fogo", database.Summon("dolphin_fire").Name, "e o Golfinho");
			Assert.Equal("Gosma de Fogo", database.Summon("slime_fire").Name, "o Limo é a Gosma");
			Assert.Equal("Fada de Fogo", database.Summon("pixie_fire").Name, "o Pixie é a Fada");
			Assert.Equal("Minerador de Fogo", database.Summon("miner_fire").Name, "e o Anão sai do nome do Minerador");
			foreach (var id in new[] { "fox", "goat", "dolphin" })
				Assert.Equal(1, database.Families.Single(f => f.Id == id).Variations.Values.First().Skills.Count, $"{id}: só a básica, sem habilidades novas");
		}

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

		[Test]
		private static void OnlyTheEightSigilSetsAreActive()
		{
			Assert.Equal(8, RuneSets.Active.Count, "oito conjuntos");
			var names = RuneSets.ActiveSets.Select(s => s.ToString()).OrderBy(s => s).ToList();
			Assert.Equal("Counter,Finesse,Haste,Lethal,Strike,Sustain,Vigor,Wrath", string.Join(",", names), "os da 0.1");

			var database = TestData.LoadReal();
			Assert.Equal("Counter,Wrath,Sustain,Finesse", string.Join(",", database.Dungeon("wyvern").Sets), "Serpe: Contragolpe, Dano Crítico, Resistência, Precisão");
			Assert.Equal("Vigor,Lethal,Haste,Strike", string.Join(",", database.Dungeon("golem").Sets), "Golem: Vida, Ataque, Velocidade, Crítico");

			Assert.Equal(Planet.Earth, RuneSets.PlanetOf(RuneSet.Vigor), "Vida é a Terra");
			Assert.Equal(Planet.Mars, RuneSets.PlanetOf(RuneSet.Lethal), "Ataque é Marte");
			Assert.Equal(Planet.Uranus, RuneSets.PlanetOf(RuneSet.Sustain), "Resistência é Urano");
		}

		[Test]
		private static void RolesAreOffensiveDefensiveOrSupport()
		{
			Assert.Equal(RoleGroup.Defensive, RoleGroups.Of(Role.HP), "Vida é Defensivo");
			Assert.Equal(RoleGroup.Defensive, RoleGroups.Of(Role.Defense), "Defesa é Defensivo");
			Assert.Equal(RoleGroup.Offensive, RoleGroups.Of(Role.Attack), "Ataque é Ofensivo");
			Assert.Equal(RoleGroup.Support, RoleGroups.Of(Role.Support), "Suporte é Suporte");
		}

		[Test]
		private static void TwoStarHomunculiCannotAwaken()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("mushroom_fire", "wolf_fire");
			player.Essence = 10_000_000;
			var twoStar = player.Monsters.First(m => m.SummonId == "mushroom_fire");
			var threeStar = player.Monsters.First(m => m.SummonId == "wolf_fire");
			Assert.False(Awakening.CanAwaken(player, twoStar, database.Summon("mushroom_fire")), "2★ não pode despertar");
			Assert.False(Awakening.Awaken(player, twoStar, database.Summon("mushroom_fire")), "nem por chamada direta");
			Assert.False(twoStar.Awakened, "e nada muda");
			Assert.True(Awakening.Awaken(player, threeStar, database.Summon("wolf_fire")), "as outras raridades despertam como antes");
		}

		[Test]
		private static void ThematicClassificationComesFromData()
		{
			var database = TestData.LoadReal();
			var adversaries = database.Families.Where(f => f.Theme == FamilyTheme.Adversaries).Select(f => f.Id).OrderBy(id => id).ToList();
			Assert.Equal("bandit,knight,pirate", string.Join(",", adversaries), "Adversários: Bandido, Cavaleiro e Pirata");
			Assert.True(database.Families.Any(f => f.Theme == FamilyTheme.None), "o resto fica sem temática, sem inferir pelo nome");
		}

		[Test]
		private static void DefaultAvatarUsesTheFormerRuneArt()
		{
			Assert.Equal("rune", SpecialAvatars.FileOf(SpecialAvatars.Default), "o padrão desenha a runa");
			Assert.True(SpecialAvatars.Has(SpecialAvatars.Classic), "o padrão de antes virou um retrato especial");
			Assert.False(SpecialAvatars.All.Contains("rune"), "e a runa não aparece duas vezes");
		}
	}
}
