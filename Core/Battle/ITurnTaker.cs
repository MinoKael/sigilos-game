namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Quem entra na barra de Ímpeto: as unidades em campo e o Conjurador, que fica fora de campo.
	/// A unidade age quando o Ímpeto chega a 100, e a barra enche em proporção à Velocidade.
	/// </summary>
	public interface ITurnTaker
	{
		string Name { get; }

		/// <summary>De 0 a 100.</summary>
		double Impeto { get; set; }

		/// <summary>Velocidade de agora, com efeitos e Assinatura.</summary>
		double TurnSpeed { get; }

		/// <summary>Está na barra: vivo, ou caído esperando renascer.</summary>
		bool CanTakeTurn { get; }
	}
}
