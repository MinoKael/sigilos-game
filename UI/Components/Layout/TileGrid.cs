using System;
using System.Collections.Generic;
using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Grade de peças do mesmo tamanho (runas, cartões de monstro) que enche a largura que tiver: cabem
	/// quantas colunas couberem com pelo menos <see cref="Gap"/> entre elas, e a sobra vira espaço entre
	/// as colunas, para a grade ir de uma borda à outra em qualquer tela. A última fileira fica à
	/// esquerda, alinhada com as de cima. O tamanho da célula é o da maior peça.
	/// </summary>
	public partial class TileGrid : Container
	{
		private int _columns;

		public TileGrid(float gap = 8)
		{
			Gap = gap;
			MouseFilter = MouseFilterEnum.Pass;
		}

		/// <summary>O espaço mínimo entre as peças, nas duas direções.</summary>
		public float Gap { get; }

		public override Vector2 _GetMinimumSize()
		{
			var (cell, count) = Measure();
			if (count == 0)
				return Vector2.Zero;
			var rows = (count + Columns(cell) - 1) / Columns(cell);
			return new Vector2(cell.X, rows * cell.Y + (rows - 1) * Gap);
		}

		public override void _Notification(int what)
		{
			if (what == NotificationSortChildren)
				Arrange();
			else if (what == NotificationResized)
			{
				// A altura depende de quantas colunas a largura nova comporta.
				var (cell, _) = Measure();
				if (Columns(cell) != _columns)
					UpdateMinimumSize();
			}
		}

		private void Arrange()
		{
			var (cell, _) = Measure();
			var columns = _columns = Columns(cell);
			var step = columns > 1 ? (Size.X - columns * cell.X) / (columns - 1) : 0;
			var index = 0;
			foreach (var child in Tiles())
			{
				var column = index % columns;
				var row = index / columns;
				FitChildInRect(child, new Rect2(column * (cell.X + step), row * (cell.Y + Gap), cell));
				index++;
			}
		}

		private int Columns(Vector2 cell) => cell.X <= 0 ? 1 : Math.Max(1, (int)((Size.X + Gap) / (cell.X + Gap)));

		private (Vector2 Cell, int Count) Measure()
		{
			var cell = Vector2.Zero;
			var count = 0;
			foreach (var child in Tiles())
			{
				cell = cell.Max(child.GetCombinedMinimumSize());
				count++;
			}

			return (cell, count);
		}

		private IEnumerable<Control> Tiles()
		{
			foreach (var node in GetChildren())
			{
				if (node is Control { Visible: true, TopLevel: false } control)
					yield return control;
			}
		}
	}
}
