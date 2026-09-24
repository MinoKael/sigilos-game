using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

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
				stages: Read("stages.json"));
		}

		public static readonly SkillDefinition Strike = new()
		{
			Name = "Golpe",
			Effects = new[] { new EffectDefinition { Kind = EffectKind.Damage, Power = 1 } },
		};

		/// <summary>
		/// Unidade com Ataque 100 e Defesa 0 por padrão, sem chance de crítico (o Dano crítico é o de
		/// base de Summoners War, 50%) e sem Resistência — que ainda assim barra 15% dos efeitos negativos.
		/// </summary>
		public static BattleUnit Unit(
			string name,
			Side side,
			double speed = 100,
			double health = 1000,
			double attack = 100,
			double defense = 0,
			double resistance = 0,
			Element element = Element.Fire,
			SkillDefinition? basic = null,
			SkillDefinition? glyphSkill = null,
			PassiveDefinition? passive = null,
			RuneSetEffects? runeEffects = null)
		{
			var stats = new StatBlock { Health = health, Attack = attack, Defense = defense, Speed = speed, CritDamage = 0.5, Resistance = resistance };
			return new BattleUnit(name, name, "", side, element, null, 1, false, stats, basic ?? Strike, glyphSkill, passive, 1, runeEffects ?? RuneSetEffects.None);
		}

		/// <summary>Efeitos de conjunto de runa: só os citados, o resto zero.</summary>
		public static RuneSetEffects Effects(
			double drain = 0,
			double stun = 0,
			double extraTurn = 0,
			double shield = 0,
			int immunity = 0,
			double counter = 0,
			double nemesis = 0,
			double destroy = 0) => new(drain, stun, extraTurn, shield, immunity, counter, nemesis, destroy);

		/// <summary>Uma luta de uma onda só.</summary>
		public static BattleSession Session(IReadOnlyList<BattleUnit> allies, IReadOnlyList<BattleUnit> enemies, int seed = 1)
		{
			foreach (var ally in allies)
				ally.Team = allies;
			foreach (var enemy in enemies)
				enemy.Team = enemies;

			return new BattleSession(allies, new[] { enemies }, seed);
		}

		/// <summary>Avança até a vez de <paramref name="unit"/>, resolvendo os outros turnos com o básico.</summary>
		public static void RunUntilTurnOf(BattleSession session, BattleUnit unit)
		{
			while (!session.IsOver)
			{
				var turn = session.BeginTurn();
				if (ReferenceEquals(turn.Actor, unit) && turn.NeedsDecision)
					return;
				if (turn.NeedsDecision)
					session.Act(new UnitAction(SkillSlot.Basic, false, null));
			}
		}
	}
}
