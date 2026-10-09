namespace Sigilos.Sounds.Mastering
{
	/// <summary>
	/// Como uma classe de <see cref="Mix"/> sai do estúdio. <see cref="LoudnessDb"/> é o volume percebido
	/// (<see cref="Loudness"/>, em dBFS); <see cref="CeilingDb"/> o pico máximo, sempre abaixo de 0 dBFS;
	/// <see cref="LimitDb"/> quanto o limitador pode segurar de pico antes de o efeito inteiro baixar.
	/// <see cref="LowCut"/> e <see cref="HighCut"/> tiram o grave que embola e o agudo que cansa;
	/// <see cref="TrimDb"/> é onde a cauda é cortada, abaixo do pico.
	/// </summary>
	public sealed record MixProfile(string Name, double LoudnessDb, double CeilingDb, double LimitDb, double LowCut, double HighCut, double TrimDb)
	{
		public static MixProfile Of(Mix mix) => mix switch
		{
			Mix.Ui => new("ui", -25, -6, 0, 160, 10000, -48),
			Mix.Soft => new("soft", -27, -6, 1, 90, 8000, -54),
			Mix.Reward => new("reward", -24, -4, 2, 70, 8000, -54),
			Mix.Combat => new("combat", -25, -4, 3, 60, 7000, -50),
			_ => new("epic", -25, -3, 3, 45, 8000, -60),
		};
	}
}
