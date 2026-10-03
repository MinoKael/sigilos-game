namespace Sigilos.Core.Content
{
	/// <summary>O que o <see cref="EffectKind.BonusPerStatus"/> dá a cada efeito contado.</summary>
	public enum BonusKind
	{
		/// <summary>Mais dano nos golpes seguintes da mesma habilidade (0,2 = +20% por efeito).</summary>
		Damage,

		/// <summary>Mais cura e escudo nos efeitos seguintes da mesma habilidade.</summary>
		Heal,

		/// <summary>Pontos de Ímpeto, na hora, para os alvos do efeito.</summary>
		Impeto,
	}
}
