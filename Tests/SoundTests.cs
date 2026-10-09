using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.UI;
using Sigilos.UI.Audio;

namespace Sigilos.Tests
{
	/// <summary>
	/// Os efeitos sonoros no jogo (docs/SONS.md): o catálogo aponta para arquivos que existem, todo nome
	/// que o código toca está no catálogo, e a luta vira os sons certos (<see cref="BattleSounds"/>).
	/// </summary>
	internal static class SoundTests
	{
		/// <summary>Um nome de som no código: categoria do catálogo, ponto, o resto em minúsculas.</summary>
		private static readonly Regex SoundLiteral = new("\"((?:ui|combat|summon|rewards|grimoire|constellation|progression)(?:\\.[a-z0-9_]+)+)\"");

		private static readonly SkillDefinition Wave = new()
		{
			Name = "Onda",
			Cooldown = 3,
			Effects = TestData.Strike.Effects,
		};

		private static readonly SkillDefinition Deluge = new()
		{
			Name = "Dilúvio",
			Cooldown = BattleSounds.GrandCooldown,
			Effects = TestData.Strike.Effects,
		};

		private static SfxCatalog Catalog() => SfxCatalog.Parse(File.ReadAllText(Path.Combine(Program.ProjectRoot, "Assets", "Audio", "sounds.json")));

		/// <summary>Os sons dos eventos, momento a momento, como a tela da luta toca.</summary>
		private static List<Cue> Sounds(BattleSounds sounds, params BattleEvent[] events) => BattlePace.Beats(events).SelectMany(sounds.Of).ToList();

		private static List<string> Names(IEnumerable<Cue> cues) => cues.Select(cue => cue.Name).ToList();

		/// <summary>Os nomes numa linha, para comparar a sequência inteira.</summary>
		private static string Line(IEnumerable<string> names) => string.Join(" ", names);

		[Test]
		private static void CatalogFilesExist()
		{
			var catalog = Catalog();
			var problems = new List<string>();
			foreach (var name in catalog.Names)
			{
				if (catalog.Files(name).Count == 0)
					problems.Add($"{name}: sem arquivo");
				foreach (var file in catalog.Files(name))
				{
					if (!file.StartsWith("res://") || !File.Exists(Path.Combine(Program.ProjectRoot, file["res://".Length..])))
						problems.Add($"{name}: {file}");
				}
			}

			Assert.Empty(problems, "arquivos do catálogo que não existem");
		}

		/// <summary>
		/// Todo "categoria.nome" que o código da UI e do GameEntry escreve é um som do catálogo, ou uma chave
		/// de texto (as duas têm a mesma cara: "summon.title" é texto).
		/// </summary>
		[Test]
		private static void CodeOnlyPlaysCatalogSounds()
		{
			var catalog = Catalog();
			var texts = new HashSet<string>();
			using (var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Program.ProjectRoot, "Data", "texts", "pt-BR.json")), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
				Flatten(document.RootElement, "", texts);

			var found = 0;
			var problems = new List<string>();
			foreach (var folder in new[] { "UI", "GameEntry" })
			{
				foreach (var path in Directory.EnumerateFiles(Path.Combine(Program.ProjectRoot, folder), "*.cs", SearchOption.AllDirectories))
				{
					foreach (Match match in SoundLiteral.Matches(File.ReadAllText(path)))
					{
						var name = match.Groups[1].Value;
						if (texts.Contains(name))
							continue;
						found++;
						if (!catalog.Has(name))
							problems.Add($"{Path.GetFileName(path)}: {name}");
					}
				}
			}

			Assert.Empty(problems, "sons fora do catálogo");
			Assert.True(found > 30, $"o código toca os sons pelo nome ({found} achados)");
		}

		private static void Flatten(JsonElement node, string prefix, HashSet<string> keys)
		{
			foreach (var property in node.EnumerateObject())
			{
				var key = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
				if (property.Value.ValueKind == JsonValueKind.Object)
					Flatten(property.Value, key, keys);
				else
					keys.Add(key);
			}
		}

