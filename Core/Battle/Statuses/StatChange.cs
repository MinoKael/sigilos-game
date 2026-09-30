using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Um atributo maior ou menor enquanto o efeito durar: Ataque+, Ataque−, Defesa+, Quebra de Defesa,
	/// Velocidade+. <c>change</c> é a fração somada (0,5 = +50%; −0,7 = −70%); quando é negativa, o
	/// efeito é negativo.
	/// </summary>
	internal sealed class StatChange : StatusBehavior
	{
		private readonly Stat _stat;
		private readonly double _change;

		public StatChange(Stat stat, double change)
		{
			_stat = stat;
			_change = change;
		}

		public override bool Harmful => _change < 0;

		public override double Modify(UnitRule rule, Stat stat, double value) => stat == _stat ? value * (1 + _change) : value;
	}
}
