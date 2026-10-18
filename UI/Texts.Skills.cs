using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Battle.Statuses;
using Sigilos.Core.Content;
using static Sigilos.UI.Locale;

namespace Sigilos.UI
{
	/// <summary>
	/// A leitura das habilidades: os efeitos do JSON viram frases ("Ataca o alvo 3 vezes (310% do Ataque por golpe) e
	/// aplica Aflição no alvo por 2 turnos com 40% de chance."). A gramática mora aqui e as palavras em
	/// <c>skill.say.*</c>; nada depende do nome de uma habilidade nem de um efeito específico: a frase sai do tipo de
	/// efeito, do alvo, da chance, dos turnos e de quando dispara (<see cref="EffectDefinition.When"/>).
	///
	/// Como se lê:
	/// - cada tipo de efeito tem uma forma fixa de frase, para que a leitura um dia possa ser desfeita (texto → JSON);
	/// - efeitos seguidos no mesmo alvo viram uma frase só ("e"), efeitos iguais em turnos e chance se juntam
	///   ("Aflição e Maldição por 2 turnos com 50% de chance cada");
	/// - depois de um ataque, o alvo dele não se repete ("Ataca o alvo ... e aplica Cegueira por 2 turnos");
	/// - o que dispara a cada golpe, crítico ou efeito causado vem em frase à parte ("Em cada golpe, ...").
	/// </summary>
	public static partial class Texts
	{
		/// <summary>Como o alvo entra na frase: objeto ("ataca o alvo"), destino, onde cai, origem e posse.</summary>
		private enum Form { Obj, To, On, From, Of }

		/// <summary>O alvo do ataque da frase: não se repete nos efeitos que vêm junto dele.</summary>
		private readonly record struct Subject(TargetKind Kind, TargetRank By);

		/// <summary>A descrição da habilidade em frases completas (maiúscula e ponto).</summary>
		public static string Describe(IEnumerable<EffectDefinition> effects) =>
			string.Join(" ", Sentences(effects.ToList()).Select(s => Sentence(s) + "."));

		/// <summary>Os efeitos num trecho só, sem maiúscula nem ponto ("cura a si em 5% da Vida; ..."): para dentro de outra frase.</summary>
		private static string Clause(IEnumerable<EffectDefinition> effects) => string.Join("; ", Sentences(effects.ToList()));

		/// <summary>A frase começa com maiúscula ("remove um efeito…" vira "Remove um efeito…").</summary>
		private static string Sentence(string text) =>
			text.Length > 0 && char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;

		/// <summary>O nome do valor que LowestAlly e HighestAlly comparam.</summary>
		public static string Name(TargetRank by) => Targeting.StatOf(by) is { } stat ? Name(stat) : Impeto;

		/// <summary>As frases da habilidade: a do que acontece ao usar e, depois, uma por gatilho (golpe, crítico, efeito causado).</summary>
		private static List<string> Sentences(IReadOnlyList<EffectDefinition> effects)
		{
			var sentences = new List<string>();
			Subject? none = null;
			Flow(Compose(effects.Where(e => e.When == HitTrigger.Skill).ToList(), none, false), sentences);

			// Cada gatilho vira uma frase; o "alvo" dos efeitos é quem o golpe acertou.
			foreach (var group in effects.Where(e => e.When != HitTrigger.Skill).GroupBy(e => (e.When, e.WhenStatus, e.WhenScope)))
			{
				var body = And(Compose(group.ToList(), none, false).Select(a => a.Text).ToList());
				var (when, status, scope) = group.Key;
				sentences.Add(when switch
				{
					HitTrigger.Hit => T("skill.say.on_hit", body),
					HitTrigger.Crit => T("skill.say.on_crit", body),
					_ => T("skill.say.on_status", body, status is { } s ? Term(s) : T($"scope.{scope ?? StatusScope.All}.any")),
				});
			}
			return sentences;
		}

		/// <summary>Junta os trechos em frases: um segundo ataque abre frase nova, e o que depende de derrubar o alvo vai à parte.</summary>
		private static void Flow(List<(string Text, bool Attack, bool Apart)> atoms, List<string> sentences)
		{
			var open = new List<string>();
			var attacked = false;
			void Close()
			{
				if (open.Count > 0)
					sentences.Add(And(open));
				open = new List<string>();
				attacked = false;
			}

			foreach (var atom in atoms)
			{
				if (atom.Apart || (atom.Attack && attacked))
					Close();
				open.Add(atom.Text);
				attacked |= atom.Attack;
				if (atom.Apart)
					Close();
			}
			Close();
		}

