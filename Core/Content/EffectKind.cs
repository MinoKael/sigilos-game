namespace Sigilos.Core.Content
{
	/// <summary>O que um efeito faz. Habilidades e páginas do Grimório usam o mesmo vocabulário:
	/// "magia é linguagem" (GDD, pilar 1).</summary>
	public enum EffectKind
	{
		Damage,
		Heal,
		Shield,
		Status,
		Impeto,
		Cleanse,
		Revive,
	}
}
