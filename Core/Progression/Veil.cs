using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Rta;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// A conta na Arena do Véu: quem pode entrar, o baú que ela leva para a fila, o que uma luta rende (Áster e
	/// Ecos do Véu) e a Loja do Véu. A luta em si é do servidor (<see cref="RtaMatch"/>); o Áster verdadeiro mora
	/// nele, e o da conta é a cópia que a tela mostra.
	/// </summary>
	public static class Veil
	{
		/// <summary>Quantas invocações diferentes a conta tem para montar um baú.</summary>
		public static int Eligible(PlayerState player, GameDatabase database) =>
			player.Monsters.Where(m => Fit(m, database)).Select(m => m.SummonId).Distinct().Count();

		public static bool CanQueue(PlayerState player, GameDatabase database) => Eligible(player, database) >= RtaRules.MinPool;

		private static bool Fit(OwnedSummon? monster, GameDatabase database) =>
			monster is { Stored: false, IsInfusionCore: false } && database.HasSummon(monster.SummonId);

		/// <summary>A melhor cópia de cada invocação (estrelas, depois nível), das mais fortes às mais fracas, até <see cref="RtaRules.MaxPool"/>.</summary>
		public static List<OwnedSummon> Best(PlayerState player, GameDatabase database) => player.Monsters
			.Where(m => Fit(m, database))
			.GroupBy(m => m.SummonId)
			.Select(g => g.OrderByDescending(m => m.Stars).ThenByDescending(m => m.Level).First())
			.OrderByDescending(m => m.Stars)
			.ThenByDescending(m => m.Level)
			.ThenBy(m => m.SummonId, StringComparer.Ordinal)
			.Take(RtaRules.MaxPool)
			.ToList();

		/// <summary>
		/// O baú da fila, de <see cref="Best"/>: é a ordem em que o automático escolhe quando o prazo estoura.
		/// </summary>
		public static List<ArenaWireMember> Pool(PlayerState player, GameDatabase database) => Best(player, database)
			.Select(m => ArenaWire.ToWire(new TeamMember(
				database.Summon(m.SummonId),
				m.Stars,
				m.Level,
				m.Awakened,
				Enumerable.Range(0, database.Summon(m.SummonId).AllSkills.Count).Select(m.SkillLevel).ToList(),
				player.RunesOn(m.Id))))
			.ToList();

		/// <summary>Áster ganho ou perdido contra um adversário com <paramref name="theirs"/> de Áster.</summary>
		public static int Delta(ArenaDefinition definition, int mine, int theirs, bool won) =>
			won ? Arena.Gain(definition, mine, theirs) : -Arena.Loss(definition, mine, theirs);

		/// <summary>O Áster nunca fica abaixo de zero.</summary>
		public static int Apply(int aster, int delta) => Math.Max(0, aster + delta);

		/// <summary>O servidor diz quantos Ecos a conta já ganhou no total (<c>profile</c>): entra só o que o jogo ainda não viu.</summary>
		public static void SeeProfile(Player.VeilState veil, int total)
		{
			if (total <= veil.EcosSeen)
				return;

			veil.Ecos += total - veil.EcosSeen;
			veil.EcosSeen = total;
		}

		/// <summary>Os Ecos de uma luta que acabou com o jogo conectado: contam já, e o total do servidor passa a incluí-los.</summary>
		public static void Earn(Player.VeilState veil, int ecos)
		{
			veil.Ecos += ecos;
			veil.EcosSeen += ecos;
		}

		/// <summary>Os Ecos do Véu de uma vitória: os do Grau da conta.</summary>
		public static int EcosFor(ArenaDefinition definition, int aster) => definition.GradeOf(aster).Ecos;

		// Loja do Véu --------------------------------------------------------------------------------

		/// <summary>Zera o contador diário quando o dia virou e o semanal quando a semana virou; o relógio que volta atrás não zera nada.</summary>
		private static void Roll(VeilState veil, ArenaDefinition definition, DateTime now)
		{
			var cycle = Arena.CycleOf(now);
			if (cycle > veil.Cycle)
			{
				foreach (var offer in definition.VeilShop.Where(o => o.Period == ArenaPeriod.Week))
					veil.Bought.Remove(offer.Id);
				veil.Cycle = cycle;
			}

			var day = now.ToString("yyyy-MM-dd");
			if (veil.BoughtDay != null && string.CompareOrdinal(day, veil.BoughtDay) <= 0)
				return;

			foreach (var offer in definition.VeilShop.Where(o => o.Period == ArenaPeriod.Day))
				veil.Bought.Remove(offer.Id);
			veil.BoughtDay = day;
		}

		public static int BoughtOf(PlayerState player, ArenaDefinition definition, ArenaOffer offer, DateTime now)
		{
			Roll(player.Veil, definition, now);
			return player.Veil.Bought.GetValueOrDefault(offer.Id);
		}

		public static bool IsSoldOut(PlayerState player, ArenaDefinition definition, ArenaOffer offer, DateTime now) =>
			offer.Limit > 0 && BoughtOf(player, definition, offer, now) >= offer.Limit;

		public static bool CanBuy(PlayerState player, ArenaDefinition definition, ArenaOffer offer, DateTime now) =>
			player.Veil.Ecos >= offer.Price && !IsSoldOut(player, definition, offer, now);

		/// <summary>Compra da Loja do Véu: paga os Ecos do Véu, conta no limite e entrega como a Loja Cósmica.</summary>
		public static bool Buy(PlayerState player, ArenaDefinition definition, ArenaOffer offer, DateTime now)
		{
			if (!CanBuy(player, definition, offer, now))
				return false;

			player.Veil.Ecos -= offer.Price;
			player.Veil.Bought[offer.Id] = player.Veil.Bought.GetValueOrDefault(offer.Id) + 1;
			Arena.Deliver(player, offer);
			return true;
		}
	}
}
