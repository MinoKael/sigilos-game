namespace Sigilos.Core.Content
{
	/// <summary>
	/// O papel como o jogador vê (versão 0.1): Ofensivo, Defensivo e Suporte. O <see cref="Role"/> do arquivo
	/// segue com os quatro valores que o modelo de atributos (BVP) usa para dividir o orçamento; este é só a
	/// apresentação, em três grupos. Vida e Defesa viram Defensivo.
	/// </summary>
	public enum RoleGroup
	{
		Offensive,
		Defensive,
		Support,
	}

	public static class RoleGroups
	{
		public static RoleGroup Of(Role role) => role switch
		{
			Role.Attack => RoleGroup.Offensive,
			Role.Support => RoleGroup.Support,
			_ => RoleGroup.Defensive,
		};

		/// <summary>Um papel de cada grupo, para as telas que oferecem os três (o filtro por papel).</summary>
		public static readonly Role[] Representatives = [Role.Attack, Role.Defense, Role.Support];
	}
}
