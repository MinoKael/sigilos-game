using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Grade de peças do mesmo tamanho (runas, cartões de monstro) que enche a largura que tiver: cabem
	/// quantas colunas couberem com pelo menos <see cref="Gap"/> entre elas, e a sobra vira espaço entre
	/// as colunas, para a grade ir de uma borda à outra em qualquer tela. A última fileira fica à
	/// esquerda, alinhada com as de cima. O tamanho da célula é o da maior peça.
	///
	/// Com <see cref="Virtualize"/> a grade não guarda uma peça por item: só as fileiras que aparecem na
	/// rolagem de quem a contém (mais uma folga) existem, e as outras são criadas e soltas conforme a lista
	/// anda. Uma lista de 800 runas fica em algumas dezenas de nós.
	/// </summary>
	public partial class TileGrid : Container
	{
		/// <summary>Fileiras a mais, acima e abaixo da tela, mantidas prontas: o dedo anda rápido e a lista não pode mostrar vazio.</summary>
		private const int Buffer = 2;

		private int _columns;
		private Func<int, Control>? _create;
		private int _count;
		private Vector2 _cell;
		private readonly Dictionary<int, Control> _live = new();
		private ScrollContainer? _scroll;
		private float _offset;

		public TileGrid(float gap = 8)
		{
			Gap = gap;
			MouseFilter = MouseFilterEnum.Pass;
		}

		/// <summary>O espaço mínimo entre as peças, nas duas direções.</summary>
		public float Gap { get; }

		/// <summary>Uma peça saiu da grade (rolou para longe): o índice dela e o controle, antes de ser solto.</summary>
		public event Action<int, Control>? Released;

		/// <summary>
		/// Passa a grade para o modo virtual: <paramref name="count"/> itens, todos do tamanho
		/// <paramref name="cell"/>, e <paramref name="create"/> monta o do índice quando ele chega à tela. A
		/// grade tem de estar dentro de um <see cref="ScrollContainer"/> (direto ou não).
		/// </summary>
		public void Virtualize(int count, Vector2 cell, Func<int, Control> create)
		{
			Reset();
			_count = count;
			_cell = cell;
			_create = create;
			UpdateMinimumSize();
			Realize();
		}

		/// <summary>
		/// Solta as peças do modo virtual e volta ao modo comum (os filhos que a tela puser dali em diante ficam
		/// em grade como sempre). É o que se chama antes de refazer a lista.
		/// </summary>
		public void Reset()
		{
			foreach (var (index, tile) in _live.ToList())
			{
				Released?.Invoke(index, tile);
				tile.QueueFree();
			}

			_live.Clear();
			_create = null;
			_count = 0;
		}

		/// <summary>As peças vivas, por índice (só no modo virtual).</summary>
		public IReadOnlyDictionary<int, Control> Live => _live;

		public override void _EnterTree()
		{
			for (Node? node = GetParent(); node != null && _scroll == null; node = node.GetParent())
				_scroll = node as ScrollContainer;
			if (_scroll == null)
				return;

			_scroll.GetVScrollBar().ValueChanged += OnScrolled;
			_scroll.Resized += Realize;
		}

		public override void _ExitTree()
		{
			if (_scroll != null && IsInstanceValid(_scroll))
			{
				_scroll.GetVScrollBar().ValueChanged -= OnScrolled;
				_scroll.Resized -= Realize;
			}

			_scroll = null;
		}

		private void OnScrolled(double value) => Realize();

		/// <summary>Cria as peças das fileiras à vista (e da folga) e solta as que saíram.</summary>
		private void Realize()
		{
			// Fora da árvore (ainda sem a rolagem que diz o que aparece), não monta nada.
			if (_create == null || _scroll == null || !IsInstanceValid(_scroll))
				return;

			var (cell, count) = Measure();
			if (count == 0)
				return;

			var columns = Columns(cell);
			var rowHeight = cell.Y + Gap;
			var rows = (count + columns - 1) / columns;
			var top = _scroll.ScrollVertical - _offset;
			var first = Math.Max(0, (int)Math.Floor(top / rowHeight) - Buffer);
			var last = Math.Min(rows - 1, (int)Math.Floor((top + _scroll.Size.Y) / rowHeight) + Buffer);

			var from = first * columns;
			var to = Math.Min(count - 1, (last + 1) * columns - 1);
			foreach (var index in _live.Keys.Where(i => i < from || i > to).ToList())
			{
				var tile = _live[index];
				_live.Remove(index);
				Released?.Invoke(index, tile);
				tile.QueueFree();
			}

			for (var index = from; index <= to; index++)
			{
				if (_live.ContainsKey(index))
					continue;

				var tile = _create(index);
				_live[index] = tile;
				AddChild(tile);
				Place(index, tile, cell, columns);
			}
		}

		private void Place(int index, Control tile, Vector2 cell, int columns)
		{
			var step = columns > 1 ? (Size.X - columns * cell.X) / (columns - 1) : 0;
			FitChildInRect(tile, new Rect2(index % columns * (cell.X + step), index / columns * (cell.Y + Gap), cell));
		}

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
			{
				// Onde a grade está dentro do conteúdo da rolagem, medido agora que as posições estão em dia.
				if (_scroll != null && IsInstanceValid(_scroll))
					_offset = GlobalPosition.Y - _scroll.GlobalPosition.Y + _scroll.ScrollVertical;
				Realize();
				Arrange();
			}
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
			if (_create != null)
			{
				foreach (var (at, tile) in _live)
					Place(at, tile, cell, columns);
				return;
			}

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
			if (_create != null)
				return (_cell, _count);

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
