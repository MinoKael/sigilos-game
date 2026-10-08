using System;
using Godot;
using Sigilos.UI.Audio;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A seta de voltar do cabeçalho: o símbolo que todo mundo reconhece, sem texto. Responde também ao
	/// Esc e ao Voltar do celular (que o GameRoot transforma em <c>ui_cancel</c>) quando nenhuma janela
	/// por cima pegou antes.
	/// </summary>
	public partial class BackButton : SigilButton
	{
		private readonly Action _onBack;

		public BackButton(Action onBack) : base(Art.Icon("back"), 52, SigilShape.Square)
		{
			_onBack = onBack;
			Pressed += Back;
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (!@event.IsActionPressed("ui_cancel") || !IsVisibleInTree())
				return;
			GetViewport().SetInputAsHandled();
			Back();
		}

		/// <summary>Volta, com o som de voltar no lugar do clique e da tela que abre.</summary>
		private void Back()
		{
			Sfx.Fallback("ui.back", 3);
			_onBack();
		}
	}
}
