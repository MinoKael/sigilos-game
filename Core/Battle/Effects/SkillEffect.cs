using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// O que um <see cref="EffectDefinition"/> faz quando a habilidade é usada: uma estratégia por
	/// <see cref="EffectKind"/> (a tabela está em <see cref="SkillEffects"/>). Uma habilidade é uma lista
	/// de efeitos, e cada um age sozinho, na ordem da lista: "3 golpes comuns e um quarto que ignora a
	/// Defesa" são dois efeitos de dano em Data/, sem código novo.
	///
	/// A estratégia recebe a habilidade em curso (<see cref="Cast"/>: quem lança, o alvo escolhido, o
	/// que já aconteceu) e age pelas ações do <see cref="EffectResolver"/>.
	///
	/// Tipo novo de efeito: o nome em <see cref="EffectKind"/>, uma classe que herda daqui, a linha na
	/// tabela e o texto dele em UI/Texts.cs.
	/// </summary>
	internal abstract class SkillEffect
	{
		public abstract void Apply(Cast cast, EffectDefinition effect);
	}
}
