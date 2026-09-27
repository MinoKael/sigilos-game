using System;
using System.Collections.Generic;
using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Abas feitas de sigilos: um por aba, só o símbolo (o nome na dica); o da aba aberta fica aceso.
	/// Em pé (a régua da ficha de Monstros) ou deitadas (Runas, Equipes).
	/// </summary>
	public partial class SigilTabs : BoxContainer
	{
		private readonly List<SigilButton> _buttons = new();
		private readonly ButtonGroup _group = new();

		public SigilTabs(bool vertical, float size = 56)
		{
			Vertical = vertical;
			Size = size;
			AddThemeConstantOverride("separation", 8);
		}

		/// <summary>A aba aberta mudou.</summary>
		public event Action<int>? Changed;

		public int Selected { get; private set; }

		private new float Size { get; }

		/// <summary>Uma aba nova com o símbolo, a dica e, se quiser, um número na plaquinha. O nó se chama <c>Tab0</c>, <c>Tab1</c>...; quem cria pode dar o nome do conteúdo.</summary>
		public SigilButton Add(Texture2D? icon, string tooltip, string badge = "")
		{
			var index = _buttons.Count;
			var button = new SigilButton(icon, tooltip, Size, SigilShape.Square) { Name = $"Tab{index}", ToggleMode = true, ButtonGroup = _group, Badge = badge };
			button.ButtonPressed = index == Selected;
			button.Pressed += () =>
			{
				if (Selected == index)
					return;
				Selected = index;
				Changed?.Invoke(index);
			};
			_buttons.Add(button);
			AddChild(button);
			return button;
		}

		/// <summary>Abre a aba sem avisar (quando a tela já sabe).</summary>
		public void Select(int index)
		{
			Selected = index;
			for (var i = 0; i < _buttons.Count; i++)
				_buttons[i].SetPressedNoSignal(i == index);
		}
	}
}
