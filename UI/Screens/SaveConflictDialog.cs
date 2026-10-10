using System;
using System.Linq;
using Godot;
using Sigilos.Core.Player;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A pergunta "qual progresso usar?", quando o save do aparelho e o da nuvem mudaram desde a última vez
	/// que se encontraram (ou quando o jogo sem conta encontra uma conta que já tem save). Mostra os dois
	/// lado a lado, com o que dá para comparar de relance: nível da conta, monstros, fase e quando foi
	/// salvo. Não fecha sem escolha; o que não for escolhido fica guardado no aparelho (quem guarda é o
	/// GameRoot).
	/// </summary>
	public static class SaveConflictDialog
	{
		private const float Width = 680;

		/// <param name="adopting">O local é o jogo sem conta deste aparelho.</param>
		/// <param name="chosen">Verdadeiro: fica o da nuvem.</param>
		public static Dialog Open(Control from, PlayerState local, DateTimeOffset? localSavedAt, PlayerState cloud, DateTimeOffset cloudSavedAt, bool adopting, Action<bool> chosen)
		{
			var dialog = Dialog.Open(from, T("account.conflict_title"), Width, null, "SaveConflictDialog");
			dialog.Dismissable = false;
			dialog.Body.AddChild(RichText.Label(T(adopting ? "account.conflict_adopt_text" : "account.conflict_text"), Width - 40).Named("Text"));

			var row = Layout.Row(Space.Loose).Named("Saves");
			row.AddChild(Summary("Local", T(adopting ? "account.conflict_offline" : "account.conflict_local"), local, localSavedAt));
			row.AddChild(Summary("Cloud", T("account.conflict_cloud"), cloud, cloudSavedAt));
			dialog.Body.AddChild(row);
			dialog.Body.AddChild(Layout.Text(T("account.conflict_note"), GameTheme.Faded, Width - 40).Named("Note"));

			dialog.AddAction(T(adopting ? "account.use_offline" : "account.use_local"), () => chosen(false)).Named("UseLocal");
			dialog.AddAction(T("account.use_cloud"), () => chosen(true)).Named("UseCloud");
			return dialog;
		}

		/// <summary>Um dos lados: o nome e as linhas do resumo.</summary>
		private static Control Summary(string name, string title, PlayerState player, DateTimeOffset? savedAt)
		{
			var (panel, content) = Layout.Section(title);
			panel.Name = name;
			panel.ThemeTypeVariation = GameTheme.InsetPanel;
			panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			content.AddChild(Line("Level", T("account.conflict_level", player.AccountLevel)));
			content.AddChild(Line("Monsters", T("account.conflict_monsters", player.Monsters.Count)));
			content.AddChild(Line("Stage", player.HighestStage > 0 ? T("account.conflict_stage", player.HighestStage) : T("account.conflict_no_stage")));
			content.AddChild(Line("Dungeons", T("account.conflict_floors", player.DungeonFloors.Values.Sum())));
			content.AddChild(Line("Wealth", T("account.conflict_wealth", Texts.Number(player.Essence), Texts.Number(player.Gold))));
			if (savedAt is { } at)
				content.AddChild(Line("Saved", T("account.conflict_saved", at.ToLocalTime().ToString("g", Culture)), GameTheme.Faded));
			return panel;
		}

		private static Label Line(string name, string text, string? variation = null) => Layout.Text(text, variation).Named(name);
	}
}
