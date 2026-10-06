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

		/// <summary>O time 6★ nível 40 sem runas: vence até o fim da região 2 (fase 40); a fase 50 pede runas.</summary>
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
		public static double WinRate(GameDatabase database, BattleTeam team, Encounter encounter, int fights) =>
			Measure(database, team, encounter, fights).Wins;

		/// <summary>As vitórias (de 0 a 1) e quanto tempo de luta as vitórias levam, em média (em rodadas).</summary>
		public static (double Wins, double Rounds) Measure(GameDatabase database, BattleTeam team, Encounter encounter, int fights)
		{
			var wins = 0;
			var rounds = 0.0;
			for (var seed = 1; seed <= fights; seed++)
			{
				var session = BattleFactory.Create(database, team, encounter, seed * 7919);
				if (AutoBattle.Run(session))
				{
					wins++;
					rounds += session.Time;
				}
			}

			return (wins / (double)fights, wins > 0 ? rounds / wins : double.PositiveInfinity);
		}

		// Especialistas ------------------------------------------------------------------------------

		/// <summary>
		/// Um monstro da equipe de especialista: os conjuntos de cada vaga e, se pede, o principal da vaga 6.
		/// <see cref="Speed"/> é a prioridade na sincronia de Velocidade (0: nenhuma): Velocidade no principal
		/// da vaga 2 e nos subatributos, mais quanto maior o número, para agir antes dos outros.
		/// </summary>
		public sealed record Member(string Id, RuneSet[] Sets, RuneStat? Six = null, int Speed = 0);

		/// <summary>Um conjunto de 4 e um de 2, nas vagas 1 a 4 e 5 e 6.</summary>
		private static RuneSet[] Sets(RuneSet four, RuneSet two) => new[] { four, four, four, four, two, two };

		/// <summary>Três conjuntos de 2.</summary>
		private static RuneSet[] Sets(RuneSet a, RuneSet b, RuneSet c) => new[] { a, a, b, b, c, c };

		/// <summary>
		/// As Masmorras de especialização (GDD, seção 11): cada chefe pede ferramentas que um time genérico
		/// não tem. Estas são as equipes que um jogador consegue montar com as taxas de invocação (GDD, seção
		/// 9): 2★ a 4★ de Fogo, Água e Vento (as 2★ caem na Campanha, até as de Luz e Trevas), as habilidades
		/// subidas com cópias e Núcleos de Infusão, e no máximo uma 5★, do elemento que tem vantagem. A
		/// líder (a primeira) dá a Liderança.
		/// - Golem: o Minerador Anão de Fogo (tira os efeitos positivos, ignora e quebra a Defesa, corta a cura do
		///   núcleo), Quebra de Defesa contra a Defesa enorme, cura.
		/// - Serpe: Purificação, Imunidade e Resistência contra as Aflições que alimentam o dano do dragão.
		/// - Cripta: Esquecimento (com Precisão) para o Rei não voltar, e dano constante.
		/// - Santuário: dano de Vento, Égide, Purificação e cura para aguentar os contragolpes.
		/// </summary>
		public static readonly IReadOnlyDictionary<string, IReadOnlyList<Member>> Specialists = new Dictionary<string, IReadOnlyList<Member>>
		{
			["golem"] = new Member[]
			{
				new("paladin_fire", Sets(RuneSet.Vigor, RuneSet.Haste, RuneSet.Ward)),
				new("knight_fire", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("crow_fire", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("dwarf_miner_fire", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("imp_fire", Sets(RuneSet.Vigor, RuneSet.Ward, RuneSet.Sustain)),
			},
			["wyvern"] = new Member[]
			{
				new("phoenix_water", Sets(RuneSet.Vigor, RuneSet.Sustain, RuneSet.Tenacity), RuneStat.Resistance),
				new("wolf_water", Sets(RuneSet.Vigor, RuneSet.Sustain, RuneSet.Tenacity), RuneStat.Resistance),
				new("slime_water", Sets(RuneSet.Vigor, RuneSet.Sustain, RuneSet.Tenacity), RuneStat.Resistance),
				new("imp_water", Sets(RuneSet.Lethal, RuneSet.Sustain)),
				new("vampire_fire", Sets(RuneSet.Lethal, RuneSet.Sustain)),
			},
			["crypt"] = new Member[]
			{
				new("paladin_fire", Sets(RuneSet.Vigor, RuneSet.Ward, RuneSet.Bulwark)),
				new("crow_water", Sets(RuneSet.Vigor, RuneSet.Finesse, RuneSet.Ward), RuneStat.Accuracy),
				new("bird_dark", Sets(RuneSet.Vigor, RuneSet.Finesse, RuneSet.Ward), RuneStat.Accuracy),
				new("knight_fire", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("vampire_fire", Sets(RuneSet.Lethal, RuneSet.Strike)),
			},
			["sanctum"] = new Member[]
			{
				new("dragon_wind", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("vampire_wind", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("gargoyle_wind", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("knight_water", Sets(RuneSet.Vigor, RuneSet.Ward, RuneSet.Sustain)),
				new("slime_water", Sets(RuneSet.Vigor, RuneSet.Ward, RuneSet.Sustain)),
			},
		};

		/// <summary>
		/// As três visões do andar 5, todas com a preparação dele (<see cref="Prepared"/>), para a diferença vir só
		/// da montagem. O Golem é a régua do balanceamento.
		/// - Gratuito (<see cref="Free"/>): um 4★ e quatro 3★ do Pergaminho Místico (Fogo, Água e Vento), o que
		///   qualquer conta tem. Vence só porque os monstros trazem os efeitos que o chefe pede, e devagar.
		/// - OK (<see cref="Specialists"/>): a equipe de especialista, que vence pelo dano bruto.
		/// - Spd (<see cref="Fast"/>): a sincronia de Velocidade. Quem empurra o Ímpeto e quem põe os efeitos
		///   negativos agem primeiro (<see cref="Member.Speed"/>); depois, o dano em área que ignora Defesa limpa
		///   as ondas e os outros derrubam a Vida. É a que vence mais rápido.
		/// </summary>
		public static readonly IReadOnlyDictionary<string, IReadOnlyList<Member>> Free = new Dictionary<string, IReadOnlyList<Member>>
		{
			// Quebra de Defesa ao acertar, roubar a Imunidade e a Defesa+ dos Bastiões, Maldição e cura, Ataque−
			// contra o Terremoto e dano em quem já está ferido.
			["golem"] = new Member[]
			{
				new("crow_fire", Sets(RuneSet.Lethal, RuneSet.Strike)),
				new("goblin_fire", Sets(RuneSet.Haste, RuneSet.Finesse), RuneStat.Accuracy),
				new("imp_fire", Sets(RuneSet.Vigor, RuneSet.Ward, RuneSet.Sustain)),
				new("slime_fire", Sets(RuneSet.Vigor, RuneSet.Finesse, RuneSet.Ward), RuneStat.Accuracy),
				new("wolf_fire", Sets(RuneSet.Lethal, RuneSet.Strike)),
			},
		};

		/// <inheritdoc cref="Free"/>
		public static readonly IReadOnlyDictionary<string, IReadOnlyList<Member>> Fast = new Dictionary<string, IReadOnlyList<Member>>
		{
			// O Cavaleiro de Vento empurra o Ímpeto de todos, o Diabrete amaldiçoa, o Corvo quebra a Defesa em área,
			// o Cavaleiro de Fogo limpa as ondas ignorando Defesa e o Paladino lidera com Ataque.
			["golem"] = new Member[]
			{
				new("paladin_fire", Sets(RuneSet.Lethal, RuneSet.Strike), Speed: 1),
				new("knight_fire", Sets(RuneSet.Lethal, RuneSet.Strike), Speed: 1),
				new("crow_fire", Sets(RuneSet.Haste, RuneSet.Strike), Speed: 2),
				new("imp_fire", Sets(RuneSet.Haste, RuneSet.Finesse), RuneStat.Accuracy, 3),
				new("knight_wind", Sets(RuneSet.Haste, RuneSet.Vigor), Speed: 4),
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
			_ => Prepared(database, Specialists[dungeon]),
		};

		/// <summary>A preparação do andar 5 (6★ nível 40, desperta, habilidades no máximo, runas 6★ boas) para estes monstros.</summary>
		public static BattleTeam Prepared(GameDatabase database, IReadOnlyList<Member> members) =>
			Team(database, _ => 6, 40, 6, 12, members, awakened: true, maxSkills: true, picks: 8, evenLevel: 15);

		/// <summary>
		/// O time forte sem especialização: o típico com o mesmo investimento do especialista do andar 5.
		/// Vence o que pede força, mas não um chefe cuja mecânica ele não sabe enfrentar.
		/// </summary>
		public static BattleTeam Powerful(GameDatabase database) =>
			Team(database, _ => 6, 40, 6, 12, null, awakened: true, maxSkills: true, picks: 8, evenLevel: 15);

		/// <summary>
		/// Um time qualquer com o investimento pedido (a ferramenta de balanceamento, <see cref="BalanceLab"/>):
		/// as estrelas de cada um, o nível, o Despertar, as habilidades e runas de <paramref name="runeGrade"/>
		/// estrelas até +<paramref name="runeLevel"/> (0: sem runas), com os conjuntos de sempre para o papel.
		/// O nível fica no máximo das estrelas de cada um.
		/// </summary>
		public static BattleTeam Custom(GameDatabase database, IReadOnlyList<string> ids, Func<SummonDefinition, int> stars, int level,
			bool awakened, bool maxSkills, int runeGrade, int runeLevel, int evenLevel, int picks)
		{
			var team = Team(database, stars, level, runeGrade, runeLevel, ids.Select(id => new Member(id, Array.Empty<RuneSet>())).ToList(),
				awakened, maxSkills, Math.Max(1, picks), evenLevel);
			return new BattleTeam(team.Members.Select(m => m with { Level = Math.Min(m.Level, m.Stars >= 6 ? 40 : 10 + 5 * m.Stars) }).ToList());
		}

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
			var mains = new[] { RuneStat.AttackFlat, attack && member.Speed == 0 ? RuneStat.AttackPercent : RuneStat.Speed, RuneStat.DefenseFlat, percent, RuneStat.HealthFlat, member.Six ?? percent };
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
					if (best == null || Score(rune, attack, member.Speed) > Score(best, attack, member.Speed))
						best = rune;
				}

				runes.Add(best!);
			}

			return runes;
		}

		/// <summary>Os subatributos que servem à função, em sorteios máximos (1 = um sorteio cheio); a Velocidade vale mais com a prioridade.</summary>
		private static double Score(Rune rune, bool attack, int speed) => rune.Substats.Sum(sub =>
		{
			var weight = sub.Stat switch
			{
				RuneStat.Speed => 1.2 + speed,
				RuneStat.AttackPercent or RuneStat.Crit or RuneStat.CritDamage => attack ? 1 : 0,
				RuneStat.HealthPercent or RuneStat.DefensePercent => attack ? 0.3 : 1,
				RuneStat.Resistance or RuneStat.Accuracy => attack ? 0.3 : 0.8,
				_ => 0,
			};
			return weight * sub.Value / RuneRules.SubstatRange(sub.Stat, rune.Grade).Max;
		});
	}
}
