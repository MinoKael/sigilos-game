using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>
	/// Os times de referência da calibragem (GDD, seção 10): o time típico (<see cref="TestData.TypicalTeam"/>)
	/// no ponto em que um jogador estaria em cada fase, e o que cada andar de Masmorra pede. As runas são as que um jogador montaria: Fatal e Lâmina em quem ataca, Energia,
	/// Guarda e Escudo nos outros, com o principal certo em cada espaço.
	/// </summary>
	internal static class ReferenceTeams
	{
		/// <summary>
		/// Quem chega à fase: o nível sobe até 20 na fase 40 e fica (o fim da Campanha pede nível 20 com
		/// runas); as runas são as que a Campanha solta até ali (1★ a 4★), melhoradas aos poucos até +12.
		/// </summary>
		public static BattleTeam AtStage(GameDatabase database, int stage)
		{
			var level = (int)Math.Round(1 + 19 * Math.Min(1.0, (stage - 1) / 39.0));
			if (stage < 3)
				return Team(database, s => s.Rarity, level);

			return Staged(database, stage, level, null);
		}

		/// <summary>As runas que a Campanha soltou até a fase (1★ a 4★, até +12), nas primeiras vagas.</summary>
		private static BattleTeam Staged(GameDatabase database, int stage, int level, IReadOnlyList<Member>? members)
		{
			var grade = stage < 8 ? 1 : stage < 13 ? 2 : stage < 18 ? 3 : 4;
			var runeLevel = Math.Min(12, 3 * ((stage - 1) / 8));
			var team = Team(database, s => s.Rarity, level, grade, runeLevel, members);
			var count = Math.Min(6, stage - 1);
			return new BattleTeam(team.Members.Select(m => m with { Runes = m.Runes.Take(count).ToList() }).ToList());
		}

		/// <summary>O time 6★ nível 40 sem runas: o outro jeito de terminar a Campanha.</summary>
		public static BattleTeam Bare(GameDatabase database) => Team(database, _ => 6, 40);

		/// <summary>
		/// Quem o andar de Masmorra pede, o mesmo em todas (Data/dungeons.json): a dificuldade acompanha o
		/// drop, então é o time que já usa runas como as que o andar solta. 1: a fase 15 (runas 3★); 2: a
		/// fase 30 (4★ +9); 3: o fim da Campanha (4★ +12); 4: 6★ nível 40 com runas 5★ +12; 5: o mesmo com 6★ +15.
		/// O andar 0 é o de baixo do 1 (a fase 10), para medir o salto.
		/// </summary>
		public static BattleTeam AtFloor(GameDatabase database, int floor) => floor switch
		{
			0 => AtStage(database, 10),
			1 => AtStage(database, 15),
			2 => AtStage(database, 30),
			3 => AtStage(database, 50),
			4 => Team(database, _ => 6, 40, 5, 12),
			_ => Team(database, _ => 6, 40, 6, 15),
		};

		/// <summary>Quantas vezes o time vence o encontro, de 0 a 1.</summary>
		public static double WinRate(GameDatabase database, BattleTeam team, Encounter encounter, int fights)
		{
			var wins = 0;
			for (var seed = 1; seed <= fights; seed++)
			{
				if (AutoBattle.Run(BattleFactory.Create(database, team, encounter, seed * 7919)))
					wins++;
			}

			return wins / (double)fights;
		}

		// Especialistas ------------------------------------------------------------------------------

		/// <summary>Um monstro da equipe de especialista: os conjuntos de cada vaga e, se pede, o principal da vaga 6.</summary>
		public sealed record Member(string Id, RuneSet[] Sets, RuneStat? Six = null);

		/// <summary>Um conjunto de 4 e um de 2, nas vagas 1 a 4 e 5 e 6.</summary>
		private static RuneSet[] Sets(RuneSet four, RuneSet two) => new[] { four, four, four, four, two, two };

		/// <summary>Três conjuntos de 2.</summary>
		private static RuneSet[] Sets(RuneSet a, RuneSet b, RuneSet c) => new[] { a, a, b, b, c, c };

		/// <summary>
		/// As Masmorras de especialização (GDD, seção 11): cada chefe pede ferramentas que um time genérico
		/// não tem. Estas são as equipes montadas para cada uma, com as runas de cada função. A líder (a
		/// primeira) dá a Liderança.
		/// - Golem: Quebra de Defesa contra a Defesa enorme, roubo de efeito positivo contra os pilares, a
		///   Bomba que ignora Defesa e cura.
		/// - Serpe: Purificação, Imunidade e Resistência contra as Aflições que alimentam o dano do dragão.
		/// - Cripta: Esquecimento (com Precisão) para o Rei não voltar, controle de turno e dano constante.
		/// - Santuário: atordoar e Ataque− contra os contragolpes, cura e golpes únicos fortes de Vento.
		/// </summary>
		public static readonly IReadOnlyDictionary<string, IReadOnlyList<Member>> Specialists = new Dictionary<string, IReadOnlyList<Member>>
		{
			["golem"] = new Member[]
			{
				new("paladin_fire", Sets(RuneSet.Vigor, RuneSet.Haste, RuneSet.Ward)),
				new("knight_fire", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("wizard_fire", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("bandit_fire", Sets(RuneSet.Haste, RuneSet.Finesse), RuneStat.Accuracy),
				new("imp_fire", Sets(RuneSet.Vigor, RuneSet.Ward, RuneSet.Sustain)),
			},
			["wyvern"] = new Member[]
			{
				new("phoenix_water", Sets(RuneSet.Vigor, RuneSet.Sustain, RuneSet.Tenacity), RuneStat.Resistance),
				new("crow_light", Sets(RuneSet.Vigor, RuneSet.Sustain, RuneSet.Tenacity), RuneStat.Resistance),
				new("wizard_water", Sets(RuneSet.Vigor, RuneSet.Sustain, RuneSet.Tenacity), RuneStat.Resistance),
				new("wizard_dark", Sets(RuneSet.Lethal, RuneSet.Sustain), RuneStat.Resistance),
				new("imp_water", Sets(RuneSet.Lethal, RuneSet.Sustain)),
			},
			["crypt"] = new Member[]
			{
				new("phoenix_light", Sets(RuneSet.Vigor, RuneSet.Finesse, RuneSet.Ward), RuneStat.Accuracy),
				new("imp_dark", Sets(RuneSet.Lethal, RuneSet.Finesse), RuneStat.Accuracy),
				new("crow_dark", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("dragon_dark", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("vampire_light", Sets(RuneSet.Vigor, RuneSet.Ward, RuneSet.Bulwark)),
			},
			["sanctum"] = new Member[]
			{
				new("paladin_wind", Sets(RuneSet.Vigor, RuneSet.Ward, RuneSet.Sustain)),
				new("wizard_water", Sets(RuneSet.Vigor, RuneSet.Ward, RuneSet.Finesse), RuneStat.Accuracy),
				new("gargoyle_water", Sets(RuneSet.Vigor, RuneSet.Ward, RuneSet.Finesse), RuneStat.Accuracy),
				new("wizard_wind", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("dragon_wind", Sets(RuneSet.Lethal, RuneSet.Strike)),
			},
		};

		/// <summary>
		/// A equipe de especialista no degrau do andar. De 0 a 3, como <see cref="AtFloor"/> (as estrelas
		/// naturais, as runas da Campanha). 4 é o ponto doce: 5★ nível 35, habilidades no máximo e runas 5★
		/// +12 com os conjuntos certos — domina o andar 4, ainda não o 5. 5 é a preparação do andar 5 (a
		/// referência é chegar ao Golem 5 em cerca de 20 dias de jogo): 6★ nível 40, desperta, habilidades no
		/// máximo e runas 6★ boas, escolhidas pelos subatributos, +15 nas vagas 2, 4 e 6 e +12 nas outras.
		/// </summary>
		public static BattleTeam Specialist(GameDatabase database, string dungeon, int floor) => floor switch
		{
			<= 0 => Staged(database, 10, Level(10), Specialists[dungeon]),
			1 => Staged(database, 15, Level(15), Specialists[dungeon]),
			2 => Staged(database, 30, Level(30), Specialists[dungeon]),
			3 => Staged(database, 50, Level(50), Specialists[dungeon]),
			4 => Team(database, _ => 5, 35, 5, 12, Specialists[dungeon], maxSkills: true, picks: 3),
			_ => Team(database, _ => 6, 40, 6, 12, Specialists[dungeon], awakened: true, maxSkills: true, picks: 8, evenLevel: 15),
		};

		/// <summary>
		/// O time forte sem especialização: o típico com o mesmo investimento do especialista do andar 5.
		/// Vence o que pede força, mas não um chefe cuja mecânica ele não sabe enfrentar.
		/// </summary>
		public static BattleTeam Powerful(GameDatabase database) =>
			Team(database, _ => 6, 40, 6, 12, null, awakened: true, maxSkills: true, picks: 8, evenLevel: 15);

		/// <summary>O nível de quem chega à fase (o mesmo de <see cref="AtStage"/>).</summary>
		private static int Level(int stage) => (int)Math.Round(1 + 19 * Math.Min(1.0, (stage - 1) / 39.0));

		/// <summary>
		/// <paramref name="members"/> nulo é o time típico, com as runas de sempre: Fatal e Lâmina em quem
		/// ataca; Energia, Guarda e Escudo nos outros. <paramref name="picks"/> é quantas runas de cada
		/// vaga o jogador já viu cair: fica a de melhores subatributos para a função. <paramref name="evenLevel"/>,
		/// quando dado, é a melhora das vagas 2, 4 e 6 (as de principal escolhido, as primeiras a ir a +15).
		/// </summary>
		private static BattleTeam Team(GameDatabase database, Func<SummonDefinition, int> stars, int level, int runeGrade = 0, int runeLevel = 0,
			IReadOnlyList<Member>? members = null, bool awakened = false, bool maxSkills = false, int picks = 1, int evenLevel = 0)
		{
			var random = new Random(7);
			var id = 1;
			members ??= TestData.TypicalTeam.Select(summonId => new Member(summonId, Array.Empty<RuneSet>())).ToList();
			return new BattleTeam(members.Select(member =>
			{
				var summon = database.Summon(member.Id);
				var runes = runeGrade > 0 ? Build(random, summon, member, runeGrade, runeLevel, evenLevel, picks, ref id) : new List<Rune>();
				var skills = maxSkills ? summon.AllSkills.Select(s => s.MaxLevel).ToList() : new List<int>();
				return new TeamMember(summon, stars(summon), level, awakened, skills, runes);
			}).ToList());
		}

		private static List<Rune> Build(Random random, SummonDefinition summon, Member member, int grade, int level, int evenLevel, int picks, ref int id)
		{
			var attack = summon.Role == Role.Attack;
			var percent = attack ? RuneStat.AttackPercent : RuneStat.HealthPercent;
			var sets = member.Sets.Length == RuneRules.Slots
				? member.Sets
				: attack
					? new[] { RuneSet.Lethal, RuneSet.Lethal, RuneSet.Lethal, RuneSet.Lethal, RuneSet.Strike, RuneSet.Strike }
					: new[] { RuneSet.Vigor, RuneSet.Vigor, RuneSet.Ward, RuneSet.Ward, RuneSet.Bulwark, RuneSet.Bulwark };
			var mains = new[] { RuneStat.AttackFlat, attack ? RuneStat.AttackPercent : RuneStat.Speed, RuneStat.DefenseFlat, percent, RuneStat.HealthFlat, member.Six ?? percent };
			var runes = new List<Rune>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				Rune? best = null;
				for (var pick = 0; pick < picks; pick++)
				{
					Rune rune;
					do
						rune = RuneForge.Generate(random, id++, grade, slot, new[] { sets[slot - 1] });
					while (rune.Main != mains[slot - 1]);
					while (rune.Level < (slot % 2 == 0 && evenLevel > 0 ? evenLevel : level))
						RuneForge.RaiseLevel(random, rune);
					if (best == null || Score(rune, attack) > Score(best, attack))
						best = rune;
				}

				runes.Add(best!);
			}

			return runes;
		}

		/// <summary>Os subatributos que servem à função, em sorteios máximos (1 = um sorteio cheio).</summary>
		private static double Score(Rune rune, bool attack) => rune.Substats.Sum(sub =>
		{
			var weight = sub.Stat switch
			{
				RuneStat.Speed => 1.2,
				RuneStat.AttackPercent or RuneStat.Crit or RuneStat.CritDamage => attack ? 1 : 0,
				RuneStat.HealthPercent or RuneStat.DefensePercent => attack ? 0.3 : 1,
				RuneStat.Resistance or RuneStat.Accuracy => attack ? 0.3 : 0.8,
				_ => 0,
			};
			return weight * sub.Value / RuneRules.SubstatRange(sub.Stat, rune.Grade).Max;
		});
	}
}
