using Godot;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// Esconde a interface enquanto a janela está minimizada.
	///
	/// Minimizada, a Godot não desenha nem apresenta quadro, mas os nós animados continuam pedindo desenho a
	/// cada quadro (a Constelação do Santuário, o mapa estelar, a luta). No renderizador de compatibilidade
	/// (OpenGL), cada arco e linha suavizados viram um buffer de GPU na hora do pedido, e o driver só libera
	/// os buffers apagados quando um quadro é apresentado: o Santuário minimizado crescia ~110 MB por segundo
	/// até a janela voltar. Nó escondido não desenha, então não pede buffer nenhum.
	///
	/// A Godot não avisa quando a janela minimiza, e minimizar nem sempre tira o foco: um relógio curto
	/// confere o modo da janela (uma leitura, sem trabalho por quadro).
	/// </summary>
	public partial class MinimizeGuard : Node
	{
		private const double CheckSeconds = 0.25;

		private readonly CanvasItem _ui;

		/// <param name="ui">A raiz de tudo o que o jogo desenha.</param>
		public MinimizeGuard(CanvasItem ui)
		{
			Name = "MinimizeGuard";
			_ui = ui;
		}

		public override void _Ready()
		{
			var check = new Timer { Name = "Check", WaitTime = CheckSeconds, Autostart = true, ProcessMode = ProcessModeEnum.Always };
			check.Timeout += Check;
			AddChild(check);
		}

		private void Check()
		{
			var visible = DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Minimized;
			if (_ui.Visible != visible)
				_ui.Visible = visible;
		}
	}
}
