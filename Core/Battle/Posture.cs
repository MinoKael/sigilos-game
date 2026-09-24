namespace Sigilos.Core.Battle
{
	/// <summary>Postura de Éter do automático (GDD, seção 7): quando gastar em aprimoramentos e quando guardar.</summary>
	public enum Posture
	{
		/// <summary>Gasta em aprimoramentos sempre que dá.</summary>
		Aggressive,

		/// <summary>Aprimora, mas guarda metade do custo da página mais cara.</summary>
		Balanced,

		/// <summary>Guarda para o Círculo III.</summary>
		Economic,
	}
}
