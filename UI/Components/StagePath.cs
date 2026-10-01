using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A trilha da Campanha: uma pedra por fase, em fileiras que vão e voltam (a primeira da esquerda
	/// para a direita, a seguinte ao contrário), ligadas por um caminho. O trecho já vencido brilha em
	/// verde; a fase escolhida fica acesa; as fechadas, apagadas.
	/// </summary>
	public partial class StagePath : Control
	{
		private const int PerRow = 5;
		private const float Node = 64;

		private readonly List<SigilButton> _nodes = new();
		private readonly int _cleared;

		/// <param name="cleared">Quantas fases já foram vencidas (o caminho brilha até aí).</param>
		public StagePath(int count, int cleared, int unlocked, int selected)
		{
			_cleared = cleared;
			var rows = (count + PerRow - 1) / PerRow;
			CustomMinimumSize = new Vector2(PerRow * 96 + 20, rows * 104 + 10);
			MouseFilter = MouseFilterEnum.Ignore;

			for (var number = 1; number <= count; number++)
			{
				var stage = number;
				var node = new SigilButton(null, number == count ? Node + 12 : Node)
				{
					Name = $"Stage{number}",
					Letters = number.ToString(),
					ToggleMode = true,
					ButtonPressed = number == selected,
					Disabled = number > unlocked,
				};
				if (number <= cleared)
					node.Accent = Palette.Spirit;
				node.Highlight = number == cleared + 1 && number <= unlocked && number != selected;
				node.Pressed += () => Chosen?.Invoke(stage);
				_nodes.Add(node);
				AddChild(node);
			}

			Resized += Arrange;
		}

		/// <summary>O jogador tocou numa fase.</summary>
		public event Action<int>? Chosen;

		public override void _Ready() => Arrange();

		public override void _Draw()
		{
			for (var i = 0; i + 1 < _nodes.Count; i++)
			{
				var a = Center(_nodes[i]);
				var b = Center(_nodes[i + 1]);
				var lit = i + 1 < _cleared;
				DrawLine(a, b, new Color(0, 0, 0, 0.5f), 8, true);
				DrawLine(a, b, lit ? new Color(Palette.Spirit, 0.7f) : new Color(Palette.GoldDark, 0.7f), lit ? 3 : 2, true);
			}
		}

		private static Vector2 Center(Control control) => control.Position + control.Size / 2;

		private void Arrange()
		{
			var rows = (_nodes.Count + PerRow - 1) / PerRow;
			var step = new Vector2((Size.X - 20) / PerRow, Mathf.Max(96, (Size.Y - 10) / Mathf.Max(1, rows)));
			for (var i = 0; i < _nodes.Count; i++)
			{
				var row = i / PerRow;
				var column = row % 2 == 0 ? i % PerRow : PerRow - 1 - i % PerRow;
				var node = _nodes[i];
				node.Size = node.CustomMinimumSize;
				// Um leve sobe-e-desce dentro da fileira, para parecer trilha e não tabela.
				var wave = Mathf.Sin(column * 1.3f + row) * 10;
				var center = new Vector2(10 + step.X * (column + 0.5f), 5 + step.Y * (row + 0.5f) + wave);
				node.Position = center - node.Size / 2;
			}

			QueueRedraw();
		}
	}
}
