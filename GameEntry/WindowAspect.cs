using System;
using Godot;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// O tamanho da interface na janela. A base é a do projeto (1280×720, stretch "canvas_items").
	///
	/// No PC, a janela do jogo fica sempre em 16:9: puxar uma borda corrige a outra (a que o jogador puxou
	/// manda), sem passar do tamanho da tela nem ficar menor que <see cref="MinimumSize"/>. Maximizada ou em
	/// tela cheia, quem decide o tamanho é o monitor; aí o conteúdo fica em 16:9 com faixas nas sobras, em
	/// vez de esticar.
	///
	/// No celular, a interface preenche a tela inteira (project.godot, aspect "expand": sem faixas pretas)
	/// e cresce com a sobra (<see cref="FitMobile"/>): numa tela mais larga que 16:9, a sobra de largura vira
	/// tamanho, até a área lógica chegar a <see cref="MobileMinimum"/>. A largura do projeto fica sempre
	/// inteira, então nada que coube em 1280 deixa de caber; a altura encolhe no máximo até 80%, e as telas
	/// rolam na vertical. O cálculo é pela proporção da tela, não por um número fixo: 16:9 fica como é,
	/// 20:9 cresce 25%. Com <c>-- --mobile</c>, o PC se comporta assim (para ver o celular no PC).
	///
	/// Num celular de verdade, a área segura da tela (o recorte da câmera, os cantos arredondados, a barra de
	/// gestos) vira <see cref="Insets"/>, em unidades da interface: o <see cref="GameRoot"/> encolhe a interface
	/// por elas, e nenhum botão fica atrás do recorte.
	/// </summary>
	public partial class WindowAspect : Node
	{
		private const float Ratio = 16f / 9f;
		private static readonly Vector2I MinimumSize = new(960, 540);

		/// <summary>A menor área lógica no celular: a largura do projeto e 80% da altura.</summary>
		private static readonly Vector2 MobileMinimum = new(1280, 576);

		private readonly bool _mobile;
		private Vector2I _last;
		private bool _fixing;

		/// <summary>A área segura mudou (girou a tela, trocou o modo de gestos): os recuos novos.</summary>
		public event Action<Insets>? InsetsChanged;

		/// <summary>Quanto a interface recua de cada borda, em unidades da interface.</summary>
		public readonly record struct Insets(float Left, float Top, float Right, float Bottom)
		{
			public static readonly Insets None = new(0, 0, 0, 0);
		}

		/// <summary>Os recuos de agora (<see cref="Insets.None"/> no PC, que não tem recorte).</summary>
		public Insets Current { get; private set; } = Insets.None;


		/// <param name="mobile">O celular, ou o PC fingindo ser um (<c>--mobile</c>).</param>
		public WindowAspect(bool mobile)
		{
			Name = "WindowAspect";
			_mobile = mobile;
		}

		public override void _Ready()
		{
			var window = GetWindow();
			if (_mobile)
			{
				window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
				window.SizeChanged += FitMobile;
				FitMobile();
				if (OS.HasFeature("mobile"))
				{
					window.SizeChanged += ReadInsets;
					ReadInsets();
				}

				return;
			}

			if (!OS.HasFeature("pc"))
				return;

			window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
			window.MinSize = MinimumSize;
			_last = window.Size;
			window.SizeChanged += Fit;
			Fit();
		}

		/// <summary>
		/// Quanto a interface cresce: a área lógica sem aumento é a base esticada pela proporção da tela;
		/// o aumento é o maior que ainda deixa as duas medidas acima de <see cref="MobileMinimum"/>, e
		/// nunca menos que 1.
		/// </summary>
		private void FitMobile()
		{
			var window = GetWindow();
			var size = (Vector2)window.Size;
			var design = (Vector2)window.ContentScaleSize;
			if (size.X <= 0 || size.Y <= 0 || design.X <= 0 || design.Y <= 0)
				return;

			var logical = size / Math.Min(size.X / design.X, size.Y / design.Y);
			window.ContentScaleFactor = Math.Max(1f, Math.Min(logical.X / MobileMinimum.X, logical.Y / MobileMinimum.Y));
		}

		/// <summary>
		/// A área segura do sistema, em pixels da tela, vira recuo em unidades da interface (a escala do conteúdo
		/// já aplicada). Quem lê duas vezes o mesmo valor não avisa de novo.
		/// </summary>
		private void ReadInsets()
		{
			// Contra a janela, não contra a tela: se o sistema já mantém a janela fora do recorte, não recua duas vezes.
			var window = GetWindow();
			var origin = window.Position;
			var end = origin + window.Size;
			var safe = DisplayServer.GetDisplaySafeArea();
			var scale = window.GetFinalTransform().Scale;
			if (window.Size.X <= 0 || window.Size.Y <= 0 || safe.Size.X <= 0 || safe.Size.Y <= 0 || scale.X <= 0 || scale.Y <= 0)
				return;

			var insets = new Insets(
				Math.Max(0, safe.Position.X - origin.X) / scale.X,
				Math.Max(0, safe.Position.Y - origin.Y) / scale.Y,
				Math.Max(0, end.X - safe.End.X) / scale.X,
				Math.Max(0, end.Y - safe.End.Y) / scale.Y);
			if (insets == Current)
				return;

			Current = insets;
			InsetsChanged?.Invoke(insets);
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
