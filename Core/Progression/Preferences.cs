using System;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// As escolhas do jogador que moram na conta e não valem nada em regra: a Batalha automática, a mira no chefe, o idioma, o
	/// tutorial, o que já foi visto. Também passam pelo servidor, para o aparelho não escrever no save; só estas chaves valem.
	/// </summary>
	public static class Preferences
	{
		public const string AutoBattle = "auto_battle";
		public const string FocusBoss = "focus_boss";
		public const string TutorialDone = "tutorial_done";
		public const string SeenMonsters = "seen_monsters";
		public const string ArenaPayoutSeen = "arena_payout_seen";
		public const string LanguagePrefix = "language:";
		public const string FeaturePrefix = "feature:";

		public static bool Set(PlayerState player, GameDatabase database, string key, bool value)
		{
			switch (key)
			{
				case AutoBattle:
					player.AutoBattle = value;
					return true;
				case FocusBoss:
					player.FocusBoss = value;
					return true;
				case TutorialDone:
					player.TutorialDone = value;
					return true;
				case SeenMonsters:
					return Roster.MarkSeen(player);
				case ArenaPayoutSeen:
					player.Arena.PayoutUnseen = false;
					return true;
			}

			if (key.StartsWith(LanguagePrefix, StringComparison.Ordinal))
			{
				var language = key.Substring(LanguagePrefix.Length);
				if (language.Length is 0 or > 10)
					return false;
				player.Language = language;
				return true;
			}

			if (key.StartsWith(FeaturePrefix, StringComparison.Ordinal) && Enum.TryParse<Feature>(key.Substring(FeaturePrefix.Length), out var feature) && Enum.IsDefined(feature))
			{
				Features.MarkSeen(player, feature);
				return true;
			}

			return false;
		}
	}
}
