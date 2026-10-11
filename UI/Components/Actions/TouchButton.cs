using Godot;
using Sigilos.UI.Animations;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A base de todo botão de toque do jogo. Faz o que todos repetiam:
	///
	/// - sem foco de teclado e com o cursor de mão (o foco não tem desenho);
	/// - afunda ao apertar e soa o clique de reserva (<see cref="Juice"/>);
	/// - o conteúdo (<see cref="Content"/>) fica por cima, não recebe toque e apaga quando o botão desliga;
	/// - mede o conteúdo, porque o <c>Button</c> não mede filhos: quem herda diz o tamanho mínimo em
	///   <see cref="MinimumFor"/>;
	/// - <see cref="Highlight"/> pulsa (<see cref="Pulse"/>) até o jogador tocar, e o processo só roda
	///   enquanto pulsa.
	///
	/// Quem herda escolhe as caixas de cada estado, o que vai dentro de <see cref="Content"/> e como o
	/// chamado aparece (<see cref="OnPulse"/>). Quem sobrescreve <c>_Ready</c>, <c>_Process</c> ou
	/// <c>_Draw</c> chama a base.
	/// </summary>
	public partial class TouchButton : Button
	{
		private readonly Pulse _pulse = new();
		private bool _highlight;

		/// <param name="press">A escala ao afundar: menor para botão pequeno (0,93), maior para cartão grande (0,97).</param>
		protected TouchButton(float press = 0.95f)
		{
			FocusMode = FocusModeEnum.None;
			MouseDefaultCursorShape = CursorShape.PointingHand;
			AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

			Content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Content);
			Content.MinimumSizeChanged += Fit;

			Juice.Attach(this, press);
		}

		/// <summary>O conteúdo desenhado por cima do botão (símbolo, texto): não recebe toque e apaga com o botão desligado.</summary>
		protected MarginContainer Content { get; } = new() { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };

		/// <summary>Pulsa em verde espiritual até o jogador tocar (quem herda desenha em <see cref="OnPulse"/>).</summary>
		public bool Highlight
		{
			get => _highlight;
			set
			{
				_highlight = value;
				SetProcess(value);
				OnHighlightChanged();
			}
		}

		/// <summary>A fase do pulso agora, de 0 a 1 (só anda com <see cref="Highlight"/> ligado).</summary>
		protected float PulsePhase => _pulse.Phase;

		/// <summary>As margens do conteúdo: dos lados, em cima e embaixo.</summary>
		protected void Pad(int sides, int top, int bottom) => Pad(sides, sides, top, bottom);

		/// <summary>As margens do conteúdo, uma por lado.</summary>
		protected void Pad(int left, int right, int top, int bottom)
		{
			Content.AddThemeConstantOverride("margin_left", left);
			Content.AddThemeConstantOverride("margin_right", right);
			Content.AddThemeConstantOverride("margin_top", top);
			Content.AddThemeConstantOverride("margin_bottom", bottom);
		}

		/// <summary>O tamanho mínimo para o conteúdo medido; nulo deixa o tamanho como o chamador pôs.</summary>
		protected virtual Vector2? MinimumFor(Vector2 content) => null;

		/// <summary>O chamado ligou ou desligou.</summary>
		protected virtual void OnHighlightChanged()
		{
		}

		/// <summary>Um quadro do pulso, com a fase de 0 a 1.</summary>
		protected virtual void OnPulse(float phase)
		{
		}

		/// <summary>Refaz o tamanho mínimo (para quem muda o que <see cref="MinimumFor"/> leva em conta).</summary>
		protected void Fit()
		{
			if (MinimumFor(Content.GetCombinedMinimumSize()) is { } minimum)
				CustomMinimumSize = minimum;
		}

		public override void _Ready()
		{
			SetProcess(_highlight);
			Fit();
		}

		public override void _Process(double delta)
		{
			_pulse.Advance(delta);
			OnPulse(_pulse.Phase);
		}

		public override void _Draw() => Content.Modulate = Disabled ? new Color(1, 1, 1, Fade.Disabled) : Colors.White;
	}
}