		/// <summary>"a", "a e b", "a, b e c".</summary>
		private static string And(IReadOnlyList<string> items) => items.Count switch
		{
			0 => "",
			1 => items[0],
			_ => string.Join(", ", items.Take(items.Count - 1)) + T("common.and") + items[^1],
		};

		/// <summary>
		/// Os trechos da lista de efeitos, na ordem. Efeitos de estado seguidos no mesmo alvo viram um trecho só.
		/// <paramref name="subject"/> é o alvo do ataque que veio antes (não se repete);
		/// <paramref name="insideKill"/> diz que já estamos dentro do "se derrubar o alvo".
		/// </summary>
		private static List<(string Text, bool Attack, bool Apart)> Compose(IReadOnlyList<EffectDefinition> list, Subject? subject, bool insideKill)
		{
			var atoms = new List<(string Text, bool Attack, bool Apart)>();
			for (var i = 0; i < list.Count;)
			{
				var effect = list[i];
				if (effect.OnKill && !insideKill)
				{
					var end = i;
					while (end < list.Count && list[end].OnKill)
						end++;
					var inner = Compose(list.Skip(i).Take(end - i).ToList(), subject, true);
					atoms.Add((T("skill.say.on_kill", And(inner.Select(a => a.Text).ToList())), false, true));
					i = end;
					continue;
				}

				if (effect.Kind is EffectKind.Status or EffectKind.Shield)
				{
					var end = i + 1;
					while (end < list.Count && SameRun(effect, list[end]))
						end++;
					atoms.Add((RunText(list.Skip(i).Take(end - i).ToList(), subject), false, false));
					i = end;
					continue;
				}

				var attack = effect.Kind == EffectKind.Damage;
				atoms.Add((EffectText(effect, subject), attack, effect.Kind == EffectKind.ExtraTurnOnKill));
				subject = attack ? Spoken(effect) : subject;
				i++;
			}
			return atoms;
		}

		private static Subject? Spoken(EffectDefinition effect) => new Subject(effect.Target, effect.By);

		/// <summary>O alvo já foi dito pelo trecho anterior (e não é "a si", que sempre se diz).</summary>
		private static bool Said(EffectDefinition effect, Subject? subject) =>
			effect.Target != TargetKind.Self && subject is { } said && said.Kind == effect.Target && said.By == effect.By;

		/// <summary>" no alvo" (com o espaço), ou nada quando o trecho anterior já falou dele.</summary>
		private static string Where(EffectDefinition effect, Form form, Subject? subject) =>
			Said(effect, subject) ? "" : " " + Who(effect.Target, effect.By, form);

		private static string Who(TargetKind kind, TargetRank by, Form form)
		{
			var side = form.ToString().ToLowerInvariant();
			return Ranked(kind, by) ? T($"target.{side}.{kind}_by", Name(by)) : T($"target.{side}.{kind}");
		}

		private static bool Ranked(TargetKind kind, TargetRank by) => by != TargetRank.Health && kind is TargetKind.LowestAlly or TargetKind.HighestAlly;

		private static bool Good(EffectDefinition effect) => effect.Kind == EffectKind.Shield || !BattleRules.IsNegative(effect.Status);

		/// <summary>Estados seguidos que cabem na mesma frase: mesmo alvo, mesmo lado (bom ou ruim) e mesma permanência.</summary>
		private static bool SameRun(EffectDefinition first, EffectDefinition next) =>
			next.Kind is EffectKind.Status or EffectKind.Shield && next.OnKill == first.OnKill && next.Target == first.Target && next.By == first.By
			&& next.Unremovable == first.Unremovable && Good(next) == Good(first);

		/// <summary>" com 40% de chance" (" cada" quando são vários), ou nada quando é certo.</summary>
		private static string ChanceText(EffectDefinition effect, bool each = false) =>
			effect.Chance < 1 ? T(each ? "skill.say.chance_each" : "skill.say.chance", Percent(effect.Chance)) : "";

