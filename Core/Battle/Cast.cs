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

		public Cast(EffectResolver resolver, BattleUnit caster, BattleUnit? main, IReadOnlyList<BattleUnit> allies, IReadOnlyList<BattleUnit> opponents, bool counter, bool joint = false)
		{
			Resolver = resolver;
			Caster = caster;
			Main = main;
			_allies = allies;
			_opponents = opponents;
			IsCounter = counter;
			IsJoint = joint;
		}

		public EffectResolver Resolver { get; }
		public BattleUnit Caster { get; }

		/// <summary>
		/// O alvo escolhido, uma vez para a habilidade inteira: se ele cai no primeiro golpe, a Queimadura
		/// que vinha depois não pula para outro inimigo. Nulo quando não sobrou inimigo.
		/// </summary>
		public BattleUnit? Main { get; }

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

		/// <summary>O dano que cada alvo atingido levou nesta habilidade, na ordem em que foram atingidos.</summary>
		public Dictionary<BattleUnit, double> Dealt { get; } = new();

		/// <summary>As unidades que um efeito atinge, do ponto de vista de quem lança.</summary>
		public IReadOnlyList<BattleUnit> Targets(EffectDefinition effect) => Targets(effect.Target, effect.By);

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
