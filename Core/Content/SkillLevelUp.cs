namespace Sigilos.Core.Content
{
	/// <summary>Um nível de habilidade: o que ele melhora e quanto. O nível 2 é o primeiro da lista.</summary>
	public sealed record SkillLevelUp
	{
		public SkillLevelKind Kind { get; init; }

		/// <summary>Fração (0,10 = 10%) ou, na recarga, turnos.</summary>
		public double Value { get; init; }
	}
}
