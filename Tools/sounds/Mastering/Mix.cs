namespace Sigilos.Sounds.Mastering
{
	/// <summary>A classe de mixagem de um efeito: decide o volume percebido, o teto, os cortes e a cauda (<see cref="MixProfile"/>).</summary>
	public enum Mix
	{
		/// <summary>Interface: baixo, sem graves, sem agudo que canse; nada de limitador.</summary>
		Ui,
		/// <summary>Grimório, constelações, status suaves: delicado, um pouco de espaço.</summary>
		Soft,
		/// <summary>Recompensas: transiente claro, mais presença.</summary>
		Reward,
		/// <summary>Golpes, dano, elementos: médios na frente, graves controlados, agudos domados.</summary>
		Combat,
		/// <summary>Raros, lendários, chefes, vitória: faixa dinâmica maior e cauda mais longa.</summary>
		Epic,
	}
}
