using System;

namespace Sigilos.Core.Rta
{
	/// <summary>
	/// O prazo de quem decide: estourou, o automático decide e o prazo seguinte cai pela metade, até o mínimo.
	/// Agir a tempo devolve o prazo inteiro. Na luta, estourar já no mínimo recomeça do prazo inteiro
	/// (<c>resetsAtMinimum</c>); na escolha do time, o mínimo fica.
	/// </summary>
	public sealed class RtaClock
	{
		private readonly int _full;
		private readonly int _minimum;
		private readonly bool _resetsAtMinimum;

		public RtaClock(int full, int minimum, bool resetsAtMinimum)
		{
			_full = full;
			_minimum = Math.Min(minimum, full);
			_resetsAtMinimum = resetsAtMinimum;
			Seconds = full;
		}

		/// <summary>O prazo da próxima decisão, em segundos.</summary>
		public int Seconds { get; private set; }

		/// <summary>A decisão foi tomada a tempo.</summary>
		public void Acted() => Seconds = _full;

		/// <summary>O prazo estourou e o automático decidiu.</summary>
		public void TimedOut()
		{
			if (_resetsAtMinimum && Seconds <= _minimum)
				Seconds = _full;
			else
				Seconds = Math.Max(_minimum, Seconds / 2);
		}
	}
}
