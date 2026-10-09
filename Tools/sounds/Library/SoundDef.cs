using System;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Library
{
	/// <summary>
	/// Um efeito da biblioteca: a pasta (<see cref="Category"/>), o nome do arquivo, a descrição, a classe de
	/// mixagem, quantas variações e a receita. <see cref="LevelDb"/> sobe ou desce o efeito em relação aos
	/// outros da mesma classe (o foco de botão bem abaixo do clique, por exemplo).
	/// </summary>
	public sealed class SoundDef
	{
		public SoundDef(string category, string name, string description, Mix mix, Action<Patch> build, int variations = 1, double levelDb = 0)
		{
			Category = category;
			Name = name;
			Description = description;
			Mix = mix;
			Build = build;
			Variations = Math.Max(1, variations);
			LevelDb = levelDb;
		}

		public string Category { get; }
		public string Name { get; }
		public string Description { get; }
		public Mix Mix { get; }
		public Action<Patch> Build { get; }
		public int Variations { get; }
		public double LevelDb { get; }

		/// <summary>O caminho dentro da biblioteca, sem extensão: <c>combat/elements/fire_impact</c>. É o que <c>--only</c> aceita.</summary>
		public string Id => $"{Category}/{Name}";

		/// <summary>O nome lógico que o jogo usa: <c>combat.elements.fire_impact</c>.</summary>
		public string LogicalName => Id.Replace('/', '.');

		/// <summary><c>nome.wav</c> com uma variação, <c>nome_01.wav</c>, <c>nome_02.wav</c>... com várias.</summary>
		public string FileName(int variation) => Variations == 1 ? $"{Name}.wav" : $"{Name}_{variation + 1:00}.wav";
	}
}
