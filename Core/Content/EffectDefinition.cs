namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma peça de habilidade: "o que faz" e "em quem". Cada campo só vale para alguns
	/// tipos; os outros ficam no padrão.
	///
	/// - Damage: <see cref="Power"/> é o multiplicador sobre o Ataque (0,8 = 80%), em <see cref="Hits"/>
	///   golpes. <see cref="IgnoreDefense"/> e <see cref="Drain"/> são frações.
	/// - Heal: <see cref="Power"/> é a fração da Vida máxima do alvo.
	/// - Shield: <see cref="Power"/> é a fração da Vida máxima de quem lança, por <see cref="Turns"/>.
	/// - Status: aplica <see cref="Status"/> com <see cref="Chance"/>, por <see cref="Turns"/>.
	/// - Impeto: soma <see cref="Power"/> pontos de Ímpeto (negativo atrasa).
	/// - Cleanse: remove um efeito negativo de cada alvo.
	///
	/// <see cref="OnKill"/> faz o efeito só acontecer se o dano anterior da mesma habilidade derrubou o alvo.
	/// </summary>
	public sealed record EffectDefinition
	{
		public EffectKind Kind { get; init; }
		public TargetKind Target { get; init; } = TargetKind.Target;
		public double Power { get; init; }
		public int Hits { get; init; } = 1;
		public double IgnoreDefense { get; init; }
		public double Drain { get; init; }
		public StatusKind Status { get; init; }
		public double Chance { get; init; } = 1;
		public int Turns { get; init; } = 1;
		public bool OnKill { get; init; }
	}
}
