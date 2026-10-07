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
	/// recompensa (o correio manda <c>avatar:&lt;id&gt;</c>, como manda os de monstro). Cada um é um ícone de
	/// Assets/Icons; o nome vem dos textos (<c>avatar.special.&lt;id&gt;</c>).
	/// </summary>
	public static class SpecialAvatars
	{
		/// <summary>O retrato padrão: o da conta que ainda não escolheu (no save, <see cref="Player.PlayerState.Avatar"/> nulo).</summary>
		public const string Default = "default";

		/// <summary>Os especiais, o padrão primeiro, e o ícone de cada um.</summary>
		public static readonly IReadOnlyList<(string Id, string Icon)> All =
		[
			(Default, "avatar"),
			("sigil", "summon"),
			("grimoire", "grimoire"),
			("star", "star"),
		];

		public static bool Has(string id) => All.Any(a => a.Id == id);

		public static string IconOf(string id) => All.FirstOrDefault(a => a.Id == id).Icon ?? All[0].Icon;
	}
}
