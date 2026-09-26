namespace Sigilos.Core.Content
{
	/// <summary>O que cada nível de habilidade melhora.</summary>
	public enum SkillLevelKind
	{
		/// <summary>Multiplica o dano da habilidade (0,10 = +10%).</summary>
		Damage,

		/// <summary>Multiplica cura e escudo da habilidade.</summary>
		Recovery,

		/// <summary>Soma à chance dos efeitos de status (0,10 = +10 pontos).</summary>
		EffectRate,

		/// <summary>Tira turnos da recarga (nunca abaixo de 1).</summary>
		Cooldown,
	}
}
