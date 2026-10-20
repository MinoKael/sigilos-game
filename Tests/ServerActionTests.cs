using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.Core.Summoning;

namespace Sigilos.Tests
{
	/// <summary>As ações do servidor (melhora de runa, pedras, reavaliação, invocação) e a conferência do save enviado.</summary>
	internal static class ServerActionTests
	{
		private static PlayerState Player()
		{
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), TestData.Database);
			player.Essence = 1_000_000;
			player.Scrolls = 100;
			player.ReappraisalGems = 2;
			return player;
		}

		private static PlayerState Copy(PlayerState player) => PlayerSave.FromJson(PlayerSave.ToJson(player))!;

		private static Rune Fresh(PlayerState player, int slot = 1) => RuneInventory.Create(new Random(3), player, 6, null, RuneRarity.Rare, slot);

		[Test]
		private static void UpgradeChargesEssenceAndTheDeltaReproducesItOnAnotherCopy()
		{
			var server = Player();
			var rune = Fresh(server);
			var game = Copy(server);
			var baseline = ServerActions.Snapshot(game);
			var essence = server.Essence;

			var delta = ServerActions.Apply(new Random(5), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.UpgradeRune, RuneId = rune.Id, Target = 12 })!;
			Assert.True(delta != null, "a melhora vale");
			Assert.Equal(12, rune.Level, "chegou ao +12");
			Assert.True(server.Essence < essence, "gastou Essência");

			ServerActions.ApplyDelta(game, delta, baseline);
			Assert.Equal(PlayerSave.ToJson(server), PlayerSave.ToJson(game), "a cópia do jogo ficou igual à do servidor");
		}

		[Test]
		private static void UpgradeWithoutEssenceOrWithAMissingRuneDoesNothing()
		{
			var player = Player();
			var rune = Fresh(player);
			player.Essence = 0;
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.UpgradeRune, RuneId = rune.Id, Target = 12 }) == null, "sem Essência");
			Assert.Equal(0, rune.Level, "nada mudou");
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.UpgradeRune, RuneId = 9999, Target = 3 }) == null, "runa que não existe");
		}

		[Test]
		private static void SummonDeliversMonstersAndKeepsThePityOnTheDelta()
		{
			var server = Player();
			var game = Copy(server);
			var baseline = ServerActions.Snapshot(game);
			var delta = ServerActions.Apply(new Random(9), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.Summon, Count = 10, Scroll = ScrollKind.Mystic })!;
			Assert.True(delta != null, "a invocação vale");
			Assert.Equal(10, delta.Monsters.Count, "dez monstros");
			Assert.Equal(10, delta.TotalPulls, "dez invocações contadas");

			var earned = game.Essence;
			game.Essence += 500;
			ServerActions.ApplyDelta(game, delta, baseline);
			Assert.Equal(earned + 500, game.Essence, "o que o jogo ganhou enquanto esperava não se perde");
			game.Essence = earned;
			Assert.Equal(PlayerSave.ToJson(server), PlayerSave.ToJson(game), "a cópia do jogo ficou igual à do servidor");
			Assert.Equal(10, ServerActions.Results(TestData.Database, ActionDelta.FromJson(delta.ToJson())!).Count, "o resultado atravessa o texto");
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.Summon, Count = 3 }) == null, "só 1 ou 10");
		}

		[Test]
		private static void ReappraisalGoesBackToTheFreshRune()
		{
			var server = Player();
			var rune = Fresh(server);
			ServerActions.Apply(new Random(5), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.UpgradeRune, RuneId = rune.Id, Target = 12 });
			var delta = ServerActions.Apply(new Random(5), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.ReappraiseRune, RuneId = rune.Id });
			Assert.True(delta != null, "reavalia");
			Assert.Equal(0, rune.Level, "voltou a +0");
			Assert.Equal(1, server.ReappraisalGems, "gastou uma gema");
		}

		[Test]
		private static void AuditRefusesWhatOnlyTheServerMayDo()
		{
			var before = Player();
			var rune = Fresh(before);
			Assert.Equal(null, SaveAudit.Check(Copy(before), before, false), "o mesmo save vale");

			var upgraded = Copy(before);
			upgraded.Runes.First(r => r.Id == rune.Id).Level = 12;
			Assert.Equal("rune_changed", SaveAudit.Check(upgraded, before, false), "melhora por conta própria");

			var substat = Copy(before);
			substat.Runes.First(r => r.Id == rune.Id).Substats[0].Rolls[0] = new RuneRoll(0, 9999);
			Assert.Equal("rune_changed", SaveAudit.Check(substat, before, false), "subatributo mexido");

			var pity = Copy(before);
			pity.PullsSinceFiveStar = 99;
			Assert.Equal("pulls_changed", SaveAudit.Check(pity, before, false), "garantia mexida");

			var locked = Copy(before);
			locked.Runes.First(r => r.Id == rune.Id).Locked = true;
			Assert.Equal(null, SaveAudit.Check(locked, before, false), "o cadeado é do jogador");
		}

		[Test]
		private static void AuditAcceptsFreshDropsAndRefusesForgedOnes()
		{
			var before = Player();
			Fresh(before);
			var next = Copy(before);
			var drop = Fresh(next, 2);
			Assert.Equal(null, SaveAudit.Check(next, before, false), "um drop novo vale");

			var maxed = Copy(next);
			maxed.Runes.First(r => r.Id == drop.Id).Level = 15;
			Assert.Equal("rune_forged", SaveAudit.Check(maxed, before, false), "runa nova já melhorada");

			var perfect = Copy(next);
			perfect.Runes.First(r => r.Id == drop.Id).Substats[0].Rolls[0] = new RuneRoll(0, 9999);
			Assert.Equal("rune_forged", SaveAudit.Check(perfect, before, false), "sorteio fora da faixa");

			var reused = Copy(before);
			var old = reused.Runes.First();
			reused.Runes.Remove(old);
			var back = Copy(before);
			Assert.Equal(null, SaveAudit.Check(reused, before, false), "vender vale");
			Assert.Equal("rune_forged", SaveAudit.Check(back, reused, false), "o id vendido não volta");

			var replaced = Copy(before);
			replaced.TotalPulls = 50;
			Assert.Equal("pulls_forged", SaveAudit.Check(replaced, before, true), "conta nova com invocações");
		}
	}
}
