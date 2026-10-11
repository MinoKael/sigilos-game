using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma regra da Influência de uma constelação: uma Passiva (as mesmas de Data/summons, com o número e
	/// os efeitos dela) que vale a luta inteira em cada unidade de <see cref="Side"/>. É com ela que a
	/// constelação muda a luta sem código novo: "todo inimigo começa a onda com Contragolpe", "o guardião
	/// cura 8% no começo de cada turno dele", "cada golpe do seu time tem 15% de chance de errar".
	///
	/// No arquivo: <c>{"side": "Guardian", "passive": {"kind": "StatusOrEffectEachTurn"}, "effects": [...]}</c>.
	/// Os efeitos são como os das habilidades e miram do ponto de vista de quem tem a regra
	/// ("AllEnemies" numa regra dos inimigos é o time do jogador).
	/// </summary>
	public sealed record InfluenceRule
	{
		public InfluenceSide Side { get; init; }

		public PassiveDefinition Passive { get; init; } = new();

		public IReadOnlyList<EffectDefinition> Effects { get; init; } = new List<EffectDefinition>();

		/// <summary>A Passiva pronta para lutar, com os efeitos dentro (como a de uma invocação no nível 1).</summary>
		public PassiveDefinition Prepared => Skill.At(1, false).Passive!;

		/// <summary>A regra como habilidade passiva: o que a validação e o texto das habilidades já sabem ler.</summary>
		public SkillDefinition Skill => new() { Name = Side.ToString(), Passive = Passive, Effects = Effects };
	}
}
