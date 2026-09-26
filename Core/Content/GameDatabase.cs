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
			IReadOnlyDictionary<Role, StatBlock> roles,
			IReadOnlyList<FamilyDefinition> families,
			IReadOnlyList<SummonDefinition> summons,
			IReadOnlyList<EnemyDefinition> enemies,
			IReadOnlyList<StageDefinition> stages,
			IReadOnlyList<DungeonDefinition> dungeons,
			IReadOnlyList<ShopOffer> shop)
		{
			Roles = roles;
			Families = families;
			Summons = summons;
			Enemies = enemies;
			Stages = stages.OrderBy(s => s.Number).ToList();
			Dungeons = dungeons;
			Shop = shop;

			var familiesById = families.ToDictionary(f => f.Id);
			foreach (var summon in summons)
			{
				if (familiesById.TryGetValue(summon.FamilyId, out var family))
					summon.Family = family;
			}

			_summonsById = summons.ToDictionary(s => s.Id);
			_enemiesById = enemies.ToDictionary(e => e.Id);
		}

		/// <summary>Atributos de base por papel, no nível 40, com 5 estrelas e sem Despertar.</summary>
		public IReadOnlyDictionary<Role, StatBlock> Roles { get; }

		public IReadOnlyList<FamilyDefinition> Families { get; }
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

		public static GameDatabase FromJson(
			string roles,
			string families,
			IEnumerable<string> summons,
			string enemies,
			string stages,
			string dungeons,
			string shop)
		{
			return new GameDatabase(
				Parse<Dictionary<Role, StatBlock>>(roles, "roles.json"),
				Parse<List<FamilyDefinition>>(families, "families.json"),
				summons.Select(text => Parse<SummonDefinition>(text, "summons/*.json")).ToList(),
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
			foreach (var role in Enum.GetValues<Role>())
			{
				if (!Roles.ContainsKey(role))
					yield return $"roles.json: falta o papel {role}.";
			}

			foreach (var family in Families)
			{
				if (family.Rarity is < 1 or > 5)
					yield return $"Família {family.Id}: raridade {family.Rarity} fora de 1 a 5.";
				if (family.Image.Length == 0 || family.AwakenedImage.Length == 0)
					yield return $"Família {family.Id}: falta a imagem normal ou a do Despertar.";
				if (family.Passive.AwakenedValue < family.Passive.Value)
					yield return $"Família {family.Id}: a Assinatura desperta é mais fraca que a normal.";
			}

			foreach (var summon in Summons)
			{
				if (Families.All(f => f.Id != summon.FamilyId))
					yield return $"Invocação {summon.Id}: família '{summon.FamilyId}' não existe.";
				if (summon.Awakening.Name.Length == 0)
					yield return $"Invocação {summon.Id}: sem nome de Despertar.";
				if (summon.Awakening.Stat is not (Stat.Speed or Stat.Crit or Stat.Resistance or Stat.Accuracy))
					yield return $"Invocação {summon.Id}: o Despertar dá Velocidade, Crítico, Resistência ou Precisão, não {summon.Awakening.Stat}.";
				if (summon.Special.Cooldown <= 0)
					yield return $"Invocação {summon.Id}: habilidade especial sem recarga.";
				foreach (var problem in ValidateSkill(summon.Basic).Concat(ValidateSkill(summon.Special)))
					yield return $"Invocação {summon.Id}: {problem}";
			}

			foreach (var enemy in Enemies)
			{
				foreach (var problem in ValidateSkill(enemy.Basic))
					yield return $"Inimigo {enemy.Id}: {problem}";
				if (enemy.Special != null)
				{
					foreach (var problem in ValidateSkill(enemy.Special))
						yield return $"Inimigo {enemy.Id}: {problem}";
				}
			}

			for (var i = 0; i < Stages.Count; i++)
			{
				var stage = Stages[i];
				if (stage.Number != i + 1)
					yield return $"Fases: esperava a fase {i + 1}, veio a {stage.Number}.";
				if (stage.Mana <= 0)
					yield return $"Fase {stage.Number}: sem custo de Mana.";
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
				foreach (var slot in wave.Where(slot => !_enemiesById.ContainsKey(slot.Enemy)))
					yield return $"inimigo '{slot.Enemy}' não existe.";
			}
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
