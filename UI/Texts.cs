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
	/// mais a marca [glyph=Nome], que o <see cref="Components.RichText"/> desenha; em dica de mouse,
	/// use <see cref="Plain"/>.
	/// </summary>
	public static class Texts
	{
		public static string Name(Element element) => T($"element.{element}");
		public static string Name(Role role) => T($"role.{role}");
		public static string Name(Stat stat) => T($"stat.{stat}");
		/// <summary>O que o atributo faz. {0} é a Defesa que corta o dano pela metade; {1}, a Resistência mínima.</summary>
		public static string Explain(Stat stat) => T($"stat_info.{stat}", Math.Round(BattleRules.DefenseConstant), Percent(BattleRules.MinResistChance));
		public static string Name(RuneStat stat) => T($"rune_stat.{stat}");
		public static string Name(RuneSet set) => T($"set.{set}");
		public static string Name(Glyph glyph) => T($"glyph.{glyph}.name");
		public static string Meaning(Glyph glyph) => T($"glyph.{glyph}.meaning");
		public static string Name(RuneRarity rarity) => T($"rarity.{rarity}");
		public static string Name(StatusKind status) => T($"effect.{status}.name");
		public static string Short(StatusKind status) => T($"effect.{status}.short");
		public static string Name(DungeonKind kind) => T($"dungeons.kind.{kind}");
		public static string Name(RuneSort sort) => T($"filter.order.{sort}");

		/// <summary>"30 Mana", "10 Pergaminhos", "1 Pergaminho".</summary>
		public static string Amount(ShopItem item, int amount) => item == ShopItem.Scrolls ? Scrolls(amount) : T($"shop.item.{item}", amount);

		/// <summary>Por que a luta não começou, com o custo em Mana dela.</summary>
		public static string Refusal(EntryProblem problem, int mana) => T($"entry.{problem}", mana, Core.Player.RuneInventory.Capacity);

		public static string Explain(StatusKind status) => status switch
		{
			StatusKind.Burn => T("effect.Burn.info", Percent(BattleRules.BurnFraction), BattleRules.MaxBurnStacks),
			StatusKind.Curse => T("effect.Curse.info", Percent(BattleRules.CurseBonus)),
			StatusKind.Blind => T("effect.Blind.info", Percent(BattleRules.BlindMissChance)),
			StatusKind.AttackUp => T("effect.AttackUp.info", Percent(BattleRules.AttackUpBonus)),
			StatusKind.AttackDown => T("effect.AttackDown.info", Percent(BattleRules.AttackDownPenalty)),
			StatusKind.DefenseUp => T("effect.DefenseUp.info", Percent(BattleRules.DefenseUpBonus)),
			StatusKind.SpeedUp => T("effect.SpeedUp.info", Percent(BattleRules.SpeedUpBonus)),
			_ => T($"effect.{status}.info"),
		};

		/// <summary>"Ataque" e "Ataque%": separa o fixo do percentual.</summary>
		public static string Label(RuneStat stat) => stat is RuneStat.HealthPercent or RuneStat.AttackPercent or RuneStat.DefensePercent
			? T("rune_stat.percent", Name(stat))
			: Name(stat);

		/// <summary>"Ataque +12%" ou "Ataque +110".</summary>
		public static string Format(RuneStat stat, double value) => $"{Name(stat)} {Amount(stat, value)}";

		/// <summary>Subatributo com o que a Pedra de Afiar somou: "Ataque +12% (+5% afiado)".</summary>
		public static string Format(RuneSubstat substat)
		{
			var text = Format(substat.Stat, substat.Total);
			return substat.Grind > 0 ? T("rune.ground", text, Amount(substat.Stat, substat.Grind)) : text;
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
		public static string Title(Rune rune) => T("rune.title", Name(rune.Set), rune.Slot);

		/// <summary>"Pedra de Afiar Heroica · Ataque%".</summary>
		public static string Name(RuneTool tool) => T($"tool.{tool.Kind}", Name(tool.Grade), Label(tool.Stat));

		/// <summary>O que a pedra dá: "+4% a +7%".</summary>
		public static string Range(RuneTool tool)
		{
			var (min, max) = tool.Kind == RuneToolKind.Grindstone
				? RuneRules.GrindRange(tool.Stat, tool.Grade) ?? (0, 0)
				: RuneRules.GemRange(tool.Stat, tool.Grade);
			return T("tool.range", Amount(tool.Stat, min), Amount(tool.Stat, max));
		}

		/// <summary>O bônus do Despertar: "+15 de Velocidade" ou "+25% de Precisão".</summary>
		public static string AwakeningBonus(Stat stat)
		{
			var value = Awakening.Bonus(stat);
			return T("awaken.bonus", StatBlock.IsAbsolute(stat) ? string.Format(Culture, "+{0:0}", value) : $"+{Percent(value)}", Name(stat));
		}

		public static string Stars(int count) => new('★', count);

		public static string Scrolls(int count) => count == 1 ? T("currency.scroll") : T("currency.scrolls", count);

		public static string Turns(int turns) => turns == 1 ? T("turns.one") : T("turns.many", turns);

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
			$"{(glyph is { } g ? $"[glyph={g}]" : "")}[color=#{Palette.Gold.ToHtml(false)}]{text}[/color]";

		public static string Term(StatusKind status) => Term(Name(status), GlyphOf(status));

		public static string Term(RuneSet set) => Term(Name(set), RuneSets.For(set).Glyph);

		public static string Impeto => Term(T("term.impetus"), RuneSets.For(RuneSet.Nemesis).Glyph);


		/// <summary>Tira BBCode e Glifos: para dica de mouse, que é texto puro.</summary>
		public static string Plain(string rich) => System.Text.RegularExpressions.Regex.Replace(rich, @"\[[^\]]*\]", "");

		// Descrições geradas -------------------------------------------------------------------------

		/// <summary>"4 peças: +25% de Velocidade".</summary>
		public static string Describe(RuneSetDefinition set)
		{
			var value = Percent(set.Value);
			var bonus = set.Effect switch
			{
				RuneSetEffect.Drain => T("set_effect.Drain", value),
				RuneSetEffect.Stun => T("set_effect.Stun", value, Term(StatusKind.Stun)),
				RuneSetEffect.ExtraTurn => T("set_effect.ExtraTurn", value),
				RuneSetEffect.AllyShield => T("set_effect.AllyShield", Term(StatusKind.Shield), value, RuneSets.ShieldTurns),
				RuneSetEffect.Immunity => T("set_effect.Immunity", Term(StatusKind.Immunity), Turns((int)set.Value)),
				RuneSetEffect.Counter => T("set_effect.Counter", value, Percent(RuneSets.CounterDamage)),
				RuneSetEffect.Nemesis => T("set_effect.Nemesis", value, Impeto, Percent(RuneSets.NemesisStep)),
				RuneSetEffect.Destroy => T("set_effect.Destroy", Percent(RuneSets.DestroyShare), value, Percent(RuneSets.DestroyLimit)),
				_ => T("set_effect.Stat", value, Name(set.Stat ?? Stat.Health)),
			};
			return T("set_effect.pieces", set.Pieces, bonus);
		}

		public static string Describe(PassiveDefinition passive, bool awakened)
		{
			var value = Percent(passive.ValueFor(awakened));
			return passive.Kind switch
			{
				PassiveKind.ShieldOnDeath => T("passive.ShieldOnDeath", Term(StatusKind.Shield), value),
				PassiveKind.ImpetoAtWaveStart => T("passive.ImpetoAtWaveStart", value, Impeto),
				PassiveKind.BurnOnHit => T("passive.BurnOnHit", value, Term(StatusKind.Burn)),
				PassiveKind.BonusVsWounded => T("passive.BonusVsWounded", value, Percent(BattleRules.WoundedFraction)),
				_ => T($"passive.{passive.Kind}", value),
			};
		}

		/// <summary>
		/// O que a habilidade faz, desperta ou não. Sem despertar, a versão do Despertar (quando existe)
		/// aparece numa linha à parte, para o jogador saber o que vai ganhar.
		/// </summary>
		public static string Describe(SkillDefinition skill, bool awakened = false)
		{
			if (skill.Passive is { } passive)
			{
				var text = Describe(passive, awakened);
				return !awakened && passive.AwakenedValue > 0 ? $"{text}\n{T("skill.awakened", Describe(passive, true))}" : text;
			}

			if (awakened && skill.AwakenedEffects.Count > 0)
				return Describe(skill.AwakenedEffects);
			var basic = Describe(skill.Effects);
			return skill.AwakenedEffects.Count > 0 ? $"{basic}\n{T("skill.awakened", Describe(skill.AwakenedEffects))}" : basic;
		}

		/// <summary>"Habilidade 2 · Selo Rompido (recarga 3) · Nv 1/4", "Passiva · Das Cinzas".</summary>
		public static string SkillHeader(SkillDefinition skill, int index, int level, bool awakened)
		{
			var header = skill.IsPassive ? T("skill.header_passive", skill.Name) : T("skill.header", index + 1, skill.Name);
			if (!skill.IsPassive && skill.Cooldown > 0)
				header += " " + T("skill.cooldown", skill.At(level, awakened).Cooldown);
			if (skill.MaxLevel > 1)
				header += " · " + T("skill.level", level, skill.MaxLevel);
			return header;
		}

		/// <summary>O que o Despertar dá além de Vida, Ataque e Defesa: atributo, habilidade nova ou habilidade melhorada.</summary>
		public static string AwakeningGain(SummonDefinition summon)
		{
			var gains = new List<string>();
			if (summon.Awakening.Stat is { } stat)
				gains.Add(AwakeningBonus(stat));
			if (summon.Awakening.Skill is { } skill)
				gains.Add(T("awaken.new_skill", skill.Name));
			var improved = summon.Skills.Where(s => s.ChangesOnAwakening).Select(s => s.Name).ToList();
			if (improved.Count > 0)
				gains.Add(T("awaken.improves", string.Join(T("common.and"), improved)));
			return string.Join(T("common.and"), gains);
		}

		/// <summary>"Nv.2 Dano +10% · Nv.3 Recarga −1": os níveis que faltam a partir de <paramref name="level"/>.</summary>
		public static string LevelUps(SkillDefinition skill, int level) => string.Join(" · ", skill.Levels
			.Select((up, i) => (Level: i + 2, Up: up))
			.Where(x => x.Level > level)
			.Select(x => T("skill.level_line", x.Level, LevelUp(x.Up))));

		public static string LevelUp(SkillLevelUp up) => up.Kind == SkillLevelKind.Cooldown
			? T("skill.level_up.Cooldown", (int)up.Value)
			: T($"skill.level_up.{up.Kind}", Percent(up.Value));

		public static string Describe(IEnumerable<EffectDefinition> effects) => string.Join("; ", effects.Select(Describe)) + ".";

		private static string Describe(EffectDefinition effect)
		{
			var where = T($"target.{effect.Target}");
			var chance = effect.Chance < 1 ? T("skill.chance", Percent(effect.Chance)) : "";
			var text = effect.Kind switch
			{
				EffectKind.Damage => DamageText(effect, where),
				EffectKind.Heal => T("skill.heal", Percent(effect.Power), where),
				EffectKind.Shield => T("skill.shield", Term(StatusKind.Shield), Percent(effect.Power), where, Turns(effect.Turns)),
				EffectKind.Status => T("skill.effect", chance, Term(effect.Status), where, Turns(effect.Turns)),
				EffectKind.Impeto => T("skill.impetus", effect.Power >= 0 ? "+" : "−", Math.Abs(effect.Power), Impeto, where),
				EffectKind.Cleanse => T("skill.cleanse", where),
				_ => effect.Kind.ToString(),
			};
			return effect.OnKill ? T("skill.on_kill", text) : text;
		}

		private static string DamageText(EffectDefinition effect, string where)
		{
			var hits = effect.Hits > 1 ? T("skill.hits", effect.Hits) : "";
			var text = T("skill.damage", hits, Percent(effect.Power), where);
			if (effect.IgnoreDefense > 0)
				text += T("skill.ignores", Percent(effect.IgnoreDefense));
			if (effect.Drain > 0)
				text += T("skill.drain", Percent(effect.Drain));
			return text;
		}

		/// <summary>Chaves que vêm de enum e faltam no arquivo de textos: o GameEntry avisa no console.</summary>
		public static IEnumerable<string> MissingEnumKeys()
		{
			IEnumerable<string> Keys<TEnum>(string format) where TEnum : struct, Enum =>
				Enum.GetValues<TEnum>().Select(value => string.Format(format, value));

			return Keys<Element>("element.{0}")
				.Concat(Keys<Role>("role.{0}"))
				.Concat(Keys<Stat>("stat.{0}"))
				.Concat(Keys<Stat>("stat_info.{0}"))
				.Concat(Keys<RuneStat>("rune_stat.{0}"))
				.Concat(Keys<RuneSet>("set.{0}"))
				.Concat(Keys<Glyph>("glyph.{0}.name"))
				.Concat(Keys<Glyph>("glyph.{0}.meaning"))
				.Concat(Keys<RuneRarity>("rarity.{0}"))
				.Concat(Keys<StatusKind>("effect.{0}.name"))
				.Concat(Keys<StatusKind>("effect.{0}.short"))
				.Concat(Keys<StatusKind>("effect.{0}.info"))
				.Concat(Keys<TargetKind>("target.{0}"))
				.Concat(Keys<PassiveKind>("passive.{0}"))
				.Concat(Keys<SkillLevelKind>("skill.level_up.{0}"))
				.Concat(Keys<RuneToolKind>("tool.{0}"))
				.Concat(Keys<DungeonKind>("dungeons.kind.{0}"))
				.Concat(Keys<RuneSort>("filter.order.{0}"))
				.Concat(Keys<ShopItem>("shop.item.{0}"))
				.Concat(Keys<EntryProblem>("entry.{0}").Where(k => !k.EndsWith("None")))
				.Concat(Keys<RuneSetEffect>("set_effect.{0}").Where(k => !k.EndsWith("None")))
				.Where(key => !Has(key));
		}
	}
}
