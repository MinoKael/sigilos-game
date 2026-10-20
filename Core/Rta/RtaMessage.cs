using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sigilos.Core.Progression;

namespace Sigilos.Core.Rta
{
	/// <summary>
	/// Uma mensagem entre o jogo e o servidor da Arena do Véu, em JSON. Um tipo só, com o campo <see cref="Type"/> dizendo
	/// o que ela é e só os campos daquele tipo preenchidos:
	///
	/// Do jogo: <c>queue</c> (<see cref="Pool"/>), <c>ghost</c> (<see cref="Pool"/>; treino contra um fantasma, sem pontos),
	/// <c>cancel</c>, <c>pick</c> / <c>ban</c> / <c>lead</c> (<see cref="Summon"/>), <c>act</c> (<see cref="Turn"/>,
	/// <see cref="Skill"/>, <see cref="Side"/>, <see cref="Index"/>) e <c>leave</c> (desiste).
	///
	/// Do servidor: <c>profile</c> (ao conectar: Áster, vitórias e derrotas), <c>queued</c>, <c>cancelled</c>, <c>matched</c>, <c>draft</c> (o estado da montagem), <c>battle</c> (as equipes e a semente),
	/// <c>turn</c> (de quem é a vez), <c>acted</c> (a decisão tomada e o <see cref="Hash"/> da luta), <c>end</c> e <c>error</c>.
	/// </summary>
	public sealed class RtaMessage
	{
		public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() }, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

		public string Type { get; set; } = "";

		public string? Error { get; set; }

		/// <summary>O baú da fila (<see cref="Veil.Pool"/>).</summary>
		public List<ArenaWireMember>? Pool { get; set; }

		// matched
		public RtaSeat? Seat { get; set; }
		public string? Opponent { get; set; }
		public int Aster { get; set; }
		public int OpponentAster { get; set; }
		public int Wins { get; set; }
		public int Losses { get; set; }
		public bool Ranked { get; set; }
		public int Seed { get; set; }

		// draft
		public RtaPhase? Phase { get; set; }
		public RtaSeat? Acting { get; set; }
		public int PicksLeft { get; set; }
		public List<string>? PicksFirst { get; set; }
		public List<string>? PicksSecond { get; set; }
		public string? BannedFirst { get; set; }
		public string? BannedSecond { get; set; }

		/// <summary>Este jogador tem algo a decidir agora.</summary>
		public bool Waiting { get; set; }

		/// <summary>Já indicou a Líder.</summary>
		public bool Led { get; set; }

		/// <summary>O prazo de quem decide, em segundos.</summary>
		public int Seconds { get; set; }

		public string? Summon { get; set; }

		// battle
		public List<ArenaWireMember>? TeamFirst { get; set; }
		public List<ArenaWireMember>? TeamSecond { get; set; }

		// turn / act / acted
		public int Turn { get; set; }
		public int Skill { get; set; }
		public int Side { get; set; } = -1;
		public int Index { get; set; } = -1;

		/// <summary>No <c>turn</c>, quem age; no <c>acted</c>, quem agiu.</summary>
		public RtaSeat? Actor { get; set; }

		/// <summary>Posição de quem age na lista do lado dele (no <c>turn</c>).</summary>
		public int ActorIndex { get; set; } = -1;

		public string? Hash { get; set; }
		public bool Auto { get; set; }

		// end
		public string? Winner { get; set; }
		public int Delta { get; set; }
		public int NewAster { get; set; }
		public int Ecos { get; set; }

		/// <summary>A partida acabou porque alguém desistiu.</summary>
		public bool Forfeit { get; set; }

		public string ToJson() => JsonSerializer.Serialize(this, Options);

		public static RtaMessage? Read(string json)
		{
			try
			{
				return JsonSerializer.Deserialize<RtaMessage>(json, Options);
			}
			catch (JsonException)
			{
				return null;
			}
		}

		[JsonIgnore]
		public RtaDecision Decision => new(Skill, Side, Index);
	}
}
