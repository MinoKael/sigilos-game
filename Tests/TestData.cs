using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	/// <summary>
	/// Dados para os testes: o banco de verdade (Data/) e peças montadas à mão, em que cada número é
	/// conhecido — um golpe de 100% com Ataque 100 contra Defesa 0 tira exatamente 100.
	/// </summary>
	internal static class TestData
	{
		public static GameDatabase LoadReal()
		{
			var data = Path.Combine(Program.ProjectRoot, "Data");
			string Read(string file) => File.ReadAllText(Path.Combine(data, file));

			return GameDatabase.FromJson(
				roles: Read("roles.json"),
				families: Read("families.json"),
				summons: Directory.GetFiles(Path.Combine(data, "summons"), "*.json").OrderBy(f => f).Select(File.ReadAllText),
				enemies: Read("enemies.json"),
				stages: Read("stages.json"),
				pages: Read("pages.json"),
				conjurers: Read("conjurers.json"));
		}

		public static readonly SkillDefinition Strike = new()
		{
			Name = "Golpe",
			Effects = new[] { new EffectDefinition { Kind = EffectKind.Damage, Power = 1 } },
		};

		public static readonly ConjurerDefinition Conjurer = new()
		{
			Id = "teste",
			Name = "Conjurador de Teste",
			Speed = 100,
			Power = 100,
			PageSlots = 4,
			ChannelGain = 2,
		};

		/// <summary>Unidade sem crítico, sem Resistência, com Ataque 100 e Defesa 0 por padrão.</summary>
		public static BattleUnit Unit(
			string name,
			Side side,
			double speed = 100,
			double health = 1000,
			double attack = 100,
			double defense = 0,
			Element element = Element.Fire,
			SkillDefinition? basic = null,
			SkillDefinition? glyphSkill = null,
			PassiveDefinition? passive = null)
		{
			var stats = new StatBlock { Health = health, Attack = attack, Defense = defense, Speed = speed };
			return new BattleUnit(name, name, "", side, element, null, stats, basic ?? Strike, glyphSkill, passive, 1);
		}

		/// <summary>Uma luta de uma onda só, com o Conjurador de teste e as páginas pedidas.</summary>
		public static BattleSession Session(
			IReadOnlyList<BattleUnit> allies,
			IReadOnlyList<BattleUnit> enemies,
			IReadOnlyList<PageSlot>? pages = null,
			double conjurerSpeed = 100,
			int seed = 1)
		{
			foreach (var ally in allies)
				ally.Team = allies;
			foreach (var enemy in enemies)
				enemy.Team = enemies;

			var conjurer = new ConjurerSeat(Conjurer with { Speed = conjurerSpeed }, 100, pages ?? new List<PageSlot>());
			return new BattleSession(allies, new[] { enemies }, conjurer, seed);
		}

		/// <summary>Avança até a vez de <paramref name="unit"/>, resolvendo os outros turnos no automático.</summary>
		public static void RunUntilTurnOf(BattleSession session, ITurnTaker unit)
		{
			while (!session.IsOver)
			{
				var turn = session.BeginTurn();
				if (ReferenceEquals(turn.Actor, unit) && turn.NeedsDecision)
					return;
				if (!turn.NeedsDecision)
					continue;

				switch (turn.Actor)
				{
					case ConjurerSeat:
						session.Act(ConjurerAction.Channel);
						break;
					case BattleUnit actor:
						session.Act(new UnitAction(SkillSlot.Basic, false, null));
						break;
				}
			}
		}
	}
}