		/// <summary>
		/// "aplica Velocidade− no alvo por 2 turnos com 50% de chance e Aflição por 2 turnos com 40% de chance",
		/// "concede Imunidade e Escudo (10% da Vida) a todos os aliados por 2 turnos", "ganha Provocação por 1 turno".
		/// </summary>
		private static string RunText(IReadOnlyList<EffectDefinition> run, Subject? subject)
		{
			var first = run[0];
			var good = Good(first);
			var self = first.Target == TargetKind.Self;
			var verb = T(good ? (self ? "skill.say.gain" : "skill.say.grant") : "skill.say.inflict");
			var place = self && good ? "" : Where(first, good ? Form.To : Form.On, subject);

			// Mesmos turnos e mesma chance, uma frase só: "Aflição e Maldição por 2 turnos com 50% de chance cada".
			var groups = new List<string>();
			for (var i = 0; i < run.Count;)
			{
				var end = i + 1;
				while (end < run.Count && run[end].Turns == run[i].Turns && run[end].Chance == run[i].Chance)
					end++;
				var names = And(run.Skip(i).Take(end - i).Select(StateName).ToList());
				// Quando o certo e o incerto se alternam, o verbo se repete: a chance vale só para o grupo dela.
				var again = i > 0 && (run[i].Chance < 1) != (run[i - 1].Chance < 1) ? verb + " " : "";
				groups.Add(again + names + (i == 0 ? place : "") + " " + T("skill.say.for", Turns(run[i].Turns)) + ChanceText(run[i], end - i > 1));
				i = end;
			}

			var text = verb + " " + And(groups);
			return first.Unremovable ? text + " " + T("skill.unremovable") : text;
		}

		/// <summary>"Aflição", "Escudo (14% da Vida)": o que o efeito de estado põe.</summary>
		private static string StateName(EffectDefinition effect)
		{
			if (effect.Kind == EffectKind.Status)
				return Term(effect.Status);

			var amount = EffectScaling.IsCustom(effect) ? Formula(effect) : T("skill.say.shield_of", Percent(effect.Power));
			return $"{Term(StatusKind.Shield)} ({amount})";
		}

		/// <summary>O trecho de um efeito que não é de estado.</summary>
		private static string EffectText(EffectDefinition effect, Subject? subject)
		{
			var chance = ChanceText(effect);
			return effect.Kind switch
			{
				EffectKind.Damage => AttackText(effect),
				EffectKind.Heal => HealText(effect),
				EffectKind.HealTeam => HealText(effect),
				EffectKind.Impeto => effect.Power < 0
					? T("skill.say.impetus_remove", -effect.Power, Impeto, Where(effect, Form.From, subject)) + chance
					: effect.Target == TargetKind.Self
						? T("skill.say.impetus_self", effect.Power, Impeto) + chance
						: T("skill.say.impetus_give", effect.Power, Impeto, Where(effect, Form.To, subject)) + chance,
				EffectKind.Cleanse => T("skill.say.cleanse", Where(effect, Form.From, subject)),
				EffectKind.StealBuff => StealText(effect, chance, subject),
				EffectKind.InvertBuffs => InvertText(effect, chance, subject),
				EffectKind.BombTrigger => T("skill.say.bomb_trigger", Term(StatusKind.Bomb), Where(effect, Form.Of, subject)),
				EffectKind.BonusPerStatus => BonusText(effect),
				EffectKind.ChangeDuration => T(effect.Turns >= 0 ? "skill.say.prolong" : "skill.say.shorten",
					Turns(Math.Abs(effect.Turns)), StatesOf(effect), Where(effect, Form.On, subject)) + chance,
				EffectKind.EqualizeHealth => T("skill.say.equalize", Who(effect.Target, effect.By, Form.Of)),
				EffectKind.JointAttack => effect.Target != TargetKind.AllAllies ? T("skill.say.joint_one", Who(effect.Target, effect.By, Form.Obj))
					: effect.Count > 0 ? T("skill.say.joint", effect.Count) : T("skill.say.joint_all"),
				EffectKind.IncreaseCooldown => T("skill.say.cooldown_up", Turns(effect.Turns > 0 ? effect.Turns : 1), Who(effect.Target, effect.By, Form.Of)) + chance,
				EffectKind.ExtraTurnOnKill => T("skill.say.extra_turn_on_kill", Turns(effect.Turns)),
				EffectKind.Revive => ReviveText(effect),
				_ => effect.Kind.ToString(),
			};
		}

		/// <summary>"ataca o alvo 3 vezes (ignora 30% da Defesa)", "ataca o alvo (baseado em Defesa)": sem o multiplicador do Ataque.</summary>
		private static string AttackText(EffectDefinition effect)
		{
			var details = new List<string>();
			if (EffectScaling.IsCustom(effect) && BasedOn(effect) is { Length: > 0 } based)
				details.Add(T("skill.say.based", based));
			if (effect.IgnoreDefense > 0)
				details.Add(T("skill.say.ignores", Percent(effect.IgnoreDefense)));
			if (effect.Drain > 0)
				details.Add(T("skill.say.drains", Percent(effect.Drain)));
			if (effect.Fixed)
				details.Add(T("skill.say.fixed"));
			var times = effect.Hits > 1 ? T("skill.say.times", effect.Hits) : "";
			var extra = details.Count > 0 ? $" ({string.Join(", ", details)})" : "";
			return T("skill.say.attack", Who(effect.Target, effect.By, Form.Obj), times, extra);
		}

