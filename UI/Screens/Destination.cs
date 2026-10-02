using Sigilos.Core.Progression;
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

		/// <summary>O símbolo e o nome de uma parte do jogo que a Campanha abre (o aviso da vitória).</summary>
		public static (string Icon, string Name) Of(Feature feature) => feature switch
		{
			Feature.Monsters => (Icon(Destination.Monsters), Name(Destination.Monsters)),
			Feature.Teams => (Icon(Destination.Teams), Name(Destination.Teams)),
			Feature.Runes => (Icon(Destination.Runes), Name(Destination.Runes)),
			Feature.Compendium => (Icon(Destination.Compendium), Name(Destination.Compendium)),
			Feature.Shop => (Icon(Destination.Shop), Name(Destination.Shop)),
			Feature.Grimoire => (Icon(Destination.Grimoire), Name(Destination.Grimoire)),
			Feature.Dungeons => (Icon(Destination.Dungeons), Name(Destination.Dungeons)),
			Feature.Channel => ("collect", T("hub.channel")),
			_ => ("repeat", T("common.auto_battle")),
		};
	}
}
