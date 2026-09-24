using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.UI
{
	/// <summary>
	/// Todo texto em português que não vem de Data/: nomes, explicações do Compêndio e a descrição de
	/// habilidades gerada a partir dos efeitos. Gerar a descrição garante que o texto diz exatamente o
	/// que o combate faz — não há frase escrita à mão para ficar desatualizada.
	/// </summary>
	public static class Texts
	{
		public static string Name(Glyph glyph) => glyph switch
		{
			Glyph.Wall => "Muralha",
			Glyph.Eye => "Olho",
			Glyph.Door => "Porta",
			Glyph.Bond => "Laço",
			Glyph.Shard => "Estilhaço",
			Glyph.Veil => "Véu",
			Glyph.Bone => "Ossada",
			Glyph.Spiral => "Espiral",
			_ => glyph.ToString(),
		};

		/// <summary>A escola de magia de origem (GDD, seção 6).</summary>
		public static string School(Glyph glyph) => glyph switch
		{
			Glyph.Wall => "Abjuração",
			Glyph.Eye => "Adivinhação",
			Glyph.Door => "Convocação",
			Glyph.Bond => "Encantamento",
			Glyph.Shard => "Evocação",
			Glyph.Veil => "Ilusão",
			Glyph.Bone => "Necromancia",
			Glyph.Spiral => "Transmutação",
			_ => "",
		};

		/// <summary>O que um Glifo quer dizer nas habilidades de quem o carrega.</summary>
		public static string Meaning(Glyph glyph) => glyph switch
		{
			Glyph.Wall => "Proteção: escudos, Égide, enfraquecer o ataque inimigo.",
			Glyph.Eye => "Ritmo: empurra o Ímpeto dos aliados, atrasa inimigos, prevê críticos.",
			Glyph.Door => "Chamado: espíritos e aliados extras. Ainda sem invocação no MVP.",
			Glyph.Bond => "Controle: provocar e atordoar.",
			Glyph.Shard => "Dano direto: golpes fortes, ignorar Defesa, agir de novo ao derrubar.",
			Glyph.Veil => "Engano: ficar Oculto, cegar, esquivar.",
			Glyph.Bone => "Vida roubada: drenar, amaldiçoar, curar com o dano.",
			Glyph.Spiral => "Mudança: aumentar ou reduzir atributos, Queimadura, Velocidade.",
			_ => "",
		};

		public static string Name(Element element) => element switch
		{
			Element.Fire => "Fogo",
			Element.Water => "Água",
			Element.Wind => "Vento",
			Element.Light => "Luz",
			Element.Dark => "Trevas",
			_ => element.ToString(),
		};

		public static string Name(Role role) => role switch
		{
			Role.Front => "Frente",
			Role.Attacker => "Atacante",
			Role.Support => "Suporte",
			Role.Control => "Controle",
			_ => role.ToString(),
		};

		public static string Name(Stat stat) => stat switch
		{
			Stat.Health => "Vida",
			Stat.Attack => "Ataque",
			Stat.Defense => "Defesa",
			Stat.Speed => "Velocidade",
			Stat.Crit => "Crítico",
			Stat.CritDamage => "Dano crítico",
			Stat.Resistance => "Resistência",
			Stat.Accuracy => "Precisão",
			_ => stat.ToString(),
		};

		public static string Name(RuneStat stat) => stat switch
		{
			RuneStat.HealthFlat or RuneStat.HealthPercent => "Vida",
			RuneStat.AttackFlat or RuneStat.AttackPercent => "Ataque",
			RuneStat.DefenseFlat or RuneStat.DefensePercent => "Defesa",
			RuneStat.Speed => "Velocidade",
			RuneStat.Crit => "Crítico",
			RuneStat.CritDamage => "Dano crítico",
			RuneStat.Resistance => "Resistência",
			_ => "Precisão",
		};

		/// <summary>Nome curto que separa o fixo do percentual: "Ataque" e "Ataque%".</summary>
		public static string Label(RuneStat stat) => stat switch
		{
			RuneStat.HealthPercent or RuneStat.AttackPercent or RuneStat.DefensePercent => $"{Name(stat)}%",
			_ => Name(stat),
		};

		/// <summary>"Ataque +12%" ou "Ataque +110".</summary>
		public static string Format(RuneStat stat, double value) =>
			RuneRules.IsFlat(stat) ? $"{Name(stat)} +{value:0}" : $"{Name(stat)} +{value * 100:0.#}%";

		/// <summary>Subatributo com o que a Pedra de Afiar somou: "Ataque +12% (+5% afiado)".</summary>
		public static string Format(RuneSubstat substat)
		{
			var text = Format(substat.Stat, substat.Total);
			if (substat.Grind > 0)
				text += $" ({Amount(substat.Stat, substat.Grind)} afiado)";
			return text;
		}

		/// <summary>"+5%" ou "+20".</summary>
		public static string Amount(RuneStat stat, double value) =>
			RuneRules.IsFlat(stat) ? $"+{value:0}" : $"+{value * 100:0.#}%";

		public static string Name(RuneSet set) => set switch
		{
			RuneSet.Energy => "Energia",
			RuneSet.Swift => "Rapidez",
			RuneSet.Guard => "Guarda",
			RuneSet.Shield => "Escudo",
			RuneSet.Focus => "Foco",
			RuneSet.Blade => "Lâmina",
			RuneSet.Violent => "Violência",
			RuneSet.Revenge => "Vingança",
			RuneSet.Despair => "Desespero",
			RuneSet.Will => "Vontade",
			RuneSet.Rage => "Fúria",
			RuneSet.Fatal => "Fatal",
			RuneSet.Endure => "Perseverança",
			RuneSet.Nemesis => "Nêmesis",
			RuneSet.Vampire => "Vampiro",
			RuneSet.Destroy => "Destruição",
			_ => set.ToString(),
		};

		/// <summary>"Violência (2)": conjunto e espaço.</summary>
		public static string Title(Rune rune) => $"{Name(rune.Set)} ({rune.Slot})";

		public static string Name(RuneRarity rarity) => rarity switch
		{
			RuneRarity.Magic => "Mágica",
			RuneRarity.Rare => "Rara",
			RuneRarity.Hero => "Heroica",
			RuneRarity.Legendary => "Lendária",
			_ => "Normal",
		};

		/// <summary>"Pedra de Afiar Heroica · Ataque%".</summary>
		public static string Name(RuneTool tool) =>
			$"{(tool.Kind == RuneToolKind.Grindstone ? "Pedra de Afiar" : "Gema Encantada")} {Name(tool.Grade)} · {Label(tool.Stat)}";

		/// <summary>O que a pedra dá: "+4% a +7%".</summary>
		public static string Range(RuneTool tool)
		{
			var (min, max) = tool.Kind == RuneToolKind.Grindstone
				? RuneRules.GrindRange(tool.Stat, tool.Grade) ?? (0, 0)
				: RuneRules.GemRange(tool.Stat, tool.Grade);
			return $"{Amount(tool.Stat, min)} a {Amount(tool.Stat, max)}";
		}

		/// <summary>O bônus do Despertar: "+15 de Velocidade" ou "+25% de Precisão".</summary>
		public static string AwakeningBonus(Stat stat)
		{
			var value = Awakening.Bonus(stat);
			return StatBlock.IsAbsolute(stat) ? $"+{value:0} de {Name(stat)}" : $"+{value * 100:0}% de {Name(stat)}";
		}

		/// <summary>Valor de atributo para a ficha: número inteiro ou porcentagem.</summary>
		public static string Value(Stat stat, double value) =>
			StatBlock.IsAbsolute(stat) ? $"{value:0}" : $"{value * 100:0.#}%";

		public static string Name(StatusKind status) => status switch
		{
			StatusKind.Shield => "Escudo",
			StatusKind.Burn => "Queimadura",
			StatusKind.Stun => "Atordoamento",
			StatusKind.Taunt => "Provocação",
			StatusKind.Hidden => "Oculto",
			StatusKind.Curse => "Maldição",
			StatusKind.Blind => "Cegueira",
			StatusKind.Ward => "Égide",
			StatusKind.Foresight => "Presságio",
			StatusKind.Immunity => "Imunidade",
			StatusKind.AttackUp => "Ataque+",
			StatusKind.AttackDown => "Ataque−",
			StatusKind.DefenseUp => "Defesa+",
			StatusKind.SpeedUp => "Velocidade+",
			_ => status.ToString(),
		};

		public static string Explain(StatusKind status) => status switch
		{
			StatusKind.Shield => "Absorve dano até o valor do escudo.",
			StatusKind.Burn => $"Perde {Percent(BattleRules.BurnFraction)} da Vida máxima no começo de cada turno. Acumula até {BattleRules.MaxBurnStacks}.",
			StatusKind.Stun => "Perde o próximo turno.",
			StatusKind.Taunt => "Só pode mirar em quem provocou.",
			StatusKind.Hidden => "Não pode ser alvo de ataques únicos.",
			StatusKind.Curse => $"Recebe {Percent(BattleRules.CurseBonus)} a mais de dano.",
			StatusKind.Blind => $"Cada golpe tem {Percent(BattleRules.BlindMissChance)} de chance de errar.",
			StatusKind.Ward => "Anula o próximo golpe recebido.",
			StatusKind.Foresight => "O próximo golpe causado é crítico.",
			StatusKind.Immunity => "Nenhum efeito negativo pega.",
			StatusKind.AttackUp => $"+{Percent(BattleRules.AttackUpBonus)} de Ataque.",
			StatusKind.AttackDown => $"−{Percent(BattleRules.AttackDownPenalty)} de Ataque.",
			StatusKind.DefenseUp => $"+{Percent(BattleRules.DefenseUpBonus)} de Defesa.",
			StatusKind.SpeedUp => $"+{Percent(BattleRules.SpeedUpBonus)} de Velocidade.",
			_ => "",
		};

		/// <summary>Três letras para o selo de efeito no cartão da unidade.</summary>
		public static string Short(StatusKind status) => status switch
		{
			StatusKind.Shield => "ESC",
			StatusKind.Burn => "QMD",
			StatusKind.Stun => "ATD",
			StatusKind.Taunt => "PRV",
			StatusKind.Hidden => "OCL",
			StatusKind.Curse => "MAL",
			StatusKind.Blind => "CEG",
			StatusKind.Ward => "ÉGI",
			StatusKind.Foresight => "PRS",
			StatusKind.Immunity => "IMU",
			StatusKind.AttackUp => "ATQ+",
			StatusKind.AttackDown => "ATQ−",
			StatusKind.DefenseUp => "DEF+",
			StatusKind.SpeedUp => "VEL+",
			_ => "?",
		};

		public static string Stars(int count) => new('★', count);

		public static string Scrolls(int count) => count == 1 ? "1 Pergaminho" : $"{count} Pergaminhos";

		/// <summary>"4 peças: +25% de Velocidade".</summary>
		public static string Describe(RuneSetDefinition set)
		{
			var value = Percent(set.Value);
			var bonus = set.Effect switch
			{
				RuneSetEffect.Drain => $"drena {value} do dano causado",
				RuneSetEffect.Stun => $"{value} de chance de atordoar cada alvo atingido (a Resistência não barra)",
				RuneSetEffect.ExtraTurn => $"{value} de chance de turno extra",
				RuneSetEffect.AllyShield => $"no começo de cada onda, todos os aliados ganham escudo de {value} da Vida de base do dono por {RuneSets.ShieldTurns} turnos",
				RuneSetEffect.Immunity => $"Imunidade por {set.Value:0} turno no começo de cada onda",
				RuneSetEffect.Counter => $"{value} de chance de contra-atacar com o básico ({Percent(RuneSets.CounterDamage)} do dano) ao ser atingido",
				RuneSetEffect.Nemesis => $"+{value} de Ímpeto a cada {Percent(RuneSets.NemesisStep)} da Vida máxima perdida num golpe",
				RuneSetEffect.Destroy => $"{Percent(RuneSets.DestroyShare)} do dano causado tira Vida máxima do alvo (até {value} por habilidade, {Percent(RuneSets.DestroyLimit)} no total)",
				_ => $"+{value} de {Name(set.Stat ?? Stat.Health)}",
			};
			return $"{set.Pieces} peças: {bonus}";
		}

		public static string Describe(PassiveDefinition passive, bool awakened)
		{
			var value = passive.ValueFor(awakened) * 100;
			return passive.Kind switch
			{
				PassiveKind.SpeedWhenLowest => $"+{value:0}% de Velocidade enquanto for o aliado com menos Vida.",
				PassiveKind.ShieldOnDeath => $"Ao cair, dá escudo de {value:0}% da própria Vida a todos os aliados.",
				PassiveKind.RebirthOnce => $"Na primeira vez que cai, renasce com {value:0}% da Vida no turno seguinte dela.",
				_ => "",
			};
		}

		public static string Describe(SkillDefinition skill)
		{
			var text = Describe(skill.Effects);
			if (skill.CanEnhance)
				text += $"\nAprimorada ({skill.EnhanceCost} Éter): {Describe(skill.EnhancedEffects)}";
			return text;
		}

		public static string Describe(IEnumerable<EffectDefinition> effects) =>
			string.Join("; ", effects.Select(Describe)) + ".";

		private static string Describe(EffectDefinition effect)
		{
			var where = Where(effect.Target);
			var chance = effect.Chance < 1 ? $"{Percent(effect.Chance)} de chance de " : "";
			var text = effect.Kind switch
			{
				EffectKind.Damage => DamageText(effect, where),
				EffectKind.Heal => $"cura {Percent(effect.Power)} da Vida {where}",
				EffectKind.Shield => $"escudo de {Percent(effect.Power)} da Vida {where} por {Turns(effect.Turns)}",
				EffectKind.Status => $"{chance}{Name(effect.Status)} {where} por {Turns(effect.Turns)}",
				EffectKind.Impeto => $"{(effect.Power >= 0 ? "+" : "−")}{System.Math.Abs(effect.Power):0}% de Ímpeto {where}",
				EffectKind.Cleanse => $"remove um efeito negativo {where}",
				_ => effect.Kind.ToString(),
			};
			return effect.OnKill ? $"se derrubar o alvo, {text}" : text;
		}

		private static string DamageText(EffectDefinition effect, string where)
		{
			var hits = effect.Hits > 1 ? $"{effect.Hits} golpes de " : "";
			var text = $"{hits}{Percent(effect.Power)} do Ataque {where}";
			if (effect.IgnoreDefense > 0)
				text += $", ignora {Percent(effect.IgnoreDefense)} da Defesa";
			if (effect.Drain > 0)
				text += $", drena {Percent(effect.Drain)} como Vida";
			return text;
		}

		private static string Where(TargetKind target) => target switch
		{
			TargetKind.Target => "no alvo",
			TargetKind.AllEnemies => "em todos os inimigos",
			TargetKind.Self => "em si",
			TargetKind.LowestAlly => "no aliado mais ferido",
			TargetKind.AllAllies => "em todos os aliados",
			_ => "",
		};

		private static string Turns(int turns) => turns == 1 ? "1 turno" : $"{turns} turnos";

		private static string Percent(double fraction) => $"{fraction * 100:0}%";
	}
}
