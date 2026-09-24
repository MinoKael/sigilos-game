using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.UI
{
	/// <summary>
	/// Todo texto em português que não vem de Data/: nomes de Glifo, elemento, efeito, e a descrição
	/// de habilidades e páginas gerada a partir dos efeitos. Gerar a descrição garante que o texto diz
	/// exatamente o que o combate faz — não há frase escrita à mão para ficar desatualizada.
	/// </summary>
	public static class Texts
	{
		public static string Name(Glyph glyph) => glyph switch
		{
			Glyph.Wall => "Muralha",
			Glyph.Eye => "Olho",
			Glyph.Door => "Porta",
			Glyph.Bond => "Laço",
			Glyph.Shard => "Estilhaço",
			Glyph.Veil => "Véu",
			Glyph.Bone => "Ossada",
			Glyph.Spiral => "Espiral",
			_ => glyph.ToString(),
		};

		public static string Name(Element element) => element switch
		{
			Element.Fire => "Fogo",
			Element.Water => "Água",
			Element.Wind => "Vento",
			Element.Light => "Luz",
			Element.Dark => "Trevas",
			_ => element.ToString(),
		};

		public static string Name(Role role) => role switch
		{
			Role.Front => "Frente",
			Role.Attacker => "Atacante",
			Role.Support => "Suporte",
			Role.Control => "Controle",
			_ => role.ToString(),
		};

		public static string Name(Form form) => form switch
		{
			Form.Single => "Único",
			Form.Double => "Dupla",
			Form.All => "Todos",
			_ => form.ToString(),
		};

		public static string Name(Posture posture) => posture switch
		{
			Posture.Aggressive => "Agressiva",
			Posture.Balanced => "Equilibrada",
			Posture.Economic => "Econômica",
			_ => posture.ToString(),
		};

		public static string Name(Stat stat) => stat switch
		{
			Stat.Health => "Vida",
			Stat.Attack => "Ataque",
			Stat.Defense => "Defesa",
			Stat.Speed => "Velocidade",
			Stat.Crit => "Crítico",
			Stat.CritDamage => "Dano crítico",
			Stat.Focus => "Foco",
			Stat.Resistance => "Resistência",
			_ => stat.ToString(),
		};

		public static string Name(StatusKind status) => status switch
		{
			StatusKind.Shield => "Escudo",
			StatusKind.Burn => "Queimadura",
			StatusKind.Stun => "Atordoamento",
			StatusKind.Taunt => "Provocação",
			StatusKind.Hidden => "Oculto",
			StatusKind.Curse => "Maldição",
			StatusKind.Blind => "Cegueira",
			StatusKind.Ward => "Égide",
			StatusKind.Foresight => "Presságio",
			StatusKind.AttackUp => "Ataque+",
			StatusKind.AttackDown => "Ataque−",
			StatusKind.DefenseUp => "Defesa+",
			StatusKind.SpeedUp => "Velocidade+",
			_ => status.ToString(),
		};

		/// <summary>Três letras para o selo de efeito no cartão da unidade.</summary>
		public static string Short(StatusKind status) => status switch
		{
			StatusKind.Shield => "ESC",
			StatusKind.Burn => "QMD",
			StatusKind.Stun => "ATD",
			StatusKind.Taunt => "PRV",
			StatusKind.Hidden => "OCL",
			StatusKind.Curse => "MAL",
			StatusKind.Blind => "CEG",
			StatusKind.Ward => "ÉGI",
			StatusKind.Foresight => "PRS",
			StatusKind.AttackUp => "ATQ+",
			StatusKind.AttackDown => "ATQ−",
			StatusKind.DefenseUp => "DEF+",
			StatusKind.SpeedUp => "VEL+",
			_ => "?",
		};

		public static string Circle(int circle) => circle switch
		{
			1 => "I",
			2 => "II",
			_ => "III",
		};

		public static string Stars(int count) => new('★', count);

		public static string Scrolls(int count) => count == 1 ? "1 Pergaminho" : $"{count} Pergaminhos";

		public static string Describe(IEnumerable<EffectDefinition> effects, string powerOf = "do Ataque") =>
			string.Join("; ", effects.Select(e => Describe(e, powerOf))) + ".";

		public static string Describe(SkillDefinition skill)
		{
			var text = Describe(skill.Effects);
			if (skill.CanEnhance)
				text += $"\n+{skill.EnhanceCost} Éter: {Describe(skill.EnhancedEffects)}";
			return text;
		}

		public static string Describe(PageDefinition page) => Describe(PageFormula.Effects(page), "do Poder");

		private static string Describe(EffectDefinition effect, string powerOf)
		{
			var where = Where(effect.Target);
			var chance = effect.Chance < 1 ? $"{Percent(effect.Chance)} de chance de " : "";
			var text = effect.Kind switch
			{
				EffectKind.Damage => DamageText(effect, powerOf, where),
				EffectKind.Heal => $"cura {Percent(effect.Power)} da Vida {where}",
				EffectKind.Shield => $"escudo de {Percent(effect.Power)} da Vida {where} por {Turns(effect.Turns)}",
				EffectKind.Status => $"{chance}{Name(effect.Status)} {where} por {Turns(effect.Turns)}",
				EffectKind.Impeto => $"{(effect.Power >= 0 ? "+" : "−")}{System.Math.Abs(effect.Power):0}% de Ímpeto {where}",
				EffectKind.Cleanse => $"remove um efeito negativo {where}",
				EffectKind.Revive => $"revive um aliado caído com {Percent(effect.Power)} da Vida",
				_ => effect.Kind.ToString(),
			};
			return effect.OnKill ? $"se derrubar o alvo, {text}" : text;
		}

		private static string DamageText(EffectDefinition effect, string powerOf, string where)
		{
			var hits = effect.Hits > 1 ? $"{effect.Hits} golpes de " : "";
			var text = $"{hits}{Percent(effect.Power)} {powerOf} {where}";
			if (effect.IgnoreDefense > 0)
				text += $", ignora {Percent(effect.IgnoreDefense)} da Defesa";
			if (effect.Drain > 0)
				text += $", drena {Percent(effect.Drain)} como Vida";
			return text;
		}

		private static string Where(TargetKind target) => target switch
		{
			TargetKind.Target => "no alvo",
			TargetKind.TwoEnemies => "em dois inimigos",
			TargetKind.AllEnemies => "em todos os inimigos",
			TargetKind.Self => "em si",
			TargetKind.LowestAlly => "no aliado mais ferido",
			TargetKind.TwoAllies => "nos dois aliados mais feridos",
			TargetKind.AllAllies => "em todos os aliados",
			TargetKind.DeadAlly => "num aliado caído",
			_ => "",
		};

		private static string Turns(int turns) => turns == 1 ? "1 turno" : $"{turns} turnos";

		private static string Percent(double fraction) => $"{fraction * 100:0}%";
	}
}