		/// <summary>"Defesa e Vida máxima": o que a conta do dano lê, quando não é só o Ataque.</summary>
		private static string BasedOn(EffectDefinition effect)
		{
			var names = EffectScaling.StatsOf(effect).Select(stat => T($"skill.say.stat.{stat}")).ToList();
			if (effect.Factor is { } factor)
				names.Add(T($"skill.say.measure.{factor.By}"));
			if (effect.Speed is { } speed)
			{
				if (!EffectScaling.StatsOf(effect).Contains(ScaleStat.Speed))
					names.Add(T("skill.say.stat.Speed"));
				if (speed.OverTarget)
					names.Add(T("skill.say.speed_target"));
			}
			var onlyAttack = EffectScaling.MainStat(effect) == ScaleStat.Attack && effect.Plus.Count == 0 && effect.Factor is null && effect.Speed is null;
			return onlyAttack ? "" : And(names);
		}

		/// <summary>Heal e HealTeam: "cura todos os aliados em 15% da Vida", ou a conta inteira quando ela vai além do de sempre.</summary>
		private static string HealText(EffectDefinition effect)
		{
			var custom = EffectScaling.IsCustom(effect);
			var amount = custom ? Formula(effect) : T("skill.say.life_share", Percent(effect.Power));
			if (effect is { Kind: EffectKind.HealTeam, Count: > 1 })
				return T("skill.say.heal_team", effect.Count, amount);
			var who = Who(effect is { Kind: EffectKind.HealTeam, Count: 1 } ? TargetKind.LowestAlly : effect.Target, effect.By, Form.Obj);
			return T("skill.say.heal", who, amount);
		}

		/// <summary>"revive os aliados caídos com 30% da Vida".</summary>
		private static string ReviveText(EffectDefinition effect)
		{
			var health = EffectScaling.IsCustom(effect) ? Formula(effect) : T("skill.say.life_share", Percent(effect.Power));
			return effect.Count > 0 ? T("skill.say.revive_some", effect.Count, health) : T("skill.say.revive", health);
		}

		/// <summary>"rouba um efeito positivo do alvo, ou, sem nenhum, rouba 20% de Ímpeto".</summary>
		private static string StealText(EffectDefinition effect, string chance, Subject? subject)
		{
			var what = effect.OnlyStatus is { } only ? Term(only) : T("scope.Buffs.one");
			var text = T("skill.say.steal", what, Where(effect, Form.From, subject)) + chance;
			if (effect.Fallback)
				text += effect.Power > 0 ? T("skill.say.steal_impetus", Math.Abs(effect.Power), Impeto) : T("skill.say.steal_all_impetus", Impeto);
			return text;
		}

		/// <summary>"inverte os efeitos positivos do alvo em negativos equivalentes (Maldição, sem equivalente)".</summary>
		private static string InvertText(EffectDefinition effect, string chance, Subject? subject)
		{
			var text = T("skill.say.invert", Where(effect, Form.Of, subject), Term(StatusBehaviors.Fallback)) + chance;
			if (effect.Fallback)
				text += effect.Power > 0 ? T("skill.say.invert_impetus", effect.Power, Impeto) : T("skill.say.invert_all_impetus", Impeto);
			return text;
		}

		/// <summary>"ganha +20% de dano nos golpes seguintes para cada efeito negativo do alvo".</summary>
		private static string BonusText(EffectDefinition effect)
		{
			var bonus = effect.Bonus switch
			{
				BonusKind.Damage => T("skill.say.bonus.Damage", Percent(effect.Power)),
				BonusKind.Heal => T("skill.say.bonus.Heal", Percent(effect.Power)),
				_ => T("skill.say.bonus.Impeto", effect.Power, Impeto, Who(effect.Target, effect.By, Form.To)),
			};
			return T("skill.say.per_status", bonus, OneState(effect), Who(effect.From, effect.By, Form.On));
		}

		/// <summary>O efeito de que a conta fala, no singular: o escolhido (<see cref="EffectDefinition.OnlyStatus"/>) ou o do escopo.</summary>
		private static string OneState(EffectDefinition effect) =>
			effect.OnlyStatus is { } only ? Term(only) : T($"scope.{effect.Scope}.one");

		/// <summary>"dos efeitos negativos", "de Bomba": os efeitos cuja duração muda.</summary>
		private static string StatesOf(EffectDefinition effect) =>
			effect.OnlyStatus is { } only ? T("skill.say.of", Term(only)) : T($"scope.{effect.Scope}.of");
	}
}
