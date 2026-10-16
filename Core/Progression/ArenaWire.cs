using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>Um integrante de uma defesa como ela viaja para o servidor e volta: só ids e números.</summary>
	public sealed class ArenaWireMember
	{
		public string Summon { get; set; } = "";
		public int Stars { get; set; }
		public int Level { get; set; }
		public bool Awakened { get; set; }
		public List<int> Skills { get; set; } = new();
		public List<Rune> Runes { get; set; } = new();
	}

	/// <summary>
	/// A defesa de outro jogador que a lista da Arena mostra no lugar de um adversário local: guardada no save,
	/// então a lista não muda ao reabrir o jogo sem conexão. <see cref="Slot"/> é a vaga da lista que ela ocupa.
	/// </summary>
	public sealed class ArenaOnlineOpponent
	{
		public int Slot { get; set; }
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public int Points { get; set; }
		public List<ArenaWireMember> Team { get; set; } = new();
	}

	/// <summary>
	/// A ponte entre a equipe da luta (<see cref="BattleTeam"/>) e o formato do servidor. Quem recebe confere tudo:
	/// invocação que o jogo não conhece, estrelas, nível ou runas fora do que existe descartam o integrante, e uma
	/// equipe sem nenhum integrante válido é descartada inteira.
	/// </summary>
	public static class ArenaWire
	{
		public const int MaxMembers = ArenaTeams.Size;

		private static readonly JsonSerializerOptions Options = new(GameDatabase.JsonOptions) { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

		public static List<ArenaWireMember> ToWire(BattleTeam team) => team.Members.Take(MaxMembers).Select(m => new ArenaWireMember
		{
			Summon = m.Summon.Id,
			Stars = m.Stars,
			Level = m.Level,
			Awakened = m.Awakened,
			Skills = m.SkillLevels.ToList(),
			Runes = m.Runes.Select(r =>
			{
				var copy = r.Clone();
				copy.EquippedOn = null;
				copy.Locked = false;
				return copy;
			}).ToList(),
		}).ToList();

		/// <summary>O JSON que o servidor guarda (camelCase, enums por nome).</summary>
		public static JsonElement ToJson(BattleTeam team) => JsonSerializer.SerializeToElement(ToWire(team), Options);

		public static List<ArenaWireMember>? ReadTeam(JsonElement json)
		{
			try
			{
				return json.ValueKind == JsonValueKind.Array ? json.Deserialize<List<ArenaWireMember>>(Options) : null;
			}
			catch (JsonException)
			{
				return null;
			}
		}

		/// <summary>A equipe de luta; nula se nenhum integrante é válido.</summary>
		public static BattleTeam? ToBattle(GameDatabase database, IReadOnlyList<ArenaWireMember> members)
		{
			var made = new List<TeamMember>();
			var nextRune = 1;
			foreach (var member in members.Take(MaxMembers))
			{
				if (!database.HasSummon(member.Summon))
					continue;

				var summon = database.Summon(member.Summon);
				var stars = Math.Clamp(member.Stars, 1, Growth.MaxStars);
				var awakened = member.Awakened && Awakening.CanBeAwakened(summon);
				var all = summon.AllSkills;
				var levels = all.Select((skill, i) => Math.Clamp(i < member.Skills.Count ? member.Skills[i] : 1, 1, Math.Max(1, skill.MaxLevel))).ToList();
				var runes = new List<Rune>();
				foreach (var rune in member.Runes.Take(6))
				{
					if (rune.Grade is < 1 or > 6 || rune.Level is < 0 or > 15 || rune.Slot is < 1 or > 6 || rune.Substats.Count > 4)
						continue;
					rune.Id = nextRune++;
					rune.EquippedOn = null;
					runes.Add(rune);
				}

				made.Add(new TeamMember(summon, stars, Math.Clamp(member.Level, 1, Growth.MaxLevel(stars)), awakened, levels, runes));
			}

			return made.Count == 0 ? null : new BattleTeam(made);
		}
	}
}
