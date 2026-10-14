using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;

namespace Sigilos.Tests
{
	/// <summary>
	/// A tela anima a ação depois de ela já estar calculada: a Vida de cada momento vem no evento
	/// (<see cref="Damaged.Health"/> e os outros), senão o cartão mostraria o fim da ação já no primeiro golpe.
	/// </summary>
	internal static class EventHealthTests
	{
		[Test]
		private static void HealthEventsCarryTheHealthOfTheirMoment()
		{
			var database = TestData.Database;
			var encounter = database.Stage(9).Encounter;
			foreach (var pair in new[] { new[] { "wolf_light", "wolf_dark" }, new[] { "knight_fire", "pixie_water" } })
			{
				var events = new List<BattleEvent>();
				AutoBattle.Run(BattleFactory.Create(database, BattleDigest.Team(database, pair, 6, 40, true, false), encounter, 7), events);

				var known = new Dictionary<BattleUnit, double>();
				var counters = 0;
				foreach (var battleEvent in events)
				{
					switch (battleEvent)
					{
						case Damaged damaged:
							Assert.True(damaged.Health != null, "o dano diz a Vida de depois");
							if (known.TryGetValue(damaged.Target, out var before))
								Assert.Near(System.Math.Max(0, before - damaged.Amount), damaged.Health!.Value, $"{damaged.Target.Name}: a Vida de depois bate com o dano");
							known[damaged.Target] = damaged.Health!.Value;
							break;
						case Healed healed:
							Assert.True(healed.Health != null, "a cura diz a Vida de depois");
							known[healed.Target] = healed.Health!.Value;
							break;
						case HealthLeveled leveled:
							Assert.True(leveled.Health != null, "o nivelamento diz a Vida de depois");
							known[leveled.Target] = leveled.Health!.Value;
							break;
						case Revived revived:
							Assert.True(revived.Health is > 0, "quem volta diz com quanta Vida");
							known[revived.Unit] = revived.Health!.Value;
							break;
						case Died died:
							known[died.Unit] = 0;
							break;
						case Counterattack:
							counters++;
							break;
					}
				}

				if (pair[0] == "wolf_light")
					Assert.True(counters > 0, "os lobos revidam na luta");

				// Os efeitos do momento: o último evento de cada unidade é o estado em que ela ficou.
				var last = new Dictionary<BattleUnit, StatusesChanged>();
				foreach (var changed in events.OfType<StatusesChanged>())
					last[changed.Unit] = changed;
				foreach (var (unit, changed) in last)
				{
					var now = unit.StatusMarks();
					Assert.True(changed.Marks.SequenceEqual(now), $"{unit.Name}: os efeitos do último evento são os do fim");
					Assert.Near(unit.ShieldValue, changed.Shield, $"{unit.Name}: o escudo também");
				}

				Assert.True(last.Count > 0, "a luta mexeu em efeitos");
			}
		}
	}
}
