using System;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// A troca do nome da conta: de graça, uma vez a cada <see cref="Cooldown"/> (sete dias corridos no relógio UTC do
	/// servidor, contados da última troca). O primeiro nome de uma conta sem nome não conta nem marca. Quem confere e
	/// marca é o servidor (/game/rename), no mesmo passo que grava o nome; a tela só lê <see cref="NextAt"/> para dizer
	/// quando a próxima troca abre.
	/// </summary>
	public static class AccountRename
	{
		public static readonly TimeSpan Cooldown = TimeSpan.FromDays(7);

		/// <summary>Quando a próxima troca abre; nulo se já está aberta (nunca trocou, ou a semana passou).</summary>
		public static DateTime? NextAt(PlayerState player, DateTime now) =>
			player.LastRename is { } last && last + Cooldown > now ? last + Cooldown : null;

		public static bool CanRename(PlayerState player, DateTime now) => NextAt(player, now) == null;

		/// <summary>A troca feita agora: a próxima só depois de <see cref="Cooldown"/>.</summary>
		public static void Mark(PlayerState player, DateTime now) => player.LastRename = now;
	}
}
