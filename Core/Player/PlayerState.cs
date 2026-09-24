using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// Tudo o que o save guarda. É só estado: as regras que mudam este estado moram em Progression/,
	/// Summoning/ e <see cref="RuneInventory"/>, e quem grava o arquivo é o GameEntry (via
	/// <see cref="PlayerSave"/>).
	/// </summary>
	public sealed class PlayerState
	{
		public const int TeamSize = 4;

		/// <summary>Formato do save. Um save de formato mais antigo não é lido: a conta recomeça.</summary>
		public const int CurrentVersion = 2;

		/// <summary>0 num save anterior ao campo existir.</summary>
		public int Version { get; set; }

		// Moedas (GDD, seção 12).
		public int Scrolls { get; set; }
		public int Essence { get; set; }

		/// <summary>Pó de Sigilo: melhora e refaz runas.</summary>
		public int Dust { get; set; }

		public int Fragments { get; set; }

		/// <summary>Maior fase vencida. 0 = nenhuma.</summary>
		public int HighestStage { get; set; }

		/// <summary>Invocações desde a última 5★: a garantia visível do gacha.</summary>
		public int PullsSinceFiveStar { get; set; }

		public int TotalPulls { get; set; }

		/// <summary>Invocações obtidas, pelo id de Data/summons.</summary>
		public Dictionary<string, OwnedSummon> Summons { get; set; } = new();

		/// <summary>Ids das invocações do time. A primeira é a Líder.</summary>
		public List<string> Team { get; set; } = new();

		/// <summary>Todas as runas, equipadas ou não (<see cref="Rune.EquippedOn"/>).</summary>
		public List<Rune> Runes { get; set; } = new();

		public int NextRuneId { get; set; } = 1;

		/// <summary>A última escolha de automático na tela de batalha: a próxima luta começa igual.</summary>
		public bool AutoBattle { get; set; } = true;

		public DateTime LastIdleCollect { get; set; }
		public DateTime LastQuickChannel { get; set; } = DateTime.MinValue;

		/// <summary>Frações de moeda que a ociosidade ainda não fechou numa unidade.</summary>
		public double IdleScrollCarry { get; set; }

		public double IdleEssenceCarry { get; set; }
		public double IdleDustCarry { get; set; }

		public bool Owns(string summonId) => Summons.ContainsKey(summonId);

		public OwnedSummon Summon(string summonId) => Summons[summonId];

		public int Echoes(string summonId) => Summons.TryGetValue(summonId, out var owned) ? owned.Echoes : 0;

		public IReadOnlyList<Rune> RunesOn(string summonId) => Runes.Where(r => r.EquippedOn == summonId).OrderBy(r => r.Slot).ToList();
	}
}
