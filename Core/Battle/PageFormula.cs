using System;
using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Custo e efeito de uma página do Grimório por fórmula, não à mão (GDD, seções 6 e 15):
	///
	///   Custo = Círculo + Forma − Ressonância, mínimo 1.
	///   Potência por alvo = Círculo (100%, 200%, 350%) × Forma (Único 100%, Dupla 75%, Todos 55%).
	///
	/// A potência escala o efeito base do Glifo; o Círculo III soma o efeito extra do Glifo.
	/// Porta (invocar espírito) fica para depois do MVP.
	/// </summary>
	public static class PageFormula
	{
		public static int CircleCost(int circle) => circle switch
		{
			1 => 1,
			2 => 3,
			_ => 6,
		};

		public static int FormCost(Form form) => form switch
		{
			Form.Single => 0,
			Form.Double => 1,
			_ => 2,
		};

		public static double CirclePotency(int circle) => circle switch
		{
			1 => 1.0,
			2 => 2.0,
			_ => 3.5,
		};

		public static double FormPotency(Form form) => form switch
		{
			Form.Single => 1.0,
			Form.Double => 0.75,
			_ => 0.55,
		};

		public static double Potency(PageDefinition page) => CirclePotency(page.Circle) * FormPotency(page.Form);

		/// <summary>Estilhaço, Ossada e Laço miram inimigos; os outros Glifos, aliados.</summary>
		public static bool IsOffensive(Glyph glyph) => glyph is Glyph.Shard or Glyph.Bone or Glyph.Bond;

		/// <summary><paramref name="sameGlyphInTeam"/>: quantas invocações do time têm o Glifo da página.</summary>
		public static int Cost(PageDefinition page, ConjurerDefinition conjurer, int sameGlyphInTeam)
		{
			var discount = conjurer.CircleDiscounts.TryGetValue(page.Circle, out var value) ? value : 0;
			var resonance = sameGlyphInTeam >= 2 ? 1 : 0;
			return Math.Max(1, CircleCost(page.Circle) - discount + FormCost(page.Form) - resonance);
		}

		public static IReadOnlyList<EffectDefinition> Effects(PageDefinition page)
		{
			var p = Potency(page);
			var third = page.Circle >= 3;
			var enemies = page.Form switch
			{
				Form.Single => TargetKind.Target,
				Form.Double => TargetKind.TwoEnemies,
				_ => TargetKind.AllEnemies,
			};
			var allies = page.Form switch
			{
				Form.Single => TargetKind.LowestAlly,
				Form.Double => TargetKind.TwoAllies,
				_ => TargetKind.AllAllies,
			};

			var effects = new List<EffectDefinition>();
			switch (page.Glyph)
			{
				case Glyph.Shard:
					effects.Add(new EffectDefinition { Kind = EffectKind.Damage, Target = enemies, Power = p, IgnoreDefense = third ? 0.5 : 0 });
					break;

				case Glyph.Bone:
					effects.Add(new EffectDefinition { Kind = EffectKind.Damage, Target = enemies, Power = 0.7 * p, Drain = 1 });
					if (third)
						effects.Add(new EffectDefinition { Kind = EffectKind.Revive, Target = TargetKind.DeadAlly, Power = 0.3 });
					break;

				case Glyph.Bond:
					effects.Add(new EffectDefinition { Kind = EffectKind.Damage, Target = enemies, Power = 0.3 * p });
					effects.Add(new EffectDefinition { Kind = EffectKind.Status, Target = enemies, Status = StatusKind.Taunt, Turns = page.Circle >= 2 ? 2 : 1 });
					if (third)
						effects.Add(new EffectDefinition { Kind = EffectKind.Status, Target = enemies, Status = StatusKind.Stun });
					break;

				case Glyph.Wall:
					effects.Add(new EffectDefinition { Kind = EffectKind.Shield, Target = allies, Power = 0.12 * p, Turns = 2 });
					if (third)
						effects.Add(new EffectDefinition { Kind = EffectKind.Status, Target = allies, Status = StatusKind.Ward });
					break;

				case Glyph.Eye:
					effects.Add(new EffectDefinition { Kind = EffectKind.Impeto, Target = allies, Power = 15 * p });
					if (third)
						effects.Add(new EffectDefinition { Kind = EffectKind.Status, Target = allies, Status = StatusKind.Foresight });
					break;

				case Glyph.Veil:
					effects.Add(new EffectDefinition { Kind = EffectKind.Status, Target = allies, Status = StatusKind.Hidden });
					effects.Add(new EffectDefinition { Kind = EffectKind.Heal, Target = allies, Power = 0.04 * p });
					if (third)
						effects.Add(new EffectDefinition { Kind = EffectKind.Status, Target = allies, Status = StatusKind.Ward });
					break;

				case Glyph.Spiral:
					var turns = page.Circle >= 2 ? 2 : 1;
					effects.Add(new EffectDefinition { Kind = EffectKind.Status, Target = allies, Status = StatusKind.SpeedUp, Turns = turns });
					if (page.Circle >= 2)
						effects.Add(new EffectDefinition { Kind = EffectKind.Status, Target = allies, Status = StatusKind.AttackUp, Turns = turns });
					if (third)
						effects.Add(new EffectDefinition { Kind = EffectKind.Status, Target = allies, Status = StatusKind.DefenseUp, Turns = turns });
					break;

				default:
					throw new NotSupportedException($"O Glifo {page.Glyph} ainda não tem fórmula de página.");
			}

			return effects;
		}
	}
}
