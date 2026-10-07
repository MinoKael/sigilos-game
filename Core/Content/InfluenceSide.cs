namespace Sigilos.Core.Content
{
	/// <summary>Em quem uma regra da Influência de uma constelação vale durante a luta.</summary>
	public enum InfluenceSide
	{
		/// <summary>Todo inimigo, em todas as ondas.</summary>
		Foes,

		/// <summary>Só o guardião (o chefe da constelação).</summary>
		Guardian,

		/// <summary>Os monstros do jogador.</summary>
		Allies,

		/// <summary>Todos em campo, dos dois lados.</summary>
		Everyone,
	}
}
