using System;
using System.Collections.Generic;
using Sigilos.Core.Battle;

namespace Sigilos.UI
{
	/// <summary>
	/// A luta de agora da Batalha automática, para ver. A Batalha automática resolve a luta na hora e
	/// espera o tempo dela; esta é a mesma luta (a mesma equipe, a mesma semente, as mesmas decisões do
	/// <see cref="AutoBattle"/>, então os mesmos eventos) andada no relógio dela, um turno de cada vez,
	/// em momentos de tela (<see cref="BattlePace"/>) na velocidade da Batalha automática.
	///
	/// Ela é da corrida (<see cref="AutoBattleRun.Fight"/>), não de quem olha: a vista pequena da janela e
	/// a tela cheia mostram a mesma, e abrir, fechar ou trocar de vista não para nem recomeça nada. Quem
	/// começa a olhar no meio monta o campo pelo estado da luta e segue dali (<see cref="Steps"/>).
	/// </summary>
	public sealed class AutoBattleFight
	{
		private readonly Queue<(double At, Beat Beat)> _beats = new();
		private readonly List<BattleEvent> _events = new();
		private readonly bool _focusBoss;
		private bool _started;

		/// <summary>Onde termina, no relógio da luta, o último momento já tirado da luta.</summary>
		private double _end;

		/// <param name="session">Uma luta nova, igual à que a Batalha automática resolveu.</param>
		public AutoBattleFight(BattleSession session, bool focusBoss)
		{
			Session = session;
			_focusBoss = focusBoss;
		}

		public BattleSession Session { get; }

		/// <summary>
		/// Quantos pedaços (o começo e cada turno) já saíram da luta. O estado dela já inclui o último, mesmo
		/// com momentos dele ainda por mostrar: quem começa a olhar agora ignora esses e segue do próximo.
		/// </summary>
		public int Steps { get; private set; }

		/// <summary>Um momento chegou à tela, com o pedaço de onde veio.</summary>
		public event Action<Beat, int>? Played;

		/// <summary>Quanto um momento fica na tela, na velocidade da Batalha automática.</summary>
		public static double Seconds(Beat beat) => beat.Seconds / BattlePace.AutoBattleFactor;

		/// <summary>Leva a luta até <paramref name="elapsed"/> segundos dela: mostra os momentos que chegaram, andando a luta quando precisa.</summary>
		public void Advance(double elapsed)
		{
			while (true)
			{
				if (_beats.Count == 0)
				{
					if (!Step())
						return;
					continue;
				}

				var (at, beat) = _beats.Peek();
				if (at > elapsed)
					return;
				_beats.Dequeue();
				Played?.Invoke(beat, Steps);
			}
		}

		/// <summary>O próximo pedaço da luta (o começo, ou um turno) em momentos; falso quando ela acabou.</summary>
		private bool Step()
		{
			if (_started && Session.IsOver)
				return false;

			_events.Clear();
			if (_started)
				AutoBattle.Turn(Session, _events, _focusBoss);
			else
				_events.AddRange(Session.Start());
			_started = true;
			Steps++;
			foreach (var beat in BattlePace.Beats(_events))
			{
				_beats.Enqueue((_end, beat));
				_end += Seconds(beat);
			}

			return true;
		}
	}
}
