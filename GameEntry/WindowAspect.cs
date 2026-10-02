using System;
using Godot;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// No PC, a janela do jogo fica sempre em 16:9: puxar uma borda corrige a outra (a que o jogador puxou
	/// manda), sem passar do tamanho da tela nem ficar menor que <see cref="MinimumSize"/>. Maximizada ou em
	/// tela cheia, quem decide o tamanho é o monitor; aí o conteúdo fica em 16:9 com faixas nas sobras, em
	/// vez de esticar. No celular nada muda: a tela não é 16:9 e a interface preenche ela inteira
	/// (project.godot, stretch "expand").
	/// </summary>
	public partial class WindowAspect : Node
	{
		private const float Ratio = 16f / 9f;
		private static readonly Vector2I MinimumSize = new(960, 540);

		private Vector2I _last;
		private bool _fixing;

		public WindowAspect()
		{
			Name = "WindowAspect";
		}

		public override void _Ready()
		{
			if (!OS.HasFeature("pc"))
				return;

			var window = GetWindow();
			window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
			window.MinSize = MinimumSize;
			_last = window.Size;
			window.SizeChanged += Fit;
			Fit();
		}

		private void Fit()
		{
			var window = GetWindow();
			if (_fixing || window.Mode != Window.ModeEnum.Windowed)
			{
				_last = window.Size;
				return;
			}

			var size = window.Size;
			var widthMoved = Math.Abs(size.X - _last.X) >= Math.Abs(size.Y - _last.Y);
			var wanted = widthMoved
				? new Vector2(size.X, size.X / Ratio)
				: new Vector2(size.Y * Ratio, size.Y);

			// Não passa da área útil da tela: se a borda corrigida não cabe, encolhe os dois lados juntos.
			var usable = DisplayServer.ScreenGetUsableRect(window.CurrentScreen).Size;
			var shrink = Math.Min(1f, Math.Min(usable.X / wanted.X, usable.Y / wanted.Y));
			var fitted = new Vector2I((int)Math.Round(wanted.X * shrink), (int)Math.Round(wanted.Y * shrink));
			fitted = new Vector2I(Math.Max(fitted.X, MinimumSize.X), Math.Max(fitted.Y, MinimumSize.Y));

			_last = fitted;
			if (fitted == size)
				return;

			_fixing = true;
			window.Size = fitted;
			_fixing = false;
		}
	}
}
