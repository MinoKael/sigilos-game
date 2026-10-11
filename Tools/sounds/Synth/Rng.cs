using System;
using System.Collections.Generic;
using System.Text;

namespace Sigilos.Sounds.Synth
{
	/// <summary>
	/// Sorteio determinístico (SplitMix64): a mesma semente dá sempre o mesmo som, em qualquer máquina e em
	/// qualquer ordem de geração. Não usa <see cref="Random"/>, que pode mudar entre versões do .NET.
	/// </summary>
	public sealed class Rng
	{
		private ulong _state;

		public Rng(ulong seed)
		{
			_state = seed;
		}

		/// <summary>A semente de uma variação de um efeito: a global misturada ao nome dele. Gerar um efeito sozinho dá os mesmos bytes que gerar a biblioteca inteira.</summary>
		public static ulong SeedFor(ulong seed, string id, int variation)
		{
			var mixer = new Rng(seed ^ Hash(id) ^ ((ulong)(variation + 1) * 0xD1B54A32D192ED03UL));
			return mixer.NextULong();
		}

		/// <summary>FNV-1a de 64 bits sobre o UTF-8.</summary>
		public static ulong Hash(string text)
		{
			var hash = 0xCBF29CE484222325UL;
			foreach (var b in Encoding.UTF8.GetBytes(text))
			{
				hash ^= b;
				hash *= 0x100000001B3UL;
			}
			return hash;
		}

		public ulong NextULong()
		{
			var z = _state += 0x9E3779B97F4A7C15UL;
			z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
			z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
			return z ^ (z >> 31);
		}

		/// <summary>De 0 (incluso) a 1 (fora).</summary>
		public double Next() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

		/// <summary>De −1 a 1.</summary>
		public double Signed() => Next() * 2 - 1;

		public double Range(double min, double max) => min + (max - min) * Next();

		public int Int(int count) => (int)(Next() * count);

		/// <summary><paramref name="value"/> mexido em até ± <paramref name="spread"/> (0,1 = 10%).</summary>
		public double Vary(double value, double spread) => value * (1 + spread * Signed());

		public T Pick<T>(IReadOnlyList<T> items) => items[Int(items.Count)];
	}
}
