using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>Coleção, Baú, fusão de cópias e equipes por conteúdo.</summary>
	internal static class RosterTests
	{
		[Test]
		private static void ChestKeepsMonstersOutOfTeamsAndBackWhenThereIsRoom()
		{
			var player = TestData.PlayerWith("imp_fire", "imp_water");
			var id = player.Monsters[0].Id;
			Teams.Toggle(player, "golem", id);
			var rune = RuneInventory.Create(new Random(1), player, 2);
			RuneInventory.Equip(player, rune, id);

			Assert.True(Roster.Store(player, id), "vai para o Baú");
			Assert.Equal(id, rune.EquippedOn, "leva as runas junto");
			Assert.False(Teams.Of(player, Teams.Campaign).Contains(id), "sai da equipe da Campanha");
			Assert.False(Teams.Of(player, "golem").Contains(id), "sai das equipes das Masmorras");
			Assert.False(Teams.Toggle(player, Teams.Campaign, id), "no Baú não entra em equipe");

			Assert.True(Roster.Retrieve(player, id), "volta do Baú");
			Assert.False(player.Monster(id)!.Stored, "na coleção");

			for (var i = player.Collection.Count(); i < player.CollectionCapacity; i++)
				Roster.Add(player, TestData.Summon("imp_light"));
			Roster.Store(player, id);
			Roster.Add(player, TestData.Summon("imp_light"));
			Assert.False(Roster.Retrieve(player, id), "coleção cheia: fica no Baú");
		}

		[Test]
		private static void ManyGoToTheChestAndComeBackWhileThereIsRoom()
		{
			var player = TestData.PlayerWith("imp_fire", "imp_water", "imp_wind");
			var ids = player.Monsters.Select(m => m.Id).ToList();

			Assert.Equal(3, Roster.StoreMany(player, ids), "os três vão para o Baú");
			Assert.Equal(0, Roster.StoreMany(player, ids), "quem já está lá fica");
			Assert.True(player.Monsters.All(m => m.Stored), "todos no Baú");
			Assert.False(ids.Any(id => Teams.Of(player, Teams.Campaign).Contains(id)), "e fora das equipes");

			while (Roster.FreeSlots(player) > 2)
				Roster.Add(player, TestData.Summon("imp_light"));
			Assert.Equal(2, Roster.RetrieveMany(player, ids), "só os que cabem voltam");
			Assert.Equal(0, Roster.FreeSlots(player), "a coleção enche");
			Assert.True(player.Monster(ids[2])!.Stored, "o terceiro fica no Baú");
		}

		[Test]
		private static void FusingACopyRaisesARandomSkill()
		{
			var database = TestData.Database;
			var player = TestData.PlayerWith("phoenix_fire", "phoenix_fire", "phoenix_water", "dragon_fire");
			var (target, copy, other, stranger) = (player.Monsters[0], player.Monsters[1], player.Monsters[2], player.Monsters[3]);
			var rune = RuneInventory.Create(new Random(1), player, 3);
			RuneInventory.Equip(player, rune, copy.Id);

			Assert.True(Fusion.CanFuse(player, database, target.Id, other.Id), "outro elemento da mesma família funde");
			Assert.False(Fusion.CanFuse(player, database, target.Id, stranger.Id), "outra família não funde");
			var index = Fusion.Fuse(new Random(1), player, database, target.Id, copy.Id);
			Assert.True(index >= 0, "funde a cópia");
			Assert.Equal(2, target.SkillLevel(index), "a habilidade sorteada sobe para o nível 2");
			Assert.Equal(null, player.Monster(copy.Id), "a cópia some");
			Assert.Equal(null, rune.EquippedOn, "as runas da cópia voltam ao inventário");
			Assert.False(Teams.Of(player, Teams.Campaign).Contains(copy.Id), "e ela sai da equipe");

			target.SkillLevels = database.Summon("phoenix_fire").Skills.Select(s => s.MaxLevel).ToList();
			var more = Roster.Add(player, TestData.Summon("phoenix_fire"));
			Assert.Equal(0, Fusion.SkillUpsLeft(database, target), "tudo no máximo");
			Assert.Equal(-1, Fusion.Fuse(new Random(1), player, database, target.Id, more.Id), "com tudo no máximo não funde mais");
		}

		[Test]
		private static void AwakeningSkillLevelsOnlyAfterAwakening()
		{
			var database = TestData.Database;
			var imp = database.Summon("imp_fire");
			var player = TestData.PlayerWith("imp_fire");
			var monster = player.Monsters[0];
			monster.SkillLevels = imp.Skills.Select(s => s.MaxLevel).ToList();
			Assert.Equal(0, Fusion.SkillUpsLeft(database, monster), "sem despertar, a do Despertar não conta");

			monster.Awakened = true;
			var extra = imp.Awakening.Skill!;
			Assert.Equal(extra.MaxLevel - 1, Fusion.SkillUpsLeft(database, monster), "desperto, a habilidade nova também sobe");
		}

		[Test]
		private static void LockedMonsterIsNeitherReleasedNorUsedAsMaterial()
		{
			var database = TestData.Database;
			var player = TestData.PlayerWith("phoenix_fire", "phoenix_fire");
			var (target, copy) = (player.Monsters[0], player.Monsters[1]);
			copy.Locked = true;

			Assert.False(Fusion.CanFuse(player, database, target.Id, copy.Id), "bloqueado não vira material");
			Assert.Equal(-1, Fusion.Fuse(new Random(1), player, database, target.Id, copy.Id), "e a fusão não acontece");
			Assert.Equal(0, Fusion.Release(player, database, copy.Id), "bloqueado não se solta");
			Assert.Equal(0, Fusion.ReleaseMany(player, database, new[] { copy.Id }), "nem em lote");
			Assert.Equal(2, player.Monsters.Count, "os dois ficam");

			target.Locked = true;
			copy.Locked = false;
			Assert.True(Fusion.Fuse(new Random(1), player, database, target.Id, copy.Id) >= 0, "bloqueado ainda recebe a fusão");

			var saved = PlayerSave.FromJson(PlayerSave.ToJson(player))!;
			Assert.True(saved.Monster(target.Id)!.Locked, "o bloqueio vai no save");
		}

		[Test]
		private static void ReleasingGivesFragmentsByRarity()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("phoenix_fire", "imp_fire");
			Assert.Equal(50, Fusion.Release(player, database, player.Monsters[0].Id), "5★ vale 50");
			Assert.Equal(15, Fusion.Release(player, database, player.Monsters[0].Id), "3★ vale 15");
			Assert.Equal(65, player.Fragments, "Fragmentos na conta");
			Assert.Equal(0, player.Monsters.Count, "os dois soltos");
		}

		[Test]
		private static void SelectingManyFusesUntilSkillsAreMaxedAndReleasesAll()
		{
			var database = TestData.Database;
			var player = TestData.PlayerWith("phoenix_fire");
			var target = player.Monsters[0];
			var room = Fusion.SkillUpsLeft(database, target);
			var copies = Enumerable.Range(0, room + 2).Select(_ => Roster.Add(player, TestData.Summon("phoenix_fire")).Id).ToList();

			Assert.Equal(room, Fusion.FuseMany(new Random(1), player, database, target.Id, copies), "funde até as habilidades chegarem ao máximo");
			Assert.Equal(0, Fusion.SkillUpsLeft(database, target), "tudo no máximo");
			var left = copies.Where(id => player.Monster(id) != null).ToList();
			Assert.Equal(2, left.Count, "as que não couberam ficam");

			Assert.Equal(2 * Fusion.FragmentsFor(5), Fusion.ReleaseMany(player, database, left), "solta as que sobraram");
			Assert.Equal(1, player.Monsters.Count, "só o alvo fica");
		}

		[Test]
		private static void RunesOnChestMonstersDoNotFillTheInventory()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("phoenix_fire");
			player.HighestStage = database.Stages.Count;
			var random = new Random(3);
			for (var i = 0; i < RuneInventory.Capacity; i++)
				RuneInventory.Create(random, player, 1);

			var golem = database.Dungeon("golem");
			var forge = database.Dungeons.First(d => d.Kind == Core.Content.DungeonKind.Tools);
			Assert.Equal(EntryProblem.RunesFull, Campaign.Check(player, database.Stage(1)), "cheio: a Campanha espera");
			Assert.Equal(EntryProblem.RunesFull, Dungeons.Check(player, golem, 1), "e a Masmorra de runas também");
			Assert.Equal(EntryProblem.None, Dungeons.Check(player, forge, 1), "a Forja não solta runa");

			var holder = Roster.Add(player, TestData.Summon("imp_fire"));
			Roster.Store(player, holder.Id);
			Assert.True(RuneInventory.Equip(player, player.Runes[0], holder.Id), "monstro do Baú recebe runa");
			Assert.Equal(RuneInventory.Capacity - 1, RuneInventory.Count(player), "e ela sai do inventário");
			Assert.Equal(EntryProblem.None, Campaign.Check(player, database.Stage(1)), "com vaga, a Campanha abre");

			RuneInventory.Create(random, player, 1);
			Assert.False(RuneInventory.Unequip(player, player.Runes[0]), "cheio: não tira runa de monstro");
		}

		[Test]
		private static void EachContentHasItsOwnTeam()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			var extra = Roster.Add(player, TestData.Summon("knight_fire"));

			Assert.False(Teams.Toggle(player, Teams.Campaign, extra.Id), $"a Campanha já tem {PlayerState.TeamSize}");
			Assert.True(Teams.Toggle(player, "golem", extra.Id), "a equipe do Golem é outra");
			Assert.True(Teams.MakeLeader(player, Teams.Campaign, player.Monsters[3].Id), "troca a Líder");
			Assert.Equal(player.Monsters[3].Id, Teams.Of(player, Teams.Campaign)[0], "a Líder vai para a frente");

			var leader = Teams.Of(player, Teams.Campaign)[0];
			Assert.True(Teams.Replace(player, Teams.Campaign, leader, extra.Id), "a equipe cheia ainda troca");
			Assert.Equal(extra.Id, Teams.Of(player, Teams.Campaign)[0], "quem entra fica na vaga de quem saiu (a Líder)");
			Assert.False(Teams.Of(player, Teams.Campaign).Contains(leader), "e quem saiu sai");
			var last = Teams.Of(player, Teams.Campaign)[4];
			Assert.True(Teams.Replace(player, Teams.Campaign, extra.Id, last), "quem já está na equipe troca de lugar");
			Assert.Equal(last, Teams.Of(player, Teams.Campaign)[0], "um vai para a vaga do outro");
			Assert.Equal(extra.Id, Teams.Of(player, Teams.Campaign)[4], "e o outro, para a dele");
			Assert.False(Teams.Replace(player, Teams.Campaign, leader, last), "quem não está na equipe não sai");
			Roster.Store(player, leader);
			Assert.False(Teams.Replace(player, Teams.Campaign, last, leader), "monstro do Baú não entra");

			Assert.Equal(5, PlayerTeam.Build(player, database, Teams.Campaign).Members.Count, "cinco na Campanha");
			Assert.Equal("knight_fire", PlayerTeam.Build(player, database, "golem").Members.Single().Summon.Id, "um no Golem");
		}

		[Test]
		private static void MonsterFilterFindsAndSorts()
		{
			var database = TestData.Database;
			var player = TestData.PlayerWith();
			var fire = Roster.Add(player, TestData.Summon("imp_fire"));
			var water = Roster.Add(player, TestData.Summon("imp_water"));
			var phoenix = Roster.Add(player, TestData.Summon("phoenix_fire"));
			var light = Roster.Add(player, TestData.Summon("imp_light"));
			var all = new[] { fire, water, phoenix, light };
			string Ids(MonsterFilter filter) => string.Join(",", filter.Apply(all, database, player).Select(m => m.Id));

			Assert.Equal(string.Join(",", new[] { fire, phoenix }.OrderByDescending(m => m.Stars).ThenBy(m => m.Id).Select(m => m.Id)),
				Ids(new MonsterFilter { Element = Element.Fire }), "elemento, mais estrelas primeiro");

			fire.Level = 20;
			light.Favorite = true;
			var byLevel = Ids(new MonsterFilter { Sort = MonsterSort.Level }).Split(',');
			Assert.Equal($"{light.Id},{fire.Id}", $"{byLevel[0]},{byLevel[1]}", "o favorito antes, depois o maior nível");

			water.Awakened = true;
			Assert.Equal($"{water.Id}", Ids(new MonsterFilter { Awakened = true }), "só o desperto");
			Assert.Equal(3, new MonsterFilter { Awakened = false }.Apply(all, database, player).Count(), "os não despertos");

			RuneInventory.Equip(player, RuneInventory.Create(new Random(2), player, 6), phoenix.Id);
			Assert.Equal($"{phoenix.Id}", Ids(new MonsterFilter { Condition = MonsterCondition.Runed }), "com runas");
			Assert.Equal(3, new MonsterFilter { Condition = MonsterCondition.Unruned }.Apply(all, database, player).Count(), "sem runas");
			Assert.Equal($"{light.Id}", Ids(new MonsterFilter { Condition = MonsterCondition.Favorite }), "favoritos");
			water.Locked = true;
			Assert.Equal($"{water.Id}", Ids(new MonsterFilter { Condition = MonsterCondition.Locked }), "bloqueados");
			Assert.Equal(3, new MonsterFilter { Condition = MonsterCondition.Unlocked }.Apply(all, database, player).Count(), "desbloqueados");

			var speeds = new MonsterFilter { Sort = MonsterSort.Stat, SortStat = Stat.Speed }.Apply(all, database, player)
				.Where(m => !m.Favorite)
				.Select(m => MonsterFilter.Value(m, database.Summon(m.SummonId), player, Stat.Speed))
				.ToList();
			Assert.True(speeds.Zip(speeds.Skip(1)).All(pair => pair.First >= pair.Second), "por Velocidade, a maior primeiro");
			Assert.Equal(2, new MonsterFilter { Element = Element.Fire, Awakened = false }.Active, "dois campos filtrando");
		}

		[Test]
		private static void VaultGroupsOnlyCopiesThatMatchInEverything()
		{
			var player = TestData.PlayerWith();
			var copies = Enumerable.Range(0, 5).Select(_ => Roster.Add(player, TestData.Summon("imp_fire"))).ToList();
			var water = Roster.Add(player, TestData.Summon("imp_water"));
			var all = copies.Append(water).ToList();
			string Shape() => string.Join(",", MonsterStack.Group(all, player).Select(stack => stack.Count));

			Assert.Equal("5,1", Shape(), "cinco iguais e um diferente");
			copies[0].SkillLevels.Add(1);
			Assert.Equal("5,1", Shape(), "habilidade no nível 1 escrita ou não é a mesma");

			copies[1].Locked = true;
			copies[2].Level = 10;
			RuneInventory.Equip(player, RuneInventory.Create(new Random(3), player, 2), copies[3].Id);
			var stacks = MonsterStack.Group(all, player);
			Assert.Equal("2,1,1,1,1", Shape(), "bloqueio, nível e runas separam");
			Assert.Equal(copies[0].Id, stacks[0].First.Id, "o grupo fica onde a primeira cópia estava");
			Assert.True(stacks[0].Copies.Contains(copies[4]), "as que sobraram iguais seguem juntas");

			copies[4].RaiseSkill(0);
			Assert.Equal(6, MonsterStack.Group(all, player).Count, "habilidade a mais separa");
		}

		[Test]
		private static void NewMonstersAreTheOnesSinceTheLastVisit()
		{
			var player = TestData.PlayerWith("imp_fire");
			Account.Open(player, TestData.Database, DateTime.UnixEpoch);
			Assert.False(player.Monsters.Any(m => Roster.IsNew(player, m)), "num save sem o campo, o que já havia conta como visto");

			var arrived = Roster.Add(player, TestData.Summon("imp_fire"));
			Roster.Add(player, TestData.Summon("imp_fire"));
			string Shape() => string.Join(",", MonsterStack.Group(player.Monsters.Where(m => m.SummonId == "imp_fire"), player).Select(stack => stack.Count));
			Assert.True(Roster.IsNew(player, arrived), "o que chega depois é novo");
			Assert.Equal("1,2", Shape(), "os novos não se juntam aos já vistos");
			Assert.Equal(player.SeenMonster, PlayerSave.FromJson(PlayerSave.ToJson(player))!.SeenMonster, "o save guarda até onde viu");

			Assert.True(Roster.MarkSeen(player), "ver os monstros tira o novo");
			Assert.False(Roster.IsNew(player, arrived), "de todos");
			Assert.False(Roster.MarkSeen(player), "ver de novo não muda nada");
			Assert.Equal("3", Shape(), "vistos, as cópias se juntam");
		}

		[Test]
		private static void MonsterFilterReadsWhatTheSkillsDo()
		{
			var database = TestData.Database;
			var player = TestData.PlayerWith();

			// A cura escala na Vida máxima do alvo; o escudo, na de quem lança (docs/COMBATE.md).
			var healer = database.Summons.First(s => s.Skills.Any(k => k.Effects.Any(e => e.Kind == EffectKind.Heal)));
			Assert.True(SkillTraits.BehaviorsOf(healer, false).Contains(SkillBehavior.Heal), $"{healer.Id} cura");
			Assert.True(SkillTraits.ScalingsOf(healer, false).Contains(SkillScaling.TargetMaxHealth), $"{healer.Id} escala na Vida do alvo");
			var multi = database.Summons.First(s => s.Skills.Any(k => k.Effects.Any(e => e.Kind == EffectKind.Damage && e.Hits > 1)));
			Assert.True(SkillTraits.BehaviorsOf(multi, false).Contains(SkillBehavior.MultiHit), $"{multi.Id} bate várias vezes");
			Assert.True(SkillTraits.ScalingsOf(multi, false).Contains(SkillScaling.Attack), $"{multi.Id} escala no Ataque");

			// Cada opção que o filtro mostra acha algum monstro (numa das formas).
			foreach (var behavior in SkillTraits.BehaviorsIn(database.Summons))
				Assert.True(database.Summons.Any(s => SkillTraits.BehaviorsOf(s, false).Contains(behavior) || SkillTraits.BehaviorsOf(s, true).Contains(behavior)), $"{behavior} existe");
			Assert.True(SkillTraits.StatusesIn(database.Summons).Count > 0, "há efeitos para filtrar");

			// O filtro olha a forma de agora: o que só o Despertar traz não conta antes dele.
			var (summon, gained) = database.Summons
				.Select(s => (Summon: s, Gained: SkillTraits.BehaviorsOf(s, true).Except(SkillTraits.BehaviorsOf(s, false)).ToList()))
				.First(x => x.Gained.Count > 0);
			var monster = Roster.Add(player, summon);
			var filter = new MonsterFilter { Behavior = gained[0] };
			Assert.False(filter.Matches(monster, summon, player), $"{summon.Id} sem o Despertar não tem {gained[0]}");
			monster.Awakened = true;
			Assert.True(filter.Matches(monster, summon, player), $"{summon.Id} desperto tem {gained[0]}");
			Assert.Equal(3, new MonsterFilter { Behavior = SkillBehavior.Heal, Scaling = SkillScaling.Attack, Applies = StatusKind.Stun }.Active, "três campos de habilidade");
		}
	}
}
