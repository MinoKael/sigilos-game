namespace Sigilos.Core.Player
{
	/// <summary>A situação de um monstro, para o filtro da grade de Monstros.</summary>
	public enum MonsterCondition
	{
		/// <summary>Favoritado.</summary>
		Favorite,

		/// <summary>Bloqueado: não se solta nem vira material de fusão.</summary>
		Locked,

		/// <summary>Desbloqueado: o que ainda pode sumir numa fusão ou ao soltar.</summary>
		Unlocked,

		/// <summary>Em alguma equipe (Campanha ou Masmorra).</summary>
		InTeam,

		/// <summary>No nível máximo das estrelas de agora: pronto para evoluir.</summary>
		MaxLevel,

		/// <summary>Com pelo menos uma runa.</summary>
		Runed,

		/// <summary>Sem nenhuma runa.</summary>
		Unruned,
	}
}
