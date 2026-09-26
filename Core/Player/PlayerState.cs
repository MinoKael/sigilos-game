using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// Tudo o que o save guarda. É só estado: as regras que mudam este estado moram em Progression/,
	/// Summoning/, <see cref="Roster"/>, <see cref="Teams"/> e <see cref="RuneInventory"/>, e quem
	/// grava o arquivo é o GameEntry (via <see cref="PlayerSave"/>).
	/// </summary>
	public sealed class PlayerState
	{
		/// <summary>Monstros por equipe, em qualquer luta.</summary>
		public const int TeamSize = 5;

		/// <summary>Monstros fora do Baú. O que passa disso vai para o Baú, que não tem limite.</summary>
		public const int CollectionCapacity = 50;

		/// <summary>Formato do save. Um save de formato mais antigo não é lido: a conta recomeça.</summary>
		public const int CurrentVersion = 5;

		/// <summary>0 num save anterior ao campo existir.</summary>
		public int Version { get; set; }

		// Moedas (GDD, seção 12).
		public int Scrolls { get; set; }

		/// <summary>Sobe o nível dos monstros, paga o Despertar e melhora runas.</summary>
		public int Essence { get; set; }

		/// <summary>A moeda rara: compra Mana e Pergaminhos na Loja (Core/Progression/Shop).</summary>
		public int Gold { get; set; }

		public int Fragments { get; set; }

		/// <summary>Paga cada vitória; a derrota não custa nada. A ociosidade recarrega até o máximo (Core/Progression/Mana).</summary>
		public int Mana { get; set; }

		/// <summary>Nível da conta, de 1 a 60: sobe com a experiência de toda vitória e aumenta a Mana máxima.</summary>
		public int AccountLevel { get; set; } = 1;

		/// <summary>Experiência da conta dentro do nível atual.</summary>
		public int AccountExperience { get; set; }

		/// <summary>Maior fase vencida. 0 = nenhuma.</summary>
		public int HighestStage { get; set; }

		/// <summary>Maior andar vencido em cada Masmorra, pelo id de Data/dungeons.json.</summary>
		public Dictionary<string, int> DungeonFloors { get; set; } = new();

		/// <summary>Invocações desde a última 5★: a garantia visível do gacha.</summary>
		public int PullsSinceFiveStar { get; set; }

		public int TotalPulls { get; set; }

		/// <summary>Todos os monstros, na coleção ou no Baú (<see cref="OwnedSummon.Stored"/>).</summary>
		public List<OwnedSummon> Monsters { get; set; } = new();

		public int NextMonsterId { get; set; } = 1;

		/// <summary>Uma equipe por conteúdo (<see cref="Player.Teams"/>): ids de monstro, a primeira é a Líder.</summary>
		public Dictionary<string, List<int>> Teams { get; set; } = new();

		/// <summary>Todas as runas, equipadas ou não (<see cref="Rune.EquippedOn"/>).</summary>
		public List<Rune> Runes { get; set; } = new();

		public int NextRuneId { get; set; } = 1;

		/// <summary>Pedras de Afiar e Gemas Encantadas guardadas. Pedras iguais se repetem na lista.</summary>
		public List<RuneTool> Tools { get; set; } = new();

		/// <summary>A última escolha de automático na tela de batalha: a próxima luta começa igual.</summary>
		public bool AutoBattle { get; set; }

		public DateTime LastIdleCollect { get; set; }
		public DateTime LastQuickChannel { get; set; } = DateTime.MinValue;

		/// <summary>Frações que a ociosidade ainda não fechou numa unidade.</summary>
		public double IdleEssenceCarry { get; set; }

		public double IdleGoldCarry { get; set; }
		public double IdleManaCarry { get; set; }

		/// <summary>Tem alguma cópia da variante (na coleção ou no Baú).</summary>
		public bool Owns(string summonId) => Monsters.Any(m => m.SummonId == summonId);

		public OwnedSummon? Monster(int id) => Monsters.FirstOrDefault(m => m.Id == id);

		/// <summary>Os monstros fora do Baú.</summary>
		public IEnumerable<OwnedSummon> Collection => Monsters.Where(m => !m.Stored);

		public IEnumerable<OwnedSummon> Storage => Monsters.Where(m => m.Stored);

		public IReadOnlyList<Rune> RunesOn(int monsterId) => Runes.Where(r => r.EquippedOn == monsterId).OrderBy(r => r.Slot).ToList();
	}
}
