using System;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Library
{
	/// <summary>
	/// Atalho das receitas: cada grupo cria um <see cref="Recipe"/> com a pasta e a classe de mixagem dele e
	/// declara os efeitos em uma linha cada (<c>Of</c>), mudando a classe só onde precisa (<c>With</c>).
	/// </summary>
	public sealed class Recipe
	{
		private readonly string _category;
		private readonly Mix _mix;

		public Recipe(string category, Mix mix)
		{
			_category = category;
			_mix = mix;
		}

		public SoundDef Of(string name, string description, Action<Patch> build, int variations = 1, double levelDb = 0) => new(_category, name, description, _mix, build, variations, levelDb);

		public SoundDef With(Mix mix, string name, string description, Action<Patch> build, int variations = 1, double levelDb = 0) => new(_category, name, description, mix, build, variations, levelDb);

		/// <summary>Uma das opções por variação, na ordem (a 1ª variação pega a 1ª): para mudar a nota de propósito.</summary>
		public static T Pick<T>(Patch p, params T[] options) => options[p.Variation % options.Length];
	}
}
