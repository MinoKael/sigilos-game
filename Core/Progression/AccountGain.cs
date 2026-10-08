namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O que a experiência de uma vitória deu à conta (<see cref="Account.GiveExperience"/>): os níveis
	/// subidos e a Essência da experiência que chegou com a conta no nível máximo.
	/// </summary>
	public readonly record struct AccountGain(int Levels, int Essence);
}
