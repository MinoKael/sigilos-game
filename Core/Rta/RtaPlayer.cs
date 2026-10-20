using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;

namespace Sigilos.Core.Rta
{
	/// <summary>
	/// Quem está numa sala da Arena do Véu: o nome, o Áster, o baú de onde escolhe (já conferido) e por onde as
	/// mensagens saem. <see cref="Gone"/>: a conexão caiu ou é um fantasma de treino, e as decisões dele vão para o
	/// automático na hora.
	/// </summary>
	public sealed class RtaPlayer
	{
		public RtaPlayer(string name, int aster, IReadOnlyList<TeamMember> pool, Action<RtaMessage> send, bool bot = false)
		{
			Name = name;
			Aster = aster;
			Pool = pool;
			Send = send;
			Bot = bot;
		}

		public string Name { get; }

		public int Aster { get; }

		/// <summary>As invocações que pode escolher, das mais fortes às mais fracas, uma por id.</summary>
		public IReadOnlyList<TeamMember> Pool { get; }

		public Action<RtaMessage> Send { get; }

		/// <summary>Fantasma de treino: joga sozinho e não ganha nem perde Áster.</summary>
		public bool Bot { get; }

		public volatile bool Gone;

		public bool Automatic => Bot || Gone;

		public TeamMember Member(string summon) => Pool.First(m => m.Summon.Id == summon);

		/// <summary>
		/// O baú que o jogador mandou, conferido: invocações que não existem saem, repetidas ficam na primeira, e a
		/// ordem é a que ele mandou. Nulo se sobraram menos que <see cref="RtaRules.MinPool"/> (ou veio mais que o teto).
		/// </summary>
		public static IReadOnlyList<TeamMember>? Check(GameDatabase database, IReadOnlyList<ArenaWireMember>? wire)
		{
			if (wire == null || wire.Count > RtaRules.MaxPool)
				return null;

			var rune = 1;
			var pool = new List<TeamMember>();
			foreach (var member in wire)
			{
				if (ArenaWire.ToMember(database, member, ref rune) is { } built && pool.All(m => m.Summon.Id != built.Summon.Id))
					pool.Add(built);
			}

			return pool.Count >= RtaRules.MinPool ? pool : null;
		}
	}
}
