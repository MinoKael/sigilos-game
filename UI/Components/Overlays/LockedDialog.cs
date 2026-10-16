using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Screens;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O aviso de uma parte do jogo que ainda não abriu: o nome dela e quando abre (a fase da Campanha, a
	/// primeira invocação ou "em breve"). Os botões das partes fechadas ficam na tela, apagados, e o toque
	/// abre este aviso (<see cref="TileButton.Locked"/>).
	/// </summary>
	public static class LockedDialog
	{
		public static void Open(Control anchor, GameDatabase database, Feature feature)
		{
			var (_, name) = Destinations.Of(feature);
			Dialog.Info(anchor, name, Reason(database, feature)).Named("Locked");
		}

		/// <summary>Quando a parte abre, por extenso.</summary>
		public static string Reason(GameDatabase database, Feature feature) => feature switch
		{
			Feature.Monsters => T("locked.monsters"),
			Feature.Observatory => T("locked.observatory"),
			_ => T("locked.stage", Features.StageOf(database, feature)),
		};
	}
}
