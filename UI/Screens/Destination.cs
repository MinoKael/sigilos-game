using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>Para onde um botão de navegação leva: a barra do Santuário e a escolha de batalha.</summary>
	public enum Destination
	{
		Campaign,
		Dungeons,
		Summon,
		Monsters,
		Runes,
		Teams,
		Shop,
		Compendium,
		Grimoire,

		/// <summary>A escolha de batalha: Campanha, Masmorras e o que ainda vem.</summary>
		Map,
	}

	/// <summary>O símbolo e o nome escrito de cada destino.</summary>
	public static class Destinations
	{
		public static string Icon(Destination destination) => destination switch
		{
			Destination.Campaign => "campaign",
			Destination.Dungeons => "dungeon",
			Destination.Summon => "summon",
			Destination.Monsters => "monster",
			Destination.Runes => "rune",
			Destination.Teams => "team",
			Destination.Shop => "shop",
			Destination.Compendium => "compendium",
			Destination.Grimoire => "grimoire",
			_ => "fight",
		};

		public static string Name(Destination destination) => T($"destination.{destination}");
	}
}
