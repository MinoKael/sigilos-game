namespace Sigilos.Sounds.Synth
{
	/// <summary>De onde vem o som da camada.</summary>
	public enum LayerSource
	{
		/// <summary>Oscilador (com glissando, vibrato, FM e uníssono).</summary>
		Tone,
		/// <summary>Ruído colorido.</summary>
		Noise,
		/// <summary>Corpo que vibra (<see cref="Modes"/>): madeira, sino, vidro.</summary>
		Modal,
		/// <summary>Corda dedilhada (Karplus-Strong): alaúde, harpa, arco.</summary>
		Pluck,
		/// <summary>Estalos soltos: papel, fogo, pena riscando.</summary>
		Crackle,
	}
}
