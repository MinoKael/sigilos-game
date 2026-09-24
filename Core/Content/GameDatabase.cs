using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Tudo o que vem de Data/. Recebe o texto dos arquivos, não caminhos, para não depender do Godot:
	/// o jogo lê via res://, o Tests/ lê do disco, e os dois montam o mesmo banco.
	/// </summary>
	public sealed class GameDatabase
	{
		public const int MaxWaves = 3;
		public const int MaxEnemiesPerWave = 5;

		public static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			Converters = { new JsonStringEnumConverter() },
		};

		private readonly Dictionary<string, SummonDefinition> _summonsById;
		private readonly Dictionary<string, EnemyDefinition> _enemiesById;
		private readonly Dictionary<string, PageDefinition> _pagesById;

		public GameDatabase(
			IReadOnlyDictionary<Role, StatBlock> roles,
			IReadOnlyList<FamilyDefinition> families,
			IReadOnlyList<SummonDefinition> summons,
			IReadOnlyList<EnemyDefinition> enemies,
			IReadOnlyList<StageDefinition> stages,
			IReadOnlyList<PageDefinition> pages,
			IReadOnlyList<ConjurerDefinition> conjurers)
		{
			Roles = roles;
			Families = families;
			Summons = summons;
			Enemies = enemies;
			Stages = stages.OrderBy(s => s.Number).ToList();
			Pages = pages;
			Conjurers = conjurers;

			var familiesById = families.ToDictionary(f => f.Id);
			foreach (var summon in summons)
			{
				if (familiesById.TryGetValue(summon.FamilyId, out var family))
					summon.Family = family;
			}

			_summonsById = summons.ToDictionary(s => s.Id);
			_enemiesById = enemies.ToDictionary(e => e.Id);
			_pagesById = pages.ToDictionary(p => p.Id);
		}

		/// <summary>Atributos de base por papel, no nível 60 e com 5 estrelas (GDD, seção 15).</summary>
		public IReadOnlyDictionary<Role, StatBlock> Roles { get; }

		public IReadOnlyList<FamilyDefinition> Families { get; }
		public IReadOnlyList<SummonDefinition> Summons { get; }
		public IReadOnlyList<EnemyDefinition> Enemies { get; }

		/// <summary>Em ordem de número.</summary>
		public IReadOnlyList<StageDefinition> Stages { get; }

		public IReadOnlyList<PageDefinition> Pages { get; }
		public IReadOnlyList<ConjurerDefinition> Conjurers { get; }

		public SummonDefinition Summon(string id) => _summonsById[id];
		public EnemyDefinition Enemy(string id) => _enemiesById[id];
		public PageDefinition Page(string id) => _pagesById[id];
		public StageDefinition Stage(int number) => Stages.First(s => s.Number == number);
		public ConjurerDefinition Conjurer(string id) => Conjurers.First(c => c.Id == id);

		public bool HasSummon(string id) => _summonsById.ContainsKey(id);
		public bool HasPage(string id) => _pagesById.ContainsKey(id);

		public static GameDatabase FromJson(
			string roles,
			string families,
			IEnumerable<string> summons,
			string enemies,
			string stages,
			string pages,
			string conjurers)
		{
			return new GameDatabase(
				Parse<Dictionary<Role, StatBlock>>(roles, "roles.json"),
				Parse<List<FamilyDefinition>>(families, "families.json"),
				summons.Select(text => Parse<SummonDefinition>(text, "summons/*.json")).ToList(),
				Parse<List<EnemyDefinition>>(enemies, "enemies.json"),
				Parse<List<StageDefinition>>(stages, "stages.json"),
				Parse<List<PageDefinition>>(pages, "pages.json"),
				Parse<List<ConjurerDefinition>>(conjurers, "conjurers.json"));
		}

		private static T Parse<T>(string json, string file)
		{
			try
			{
				return JsonSerializer.Deserialize<T>(json, JsonOptions)
					?? throw new InvalidOperationException($"{file} está vazio.");
			}
			catch (JsonException exception)
			{
				throw new InvalidOperationException($"{file}: {exception.Message}", exception);
			}
		}

		/// <summary>Erros de dados: referência quebrada, número fora da faixa. Vazio quando está tudo certo.</summary>
		public IEnumerable<string> Validate()
		{
			foreach (var role in Enum.GetValues<Role>())
			{
				if (!Roles.ContainsKey(role))
					yield return $"roles.json: falta o papel {role}.";
			}

			foreach (var family in Families)
			{
				if (family.Rarity is < 1 or > 5)
					yield return $"Família {family.Id}: raridade {family.Rarity} fora de 1 a 5.";
				if (family.Image.Length == 0)
					yield return $"Família {family.Id}: sem imagem.";
			}

			foreach (var summon in Summons)
			{
				if (Families.All(f => f.Id != summon.FamilyId))
					yield return $"Invocação {summon.Id}: família '{summon.FamilyId}' não existe.";
				if (summon.GlyphSkill.Cooldown <= 0)
					yield return $"Invocação {summon.Id}: habilidade de Glifo sem recarga.";
				foreach (var problem in ValidateSkill(summon.Basic).Concat(ValidateSkill(summon.GlyphSkill)))
					yield return $"Invocação {summon.Id}: {problem}";
			}

			foreach (var enemy in Enemies)
			{
				foreach (var problem in ValidateSkill(enemy.Basic))
					yield return $"Inimigo {enemy.Id}: {problem}";
				if (enemy.GlyphSkill != null)
				{
					foreach (var problem in ValidateSkill(enemy.GlyphSkill))
						yield return $"Inimigo {enemy.Id}: {problem}";
				}
			}

			for (var i = 0; i < Stages.Count; i++)
			{
				var stage = Stages[i];
				if (stage.Number != i + 1)
					yield return $"Fases: esperava a fase {i + 1}, veio a {stage.Number}.";
				if (stage.Waves.Count is < 1 or > MaxWaves)
					yield return $"Fase {stage.Number}: {stage.Waves.Count} ondas (de 1 a {MaxWaves}).";
				foreach (var wave in stage.Waves)
				{
					if (wave.Count is < 1 or > MaxEnemiesPerWave)
						yield return $"Fase {stage.Number}: onda com {wave.Count} inimigos (de 1 a {MaxEnemiesPerWave}).";
					foreach (var slot in wave.Where(slot => !_enemiesById.ContainsKey(slot.Enemy)))
						yield return $"Fase {stage.Number}: inimigo '{slot.Enemy}' não existe.";
				}
			}

			foreach (var page in Pages)
			{
				if (page.Circle is < 1 or > 3)
					yield return $"Página {page.Id}: Círculo {page.Circle} fora de 1 a 3.";
				if (page.Glyph == Glyph.Door)
					yield return $"Página {page.Id}: Porta ainda não tem fórmula no MVP.";
			}

			if (Conjurers.Count == 0)
				yield return "conjurers.json: nenhum Conjurador.";
		}

		private static IEnumerable<string> ValidateSkill(SkillDefinition skill)
		{
			if (skill.Effects.Count == 0)
				yield return $"'{skill.Name}' não tem efeitos.";
			foreach (var effect in skill.Effects.Concat(skill.EnhancedEffects))
			{
				if (effect.Hits < 1)
					yield return $"'{skill.Name}': efeito com {effect.Hits} golpes.";
				if (effect.Chance is < 0 or > 1)
					yield return $"'{skill.Name}': chance {effect.Chance} fora de 0 a 1.";
				if (effect.Kind == EffectKind.Status && effect.Turns < 1)
					yield return $"'{skill.Name}': efeito de status sem duração.";
			}

			if (skill.EnhanceCost > 0 && skill.EnhancedEffects.Count == 0)
				yield return $"'{skill.Name}': tem custo de aprimoramento mas não tem efeitos aprimorados.";
		}
	}
}
