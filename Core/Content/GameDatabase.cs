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

		/// <summary>A Campanha solta runas até esta estrela; 5★ e 6★ são das Masmorras.</summary>
		public const int MaxCampaignRuneGrade = 4;

		public static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			Converters = { new JsonStringEnumConverter() },
		};

		private readonly Dictionary<string, SummonDefinition> _summonsById;
		private readonly Dictionary<string, EnemyDefinition> _enemiesById;

		public GameDatabase(
			StatModel statModel,
			IReadOnlyList<FamilyDefinition> families,
			IReadOnlyList<EnemyDefinition> enemies,
			IReadOnlyList<StageDefinition> stages,
			IReadOnlyList<DungeonDefinition> dungeons,
			IReadOnlyList<ShopOffer> shop)
		{
			StatModel = statModel;
			Families = families;
			Enemies = enemies;
			Stages = stages.OrderBy(s => s.Number).ToList();
			Dungeons = dungeons;
			Shop = shop;

			// As variantes saem dos arquivos das famílias, na ordem dos elementos. O arquivo só traz Vida,
			// Ataque, Defesa e Velocidade: os outros quatro atributos são os de base do modelo.
			var summons = new List<SummonDefinition>();
			foreach (var family in families)
			{
				foreach (var summon in family.Variations.Values.OrderBy(s => s.Element))
				{
					summon.Family = family;
					summon.Stats = statModel.Complete(summon.Stats);
					summon.AwakenedStats = statModel.Complete(summon.AwakenedStats);
					summons.Add(summon);
				}
			}

			foreach (var enemy in enemies)
				enemy.Stats = statModel.Complete(enemy.Stats);

			Summons = summons;
			_summonsById = summons.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
			_enemiesById = enemies.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
		}

		/// <summary>Os parâmetros com que os atributos de Data/summons foram calculados.</summary>
		public StatModel StatModel { get; }

		/// <summary>Uma por arquivo de Data/summons.</summary>
		public IReadOnlyList<FamilyDefinition> Families { get; }

		/// <summary>Todas as variantes de todas as famílias.</summary>
		public IReadOnlyList<SummonDefinition> Summons { get; }
		public IReadOnlyList<EnemyDefinition> Enemies { get; }

		/// <summary>Em ordem de número.</summary>
		public IReadOnlyList<StageDefinition> Stages { get; }

		public IReadOnlyList<DungeonDefinition> Dungeons { get; }

		/// <summary>As ofertas da Loja, na ordem de Data/shop.json.</summary>
		public IReadOnlyList<ShopOffer> Shop { get; }

		public SummonDefinition Summon(string id) => _summonsById[id];
		public EnemyDefinition Enemy(string id) => _enemiesById[id];
		public StageDefinition Stage(int number) => Stages.First(s => s.Number == number);
		public DungeonDefinition Dungeon(string id) => Dungeons.First(d => d.Id == id);

		public bool HasSummon(string id) => _summonsById.ContainsKey(id);

		/// <summary>Nome, desenho e elemento de um inimigo de onda, seja invocação ou criatura única.</summary>
		public (string Name, string Image, Element Element) Foe(StageEnemy slot)
		{
			if (slot.Summon is { } id)
			{
				var summon = Summon(id);
				return (summon.Name, summon.ImageFor(false), summon.Element);
			}

			var enemy = Enemy(slot.Enemy!);
			return (enemy.Name, enemy.Image, slot.Element);
		}

		/// <param name="families">O texto de cada arquivo de Data/summons, um por família.</param>
		public static GameDatabase FromJson(
			string statModel,
			IEnumerable<string> families,
			string enemies,
			string stages,
			string dungeons,
			string shop)
		{
			return new GameDatabase(
				Parse<StatModel>(statModel, "stat_model.json"),
				families.Select(text => Parse<FamilyDefinition>(text, "summons/*.json")).ToList(),
				Parse<List<EnemyDefinition>>(enemies, "enemies.json"),
				Parse<List<StageDefinition>>(stages, "stages.json"),
				Parse<List<DungeonDefinition>>(dungeons, "dungeons.json"),
				Parse<List<ShopOffer>>(shop, "shop.json"));
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
			foreach (var problem in StatModel.Validate())
				yield return problem;

			foreach (var family in Families)
			{
				if (StatModel.Budget(family.Rarity, false) == null)
					yield return $"Família {family.Id}: {family.Rarity}★ não tem orçamento em stat_model.json.";
				if (family.Image.Length == 0 || family.AwakenedImage.Length == 0)
					yield return $"Família {family.Id}: falta a imagem normal ou a do Despertar.";
				if (family.Variations.Count == 0)
					yield return $"Família {family.Id}: sem variantes.";
				foreach (var (key, summon) in family.Variations)
				{
					if (!string.Equals(key, summon.Element.ToString(), StringComparison.OrdinalIgnoreCase))
						yield return $"Família {family.Id}: a variante \"{key}\" é do elemento {summon.Element}.";
				}
			}

			foreach (var id in Families.Select(f => f.Id).Concat(Summons.Select(s => s.Id)).GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key))
				yield return $"Invocações: o id '{id}' aparece mais de uma vez.";

			foreach (var summon in Summons)
			{
				foreach (var problem in ValidateStats(summon))
					yield return $"Invocação {summon.Id}: {problem}";
				if (summon.Awakening.Name.Length == 0)
					yield return $"Invocação {summon.Id}: sem nome de Despertar.";
				if (summon.Awakening.Stat is { } stat && stat is not (Stat.Speed or Stat.Crit or Stat.Resistance or Stat.Accuracy))
					yield return $"Invocação {summon.Id}: o Despertar dá Velocidade, Crítico, Resistência ou Precisão, não {stat}.";
				if (summon.Awakening.Stat == null && summon.Awakening.Skill == null && summon.Skills.All(s => !s.ChangesOnAwakening))
					yield return $"Invocação {summon.Id}: o Despertar não dá nada (atributo, habilidade nova ou melhorada).";
				if (summon.Awakening.Skill is { Cooldown: 0, IsPassive: false })
					yield return $"Invocação {summon.Id}: a habilidade do Despertar precisa de recarga ou ser passiva.";
				foreach (var problem in ValidateSkills(summon.AllSkills))
					yield return $"Invocação {summon.Id}: {problem}";
			}

			foreach (var enemy in Enemies)
			{
				if (StatModel.Budget(enemy.Rarity, false) == null)
					yield return $"Inimigo {enemy.Id}: {enemy.Rarity}★ não tem orçamento em stat_model.json.";
				else if (Mismatch(enemy.Stats, StatModel.Compute(enemy.Rarity, enemy.Role, false)) is { } mismatch)
					yield return $"Inimigo {enemy.Id}: \"stats\" {mismatch}";
				foreach (var problem in ValidateSkills(enemy.Skills))
					yield return $"Inimigo {enemy.Id}: {problem}";
			}

			for (var i = 0; i < Stages.Count; i++)
			{
				var stage = Stages[i];
				if (stage.Number != i + 1)
					yield return $"Fases: esperava a fase {i + 1}, veio a {stage.Number}.";
				if (stage.Mana <= 0)
					yield return $"Fase {stage.Number}: sem custo de Mana.";
				if (!ValidLevel(stage.Stars, stage.Level))
					yield return $"Fase {stage.Number}: inimigos {stage.Stars}★ nível {stage.Level}.";
				if (stage.RuneGrade is < 1 or > MaxCampaignRuneGrade)
					yield return $"Fase {stage.Number}: runa de {stage.RuneGrade} estrelas (a Campanha solta de 1 a {MaxCampaignRuneGrade}; as maiores vêm das Masmorras).";
				foreach (var problem in ValidateWaves(stage.Waves))
					yield return $"Fase {stage.Number}: {problem}";
			}

			foreach (var dungeon in Dungeons)
			{
				if (dungeon.Floors.Count == 0)
					yield return $"Masmorra {dungeon.Id}: sem andares.";
				if (dungeon.Kind == DungeonKind.Runes && dungeon.Sets.Count == 0)
					yield return $"Masmorra {dungeon.Id}: de runas, mas sem conjuntos.";
				for (var i = 0; i < dungeon.Floors.Count; i++)
				{
					var floor = dungeon.Floors[i];
					foreach (var problem in ValidateWaves(floor.Waves))
						yield return $"Masmorra {dungeon.Id}, andar {i + 1}: {problem}";
					if (floor.Mana <= 0)
						yield return $"Masmorra {dungeon.Id}, andar {i + 1}: sem custo de Mana.";
					if (!ValidLevel(floor.Stars, floor.Level))
						yield return $"Masmorra {dungeon.Id}, andar {i + 1}: inimigos {floor.Stars}★ nível {floor.Level}.";
					if (dungeon.Kind == DungeonKind.Runes && (floor.MinGrade < 1 || floor.MaxGrade > 6 || floor.MinGrade > floor.MaxGrade))
						yield return $"Masmorra {dungeon.Id}, andar {i + 1}: estrelas de {floor.MinGrade} a {floor.MaxGrade}.";
					if (dungeon.Kind == DungeonKind.Tools && (floor.ToolGrade is < 1 or > 4 || floor.ToolCount < 1))
						yield return $"Masmorra {dungeon.Id}, andar {i + 1}: pedra de grau {floor.ToolGrade} ({floor.ToolCount}).";
				}
			}

			if (Dungeons.Select(d => d.Id).Distinct().Count() != Dungeons.Count || Dungeons.Any(d => d.Id == "campaign"))
				yield return "Masmorras: ids repetidos ou reservados.";

			foreach (var offer in Shop.Where(o => o.Amount <= 0 || o.Price <= 0 || o.Name.Length == 0))
				yield return $"Loja, oferta {offer.Id}: sem nome, quantidade ou preço.";
			if (Shop.Select(o => o.Id).Distinct().Count() != Shop.Count)
				yield return "Loja: ids repetidos.";
		}

		private IEnumerable<string> ValidateWaves(IReadOnlyList<IReadOnlyList<StageEnemy>> waves)
		{
			if (waves.Count is < 1 or > MaxWaves)
				yield return $"{waves.Count} ondas (de 1 a {MaxWaves}).";
			foreach (var wave in waves)
			{
				if (wave.Count is < 1 or > MaxEnemiesPerWave)
					yield return $"onda com {wave.Count} inimigos (de 1 a {MaxEnemiesPerWave}).";
				foreach (var slot in wave)
				{
					if ((slot.Summon == null) == (slot.Enemy == null))
						yield return "cada inimigo tem \"summon\" ou \"enemy\", um dos dois.";
					else if (slot.Summon != null && !_summonsById.ContainsKey(slot.Summon))
						yield return $"invocação '{slot.Summon}' não existe.";
					else if (slot.Enemy != null && !_enemiesById.ContainsKey(slot.Enemy))
						yield return $"inimigo '{slot.Enemy}' não existe.";
				}
			}
		}

		/// <summary>
		/// Os atributos guardados têm de ser os que o modelo dá à variante (o jogo não recalcula: confere)
		/// e fechar o orçamento das estrelas dela.
		/// </summary>
		private IEnumerable<string> ValidateStats(SummonDefinition summon)
		{
			if (StatModel.Budget(summon.Rarity, false) == null || !StatModel.Roles.TryGetValue(summon.Rarity, out var roles) || !roles.ContainsKey(summon.Role))
				yield break;

			foreach (var awakened in new[] { false, true })
			{
				var field = awakened ? "awakened_stats" : "stats";
				var stats = summon.StatsFor(awakened);
				if (Mismatch(stats, StatModel.Compute(summon, awakened)) is { } mismatch)
					yield return $"\"{field}\" {mismatch}";

				var budget = StatModel.Budget(summon.Rarity, awakened) ?? 0;
				var bvp = StatModel.Bvp(stats);
				if (stats.Health < 0 || stats.Attack < 0 || stats.Defense < 0 || stats.Speed <= 0)
					yield return $"\"{field}\" com atributo negativo ou sem Velocidade.";
				else if (Math.Abs(bvp - budget) > StatModel.Tolerance + 1e-9)
					yield return $"\"{field}\" vale {bvp:0.#} BVP, fora do orçamento de {budget:0.#} (tolerância {StatModel.Tolerance:0.#}).";
			}
		}

		/// <summary>Nulo quando os quatro atributos do modelo batem; senão, o que era esperado.</summary>
		private static string? Mismatch(StatBlock stored, StatBlock expected)
		{
			static string Text(StatBlock s) => $"{s.Health:0}/{s.Attack:0}/{s.Defense:0}/{s.Speed:0}";

			return stored.Health == expected.Health && stored.Attack == expected.Attack && stored.Defense == expected.Defense && stored.Speed == expected.Speed
				? null
				: $"está {Text(stored)} (Vida/Ataque/Defesa/Velocidade) e o modelo dá {Text(expected)}: abra docs/summon_family_builder.html, carregue a pasta do projeto e use \"Recalcular todas\".";
		}

		/// <summary>Estrelas de 1 a 6, nível até o máximo delas (15 no 1★, +5 por estrela).</summary>
		private static bool ValidLevel(int stars, int level) => stars is >= 1 and <= 6 && level >= 1 && level <= 10 + 5 * stars;

		/// <summary>A primeira é ativa e sem recarga; as outras ativas têm recarga; no máximo uma passiva.</summary>
		private static IEnumerable<string> ValidateSkills(IReadOnlyList<SkillDefinition> skills)
		{
			if (skills.Count == 0 || skills[0].IsPassive || skills[0].Cooldown != 0)
				yield return "a primeira habilidade é ativa e sem recarga.";
			if (skills.Skip(1).Any(s => !s.IsPassive && s.Cooldown <= 0))
				yield return "habilidade ativa além da primeira sem recarga.";
			if (skills.Count(s => s.IsPassive) > 1)
				yield return "mais de uma passiva.";
			foreach (var problem in skills.SelectMany(ValidateSkill))
				yield return problem;
		}

		private static IEnumerable<string> ValidateSkill(SkillDefinition skill)
		{
			if (skill.Effects.Count == 0 && !skill.IsPassive)
				yield return $"'{skill.Name}' não tem efeitos.";
			if (skill.Levels.Any(l => l.Value <= 0))
				yield return $"'{skill.Name}': nível que não melhora nada.";
			foreach (var effect in skill.Effects.Concat(skill.AwakenedEffects))
			{
				if (effect.Hits < 1)
					yield return $"'{skill.Name}': efeito com {effect.Hits} golpes.";
				if (effect.Chance is < 0 or > 1)
					yield return $"'{skill.Name}': chance {effect.Chance} fora de 0 a 1.";
				if (effect.Kind == EffectKind.Status && effect.Turns < 1)
					yield return $"'{skill.Name}': efeito de status sem duração.";
			}

		}
	}
}
