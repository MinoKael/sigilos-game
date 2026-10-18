using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// O auto-equipar da tela de Runas, pensado para quem está começando: monta, com os sigilos soltos e os
	/// que o homúnculo já usa, a combinação que mais soma ao que ele faz.
	///
	/// O que conta:
	/// - O papel (<see cref="Role"/>): Ataque quer Ataque, Crítico e Velocidade; Vida e Defesa querem a
	///   própria base e Velocidade; Suporte quer Velocidade, Precisão, Resistência e resistência de corpo.
	/// - As habilidades: o atributo em que o dano e o escudo dela escalam puxa o peso para ele.
	/// - A variante: o atributo em que ela é mais forte que a média dos outros pesa mais (e o fraco, menos);
	///   o valor de cada sigilo é o ganho sobre a base DELA, e os conjuntos entram pelo bônus real que dão.
	///
	/// Os conjuntos: um passo de cada vez nunca acharia um conjunto de 4, porque três peças não somam nada.
	/// Por isso a busca monta planos (4, 4+2, 2+2+2, 2 e as sobras livres), ocupa os espaços de cada plano
	/// com o melhor sigilo de cada conjunto e compara as combinações inteiras, pela ficha de verdade.
	///
	/// Só entram os sigilos soltos e os do próprio homúnculo: tirar de outro estragaria a montagem dele. Quem
	/// chama põe o resultado na prova da tela; nada é equipado aqui.
	/// </summary>
	public static class RuneAutoEquip
	{
		/// <summary>Quanto o que as habilidades escalam desloca o peso do papel (o peso de um atributo que só elas usam).</summary>
		private const double SkillShare = 0.6;

		/// <summary>Quanto o atributo forte (ou fraco) da variante, contra a média, desloca o peso dele.</summary>
		private const double StrengthMin = 0.6;
		private const double StrengthMax = 1.6;

		/// <summary>Os sigilos da melhor combinação, no máximo um por espaço. Empate: o de menor Id.</summary>
		/// <param name="peers">Os outros homúnculos do jogo: a média deles diz o que é forte nesta variante.</param>
		public static IReadOnlyList<Rune> Choose(SummonDefinition summon, OwnedSummon monster, IEnumerable<Rune> runes, IReadOnlyCollection<SummonDefinition>? peers = null)
		{
			var pool = runes
				.Where(r => RuneSets.IsActive(r.Set) && (r.EquippedOn == null || r.EquippedOn == monster.Id))
				.OrderBy(r => r.Id)
				.ToList();
			var baseStats = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, Array.Empty<Rune>()).Base;
			var weights = Weights(summon, monster.Awakened, peers);

			double Score(IReadOnlyCollection<Rune> build)
			{
				var total = baseStats.Plus(RuneBonuses.Compute(baseStats, build).Stats);
				var score = 0.0;
				foreach (var (stat, weight) in weights)
				{
					var before = baseStats.Get(stat);
					var after = total.Get(stat);
					if (StatBlock.IsAbsolute(stat))
						score += before > 0 ? weight * (after - before) / before : 0;
					else
						score += weight * (Math.Min(1, after) - Math.Min(1, before));
				}

				return score;
			}

			var current = pool.Where(r => r.EquippedOn == monster.Id).ToList();
			var best = (Build: (IReadOnlyList<Rune>)current, Score: Score(current));

			// O melhor de cada espaço, sozinho e por conjunto: sozinho um sigilo nunca fecha conjunto, então o
			// número é só o dos atributos dele.
			var alone = pool.ToDictionary(r => r.Id, r => Score(new[] { r }));
			var bestAny = new Rune?[RuneRules.Slots + 1];
			var bestOfSet = new Dictionary<(RuneSet, int), Rune>();
			foreach (var rune in pool)
			{
				if (bestAny[rune.Slot] == null || alone[rune.Id] > alone[bestAny[rune.Slot]!.Id])
					bestAny[rune.Slot] = rune;
				var key = (rune.Set, rune.Slot);
				if (!bestOfSet.TryGetValue(key, out var held) || alone[rune.Id] > alone[held.Id])
					bestOfSet[key] = rune;
			}

			foreach (var plan in Plans())
			{
				foreach (var build in Builds(plan, bestAny, bestOfSet))
				{
					var score = Score(build);
					if (score > best.Score + 1e-12)
						best = (build, score);
				}
			}

			return best.Build;
		}

		/// <summary>Os sigilos da combinação que ainda não estão no homúnculo: o que a tela põe na prova.</summary>
		public static IReadOnlyList<Rune> Changes(IEnumerable<Rune> chosen, OwnedSummon monster) =>
			chosen.Where(r => r.EquippedOn != monster.Id).ToList();

		/// <summary>
		/// O peso de cada atributo: a tabela do papel, mais a fatia que as habilidades escalam em Vida, Ataque,
		/// Defesa e Velocidade. Em fração da base (absolutos) ou em pontos (os outros quatro).
		/// </summary>
		private static Dictionary<Stat, double> Weights(SummonDefinition summon, bool awakened, IReadOnlyCollection<SummonDefinition>? peers)
		{
			var weights = summon.Role switch
			{
				Role.Attack => Table(health: 0.3, attack: 1.0, defense: 0.3, speed: 0.6, crit: 0.5, critDamage: 0.4, resistance: 0.1, accuracy: 0.15),
				Role.HP => Table(health: 1.0, attack: 0.1, defense: 0.5, speed: 0.5, crit: 0.05, critDamage: 0.05, resistance: 0.35, accuracy: 0.15),
				Role.Defense => Table(health: 0.5, attack: 0.1, defense: 1.0, speed: 0.5, crit: 0.05, critDamage: 0.05, resistance: 0.35, accuracy: 0.15),
				_ => Table(health: 0.6, attack: 0.1, defense: 0.6, speed: 1.0, crit: 0, critDamage: 0, resistance: 0.5, accuracy: 0.5),
			};

			var scaled = ScaledStats(summon, awakened);
			var sum = scaled.Values.Sum();
			if (sum > 0)
			{
				foreach (var (stat, amount) in scaled)
					weights[stat] += SkillShare * amount / sum;
			}

			// A variante puxa para o que ela tem de mais forte: o peso de Vida, Ataque, Defesa e Velocidade
			// sobe ou desce pela razão do atributo dela para a média dos outros (limitada, para não virar só isso).
			if (peers is { Count: > 0 })
			{
				foreach (var stat in new[] { Stat.Health, Stat.Attack, Stat.Defense, Stat.Speed })
				{
					var average = peers.Average(p => p.Stats.Get(stat));
					if (average > 0)
						weights[stat] *= Math.Clamp(summon.Stats.Get(stat) / average, StrengthMin, StrengthMax);
				}
			}

			return weights;
		}

		private static Dictionary<Stat, double> Table(double health, double attack, double defense, double speed, double crit, double critDamage, double resistance, double accuracy) => new()
		{
			[Stat.Health] = health,
			[Stat.Attack] = attack,
			[Stat.Defense] = defense,
			[Stat.Speed] = speed,
			[Stat.Crit] = crit,
			[Stat.CritDamage] = critDamage,
			[Stat.Resistance] = resistance,
			[Stat.Accuracy] = accuracy,
		};

		/// <summary>Quanto as habilidades de combate escalam em cada atributo (dano no Ataque, escudo na Vida, termos somados e a Velocidade).</summary>
		private static Dictionary<Stat, double> ScaledStats(SummonDefinition summon, bool awakened)
		{
			var scaled = new Dictionary<Stat, double>();

			void Add(ScaleStat? source, double amount)
			{
				Stat? stat = source switch
				{
					ScaleStat.Attack => Stat.Attack,
					ScaleStat.Defense => Stat.Defense,
					ScaleStat.MaxHealth => Stat.Health,
					ScaleStat.Speed => Stat.Speed,
					_ => null,
				};
				if (stat is { } found && amount > 0)
					scaled[found] = scaled.GetValueOrDefault(found) + amount;
			}

			foreach (var skill in summon.AllSkills.Where(s => !s.IsPassive))
			{
				var effects = awakened && skill.AwakenedEffects.Count > 0 ? skill.AwakenedEffects : skill.Effects;
				foreach (var effect in effects)
				{
					var power = effect.Power * Math.Max(1, effect.Hits);
					switch (effect.Kind)
					{
						case EffectKind.Damage:
							Add(effect.Stat ?? ScaleStat.Attack, power);
							break;
						case EffectKind.Shield:
							Add(effect.Stat ?? ScaleStat.MaxHealth, power);
							break;
						default:
							Add(effect.Stat, power);
							break;
					}

					foreach (var term in effect.Plus)
						Add(term.Stat, term.Power);
					if (effect.Speed != null)
						Add(ScaleStat.Speed, 0.5);
				}
			}

			return scaled;
		}

		/// <summary>Os planos de conjunto: conjuntos diferentes, cada um com um múltiplo das peças dele, até 6 espaços. O vazio vem primeiro.</summary>
		private static IEnumerable<IReadOnlyList<(RuneSet Set, int Count)>> Plans()
		{
			var sets = RuneSets.Active;
			var chosen = new List<(RuneSet, int)>();

			IEnumerable<IReadOnlyList<(RuneSet, int)>> Walk(int from, int used)
			{
				yield return chosen.ToList();
				for (var i = from; i < sets.Count; i++)
				{
					for (var count = sets[i].Pieces; used + count <= RuneRules.Slots; count += sets[i].Pieces)
					{
						chosen.Add((sets[i].Set, count));
						foreach (var plan in Walk(i + 1, used + count))
							yield return plan;
						chosen.RemoveAt(chosen.Count - 1);
					}
				}
			}

			return Walk(0, 0);
		}

		/// <summary>
		/// As montagens de um plano: cada conjunto toma os espaços que o plano manda (todas as divisões possíveis)
		/// com o melhor sigilo dele em cada um; os espaços que sobram levam o melhor de qualquer conjunto.
		/// </summary>
		private static IEnumerable<IReadOnlyList<Rune>> Builds(IReadOnlyList<(RuneSet Set, int Count)> plan, Rune?[] bestAny, Dictionary<(RuneSet, int), Rune> bestOfSet)
		{
			const int all = (1 << RuneRules.Slots) - 1;
			var assigned = new Rune?[RuneRules.Slots + 1];

			IEnumerable<IReadOnlyList<Rune>> Fill(int index, int free)
			{
				if (index == plan.Count)
				{
					var build = new List<Rune>();
					for (var slot = 1; slot <= RuneRules.Slots; slot++)
					{
						var rune = (free >> (slot - 1) & 1) == 1 ? bestAny[slot] : assigned[slot];
						if (rune != null)
							build.Add(rune);
					}

					yield return build;
					yield break;
				}

				var (set, count) = plan[index];
				for (var mask = free; mask > 0; mask = (mask - 1) & free)
				{
					if (System.Numerics.BitOperations.PopCount((uint)mask) != count)
						continue;

					var ok = true;
					for (var slot = 1; slot <= RuneRules.Slots && ok; slot++)
					{
						if ((mask >> (slot - 1) & 1) == 0)
							continue;
						ok = bestOfSet.TryGetValue((set, slot), out var rune);
						assigned[slot] = ok ? rune : null;
					}

					if (!ok)
						continue;

					foreach (var build in Fill(index + 1, free & ~mask))
						yield return build;
				}
			}

			return Fill(0, all);
		}
	}
}
