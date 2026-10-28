using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

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
	/// - Cleanse: remove <see cref="Count"/> efeitos negativos de cada alvo, os mais antigos primeiro (0 = todos); com
	///   <see cref="Removes"/>, os desse escopo no lugar (positivos, negativos ou todos). Irremovíveis ficam.
	/// - StealBuff: com <see cref="Chance"/>, rouba de cada alvo um efeito positivo (o mais novo; só
	///   <see cref="OnlyStatus"/>, se houver) e o põe em quem lança; sem nenhum e com <see cref="Fallback"/>,
	///   rouba <see cref="Power"/> pontos de Ímpeto (0 = todo o Ímpeto do alvo).
	/// - BonusPerStatus: conta os efeitos de <see cref="Scope"/> (ou só <see cref="OnlyStatus"/>) nas unidades
	///   de <see cref="From"/> e dá <see cref="Bonus"/>, <see cref="Power"/> por efeito: dano ou cura nos
	///   efeitos seguintes da habilidade, ou Ímpeto na hora para os alvos. <see cref="Max"/> é o teto do bônus de
	///   dano (ou de cura) da habilidade toda, somando os efeitos desse tipo (0 = sem teto).
	/// - ChangeDuration: com <see cref="Chance"/>, soma <see cref="Turns"/> (negativo encurta) aos efeitos de
	///   <see cref="Scope"/> (ou só <see cref="OnlyStatus"/>) de cada alvo; o que chega a 0 sai.
	/// - EqualizeHealth: os alvos ficam todos com a média da fração de Vida deles.
	/// - HealTeam: cura <see cref="Power"/> da Vida máxima dos <see cref="Count"/> alvos mais feridos (0 = todos).
	/// - JointAttack: <see cref="Count"/> aliados do alvo do efeito (0 = todos), sorteados, atacam com a
	///   básica o alvo da habilidade.
	/// - ExtraTurnOnKill: se a habilidade derrubou alguém até aqui, turno extra para quem lança e
	///   <see cref="Turns"/> turnos a menos na recarga dela.
	/// - Revive: traz de volta, na hora, os aliados caídos (<see cref="Count"/> deles, sorteados; 0 = todos)
	///   com <see cref="Power"/> da Vida máxima de cada um.
	///
	/// A conta de Damage, Heal, HealTeam, Shield e Revive (<see cref="EffectScaling"/>): <see cref="Power"/> ×
	/// o atributo de sempre do tipo (ou <see cref="Stat"/>), mais cada termo de <see cref="Plus"/>, vezes
	/// <see cref="Factor"/> e <see cref="Speed"/>. <see cref="Fixed"/> faz o dano não passar pela Defesa,
	/// pelo elemento nem pelo crítico.
	///
	/// <see cref="By"/> diz o que LowestAlly e HighestAlly comparam (em qualquer tipo, e no <see cref="From"/>).
	///
	/// <see cref="OnKill"/> faz o efeito só acontecer se o dano anterior da mesma habilidade derrubou o alvo.
	///
	/// <see cref="When"/> liga o efeito a cada golpe do dano da habilidade (a cada golpe, a cada crítico, a cada efeito de
	/// status causado): ali, o "Target" do efeito é quem o golpe acertou, e a <see cref="Chance"/> vale em cada golpe.
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

		/// <summary>Quando acontece: uma vez (padrão) ou a cada golpe do dano da habilidade (<see cref="HitTrigger"/>).</summary>
		public HitTrigger When { get; init; }

		/// <summary>When = Status: só o efeito causado que for este dispara.</summary>
		public StatusKind? WhenStatus { get; init; }

		/// <summary>When = Status, sem <see cref="WhenStatus"/>: dispara com os efeitos causados deste escopo (padrão: todos).</summary>
		public StatusScope? WhenScope { get; init; }

		/// <summary>Status e Shield: o efeito posto é irremovível; só os turnos dele o acabam (<see cref="Battle.StatusEffect.Unremovable"/>).</summary>
		public bool Unremovable { get; init; }

		/// <summary>StealBuff: sem efeito positivo para roubar, rouba Ímpeto.</summary>
		public bool Fallback { get; init; }

		/// <summary>BonusPerStatus e ChangeDuration: que efeitos contam.</summary>
		public StatusScope Scope { get; init; }

		/// <summary>StealBuff, BonusPerStatus e ChangeDuration: só este efeito conta (nulo = todos do <see cref="Scope"/>).</summary>
		public StatusKind? OnlyStatus { get; init; }

		/// <summary>BonusPerStatus: em quem os efeitos são contados.</summary>
		public TargetKind From { get; init; } = TargetKind.Target;

		/// <summary>BonusPerStatus: o que cada efeito contado dá.</summary>
		public BonusKind Bonus { get; init; }

		/// <summary>Cleanse: que efeitos remove (nulo: só os negativos, como sempre).</summary>
		public StatusScope? Removes { get; init; }
		/// <summary>BonusPerStatus: o teto do bônus da habilidade inteira, em Dano ou Cura (0,8 = +80%; 0 = sem teto).</summary>
		public double Max { get; init; }
		/// <summary>HealTeam, JointAttack e Revive: quantos alvos (0 = todos).</summary>
		public int Count { get; init; }

		/// <summary>O efeito não atinge quem lança: nos alvos de aliados, ele fica de fora (o aliado com mais Ataque é outro, não o dono).</summary>
		public bool OthersOnly { get; init; }

		/// <summary>Sacrifice: a Vida sacrificada vem da Vida máxima destruída (como o Oblívio) em vez de só descontada.</summary>
		public bool Destroy { get; init; }

		/// <summary>
		/// Os campos que mudam no Despertar (só o valor: "awakened": {"power": 2.94, "ignoreDefense": 0.3}); o resto do efeito fica
		/// como na forma normal. Não troca o tipo do efeito. A forma desperta da habilidade sai daqui (<see cref="AwakenedForm"/>).
		/// </summary>
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyDictionary<string, JsonElement>? Awakened { get; init; }

		/// <summary>O efeito some no Despertar (a forma desperta da habilidade não o tem). Raro: o Despertar só melhora.</summary>
		public bool LostAfterAwaken { get; init; }

		/// <summary>O efeito só existe desperto: a forma normal da habilidade não o tem.</summary>
		public bool OnlyAwakened { get; init; }

		/// <summary>LowestAlly e HighestAlly: o valor comparado (o padrão é a fração de Vida).</summary>
		public TargetRank By { get; init; }

		/// <summary>O atributo do <see cref="Power"/> no lugar do de sempre do tipo (nulo: o de sempre).</summary>
		public ScaleStat? Stat { get; init; }

		/// <summary>Termos somados a <see cref="Power"/> × o atributo: 0,5 do Ataque mais 0,08 da Vida máxima.</summary>
		public IReadOnlyList<ScaleTerm> Plus { get; init; } = NoTerms;

		/// <summary>Multiplica a conta por uma fração de agora (a Vida de quem lança, a do alvo, os aliados de pé).</summary>
		public ScaleFactor? Factor { get; init; }

		/// <summary>Multiplica a conta pela Velocidade de quem lança.</summary>
		public SpeedFactor? Speed { get; init; }

		/// <summary>Damage: dano fixo, sem Defesa, elemento nem crítico; o que mexe no dano recebido pelo alvo ainda conta.</summary>
		public bool Fixed { get; init; }

		/// <summary>
		/// Condição: o efeito só acontece se um aliado desta variante (o id, ex.: "wolf_light") estiver vivo no
		/// time de quem lança. Vazio: sem condição.
		/// </summary>
		public string IfAlly { get; init; } = "";

		/// <summary>Condição: o efeito só acontece se a Defesa de agora de quem lança passa a do alvo principal.</summary>
		public bool IfDefenseAbove { get; init; }

		private static readonly IReadOnlyList<ScaleTerm> NoTerms = Array.Empty<ScaleTerm>();
	}
}
