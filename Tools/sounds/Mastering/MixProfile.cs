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
			Mix.Soft => new("soft", -22, -4, 1, 90, 13000, -54),
			Mix.Reward => new("reward", -19, -2, 2, 70, 14000, -54),
			Mix.Combat => new("combat", -18, -1.5, 3, 55, 11000, -50),
			_ => new("epic", -18, -1, 4, 40, 15000, -60),
		};
	}
}
