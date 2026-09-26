using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI
{
	/// <summary>
	/// Os textos que dependem de regra: nomes por enum, a descrição de habilidades, conjuntos e
	/// Assinaturas gerada a partir dos efeitos, e os termos de efeito em dourado. As palavras moram em
	/// Data/texts (via <see cref="Locale"/>); aqui só se monta. Gerar a descrição garante que o texto
	/// diz exatamente o que o combate faz.
	///
	/// Texto rico (<see cref="Term"/>, <see cref="Describe(SkillDefinition)"/>...) é BBCode do Godot
	/// mais a marca [glifo=Nome], que o <see cref="Components.RichText"/> desenha; em dica de mouse,
	/// use <see cref="Plain"/>.
	/// </summary>
	public static class Texts
	{
		public static string Name(Element element) => T($"elemento.{element}");
		public static string Name(Role role) => T($"papel.{role}");
		public static string Name(Stat stat) => T($"atributo.{stat}");
		/// <summary>O que o atributo faz. {0} é a Defesa que corta o dano pela metade; {1}, a Resistência mínima.</summary>
		public static string Explain(Stat stat) => T($"atributo_explica.{stat}", Math.Round(BattleRules.DefenseConstant), Percent(BattleRules.MinResistChance));
		public static string Name(RuneStat stat) => T($"runa_atributo.{stat}");
		public static string Name(RuneSet set) => T($"conjunto.{set}");
		public static string Name(Glyph glyph) => T($"glifo.{glyph}.nome");
		public static string Meaning(Glyph glyph) => T($"glifo.{glyph}.sentido");
		public static string Name(RuneRarity rarity) => T($"raridade.{rarity}");
		public static string Name(StatusKind status) => T($"efeito.{status}.nome");
		public static string Short(StatusKind status) => T($"efeito.{status}.sigla");
		public static string Name(DungeonKind kind) => T($"masmorras.tipo.{kind}");
		public static string Name(RuneSort sort) => T($"filtro.ordem.{sort}");

		/// <summary>"30 Mana", "10 Pergaminhos", "1 Pergaminho".</summary>
		public static string Amount(ShopItem item, int amount) => item == ShopItem.Scrolls ? Scrolls(amount) : T($"loja.item.{item}", amount);

		/// <summary>Por que a luta não começou, com o custo em Mana dela.</summary>
		public static string Refusal(EntryProblem problem, int mana) => T($"entrada.{problem}", mana, Core.Player.RuneInventory.Capacity);

		public static string Explain(StatusKind status) => status switch
		{
			StatusKind.Burn => T("efeito.Burn.explica", Percent(BattleRules.BurnFraction), BattleRules.MaxBurnStacks),
			StatusKind.Curse => T("efeito.Curse.explica", Percent(BattleRules.CurseBonus)),
			StatusKind.Blind => T("efeito.Blind.explica", Percent(BattleRules.BlindMissChance)),
			StatusKind.AttackUp => T("efeito.AttackUp.explica", Percent(BattleRules.AttackUpBonus)),
			StatusKind.AttackDown => T("efeito.AttackDown.explica", Percent(BattleRules.AttackDownPenalty)),
			StatusKind.DefenseUp => T("efeito.DefenseUp.explica", Percent(BattleRules.DefenseUpBonus)),
			StatusKind.SpeedUp => T("efeito.SpeedUp.explica", Percent(BattleRules.SpeedUpBonus)),
			_ => T($"efeito.{status}.explica"),
		};

		/// <summary>"Ataque" e "Ataque%": separa o fixo do percentual.</summary>
		public static string Label(RuneStat stat) => stat is RuneStat.HealthPercent or RuneStat.AttackPercent or RuneStat.DefensePercent
			? T("runa_atributo.percentual", Name(stat))
			: Name(stat);

		/// <summary>"Ataque +12%" ou "Ataque +110".</summary>
		public static string Format(RuneStat stat, double value) => $"{Name(stat)} {Amount(stat, value)}";

		/// <summary>Subatributo com o que a Pedra de Afiar somou: "Ataque +12% (+5% afiado)".</summary>
		public static string Format(RuneSubstat substat)
		{
			var text = Format(substat.Stat, substat.Total);
			return substat.Grind > 0 ? T("runa.afiado", text, Amount(substat.Stat, substat.Grind)) : text;
		}

		/// <summary>"+5%" ou "+20".</summary>
		public static string Amount(RuneStat stat, double value) => RuneRules.IsFlat(stat)
			? string.Format(Culture, "+{0:0}", value)
			: string.Format(Culture, "+{0:0.#}%", value * 100);

		/// <summary>Valor de atributo para a ficha: número inteiro ou porcentagem.</summary>
		public static string Value(Stat stat, double value) => StatBlock.IsAbsolute(stat)
			? string.Format(Culture, "{0:0}", value)
			: string.Format(Culture, "{0:0.#}%", value * 100);

		/// <summary>"Violência (2)": conjunto e espaço.</summary>
		public static string Title(Rune rune) => T("runa.titulo", Name(rune.Set), rune.Slot);

		/// <summary>"Pedra de Afiar Heroica · Ataque%".</summary>
		public static string Name(RuneTool tool) => T($"pedra.{tool.Kind}", Name(tool.Grade), Label(tool.Stat));

		/// <summary>O que a pedra dá: "+4% a +7%".</summary>
		public static string Range(RuneTool tool)
		{
			var (min, max) = tool.Kind == RuneToolKind.Grindstone
				? RuneRules.GrindRange(tool.Stat, tool.Grade) ?? (0, 0)
				: RuneRules.GemRange(tool.Stat, tool.Grade);
			return T("pedra.faixa", Amount(tool.Stat, min), Amount(tool.Stat, max));
		}

		/// <summary>O bônus do Despertar: "+15 de Velocidade" ou "+25% de Precisão".</summary>
		public static string AwakeningBonus(Stat stat)
		{
			var value = Awakening.Bonus(stat);
			return T("despertar.bonus", StatBlock.IsAbsolute(stat) ? string.Format(Culture, "+{0:0}", value) : $"+{Percent(value)}", Name(stat));
		}

		public static string Stars(int count) => new('★', count);

		public static string Scrolls(int count) => count == 1 ? T("moeda.pergaminho") : T("moeda.pergaminhos", count);

		public static string Turns(int turns) => turns == 1 ? T("turnos.um") : T("turnos.varios", turns);

		public static string Percent(double fraction) => string.Format(Culture, "{0:0.#}%", fraction * 100);

		// Termos e Glifos --------------------------------------------------------------------------

		/// <summary>O Glifo que quer dizer este atributo: o do conjunto que o aumenta.</summary>
		public static Glyph GlyphOf(Stat stat) => RuneSets.All.First(s => s.Stat == stat).Glyph;

		/// <summary>O Glifo de um efeito, quando ele tem um (atordoar é Gebo, Defesa+ é Algiz...).</summary>
		public static Glyph? GlyphOf(StatusKind status) => status switch
		{
			StatusKind.Stun => RuneSets.For(RuneSet.Despair).Glyph,
			StatusKind.Shield => RuneSets.For(RuneSet.Shield).Glyph,
			StatusKind.Immunity => RuneSets.For(RuneSet.Will).Glyph,
			StatusKind.AttackUp or StatusKind.AttackDown => GlyphOf(Stat.Attack),
			StatusKind.DefenseUp => GlyphOf(Stat.Defense),
			StatusKind.SpeedUp => GlyphOf(Stat.Speed),
			StatusKind.Foresight => GlyphOf(Stat.Crit),
			_ => null,
		};

		/// <summary>Um termo único do jogo, em dourado, com o Glifo na frente quando tem.</summary>
		public static string Term(string text, Glyph? glyph = null) =>
			$"{(glyph is { } g ? $"[glifo={g}]" : "")}[color=#{Palette.Gold.ToHtml(false)}]{text}[/color]";

		public static string Term(StatusKind status) => Term(Name(status), GlyphOf(status));

		public static string Term(RuneSet set) => Term(Name(set), RuneSets.For(set).Glyph);

		public static string Impeto => Term(T("termo.impeto"), RuneSets.For(RuneSet.Nemesis).Glyph);

		public static string Ether => Term(T("termo.eter"));

		/// <summary>Tira BBCode e Glifos: para dica de mouse, que é texto puro.</summary>
		public static string Plain(string rich) => System.Text.RegularExpressions.Regex.Replace(rich, @"\[[^\]]*\]", "");

		// Descrições geradas -------------------------------------------------------------------------

		/// <summary>"4 peças: +25% de Velocidade".</summary>
		public static string Describe(RuneSetDefinition set)
		{
			var value = Percent(set.Value);
			var bonus = set.Effect switch
			{
				RuneSetEffect.Drain => T("conjunto_efeito.Drain", value),
				RuneSetEffect.Stun => T("conjunto_efeito.Stun", value, Term(StatusKind.Stun)),
				RuneSetEffect.ExtraTurn => T("conjunto_efeito.ExtraTurn", value),
				RuneSetEffect.AllyShield => T("conjunto_efeito.AllyShield", Term(StatusKind.Shield), value, RuneSets.ShieldTurns),
				RuneSetEffect.Immunity => T("conjunto_efeito.Immunity", Term(StatusKind.Immunity), Turns((int)set.Value)),
				RuneSetEffect.Counter => T("conjunto_efeito.Counter", value, Percent(RuneSets.CounterDamage)),
				RuneSetEffect.Nemesis => T("conjunto_efeito.Nemesis", value, Impeto, Percent(RuneSets.NemesisStep)),
				RuneSetEffect.Destroy => T("conjunto_efeito.Destroy", Percent(RuneSets.DestroyShare), value, Percent(RuneSets.DestroyLimit)),
				_ => T("conjunto_efeito.Stat", value, Name(set.Stat ?? Stat.Health)),
			};
			return T("conjunto_efeito.pecas", set.Pieces, bonus);
		}

		public static string Describe(PassiveDefinition passive, bool awakened)
		{
			var value = Percent(passive.ValueFor(awakened));
			return passive.Kind switch
			{
				PassiveKind.ShieldOnDeath => T("assinatura.ShieldOnDeath", Term(StatusKind.Shield), value),
				_ => T($"assinatura.{passive.Kind}", value),
			};
		}

		public static string Describe(SkillDefinition skill)
		{
			var text = Describe(skill.Effects);
			return skill.CanEnhance ? $"{text}\n{T("habilidade.aprimorada", skill.EnhanceCost, Ether, Describe(skill.EnhancedEffects))}" : text;
		}

		public static string Describe(IEnumerable<EffectDefinition> effects) => string.Join("; ", effects.Select(Describe)) + ".";

		private static string Describe(EffectDefinition effect)
		{
			var where = T($"alvo.{effect.Target}");
			var chance = effect.Chance < 1 ? T("habilidade.chance", Percent(effect.Chance)) : "";
			var text = effect.Kind switch
			{
				EffectKind.Damage => DamageText(effect, where),
				EffectKind.Heal => T("habilidade.cura", Percent(effect.Power), where),
				EffectKind.Shield => T("habilidade.escudo", Term(StatusKind.Shield), Percent(effect.Power), where, Turns(effect.Turns)),
				EffectKind.Status => T("habilidade.efeito", chance, Term(effect.Status), where, Turns(effect.Turns)),
				EffectKind.Impeto => T("habilidade.impeto", effect.Power >= 0 ? "+" : "−", Math.Abs(effect.Power), Impeto, where),
				EffectKind.Cleanse => T("habilidade.purifica", where),
				_ => effect.Kind.ToString(),
			};
			return effect.OnKill ? T("habilidade.se_derrubar", text) : text;
		}

		private static string DamageText(EffectDefinition effect, string where)
		{
			var hits = effect.Hits > 1 ? T("habilidade.golpes", effect.Hits) : "";
			var text = T("habilidade.dano", hits, Percent(effect.Power), where);
			if (effect.IgnoreDefense > 0)
				text += T("habilidade.ignora", Percent(effect.IgnoreDefense));
			if (effect.Drain > 0)
				text += T("habilidade.drena", Percent(effect.Drain));
			return text;
		}

		/// <summary>Chaves que vêm de enum e faltam no arquivo de textos: o GameEntry avisa no console.</summary>
		public static IEnumerable<string> MissingEnumKeys()
		{
			IEnumerable<string> Keys<TEnum>(string format) where TEnum : struct, Enum =>
				Enum.GetValues<TEnum>().Select(value => string.Format(format, value));

			return Keys<Element>("elemento.{0}")
				.Concat(Keys<Role>("papel.{0}"))
				.Concat(Keys<Stat>("atributo.{0}"))
				.Concat(Keys<Stat>("atributo_explica.{0}"))
				.Concat(Keys<RuneStat>("runa_atributo.{0}"))
				.Concat(Keys<RuneSet>("conjunto.{0}"))
				.Concat(Keys<Glyph>("glifo.{0}.nome"))
				.Concat(Keys<Glyph>("glifo.{0}.sentido"))
				.Concat(Keys<RuneRarity>("raridade.{0}"))
				.Concat(Keys<StatusKind>("efeito.{0}.nome"))
				.Concat(Keys<StatusKind>("efeito.{0}.sigla"))
				.Concat(Keys<StatusKind>("efeito.{0}.explica"))
				.Concat(Keys<TargetKind>("alvo.{0}"))
				.Concat(Keys<PassiveKind>("assinatura.{0}"))
				.Concat(Keys<RuneToolKind>("pedra.{0}"))
				.Concat(Keys<DungeonKind>("masmorras.tipo.{0}"))
				.Concat(Keys<RuneSort>("filtro.ordem.{0}"))
				.Concat(Keys<ShopItem>("loja.item.{0}"))
				.Concat(Keys<EntryProblem>("entrada.{0}").Where(k => !k.EndsWith("None")))
				.Concat(Keys<RuneSetEffect>("conjunto_efeito.{0}").Where(k => !k.EndsWith("None")))
				.Where(key => !Has(key));
		}
	}
}
