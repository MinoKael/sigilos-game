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

		/// <summary>As vagas da coleção numa conta nova (e num save de antes do campo existir).</summary>
		public const int StartingCollectionCapacity = 50;

		/// <summary>Formato do save. Um save de formato mais antigo não é lido: a conta recomeça.</summary>
		public const int CurrentVersion = 8;

		/// <summary>0 num save anterior ao campo existir.</summary>
		public int Version { get; set; }

		// Moedas (GDD, seção 12).
		public int Scrolls { get; set; }

		/// <summary>Sobe o nível dos monstros, paga o Despertar e melhora runas.</summary>
		public int Essence { get; set; }

		/// <summary>A moeda rara: compra Mana e Pergaminhos na Loja (Core/Progression/Shop).</summary>
		public int Gold { get; set; }

		public int Fragments { get; set; }

		/// <summary>Gemas de Reavaliação, compradas na Loja: cada uma devolve uma runa ao estado em que caiu.</summary>
		public int ReappraisalGems { get; set; }

		/// <summary>
		/// Monstros fora do Baú. O que passa disso vai para o Baú, que não tem limite. Cresce com a Expansão de
		/// Coleção da Loja, até <see cref="Progression.Account.MaxCollectionCapacity"/>.
		/// </summary>
		public int CollectionCapacity { get; set; } = StartingCollectionCapacity;

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

		/// <summary>
		/// No automático (na tela e na Batalha automática), a equipe mira o chefe sempre que pode. Muda na
		/// pausa da luta; começa ligado, inclusive em save antigo.
		/// </summary>
		public bool FocusBoss { get; set; } = true;

		/// <summary>A luta de treino já foi feita (ou pulada): não abre mais sozinha (<see cref="Progression.Tutorial"/>).</summary>
		public bool TutorialDone { get; set; }

		/// <summary>O idioma escolhido na Configuração (Data/texts/{nome}.json); nulo = o padrão.</summary>
		public string? Language { get; set; }

		/// <summary>
		/// O retrato da conta, no Santuário: a variante de um monstro que a conta tem; nulo = o sigilo padrão.
		/// Quem troca é <see cref="Progression.Account.SetAvatar"/>.
		/// </summary>
		public string? Avatar { get; set; }

		/// <summary>O retrato é a forma desperta da variante.</summary>
		public bool AvatarAwakened { get; set; }

		/// <summary>
		/// Retratos liberados sem ter o monstro (presente do correio): o id da variante, com ":awakened" na
		/// forma desperta (<see cref="Progression.Account.UnlockAvatar"/>).
		/// </summary>
		public List<string> AvatarUnlocks { get; set; } = new();

		public DateTime LastIdleCollect { get; set; }

		/// <summary>Frações que a ociosidade ainda não fechou numa unidade.</summary>
		public double IdleEssenceCarry { get; set; }

		public double IdleGoldCarry { get; set; }
		public double IdleManaCarry { get; set; }

		/// <summary>O melhor tempo de cada luta vencida, em segundos (<see cref="Progression.Records"/>).</summary>
		public Dictionary<string, double> BestTimes { get; set; } = new();

		/// <summary>As cartas do correio já coletadas neste save, pelo id do servidor (<see cref="Progression.Mailbox"/>).</summary>
		public List<string> ClaimedMail { get; set; } = new();

		/// <summary>Tem alguma cópia da variante (na coleção ou no Baú).</summary>
		public bool Owns(string summonId) => Monsters.Any(m => m.SummonId == summonId);

		public OwnedSummon? Monster(int id) => Monsters.FirstOrDefault(m => m.Id == id);

		/// <summary>Os monstros fora do Baú.</summary>
		public IEnumerable<OwnedSummon> Collection => Monsters.Where(m => !m.Stored);

		public IEnumerable<OwnedSummon> Storage => Monsters.Where(m => m.Stored);

		public IReadOnlyList<Rune> RunesOn(int monsterId) => Runes.Where(r => r.EquippedOn == monsterId).OrderBy(r => r.Slot).ToList();
	}
}
