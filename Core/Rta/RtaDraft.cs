using System;
using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Rta
{
	public enum RtaPhase
	{
		/// <summary>Cada um monta cinco invocações, na ordem de <see cref="RtaRules.PickOrder"/>.</summary>
		Picking,

		/// <summary>Cada um bane uma das cinco do outro, o primeiro antes.</summary>
		Banning,

		/// <summary>Cada um indica a Líder entre as quatro que sobraram; os dois ao mesmo tempo.</summary>
		Leading,

		Done,
	}

	/// <summary>
	/// A montagem do time na Arena do Véu: escolhas alternadas, banimento e Líder. Sem rede e sem relógio: quem
	/// usa (o servidor) decide quem age, quando o prazo estoura e chama <see cref="AutoPick"/>, <see cref="AutoBan"/>
	/// ou <see cref="AutoLeader"/> no lugar do jogador. Invocação repetida, de um lado ou de outro, não entra: o
	/// que um pegou o outro não pega. Cada lado escolhe só do próprio baú (<c>pool</c>).
	/// </summary>
	public sealed class RtaDraft
	{
		private readonly IReadOnlyList<string>[] _pools = new IReadOnlyList<string>[2];
		private readonly List<string>[] _picks = { new(), new() };
		private readonly string?[] _banned = new string?[2];
		private readonly string?[] _leaders = new string?[2];
		private int _step;
		private int _inStep;
		private int _bans;

		/// <param name="first">As invocações de quem escolhe primeiro, das mais fortes às mais fracas.</param>
		/// <param name="second">As de quem escolhe depois.</param>
		public RtaDraft(IReadOnlyList<string> first, IReadOnlyList<string> second)
		{
			_pools[0] = first.Distinct().ToList();
			_pools[1] = second.Distinct().ToList();
		}

		public RtaPhase Phase { get; private set; } = RtaPhase.Picking;

		/// <summary>De quem é a vez de escolher ou banir; nulo na hora da Líder (os dois) e no fim.</summary>
		public RtaSeat? Acting => Phase switch
		{
			RtaPhase.Picking => RtaRules.PickOrder[_step].Seat,
			RtaPhase.Banning => _bans == 0 ? RtaSeat.First : RtaSeat.Second,
			_ => null,
		};

		/// <summary>Quantas este lado ainda escolhe antes de passar a vez.</summary>
		public int PicksLeft => Phase == RtaPhase.Picking ? RtaRules.PickOrder[_step].Count - _inStep : 0;

		public IReadOnlyList<string> Picks(RtaSeat seat) => _picks[(int)seat];

		/// <summary>A invocação de <paramref name="seat"/> que o rival baniu, se já baniu.</summary>
		public string? Banned(RtaSeat seat) => _banned[(int)seat];

		public string? Leader(RtaSeat seat) => _leaders[(int)seat];

		/// <summary>Este lado tem algo a decidir agora.</summary>
		public bool Waiting(RtaSeat seat) => Phase switch
		{
			RtaPhase.Picking or RtaPhase.Banning => Acting == seat,
			RtaPhase.Leading => _leaders[(int)seat] == null,
			_ => false,
		};

		public bool CanPick(RtaSeat seat, string summon) =>
			Phase == RtaPhase.Picking && Acting == seat
			&& _pools[(int)seat].Contains(summon)
			&& !_picks[0].Contains(summon) && !_picks[1].Contains(summon);

		public bool Pick(RtaSeat seat, string summon)
		{
			if (!CanPick(seat, summon))
				return false;

			_picks[(int)seat].Add(summon);
			if (++_inStep >= RtaRules.PickOrder[_step].Count)
			{
				_inStep = 0;
				if (++_step >= RtaRules.PickOrder.Count)
					Phase = RtaPhase.Banning;
			}

			return true;
		}

		public bool CanBan(RtaSeat seat, string summon) =>
			Phase == RtaPhase.Banning && Acting == seat && Other(seat) is var rival && _picks[(int)rival].Contains(summon);

		/// <summary>Bane uma das cinco do rival.</summary>
		public bool Ban(RtaSeat seat, string summon)
		{
			if (!CanBan(seat, summon))
				return false;

			_banned[(int)Other(seat)] = summon;
			if (++_bans >= 2)
				Phase = RtaPhase.Leading;

			return true;
		}

		public bool CanLead(RtaSeat seat, string summon) =>
			Phase == RtaPhase.Leading && _leaders[(int)seat] == null && Remaining(seat).Contains(summon);

		public bool Lead(RtaSeat seat, string summon)
		{
			if (!CanLead(seat, summon))
				return false;

			_leaders[(int)seat] = summon;
			if (_leaders[0] != null && _leaders[1] != null)
				Phase = RtaPhase.Done;

			return true;
		}

		/// <summary>A primeira do baú que ainda pode ser escolhida (o baú vem das mais fortes às mais fracas).</summary>
		public string AutoPick(RtaSeat seat) =>
			_pools[(int)seat].FirstOrDefault(s => !_picks[0].Contains(s) && !_picks[1].Contains(s))
			?? throw new InvalidOperationException("O baú acabou: a conta não tinha invocações suficientes.");

		/// <summary>A primeira que o rival escolheu.</summary>
		public string AutoBan(RtaSeat seat) => _picks[(int)Other(seat)][0];

		/// <summary>A primeira das quatro que sobraram.</summary>
		public string AutoLeader(RtaSeat seat) => Remaining(seat)[0];

		/// <summary>As quatro que lutam: a Líder primeiro (só depois da <see cref="RtaPhase.Done"/>).</summary>
		public IReadOnlyList<string> Team(RtaSeat seat)
		{
			var leader = _leaders[(int)seat];
			return Remaining(seat).OrderBy(s => s == leader ? 0 : 1).ToList();
		}

		/// <summary>As cinco menos a banida (todas, se o banimento ainda não veio).</summary>
		public IReadOnlyList<string> Remaining(RtaSeat seat) =>
			_picks[(int)seat].Where(s => s != _banned[(int)seat]).ToList();

		public static RtaSeat Other(RtaSeat seat) => seat == RtaSeat.First ? RtaSeat.Second : RtaSeat.First;
	}
}
