using Sigilos.Core.Battle.Statuses;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Um efeito ativo numa unidade. O que ele faz é a estratégia do <see cref="Kind"/>
	/// (Statuses/StatusBehaviors); aqui ficam os números desta aplicação. A duração conta turnos do
	/// dono: diminui no fim de cada turno dele.
	///
	/// <see cref="Fresh"/> marca o efeito que a unidade recebeu durante o próprio turno — "fica Oculta
	/// por 1 turno" tem que durar até o fim do próximo turno dela, não acabar no mesmo instante.
	/// </summary>
	public sealed class StatusEffect : UnitRule
	{
		public StatusEffect(StatusKind kind, int turns, double value = 0, BattleUnit? source = null)
			: base(StatusBehaviors.Of(kind), value, source)
		{
			Kind = kind;
			Turns = turns;
		}

		public StatusKind Kind { get; }
		public int Turns { get; set; }

		public bool Fresh { get; set; }
	}
}
