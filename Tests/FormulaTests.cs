using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>
	/// A conta dos efeitos (Core/Content/EffectScaling, Core/Battle/EffectAmount): outro atributo, termos somados,
	/// os fatores de Vida, aliados e Velocidade, o dano fixo e o Revive. Os números são de unidades montadas à
	/// mão: Ataque 100, Vida 1000, Defesa 0, sem crítico.
	/// </summary>
	internal static class FormulaTests
	{
		private static SkillDefinition Skill(params EffectDefinition[] effects) => TestData.Strike with { Effects = effects };

		/// <summary>O dano de cada golpe da básica de <paramref name="hero"/> em <paramref name="foe"/>.</summary>
		private static List<int> Hits(BattleUnit hero, BattleUnit foe, params BattleUnit[] friends)
		{
			var session = TestData.Session(friends.Prepend(hero).ToList(), new[] { foe });
			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			return session.Act(new UnitAction(0, foe)).OfType<Damaged>().Where(d => d.Target == foe).Select(d => d.Amount).ToList();
		}

		private static BattleUnit Foe(double defense = 0, double health = 1_000_000, double speed = 100) =>
			TestData.Unit("inimigo", Side.Enemies, speed: speed, health: health, attack: 1, defense: defense);

		[Test]
		private static void JsonReadsTheFormulaFields()
		{
			const string json = "{\"kind\": \"Damage\", \"power\": 1, \"stat\": \"Defense\", \"fixed\": true, " +
				"\"plus\": [{\"stat\": \"TargetMaxHealth\", \"power\": 0.06}], " +
				"\"factor\": {\"by\": \"LivingAllies\", \"base\": 13.5, \"slope\": -5.5}, " +
				"\"speed\": {\"add\": 80, \"overTarget\": true}}";
			var effect = JsonSerializer.Deserialize<EffectDefinition>(json, GameDatabase.JsonOptions)!;
			Assert.Equal(ScaleStat.Defense, effect.Stat, "stat");
			Assert.True(effect.Fixed, "fixed");
			Assert.Equal(ScaleStat.TargetMaxHealth, effect.Plus.Single().Stat, "plus");
			Assert.Equal(ScaleMeasure.LivingAllies, effect.Factor?.By, "factor");
			Assert.True(effect.Speed is { Add: 80, OverTarget: true }, "speed");

			var plain = JsonSerializer.Deserialize<EffectDefinition>("{\"kind\": \"Damage\", \"power\": 1}", GameDatabase.JsonOptions)!;
			Assert.False(EffectScaling.IsCustom(plain), "sem os campos, a conta é a de sempre");
			Assert.True(plain == new EffectDefinition { Kind = EffectKind.Damage, Power = 1 }, "e o efeito continua igual a outro sem eles");
		}

	}
}
