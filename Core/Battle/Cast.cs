using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Uma habilidade sendo usada: quem lança, em quem mira e o que ela já fez. Os efeitos
	/// (Effects/SkillEffect) recebem isto para saber em quem agir, e as regras de quem lança e de quem
	/// apanha leem o resultado no fim.
	/// </summary>
	internal sealed class Cast
	{
		private readonly IReadOnlyList<BattleUnit> _allies;
		private readonly IReadOnlyList<BattleUnit> _opponents;
		private readonly HashSet<(UnitRule Rule, BattleUnit Target)> _once = new();
		private readonly Dictionary<(TargetKind Kind, bool ExceptCaster), IReadOnlyList<BattleUnit>> _drawn = new();

		public Cast(EffectResolver resolver, BattleUnit caster, BattleUnit? main, IReadOnlyList<BattleUnit> allies, IReadOnlyList<BattleUnit> opponents, bool counter, bool joint = false, bool trigger = false, BattleUnit? chosenAlly = null)
		{
			IsTrigger = trigger;
			_chosenAlly = chosenAlly;
			Resolver = resolver;
			Caster = caster;
			Main = main;
			_allies = allies;
			_opponents = opponents;
			IsCounter = counter;
			IsJoint = joint;
		}

		private readonly BattleUnit? _chosenAlly;

		public EffectResolver Resolver { get; }
		public BattleUnit Caster { get; }

		/// <summary>Os efeitos da habilidade ligados a cada golpe (<see cref="EffectDefinition.When"/> diferente de Skill).</summary>
		public IReadOnlyList<EffectDefinition> HitEffects { get; set; } = System.Array.Empty<EffectDefinition>();

		/// <summary>
		/// Quem o golpe de agora acertou, enquanto os efeitos de cada golpe agem: o "Target" deles é este, não o alvo
		/// principal da habilidade. Nulo fora disso.
		/// </summary>
		public BattleUnit? Struck { get; set; }

		/// <summary>
		/// O alvo escolhido, uma vez para a habilidade inteira: se ele cai no primeiro golpe, a Queimadura
		/// que vinha depois não pula para outro inimigo. Nulo quando não sobrou inimigo.
		/// </summary>
		public BattleUnit? Main { get; }

		/// <summary>Os efeitos de uma Passiva genérica: nelas, "Target" é o atingido ou quem atacou, não o alvo da habilidade.</summary>
		public bool IsTrigger { get; }

		/// <summary>Contra-ataque: dano reduzido, e quem apanha não contra-ataca de volta.</summary>
		public bool IsCounter { get; }

		/// <summary>A básica de um aliado chamado por um ataque conjunto: ela não chama outros.</summary>
		public bool IsJoint { get; }

		/// <summary>Multiplica o dano de todo golpe desta habilidade (contra-ataque e o bônus por efeitos).</summary>
		public double Scale => (IsCounter ? RuneSets.CounterDamage : 1) * (1 + DamageBonus);

		/// <summary>Dano a mais nos golpes seguintes desta habilidade (0,2 = +20%), somado pelo BonusPerStatus.</summary>
		public double DamageBonus { get; set; }

		/// <summary>Cura e escudo a mais nos efeitos seguintes desta habilidade, somados pelo BonusPerStatus.</summary>
		public double HealBonus { get; set; }

		/// <summary>Turnos a menos na recarga desta habilidade (o ExtraTurnOnKill).</summary>
		public int CooldownReduction { get; set; }

		/// <summary>Esta habilidade já deu um turno extra: outro efeito igual nela não dá de novo.</summary>
		public bool ExtraTurnGranted { get; set; }

		/// <summary>Algum golpe desta habilidade derrubou o alvo: libera os efeitos "ao derrubar".</summary>
		public bool Killed { get; set; }

		/// <summary>A Vida que o Sacrifício desta habilidade tirou de quem lança: os efeitos seguintes escalam nela (<see cref="ScaleStat.Sacrificed"/>).</summary>
		public double Sacrificed { get; set; }

		/// <summary>O dano que cada alvo atingido levou nesta habilidade, na ordem em que foram atingidos.</summary>
		public Dictionary<BattleUnit, double> Dealt { get; } = new();

		/// <summary>
		/// As condições do efeito (<see cref="EffectDefinition.IfAlly"/>, <see cref="EffectDefinition.IfDefenseAbove"/>)
		/// valem agora. Sem condição, sempre.
		/// </summary>
		public bool Allows(EffectDefinition effect)
		{
			if (effect.IfAlly.Length > 0 && !_allies.Any(unit => unit != Caster && unit.IsAlive && unit.DefinitionId == effect.IfAlly))
				return false;

			return !effect.IfDefenseAbove || (Main is { } main && Caster.Defense > main.Defense);
		}

		/// <summary>As unidades que um efeito atinge, do ponto de vista de quem lança.</summary>
		public IReadOnlyList<BattleUnit> Targets(EffectDefinition effect)
		{
			// ChosenAlly: o aliado que o jogador escolheu; sem escolha que valha, o de menor fração de Vida.
			if (effect.Target == TargetKind.ChosenAlly)
			{
				var ally = _chosenAlly is { IsAlive: true } picked && picked != Caster && _allies.Contains(picked) ? picked : Targeting.DefaultAlly(Caster, _allies);
				return ally == null ? System.Array.Empty<BattleUnit>() : new[] { ally };
			}

			// O efeito de cada golpe mira quem foi acertado (antes da regra do chefe, que o abriria para todos).
			if (Struck != null && effect.Target == TargetKind.Target)
				return Struck.IsAlive ? new[] { Struck } : System.Array.Empty<BattleUnit>();

			return Targets(Scope(effect), effect.By, effect.OthersOnly);
		}

		/// <summary>
		/// Todo golpe de chefe é em área: o efeito ofensivo de uma habilidade dele que miraria o alvo único atinge todos
		/// os inimigos. Vale para qualquer chefe (os de Data/enemies.json já vêm em área; o guardião de uma constelação
		/// usa as habilidades da família, compartilhadas com o jogador) e fica fora das Passivas, onde "Target" é o atingido.
		/// </summary>
		private TargetKind Scope(EffectDefinition effect) =>
			Caster.IsBoss && !IsTrigger && effect.Target == TargetKind.Target
			&& effect.Kind is EffectKind.Damage or EffectKind.Status or EffectKind.Impeto or EffectKind.StealBuff or EffectKind.InvertBuffs or EffectKind.BombTrigger
				? TargetKind.AllEnemies
				: effect.Target;

		/// <summary>
		/// As unidades de <paramref name="kind"/>, do ponto de vista de quem lança. O sorteio dos ao acaso vale
		/// para a habilidade inteira, como o alvo principal: o efeito seguinte cai no mesmo sorteado.
		/// </summary>
		/// <param name="exceptCaster">Os aliados sem quem lança (o ataque conjunto chama os outros).</param>
		public IReadOnlyList<BattleUnit> Targets(TargetKind kind, TargetRank by, bool exceptCaster = false)
		{
			var allies = exceptCaster ? _allies.Where(unit => unit != Caster).ToList() : _allies;
			if (kind is not (TargetKind.RandomAlly or TargetKind.RandomEnemy))
				return Targeting.Resolve(kind, by, Caster, Main, allies, _opponents, Resolver.Random);

			if (!_drawn.TryGetValue((kind, exceptCaster), out var drawn))
				_drawn[(kind, exceptCaster)] = drawn = Targeting.Resolve(kind, by, Caster, Main, allies, _opponents, Resolver.Random);
			return drawn;
		}

		/// <summary>
		/// Verdadeiro só na primeira vez que a regra pergunta por este alvo: é o "um sorteio por alvo a
		/// cada habilidade" do conjunto Tormento e da Passiva dos Dragões, que não repete a cada golpe.
		/// </summary>
		public bool FirstTime(UnitRule rule, BattleUnit target) => _once.Add((rule, target));
	}
}
