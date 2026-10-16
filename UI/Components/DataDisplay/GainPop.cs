using System;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O ganho que aparece ao lado de um valor e sobe, sumindo: o "+3" verde da melhora de uma runa e o "+1" da
	/// habilidade que subiu com uma cópia. É um texto solto (<c>TopLevel</c>, que o contêiner não arruma) que
	/// parte do fim do valor e sobe sozinho.
	/// </summary>
	public static class GainPop
	{
		private const float RiseHeight = 30;
		private const float RiseSeconds = 0.75f;

		/// <param name="owner">Quem hospeda o texto (e dá o z); some junto com ele.</param>
		/// <param name="from">O valor ao lado do qual o ganho nasce.</param>
		public static void Show(Control owner, Control from, string text)
		{
			if (!owner.IsInsideTree() || !from.IsInsideTree())
				return;

			// TopLevel sai da árvore de desenho do pai: o z das janelas por cima deixa de somar, e o texto, com 20
			// sozinho, ficava atrás da ficha da Batalha automática. Soma aqui o que o pai somaria.
			var z = 0;
			for (Node? node = owner; node is CanvasItem item; node = node.GetParent())
			{
				z += item.ZIndex;
				if (!item.ZAsRelative || item.TopLevel)
					break;
			}

			var pop = new Label { Name = "Rise", Text = text, TopLevel = true, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = Math.Min(z + 20, (int)RenderingServer.CanvasItemZMax) };
			pop.AddThemeColorOverride("font_color", Palette.Positive);
			pop.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
			pop.AddThemeConstantOverride("outline_size", 4);
			pop.AddThemeFontOverride("font", GameTheme.Serif);
			pop.AddThemeFontSizeOverride("font_size", 20);
			owner.AddChild(pop);
			// No fim do valor, onde o ganho em verde se escreve, e sobe dali.
			var rect = from.GetGlobalRect();
			var start = new Vector2(rect.End.X + 6, rect.Position.Y - 2);
			pop.GlobalPosition = start;

			var tween = pop.CreateTween().SetParallel();
			tween.TweenProperty(pop, "global_position:y", start.Y - RiseHeight, RiseSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(pop, "modulate:a", 0f, RiseSeconds * 0.55f).SetDelay(RiseSeconds * 0.45f);
			tween.Chain().TweenCallback(Callable.From(pop.QueueFree));
		}
	}
}
