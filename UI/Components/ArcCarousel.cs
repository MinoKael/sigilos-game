using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>Uma opção do carrossel: símbolo (ou letras, quando não há símbolo), dica e cor.</summary>
	public sealed record ArcItem(Texture2D? Icon, string Tooltip, Color Ink, string Letters = "", Color? Accent = null, Core.Content.Glyph? Rune = null);

	/// <summary>
	/// O seletor do jogo, no lugar da lista suspensa: as opções são sigilos num arco entalhado, a
	/// escolhida no alto, maior; as vizinhas descem pelo arco, menores e apagadas. A roda do mouse, as
	/// setas das pontas ou um clique giram o arco.
	///
	/// Com <see cref="ChooseOnClick"/> o clique já escolhe (no <see cref="ArcPicker"/>); sem, o clique
	/// só traz a opção para o alto e avisa <see cref="Selected"/>.
	/// </summary>
	public partial class ArcCarousel : Control
	{
		private readonly IReadOnlyList<ArcItem> _items;
		private readonly List<Control> _holders = new();
		private readonly float _itemSize;
		private readonly float _radius;
		private readonly float _step;
		private readonly int _visible;
		private float _offset;

		public ArcCarousel(IReadOnlyList<ArcItem> items, int selected, float itemSize = 64, float radius = 300, int visible = 7)
		{
			_items = items;
			_itemSize = itemSize;
			_radius = radius;
			_visible = Math.Max(3, visible | 1);
			// O passo em ângulo que deixa um espaço de 1/4 do sigilo entre vizinhos no arco.
			_step = (itemSize * 1.2f) / radius;
			SelectedIndex = Math.Clamp(selected, 0, Math.Max(0, items.Count - 1));
			_offset = SelectedIndex;
			MouseFilter = MouseFilterEnum.Stop;

			// Largura e altura que cabem os sigilos visíveis e as setas das pontas, mais baixas no arco.
			var spread = _visible / 2 * _step;
			var edge = EdgeAngle;
			var arrow = itemSize * 0.55f;
			CustomMinimumSize = new Vector2(
				Mathf.Max(2 * _radius * Mathf.Sin(spread) + itemSize, 2 * _radius * Mathf.Sin(edge) + arrow) + 16,
				itemSize / 2 + 4 + _radius * (1 - Mathf.Cos(edge)) + arrow / 2 + 8);

			for (var i = 0; i < items.Count; i++)
			{
				var index = i;
				var item = items[i];
				var holder = new Control { Name = $"Item{i}", Size = new Vector2(itemSize, itemSize), PivotOffset = new Vector2(itemSize, itemSize) / 2, MouseFilter = MouseFilterEnum.Ignore };
				var button = new SigilButton(item.Icon, item.Tooltip, itemSize) { Name = "Sigil", Ink = item.Ink, Accent = item.Accent };
				if (item.Rune is { } glyph)
					button.Rune = glyph;
				else if (item.Letters.Length > 0)
					button.Letters = item.Letters;
				button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				button.Pressed += () => Click(index);
				holder.AddChild(button);
				AddChild(holder);
				_holders.Add(holder);
			}

			var previous = new SigilButton(null, "", itemSize * 0.55f) { Name = "Previous", Letters = "‹" };
			previous.Pressed += () => Select(SelectedIndex - 1);
			var next = new SigilButton(null, "", itemSize * 0.55f) { Name = "Next", Letters = "›" };
			next.Pressed += () => Select(SelectedIndex + 1);
			Previous = previous;
			Next = next;
			AddChild(previous);
			AddChild(next);
			Resized += Arrange;
		}

		/// <summary>A opção no alto do arco mudou (sem <see cref="ChooseOnClick"/>).</summary>
		public event Action<int>? Selected;

		/// <summary>O jogador escolheu esta opção (com <see cref="ChooseOnClick"/>).</summary>
		public event Action<int>? Chosen;

		public bool ChooseOnClick { get; init; }

		public int SelectedIndex { get; private set; }

		private SigilButton Previous { get; }
		private SigilButton Next { get; }

		/// <summary>O ângulo das setas: logo depois do último sigilo visível.</summary>
		private float EdgeAngle => (_visible / 2 + 0.9f) * _step;

		public void Select(int index)
		{
			if (_items.Count == 0)
				return;
			index = Math.Clamp(index, 0, _items.Count - 1);
			if (index == SelectedIndex)
				return;
			SelectedIndex = index;
			SetProcess(true);
			Selected?.Invoke(index);
		}

		public override void _Ready() => Arrange();

		public override void _Process(double delta)
		{
			_offset = Mathf.Lerp(_offset, SelectedIndex, 1 - Mathf.Exp(-14 * (float)delta));
			if (Mathf.Abs(_offset - SelectedIndex) < 0.001f)
			{
				_offset = SelectedIndex;
				SetProcess(false);
			}

			Arrange();
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: true } wheel)
			{
				if (wheel.ButtonIndex == MouseButton.WheelUp)
					Select(SelectedIndex - 1);
				else if (wheel.ButtonIndex == MouseButton.WheelDown)
					Select(SelectedIndex + 1);
				else
					return;
				AcceptEvent();
			}
		}

		public override void _Draw()
		{
			// O sulco do arco, por onde os sigilos correm.
			var center = ArcCenter();
			var spread = (_visible / 2 + 0.5f) * _step;
			DrawArc(center, _radius, -Mathf.Pi / 2 - spread, -Mathf.Pi / 2 + spread, 48, new Color(0, 0, 0, 0.5f), 6, true);
			DrawArc(center, _radius, -Mathf.Pi / 2 - spread, -Mathf.Pi / 2 + spread, 48, new Color(Palette.GoldDark, 0.8f), 1.5f, true);
		}

		/// <summary>O centro do círculo do arco: abaixo do controle, para o arco subir no meio.</summary>
		private Vector2 ArcCenter() => new(Size.X / 2, _itemSize / 2 + 4 + _radius);

		private void Arrange()
		{
			var center = ArcCenter();
			var half = _visible / 2 + 0.5f;
			for (var i = 0; i < _holders.Count; i++)
			{
				var holder = _holders[i];
				var distance = i - _offset;
				var visible = Mathf.Abs(distance) <= half;
				holder.Visible = visible;
				if (!visible)
					continue;

				var angle = distance * _step;
				var point = center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * _radius;
				holder.Position = point - holder.Size / 2;
				var depth = Mathf.Abs(distance);
				holder.Scale = Vector2.One * Mathf.Max(0.55f, 1 - 0.11f * depth);
				holder.Modulate = new Color(1, 1, 1, Mathf.Clamp(1.1f - 0.2f * depth, 0.25f, 1));
				holder.ZIndex = 10 - (int)depth;
			}

			// As setas nas pontas do arco.
			var edge = EdgeAngle;
			var arrow = Previous.CustomMinimumSize;
			Previous.Size = Next.Size = arrow;
			Previous.Position = center + new Vector2(-Mathf.Sin(edge), -Mathf.Cos(edge)) * _radius - arrow / 2;
			Next.Position = center + new Vector2(Mathf.Sin(edge), -Mathf.Cos(edge)) * _radius - arrow / 2;
			Previous.Disabled = SelectedIndex <= 0;
			Next.Disabled = SelectedIndex >= _items.Count - 1;
			QueueRedraw();
		}

		private void Click(int index)
		{
			if (ChooseOnClick)
			{
				Chosen?.Invoke(index);
				return;
			}

			Select(index);
		}
	}

	/// <summary>
	/// O carrossel por cima da tela, para escolher uma opção e voltar: a tela escurece, o arco aparece
	/// perto de quem abriu e o clique numa opção escolhe e fecha. Clicar fora ou Esc fecha sem mudar.
	/// </summary>
	public partial class ArcPicker : ColorRect
	{
		private ArcPicker(IReadOnlyList<ArcItem> items, int selected, Vector2 near, Action<int> chosen)
		{
			Name = nameof(ArcPicker);
			Color = new Color(0, 0, 0, 0.55f);
			MouseFilter = MouseFilterEnum.Stop;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			var carousel = new ArcCarousel(items, selected, 60, 280) { Name = "Carousel", ChooseOnClick = true };
			carousel.Chosen += index =>
			{
				QueueFree();
				chosen(index);
			};
			AddChild(carousel);
			Near = near;
			Carousel = carousel;
		}

		private Vector2 Near { get; }
		private ArcCarousel Carousel { get; }

		/// <summary>Abre por cima da tela de <paramref name="from"/>, com o arco perto de <paramref name="near"/> (posição global).</summary>
		public static void Open(Control from, IReadOnlyList<ArcItem> items, int selected, Vector2 near, Action<int> chosen) =>
			Layout.Host(from).AddChild(new ArcPicker(items, selected, near, chosen));

		public override void _Ready()
		{
			var size = Carousel.CustomMinimumSize;
			Carousel.Size = size;
			var screen = GetViewportRect().Size;
			// O arco abre acima de quem chamou; sem espaço em cima, abre embaixo.
			var local = Near - GetGlobalRect().Position;
			var above = local.Y - size.Y - 36;
			var position = new Vector2(local.X - size.X / 2, above >= 8 ? above : local.Y + 48);
			Carousel.Position = new Vector2(Mathf.Clamp(position.X, 8, screen.X - size.X - 8), Mathf.Clamp(position.Y, 8, screen.Y - size.Y - 8));
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				QueueFree();
		}

		public override void _UnhandledKeyInput(InputEvent @event)
		{
			if (@event.IsActionPressed("ui_cancel"))
				QueueFree();
		}
	}
}
