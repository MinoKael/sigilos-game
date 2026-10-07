using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.Core.Social;
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

		/// <summary>A sigla que cabe num sigilo: "HP%", "ATK", "SPD".</summary>
		public static string Short(RuneStat stat) => T($"rune_stat.short.{stat}");
		public static string Name(RuneSet set) => T($"set.{set}");
		public static string Name(Glyph glyph) => T($"glyph.{glyph}.name");
		public static string Meaning(Glyph glyph) => T($"glyph.{glyph}.meaning");
		public static string Name(RuneRarity rarity) => T($"rarity.{rarity}");
		public static string Name(StatusKind status) => T($"effect.{status}.name");
		public static string Name(DungeonKind kind) => T($"dungeons.kind.{kind}");
		public static string Name(Hemisphere hemisphere) => T($"exploration.hemisphere.{hemisphere}");

		/// <summary>Em quem uma regra da Influência vale: "Inimigos", "Guardião", "Seu time", "Todos".</summary>
		public static string Name(InfluenceSide side) => T($"exploration.side.{side}");
		public static string Name(RuneSort sort) => T($"filter.order.{sort}");
		public static string Name(MonsterSort sort) => T($"filter.monster_order.{sort}");

		/// <summary>O feito do Chat global numa frase: "Fulano conseguiu um novo monstro 5★!".</summary>
		public static string Describe(Feat feat, string by) => T(feat is RuneFeat ? "chat.feat_rune" : "chat.feat_summon", by);

		/// <summary>"30 Mana", "10 Pergaminhos", "1 Pergaminho".</summary>
		public static string Amount(ShopItem item, int amount) => item == ShopItem.Scrolls ? Scrolls(amount) : T($"shop.item.{item}", Number(amount));

		/// <summary>Por que a luta não começou, com o custo em Mana dela.</summary>
		public static string Refusal(EntryProblem problem, int mana) => T($"entry.{problem}", mana, Core.Player.RuneInventory.Capacity);

		public static string Explain(StatusKind status) => status switch
		{
			StatusKind.Affliction => T("effect.Affliction.info", Percent(BattleRules.AfflictionFraction), BattleRules.MaxStatuses),
			StatusKind.Curse => T("effect.Curse.info", Percent(BattleRules.CurseBonus)),
			StatusKind.Blind => T("effect.Blind.info", Percent(BattleRules.BlindMissChance)),
			StatusKind.AttackUp => T("effect.AttackUp.info", Percent(BattleRules.AttackUpBonus)),
			StatusKind.AttackDown => T("effect.AttackDown.info", Percent(BattleRules.AttackDownPenalty)),
			StatusKind.DefenseUp => T("effect.DefenseUp.info", Percent(BattleRules.DefenseUpBonus)),
			StatusKind.SpeedUp => T("effect.SpeedUp.info", Percent(BattleRules.SpeedUpBonus)),
			StatusKind.DefenseBreak => T("effect.DefenseBreak.info", Percent(BattleRules.DefenseDownPenalty)),
			StatusKind.Bomb => T("effect.Bomb.info", Percent(BattleRules.BombDamageMultiplier)),
			StatusKind.SpeedDown => T("effect.SpeedDown.info", Percent(BattleRules.SpeedDownPenalty)),
			StatusKind.CritUp => T("effect.CritUp.info", Percent(BattleRules.CritUpBonus)),
			StatusKind.CritResist => T("effect.CritResist.info", Percent(1 - BattleRules.CritResistFactor)),
			StatusKind.Blessing => T("effect.Blessing.info", Percent(BattleRules.BlessingFraction)),
			StatusKind.Counter => T("effect.Counter.info", Percent(RuneSets.CounterDamage)),
			StatusKind.Revive => T("effect.Revive.info", Percent(BattleRules.ReviveHealth)),
            _ => T($"effect.{status}.info"),
		};

		/// <summary>"Ataque" e "Ataque%": separa o fixo do percentual.</summary>
		public static string Label(RuneStat stat) => stat is RuneStat.HealthPercent or RuneStat.AttackPercent or RuneStat.DefensePercent
			? T("rune_stat.percent", Name(stat))
			: Name(stat);

		/// <summary>"Ataque +12%" ou "Ataque +110".</summary>
		public static string Format(RuneStat stat, double value) => $"{Name(stat)} {Amount(stat, value)}";

		/// <summary>Subatributo numa linha só, com o que a Pedra de Afiar somou: "Ataque +12% (+5% afiado)".</summary>
		public static string Format(RuneSubstat substat)
		{
			var text = Format(substat.Stat, substat.Total);
			return substat.Grind > 0 ? $"{text} {T("rune.ground", Amount(substat.Stat, substat.Grind))}" : text;
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

		/// <summary>
		/// O que o Despertar muda nos atributos desta variante, um item por atributo que muda. Vida,
		/// Ataque e Defesa em porcentagem (vale em qualquer estrela e nível); Velocidade em número;
		/// Crítico, Resistência e Precisão em pontos. O bônus de atributo da variante já vem somado.
		/// </summary>
		public static IEnumerable<(Stat Stat, string Text)> AwakeningStats(SummonDefinition summon)
		{
			var after = Awakening.Apply(summon.AwakenedStats, summon.Awakening);
			foreach (var stat in Enum.GetValues<Stat>())
			{
				var from = summon.Stats.Get(stat);
				var gain = after.Get(stat) - from;
				if (Math.Abs(gain) < 1e-9)
					continue;

				var sign = gain > 0 ? "+" : "−";
				var amount = Math.Abs(gain);
				var text = stat == Stat.Speed ? string.Format(Culture, "{0:0}", amount)
					: StatBlock.IsAbsolute(stat) ? Percent(from > 0 ? amount / from : 0)
					: Percent(amount);
				yield return (stat, sign + text);
			}
		}

		public static string Stars(int count) => new('★', count);

		/// <summary>A letra que desenha o Glifo na fonte das runas (<see cref="Style.GameTheme.Runes"/>).</summary>
		public static string Rune(Glyph glyph) => glyph switch
		{
			Glyph.Uruz => "U",
			Glyph.Raido => "R",
			Glyph.Algiz => "Z",
			Glyph.Othalan => "O",
			Glyph.Kauna => "K",
			Glyph.Pertho => "P",
			Glyph.Jeran => "J",
			Glyph.Thurisaz => "Þ",
			Glyph.Gebo => "G",
			Glyph.Dagaz => "D",
			Glyph.Sowilo => "S",
			Glyph.Tiwaz => "T",
			Glyph.Iwaz => "Y",
			Glyph.Naudiz => "N",
			Glyph.Laukaz => "L",
			_ => "H",
		};

		/// <summary>Número romano pequeno: ondas e habilidades (I, II, III).</summary>
		public static string Roman(int number) => number switch
		{
			1 => "I",
			2 => "II",
			3 => "III",
			4 => "IV",
			5 => "V",
			_ => number.ToString(Culture),
		};

		public static string Scrolls(int count) => count == 1 ? T("currency.scroll") : T("currency.scrolls", Number(count));

		public static string Turns(int turns) => turns == 1 ? T("turns.one") : T("turns.many", turns);

		public static string Percent(double fraction) => string.Format(Culture, "{0:0.#}%", fraction * 100);

		/// <summary>
		/// O número com o separador de milhar do idioma: 12345 → 12.345 (12,345 em inglês). Só de um milhão para
		/// cima abrevia, em milhões com três casas, cortando o resto (nunca mostra mais do que o jogador tem):
		/// 1167890 → 1,167M (1.167M em inglês).
		/// </summary>
		public static string Number(int value) => value is >= Million or <= -Million
			? $"{(value / 1000 / 1000m).ToString("N3", Culture)}M"
			: value.ToString("N0", Culture);

		private const int Million = 1_000_000;

		// Termos e Glifos --------------------------------------------------------------------------

		/// <summary>O Glifo que quer dizer este atributo: o do conjunto que o aumenta.</summary>
		public static Glyph GlyphOf(Stat stat) => RuneSets.All.First(s => s.Stat == stat).Glyph;

		/// <summary>O Glifo de um atributo de runa: o do atributo que ele soma (fixo ou percentual).</summary>
		public static Glyph GlyphOf(RuneStat stat) => GlyphOf(stat switch
		{
			RuneStat.HealthFlat or RuneStat.HealthPercent => Stat.Health,
			RuneStat.AttackFlat or RuneStat.AttackPercent => Stat.Attack,
			RuneStat.DefenseFlat or RuneStat.DefensePercent => Stat.Defense,
			RuneStat.Speed => Stat.Speed,
			RuneStat.Crit => Stat.Crit,
			RuneStat.CritDamage => Stat.CritDamage,
			RuneStat.Resistance => Stat.Resistance,
			_ => Stat.Accuracy,
		});

		/// <summary>Um termo único do jogo, em dourado, com o Glifo na frente quando tem.</summary>
		public static string Term(string text, Glyph? glyph = null) =>
			$"{(glyph is { } g ? $"[glyph={g}]" : "")}[color=#{Palette.Gold.ToHtml(false)}]{text}[/color]";

		/// <summary>Um efeito de batalha em dourado, com o símbolo dele na frente ([effect=Nome]).</summary>
		public static string Term(StatusKind status) => $"[effect={status}][color=#{Palette.Gold.ToHtml(false)}]{Name(status)}[/color]";

		public static string Term(RuneSet set) => Name(set);

		public static string Impeto => Term(T("term.impetus"), RuneSets.For(RuneSet.Bane).Glyph);


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
				RuneSetEffect.Bane => T("set_effect.Bane", value, Impeto, Percent(RuneSets.BaneStep)),
				RuneSetEffect.Oblivion => T("set_effect.Oblivion", Percent(RuneSets.DestroyShare), value, Percent(RuneSets.DestroyLimit)),
				_ => T("set_effect.Stat", value, Name(set.Stat ?? Stat.Health)),
			};
			return T("set_effect.pieces", set.Pieces, bonus);
		}

		/// <param name="effects">Os efeitos da genérica (nulo: os da definição, já prontos na luta).</param>
		public static string Describe(PassiveDefinition passive, bool awakened, IReadOnlyList<EffectDefinition>? effects = null)
		{
			var value = Percent(passive.ValueFor(awakened));
			// O número do Rei Ossudo é a Vida com que ele volta, não a chance de disparar.
			if (passive.Kind == PassiveKind.Undying)
				return Sentence(T("passive.Undying", Clause(effects ?? passive.Effects), value, Impeto, Term(StatusKind.Oblivion)));

			if (passive.UsesEffects)
			{
				// "No começo de cada turno dele: cura 5% da Vida máxima em si (50% de chance)."
				var text = T($"passive.{passive.Kind}", Clause(effects ?? passive.Effects), Counted(passive));
				return Sentence((passive.ValueFor(awakened) > 0 ? T("passive.chance", text, value) : text) + ".");
			}

			return passive.Kind switch
			{
				PassiveKind.ImpetoAtWaveStart => T("passive.ImpetoAtWaveStart", value, Impeto),
				PassiveKind.BonusVsWounded => T("passive.BonusVsWounded", value, Percent(BattleRules.WoundedFraction)),
				PassiveKind.BonusVsStatusOrEffect => T("passive.BonusVsStatusOrEffect", value, Counted(passive)),
				_ => T($"passive.{passive.Kind}", value),
			};
		}

		/// <summary>Os efeitos que a Passiva olha: os escolhidos ("Aflição ou Maldição") ou os do escopo ("efeito negativo").</summary>
		private static string Counted(PassiveDefinition passive) => passive.Statuses.Count > 0
			? string.Join(T("common.or"), passive.Statuses.Select(s => Term(s)))
			: T($"scope.{passive.Scope}.one");

		/// <summary>
		/// O que a habilidade faz, desperta ou não. Sem despertar, a versão do Despertar (quando existe)
		/// aparece numa linha à parte, para o jogador saber o que vai ganhar.
		/// </summary>
		public static string Describe(SkillDefinition skill, bool awakened = false)
		{
			if (skill.Passive is { } passive)
			{
				var effects = awakened && skill.AwakenedEffects.Count > 0 ? skill.AwakenedEffects : skill.Effects;
				var text = Describe(passive, awakened, effects);
				var changes = passive.AwakenedValue > 0 || skill.AwakenedEffects.Count > 0;
				return !awakened && changes ? $"{text}\n{T("skill.awakened", Describe(passive, true, skill.AwakenedEffects.Count > 0 ? skill.AwakenedEffects : skill.Effects))}" : text;
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

		public static string Describe(IEnumerable<EffectDefinition> effects) => Sentence(Clause(effects) + ".");

		/// <summary>Os efeitos numa frase só, sem maiúscula nem ponto ("cura 5%…; põe Aflição…").</summary>
		private static string Clause(IEnumerable<EffectDefinition> effects) => string.Join("; ", effects.Select(Describe));

		/// <summary>A frase começa com maiúscula ("remove um efeito…" vira "Remove um efeito…").</summary>
		private static string Sentence(string text) =>
			text.Length > 0 && char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;

		private static string Describe(EffectDefinition effect)
		{
			var where = Where(effect.Target, effect.By);
			var chance = effect.Chance < 1 ? T("skill.chance", Percent(effect.Chance)) : "";
			var text = effect.Kind switch
			{
				EffectKind.Damage => DamageText(effect, where),
				EffectKind.Heal => T("skill.heal", Percent(effect.Power), where),
				EffectKind.Shield => T("skill.shield", Term(StatusKind.Shield), Percent(effect.Power), where, Turns(effect.Turns)),
				EffectKind.Status => T("skill.effect", chance, Term(effect.Status), where, Turns(effect.Turns)),
				EffectKind.Impeto => T("skill.impetus", effect.Power >= 0 ? "+" : "−", Math.Abs(effect.Power), Impeto, where),
				EffectKind.Cleanse => T("skill.cleanse", where),
				EffectKind.StealBuff => StealText(effect, chance, where),
				EffectKind.BonusPerStatus => BonusText(effect, where),
				EffectKind.ChangeDuration => T(effect.Turns >= 0 ? "skill.prolong" : "skill.shorten", chance, Turns(Math.Abs(effect.Turns)), Statuses(effect, many: true), where),
				EffectKind.EqualizeHealth => T("skill.equalize", where),
				EffectKind.HealTeam => effect.Count > 0 ? T("skill.heal_team", Percent(effect.Power), effect.Count, where) : T("skill.heal", Percent(effect.Power), where),
				EffectKind.JointAttack => effect.Target != TargetKind.AllAllies ? T("skill.joint_one", Ally(effect.Target, effect.By))
					: effect.Count > 0 ? T("skill.joint", effect.Count) : T("skill.joint_all"),
				EffectKind.ExtraTurnOnKill => T("skill.extra_turn_on_kill", Turns(effect.Turns)),
				_ => effect.Kind.ToString(),
			};
			return effect.OnKill ? T("skill.on_kill", text) : text;
		}

		/// <summary>"rouba um efeito positivo do alvo; sem nenhum, rouba 20% de Ímpeto".</summary>
		private static string StealText(EffectDefinition effect, string chance, string where)
		{
			var what = effect.OnlyStatus is { } only ? Term(only) : T("scope.Buffs.one");
			var text = T("skill.steal", chance, what, where);
			if (effect.Fallback)
				text += effect.Power > 0 ? T("skill.steal_impetus", Math.Abs(effect.Power), Impeto) : T("skill.steal_all_impetus", Impeto);
			return text;
		}

		/// <summary>"+20% de dano nos golpes seguintes para cada efeito negativo no alvo".</summary>
		private static string BonusText(EffectDefinition effect, string where)
		{
			var bonus = effect.Bonus switch
			{
				BonusKind.Damage => T("skill.bonus.Damage", Percent(effect.Power)),
				BonusKind.Heal => T("skill.bonus.Heal", Percent(effect.Power)),
				_ => T("skill.bonus.Impeto", effect.Power, Impeto, where),
			};
			return T("skill.per_status", bonus, Statuses(effect, many: false), Where(effect.From, effect.By));
		}

		/// <summary>"no aliado com mais Ataque": o alvo, com o valor que escolhe o aliado quando não é a Vida.</summary>
		private static string Where(TargetKind kind, TargetRank by) => Ranked(kind, by) ? T($"target.{kind}_by", Name(by)) : T($"target.{kind}");

		/// <summary>"o aliado com mais Ataque": quem o ataque conjunto chama.</summary>
		private static string Ally(TargetKind kind, TargetRank by) => Ranked(kind, by) ? T($"target.ally.{kind}_by", Name(by)) : T($"target.ally.{kind}");

		private static bool Ranked(TargetKind kind, TargetRank by) => by != TargetRank.Health && kind is TargetKind.LowestAlly or TargetKind.HighestAlly;

		/// <summary>O nome do valor que LowestAlly e HighestAlly comparam.</summary>
		public static string Name(TargetRank by) => Targeting.StatOf(by) is { } stat ? Name(stat) : Impeto;

		/// <summary>Os efeitos que o efeito olha: o escolhido (<see cref="EffectDefinition.OnlyStatus"/>) ou os do escopo.</summary>
		private static string Statuses(EffectDefinition effect, bool many) =>
			effect.OnlyStatus is { } only ? Term(only) : T($"scope.{effect.Scope}.{(many ? "many" : "one")}");

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
				.Concat(Keys<RuneStat>("rune_stat.short.{0}"))
				.Concat(Keys<RuneSet>("set.{0}"))
				.Concat(Keys<Glyph>("glyph.{0}.name"))
				.Concat(Keys<Glyph>("glyph.{0}.meaning"))
				.Concat(Keys<RuneRarity>("rarity.{0}"))
				.Concat(Keys<StatusKind>("effect.{0}.name"))
				.Concat(Keys<StatusKind>("effect.{0}.info"))
				.Concat(Keys<TargetKind>("target.{0}"))
				.Concat(Keys<PassiveKind>("passive.{0}"))
				.Concat(Keys<SkillLevelKind>("skill.level_up.{0}"))
				.Concat(Keys<RuneToolKind>("tool.{0}"))
				.Concat(Keys<DungeonKind>("dungeons.kind.{0}"))
				.Concat(Keys<Hemisphere>("exploration.hemisphere.{0}"))
				.Concat(Keys<InfluenceSide>("exploration.side.{0}"))
				.Concat(Keys<RuneSort>("filter.order.{0}"))
				.Concat(Keys<MonsterSort>("filter.monster_order.{0}").Where(k => !k.EndsWith(".Stat")))
				.Concat(Keys<MonsterCondition>("filter.condition_kind.{0}"))
				.Concat(Keys<ShopItem>("shop.item.{0}"))
				.Concat(Keys<EntryProblem>("entry.{0}").Where(k => !k.EndsWith("None")))
				.Concat(Keys<RuneSetEffect>("set_effect.{0}").Where(k => !k.EndsWith("None")))
				.Concat(Keys<DefeatAdvice.Kind>("battle.advice.{0}"))
				.Where(key => !Has(key));
		}
	}
}
