using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// O modelo de atributos (Data/stat_model.json; a explicação inteira está em
	/// docs/summon_family_stat_formula_spec.md). Todo monstro das mesmas estrelas naturais gasta o mesmo
	/// orçamento de BVP (pontos de valor de base), e o papel decide onde:
	///
	///   BVP = Vida / peso da Vida + Ataque + Defesa + Velocidade × peso da Velocidade
	///
	/// O jogo não calcula atributo na hora: Data/summons e Data/enemies.json já trazem os números
	/// prontos, no 6★ nível 40, gerados por docs/summon_family_builder.html. Esta classe é a mesma conta
	/// do lado do jogo, para uma coisa só: conferir que os números guardados são os que o modelo dá
	/// (<see cref="GameDatabase.Validate"/>). Mudou um parâmetro? O teste de dados acusa toda variante
	/// que ficou para trás, e o construtor recalcula todas de uma vez.
	///
	/// A conta tem que ser igual à do construtor, operação por operação, para dar o mesmo número inteiro.
	/// </summary>
	public sealed record StatModel
	{
		/// <summary>Quanta Vida vale 1 ponto do orçamento (15).</summary>
		[JsonPropertyName("hp_weight")]
		public double HealthWeight { get; init; } = 15;

		/// <summary>Quantos pontos do orçamento vale 1 de Velocidade (3).</summary>
		[JsonPropertyName("spd_weight")]
		public double SpeedWeight { get; init; } = 3;

		/// <summary>Quanto o BVP de uma variante pode ficar longe do orçamento, depois do arredondamento.</summary>
		public double Tolerance { get; init; } = 1;

		/// <summary>Velocidade que o Despertar soma, quando a variante não diz outra.</summary>
		[JsonPropertyName("awakening_spd")]
		public double AwakeningSpeed { get; init; } = 1;

		/// <summary>O orçamento de BVP por estrelas naturais.</summary>
		[JsonPropertyName("stat_budget")]
		public IReadOnlyDictionary<int, double> Budgets { get; init; } = new Dictionary<int, double>();

		/// <summary>O orçamento depois do Despertar: é outro, não um multiplicador.</summary>
		[JsonPropertyName("awakened_stat_budget")]
		public IReadOnlyDictionary<int, double> AwakenedBudgets { get; init; } = new Dictionary<int, double>();

		/// <summary>Por estrelas naturais e por papel: as fatias do orçamento e a Velocidade de base.</summary>
		[JsonPropertyName("role_profiles")]
		public IReadOnlyDictionary<int, IReadOnlyDictionary<Role, StatWeights>> Roles { get; init; } = new Dictionary<int, IReadOnlyDictionary<Role, StatWeights>>();

		/// <summary>A tendência de cada elemento. Muda a distribuição, nunca o total.</summary>
		[JsonPropertyName("element_bias")]
		public IReadOnlyDictionary<Element, StatWeights> Elements { get; init; } = new Dictionary<Element, StatWeights>();

		/// <summary>Crítico, Dano crítico, Resistência e Precisão de base: iguais para todo monstro, fora do orçamento.</summary>
		[JsonPropertyName("base_stats")]
		public StatBlock BaseStats { get; init; } = new();

		/// <summary>O orçamento destas estrelas naturais; nulo quando o modelo não as conhece.</summary>
		public double? Budget(int stars, bool awakened) =>
			(awakened ? AwakenedBudgets : Budgets).TryGetValue(stars, out var budget) ? budget : null;

		public double Bvp(StatBlock stats) =>
			stats.Health / HealthWeight + stats.Attack + stats.Defense + stats.Speed * SpeedWeight;

		/// <summary>Os quatro atributos do modelo com os quatro de base, que são de todos.</summary>
		public StatBlock Complete(StatBlock stats) => BaseStats with
		{
			Health = stats.Health,
			Attack = stats.Attack,
			Defense = stats.Defense,
			Speed = stats.Speed,
		};

		/// <summary>
		/// Os atributos de uma variante no 6★ nível 40: separa a Velocidade, reparte o que sobrou do
		/// orçamento pelas fatias do papel (passadas pelos vieses e normalizadas, para o viés nunca criar
		/// BVP) e arredonda — a Vida em múltiplos do peso dela, e o que o arredondamento deixou vai para a
		/// Defesa, para fechar o orçamento.
		/// </summary>
		public StatBlock Compute(
			int stars,
			Role role,
			bool awakened,
			StatWeights? element = null,
			StatWeights? family = null,
			StatWeights? manual = null,
			double? awakeningSpeed = null)
		{
			var budget = Budget(stars, awakened) ?? throw new ArgumentOutOfRangeException(nameof(stars), stars, "Estrelas sem orçamento no modelo.");
			var profile = Roles[stars][role];
			element ??= StatWeights.Neutral;
			family ??= StatWeights.Neutral;
			manual ??= StatWeights.Neutral;

			var speed = Round(profile.Speed + family.Speed + element.Speed + manual.Speed + (awakened ? awakeningSpeed ?? AwakeningSpeed : 0));
			var room = Math.Max(0, budget - speed * SpeedWeight);

			var health = profile.Health * element.Health * family.Health * manual.Health;
			var attack = profile.Attack * element.Attack * family.Attack * manual.Attack;
			var defense = profile.Defense * element.Defense * family.Defense * manual.Defense;
			var total = health + attack + defense;
			if (total <= 0)
				total = 1;

			var hp = Round(Math.Max(0, Round(room * health / total)) * HealthWeight);
			var atk = Math.Max(0, Round(room * attack / total));
			var def = Math.Max(0, Round(room * defense / total));
			def = Math.Max(0, def + Round(budget - hp / HealthWeight - atk - def - speed * SpeedWeight));

			return Complete(new StatBlock { Health = hp, Attack = atk, Defense = def, Speed = speed });
		}

		/// <summary>Os atributos que o modelo dá a esta variante, com os vieses dela, da família e do elemento.</summary>
		public StatBlock Compute(SummonDefinition summon, bool awakened) => Compute(
			summon.Rarity,
			summon.Role,
			awakened,
			Elements.GetValueOrDefault(summon.Element),
			summon.Family.Bias,
			summon.Bias,
			summon.AwakeningSpeed);

		/// <summary>Parâmetros que faltam ou não fazem sentido. Vazio quando está tudo certo.</summary>
		public IEnumerable<string> Validate()
		{
			if (HealthWeight <= 0 || SpeedWeight <= 0)
				yield return "stat_model.json: os pesos de Vida e de Velocidade têm de ser maiores que zero.";
			if (Budgets.Count == 0)
				yield return "stat_model.json: sem orçamento (\"stat_budget\").";

			foreach (var (stars, budget) in Budgets)
			{
				if (!AwakenedBudgets.TryGetValue(stars, out var awakened))
					yield return $"stat_model.json: {stars}★ sem orçamento desperto.";
				else if (awakened < budget)
					yield return $"stat_model.json: {stars}★ com orçamento desperto ({awakened}) menor que o de base ({budget}).";

				if (!Roles.TryGetValue(stars, out var roles))
				{
					yield return $"stat_model.json: {stars}★ sem perfis de papel.";
					continue;
				}

				foreach (var role in Enum.GetValues<Role>())
				{
					if (!roles.TryGetValue(role, out var profile))
						yield return $"stat_model.json: {stars}★ sem o perfil do papel {role}.";
					else if (profile.Health <= 0 || profile.Attack <= 0 || profile.Defense <= 0 || profile.Speed <= 0)
						yield return $"stat_model.json: {stars}★ {role} com fatia ou Velocidade que não é maior que zero.";
				}
			}
		}

		/// <summary>Meio para cima, como o <c>Math.round</c> do construtor (o Math.Round do C# arredonda meio para o par).</summary>
		private static double Round(double value) => Math.Floor(value + 0.5);
	}
}