		[Test]
		private static void BasicHitAndKnockout()
		{
			var hero = TestData.Unit("herói", Side.Allies);
			var foe = TestData.Unit("inimigo", Side.Enemies);
			var cues = Sounds(new BattleSounds(), new SkillUsed(hero, TestData.Strike), new Damaged(foe, 120, 0, false, 1), new Died(foe));

			Assert.Equal("combat.hit", cues[0].Name, "a básica corre calada e acerta");
			Assert.Equal(0d, cues[0].Delay, "o acerto na hora");
			Assert.Equal("combat.knockout", cues[1].Name, "a queda");
			Assert.Equal(BattleSounds.FollowDelay, cues[1].Delay, "a queda depois do acerto");
			Assert.Equal(2, cues.Count, "nada mais");
		}

		[Test]
		private static void SkillsSoundByElement()
		{
			var mage = TestData.Unit("maga", Side.Allies, element: Element.Water);
			var foe = TestData.Unit("inimigo", Side.Enemies);

			var cues = Names(Sounds(new BattleSounds(), new SkillUsed(mage, Wave), new Damaged(foe, 120, 0, false, 1)));
			Assert.Equal("combat.elements.water_impact", Line(cues), "a habilidade é o impacto do elemento, e a corrida é calada");

			cues = Names(Sounds(new BattleSounds(), new SkillUsed(mage, Deluge), new Damaged(foe, 120, 0, false, 1)));
			Assert.Equal("combat.elements.water_grand", Line(cues), "a de recarga longa é o grande feitiço");

			cues = Names(Sounds(new BattleSounds(), new SkillUsed(mage, Wave), new Damaged(foe, 60, 0, false, 1), new Damaged(foe, 60, 0, false, 1), new Damaged(foe, 60, 0, false, 1)));
			Assert.Equal("combat.elements.water_impact combat.hit combat.hit", Line(cues), "os golpes seguintes da ação são o acerto simples");
		}

		[Test]
		private static void BossHasItsOwnVoice()
		{
			var boss = TestData.Unit("chefe", Side.Enemies, boss: true);
			var hero = TestData.Unit("herói", Side.Allies);

			var cues = Names(Sounds(new BattleSounds(), new SkillUsed(boss, Deluge), new Damaged(hero, 300, 0, false, 1)));
			Assert.Equal("combat.boss.special", Line(cues), "a habilidade do chefe é a voz dele");

			cues = Names(Sounds(new BattleSounds(), new SkillUsed(boss, TestData.Strike), new Damaged(hero, 300, 0, false, 1)));
			Assert.Equal("combat.hit_heavy", Line(cues), "a básica do chefe é o acerto pesado");

			cues = Names(Sounds(new BattleSounds(), new SkillUsed(hero, TestData.Strike), new Damaged(boss, 300, 0, false, 1), new Died(boss)));
			Assert.True(cues.Contains("combat.boss.defeated"), "o chefe cai com o som dele");
			Assert.False(cues.Contains("combat.knockout"), "não o nocaute comum");
		}

		[Test]
		private static void CritAndAreaAreOneHit()
		{
			var hero = TestData.Unit("herói", Side.Allies, element: Element.Fire);
			var a = TestData.Unit("a", Side.Enemies, element: Element.Wind);
			var b = TestData.Unit("b", Side.Enemies, element: Element.Wind);

			var cues = Names(Sounds(new BattleSounds(), new SkillUsed(hero, TestData.Strike), new Damaged(a, 120, 30, false, 1.3), new Damaged(b, 240, 0, true, 1.3)));
			Assert.Equal("combat.hit", Line(cues), "área, crítico, escudo e vantagem: um acerto só");

			cues = Names(Sounds(new BattleSounds(), new SkillUsed(hero, TestData.Strike), new Missed(a), new Protected(b)));
			Assert.Equal("", Line(cues), "o erro e a Égide são calados");
		}

