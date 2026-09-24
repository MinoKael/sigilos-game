using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Uma página do Grimório durante a luta: custo já com Ressonância e desconto do Conjurador,
	/// efeitos já calculados pela <see cref="PageFormula"/> e a recarga que falta.
	/// </summary>
	public sealed class PageSlot
	{
		public PageSlot(PageDefinition page, int cost, bool resonant, IReadOnlyList<EffectDefinition> effects)
		{
			Page = page;
			Cost = cost;
			Resonant = resonant;
			Effects = effects;
		}

		public PageDefinition Page { get; }
		public int Cost { get; }

		/// <summary>O Glifo da página existe no time: sem isso a página não pode ser lançada (GDD, seção 6).</summary>
		public bool Resonant { get; }

		public IReadOnlyList<EffectDefinition> Effects { get; }

		/// <summary>Turnos do Conjurador até a página voltar. 0 = pronta.</summary>
		public int Cooldown { get; set; }

		public bool NeedsTarget => PageFormula.IsOffensive(Page.Glyph) && Page.Form != Form.All;
	}
}
