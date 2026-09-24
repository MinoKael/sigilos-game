using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Um efeito ativo numa unidade. A duração conta turnos do dono: diminui no fim de cada turno dele.
	///
	/// <see cref="Fresh"/> marca o efeito que a unidade recebeu durante o próprio turno — "fica Oculta
	/// por 1 turno" tem que durar até o fim do próximo turno dela, não acabar no mesmo instante.
	/// </summary>
	public sealed class StatusEffect
	{
		public StatusEffect(StatusKind kind, int turns, double value = 0, BattleUnit? source = null)
		{
			Kind = kind;
			Turns = turns;
			Value = value;
			Source = source;
		}

		public StatusKind Kind { get; }
		public int Turns { get; set; }

		/// <summary>Só o escudo usa: quanto dano ainda absorve.</summary>
		public double Value { get; set; }

		/// <summary>Só a provocação usa: em quem o provocado tem que mirar.</summary>
		public BattleUnit? Source { get; set; }

		public bool Fresh { get; set; }
	}
}
