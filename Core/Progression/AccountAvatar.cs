using System;
using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Progression
{
	/// <summary>De onde vem um retrato da conta: um monstro (da coleção, do Baú ou do correio) ou um especial (<see cref="SpecialAvatars"/>).</summary>
	public enum AvatarKind
	{
		Summon,
		Special,
	}

	/// <summary>Um retrato que a conta pode usar: o id (a variante do monstro ou o especial), se é a forma desperta e de que tipo é.</summary>
	public readonly record struct AccountAvatar(string Id, bool Awakened, AvatarKind Kind);

	/// <summary>
	/// Os retratos que não são monstros: o padrão do jogo, que toda conta tem, e os que só chegam de
	/// recompensa (o correio manda <c>avatar:&lt;id&gt;</c>, como manda os de monstro). Os de recompensa são
	/// os desenhos de Assets/Avatars, um por arquivo; o nome vem dos textos (<c>avatar.special.&lt;id&gt;</c>).
	/// </summary>
	public static class SpecialAvatars
	{
		/// <summary>O retrato padrão: o da conta que ainda não escolheu (no save, <see cref="Player.PlayerState.Avatar"/> nulo).</summary>
		public const string Default = "default";

		/// <summary>O desenho do padrão, em Assets/Icons.</summary>
		public const string DefaultIcon = "avatar";

		private static Dictionary<string, string> _files = new();

		/// <summary>Os especiais: o padrão primeiro, depois os de recompensa pelo id.</summary>
		public static IReadOnlyList<string> All { get; private set; } = [Default];

		/// <summary>
		/// Os de recompensa, pelos nomes dos desenhos de Assets/Avatars (sem a extensão). Quem lê a pasta é o
		/// GameEntry (<c>ContentLoader.LoadAvatars</c>, antes dos textos) e, nos testes, o Program.
		/// </summary>
		public static void Register(IEnumerable<string> files)
		{
			_files = files
				.GroupBy(IdOf)
				.Where(group => group.Key != Default)
				.ToDictionary(group => group.Key, group => group.OrderBy(file => file, StringComparer.Ordinal).First());
			All = [Default, .. _files.Keys.OrderBy(id => id, StringComparer.Ordinal)];
		}

		/// <summary>
		/// O id de um desenho: o nome em minúsculas, com "_" no lugar de "-" (astronaut-helmet.svg é
		/// <c>avatar:astronaut_helmet</c>), porque a chave do correio só aceita letras, números e "_".
		/// </summary>
		public static string IdOf(string file) => file.ToLowerInvariant().Replace('-', '_');

		public static bool Has(string id) => id == Default || _files.ContainsKey(id);

		/// <summary>O desenho de um especial em Assets/Avatars (sem a extensão); nulo no padrão e no que não existe.</summary>
		public static string? FileOf(string id) => _files.GetValueOrDefault(id);
	}
}