		[Test]
		private static void DamageOverTimeAndBomb()
		{
			var hero = TestData.Unit("herói", Side.Allies);
			var foe = TestData.Unit("inimigo", Side.Enemies);
			var sounds = new BattleSounds();

			var cues = Names(Sounds(sounds, new TurnStarted(foe, 2), new Damaged(foe, 80, 0, false, 1)));
			Assert.Equal("", Line(cues), "o Veneno é calado");

			cues = Names(Sounds(sounds, new TurnStarted(foe, 3), new Damaged(foe, 80, 0, false, 1), new Died(foe)));
			Assert.Equal("combat.knockout", Line(cues), "a queda pelo Veneno soa");

			cues = Names(Sounds(sounds, new TurnStarted(foe, 3), new StatusRemoved(foe, StatusKind.Bomb), new Damaged(foe, 200, 0, false, 1)));
			Assert.Equal("combat.hit_heavy", Line(cues), "a Bomba explode como o acerto pesado");

			cues = Names(Sounds(sounds, new SkillUsed(hero, TestData.Strike), new Damaged(foe, 120, 0, false, 1), new TurnStarted(hero, 4), new StatusRemoved(hero, StatusKind.Shield), new StatusApplied(hero, StatusKind.AttackUp, 1), new Healed(hero, 50)));
			Assert.Equal("combat.hit", Line(cues), "o que vence, pega ou cura sozinho no turno fica calado");
		}

		[Test]
		private static void SupportSoundsOnce()
		{
			var healer = TestData.Unit("curandeira", Side.Allies, element: Element.Light);
			var foe = TestData.Unit("inimigo", Side.Enemies);
			var allies = Enumerable.Range(1, 5).Select(i => TestData.Unit($"aliado{i}", Side.Allies)).ToList();
			var events = new List<BattleEvent> { new SkillUsed(healer, Wave) };
			events.AddRange(allies.Select(ally => new Healed(ally, 100)));
			events.AddRange(allies.Select(ally => new StatusApplied(ally, StatusKind.AttackUp, 2)));
			events.Add(new StatusApplied(foe, StatusKind.Stun, 1));
			events.Add(new Revived(allies[0]));

			var cues = Names(Sounds(new BattleSounds(), events.ToArray()));
			Assert.Equal("combat.status.boon", Line(cues), "curar, fortalecer, enfraquecer e reviver numa ação: um som só");

			cues = Names(Sounds(new BattleSounds(), new SkillUsed(healer, Wave), new StatusApplied(foe, StatusKind.Stun, 1), new StatusApplied(foe, StatusKind.AttackDown, 2)));
			Assert.Equal("combat.status.bane", Line(cues), "a ação que só enfraquece");
		}

		[Test]
		private static void WhatAHitCausesIsQuiet()
		{
			var hero = TestData.Unit("herói", Side.Allies, element: Element.Fire);
			var foe = TestData.Unit("inimigo", Side.Enemies, element: Element.Wind);
			var cues = Sounds(new BattleSounds(),
				new SkillUsed(hero, TestData.Strike),
				new Damaged(foe, 120, 30, false, 1.3),
				new StatusApplied(foe, StatusKind.Stun, 1),
				new StatusApplied(foe, StatusKind.Affliction, 2),
				new Resisted(foe),
				new ImpetoChanged(foe, -0.2),
				new Died(foe),
				new ExtraTurn(hero),
				new TurnSkipped(foe, StatusKind.Stun));

			Assert.Equal("combat.hit combat.knockout", Line(Names(cues)), "o acerto e a queda; efeitos, Ímpeto e turnos calados");
		}

		[Test]
		private static void WavesAndTheEnd()
		{
			var foe = TestData.Unit("inimigo", Side.Enemies);
			var boss = TestData.Unit("chefe", Side.Enemies, boss: true);
			var cues = Names(Sounds(new BattleSounds(),
				new WaveStarted(1, 2, new[] { foe }),
				new WaveStarted(2, 2, new[] { foe, boss }),
				new BattleEnded(true)));

			Assert.Equal("progression.new_wave combat.boss.enter progression.victory", Line(cues), "a onda, a do chefe e a vitória");
			Assert.Equal("progression.defeat", Line(Names(Sounds(new BattleSounds(), new BattleEnded(false)))), "a derrota");
		}
	}
}
