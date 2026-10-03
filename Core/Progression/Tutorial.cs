using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// A luta de treino da primeira abertura (GDD, seção 7): três monstros de Água emprestados — um que
	/// bate, um que atordoa e um que cura — contra inimigos fracos de Fogo e um de Vento, para ensinar
	/// jogando o básico, a recarga, os elementos, os efeitos, o suporte e o Automático. Os monstros não
	/// entram na conta, e a luta não cobra nem dá nada.
	///
	/// Depois da luta vem a primeira invocação, a ×10 (<see cref="GuidesFirstSummon"/>): é nela que chegam
	/// o Cavaleiro de Fogo e a 5★ garantidos (<see cref="Summoning.SummonRitual"/>).
	/// </summary>
	public static class Tutorial
	{
		/// <summary>Quem bate, quem atordoa (a segunda habilidade) e quem cura, nesta ordem.</summary>
		public const string Striker = "imp_water";
		public const string Stunner = "goblin_water";
		public const string Healer = "pixie_water";

		public const int AllyLevel = 15;

		/// <summary>A habilidade do <see cref="Stunner"/> que atordoa.</summary>
		public const int StunSkill = 1;

		/// <summary>Abre sozinha numa conta que ainda não invocou nem lutou, e só uma vez.</summary>
		public static bool ShouldStart(PlayerState player) => !player.TutorialDone && player.TotalPulls == 0 && player.HighestStage == 0;

		/// <summary>
		/// A tela de Invocação ensina a primeira invocação da conta (a ×10) enquanto a conta não invocou nada e
		/// tem Pergaminhos para ela.
		/// </summary>
		public static bool GuidesFirstSummon(PlayerState player, GameDatabase database) =>
			player.TotalPulls == 0 && player.Scrolls >= Summoning.SummonRitual.CostFor(10) && database.HasSummon(Summoning.SummonRates.FirstSummon);

		public static BattleTeam Team(GameDatabase database) => new(new[] { Striker, Stunner, Healer }
			.Select(id => database.Summon(id))
			.Select(summon => new TeamMember(summon, summon.Rarity, AllyLevel, false, Array.Empty<int>(), Array.Empty<Runes.Rune>()))
			.ToList());

		/// <summary>Duas ondas: dois de Fogo; depois um de Vento (a desvantagem da Água) e um de Fogo.</summary>
		public static Encounter Encounter() => new(3, 1, new IReadOnlyList<StageEnemy>[]
		{
			new[] { new StageEnemy { Summon = "slime_fire" }, new StageEnemy { Summon = "goblin_fire" } },
			new[] { new StageEnemy { Summon = "wolf_wind" }, new StageEnemy { Summon = "bandit_fire" } },
		}, Scale: 0.45);
	}
}
