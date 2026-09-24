using System;
using System.Collections.Generic;
using Sigilos.Core.Battle;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// Tudo o que o save guarda. É só estado: as regras que mudam este estado moram em Progression/ e
	/// Summoning/, e quem grava o arquivo é o GameEntry (via <see cref="PlayerSave"/>).
	/// </summary>
	public sealed class PlayerState
	{
		public const int TeamSize = 4;

		// Moedas (GDD, seção 12). O Pó de Sigilo entra com os Sigilos, na v0.5.
		public int Scrolls { get; set; }
		public int Essence { get; set; }
		public int Fragments { get; set; }

		/// <summary>Nível compartilhado por todas as invocações e pelo Conjurador.</summary>
		public int Level { get; set; } = 1;

		/// <summary>Maior fase vencida. 0 = nenhuma.</summary>
		public int HighestStage { get; set; }

		/// <summary>Invocações desde a última 5★: a garantia visível do gacha.</summary>
		public int PullsSinceFiveStar { get; set; }

		public int TotalPulls { get; set; }

		/// <summary>Invocações obtidas: id → Ecos (0 a 5).</summary>
		public Dictionary<string, int> Collection { get; set; } = new();

		/// <summary>Ids das invocações do time. A primeira é a Líder.</summary>
		public List<string> Team { get; set; } = new();

		/// <summary>Ids das páginas do Grimório, em ordem de prioridade.</summary>
		public List<string> Grimoire { get; set; } = new();

		public Posture Posture { get; set; } = Posture.Balanced;

		/// <summary>A última escolha de automático na tela de batalha: a próxima luta começa igual.</summary>
		public bool AutoBattle { get; set; } = true;

		public string ConjurerId { get; set; } = "erudito";

		public DateTime LastIdleCollect { get; set; }
		public DateTime LastQuickChannel { get; set; } = DateTime.MinValue;

		/// <summary>Frações de Pergaminho e Essência que ainda não fecharam uma unidade.</summary>
		public double IdleScrollCarry { get; set; }

		public double IdleEssenceCarry { get; set; }

		public bool Owns(string summonId) => Collection.ContainsKey(summonId);

		public int Echoes(string summonId) => Collection.TryGetValue(summonId, out var echoes) ? echoes : 0;
	}
}
