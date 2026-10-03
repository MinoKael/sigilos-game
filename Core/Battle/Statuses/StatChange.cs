using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Um atributo maior ou menor enquanto o efeito durar: Ataque+, Ataque−, Defesa+, Quebra de Defesa,
	/// Velocidade+, Velocidade−, Crítico+. <c>change</c> é a fração somada (0,5 = +50%; −0,7 = −70%); com
	/// <c>additive</c>, são pontos somados ao atributo (Crítico+: 0,3 = +30% de chance). Quando é
	/// negativa, o efeito é negativo.
	/// </summary>
	internal sealed class StatChange : StatusBehavior
	{
		private readonly Stat _stat;
		private readonly double _change;
		private readonly bool _additive;

		public StatChange(Stat stat, double change, bool additive = false)
		{
			_stat = stat;
			_change = change;
			_additive = additive;
		}

		public override bool Harmful => _change < 0;

		public override double Modify(UnitRule rule, Stat stat, double value) =>
			stat != _stat ? value : _additive ? value + _change : value * (1 + _change);
	}
}
