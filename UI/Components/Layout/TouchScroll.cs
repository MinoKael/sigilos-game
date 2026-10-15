using System;
using System.Collections.Generic;
using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Rolagem por arrasto, com o dedo no celular e com o mouse no PC, para toda lista do jogo, de um lugar só.
	///
	/// O arrasto nativo do <see cref="ScrollContainer"/> só funciona se o toque começa fora de um botão: o
	/// botão (<c>MouseFilter.Stop</c>) fica com o toque e a lista não rola — na tela cheia de botões (Fases,
	/// Masmorras, Loja) o jogador tinha que achar o vão entre eles. Aqui o toque é visto antes da interface:
	/// acha o controle mais alto sob o dedo (<see cref="TopControlAt"/>), junta as listas que o contêm (da mais
	/// de dentro para a de fora) e, passando de <see cref="Slop"/>, rola a primeira que anda no sentido do
	/// arrasto (uma fileira que rola de lado e a página que rola para baixo dividem o mesmo dedo). O botão sob o
	/// dedo perde o clique (soltar depois de arrastar
	/// não compra nem abre nada); os cartões e as runas (<see cref="Press"/>) já se cancelam sozinhos quando o
	/// dedo anda. Ao soltar em movimento a lista segue com inércia, que perde força até parar.
	///
	/// O arrasto do Godot é desligado nas listas que passam por aqui (zona morta enorme): senão rolariam
	/// duas vezes. Controle deslizante, barra de rolagem e campo de texto sob o dedo ficam com o gesto deles.
	/// </summary>
	public partial class TouchScroll : Node
	{
		/// <summary>Quanto o dedo anda, em px, antes de virar arrasto (e de o botão perder o clique).</summary>
		private const float Slop = 12;

		/// <summary>Quanto da velocidade sobra depois de um segundo de inércia: perto de nada.</summary>
		private const float Friction = 0.02f;

		/// <summary>Abaixo disso, em px por segundo, a inércia acabou.</summary>
		private const float StopSpeed = 30;

		/// <summary>Soltar com o dedo parado por mais que isso, em ms, não leva inércia.</summary>
		private const ulong RestMs = 90;

		private readonly List<ScrollContainer> _candidates = new();
		private ScrollContainer? _scroll;
		private bool _armed;
		private BaseButton? _button;
		private bool _touching;
		private bool _dragging;
		private Vector2 _start;
		private Vector2 _origin;
		private Vector2 _last;
		private ulong _lastTime;
		private Vector2 _velocity;
		private Vector2 _position;
		private ScrollContainer? _coasting;

		public TouchScroll()
		{
			Name = "TouchScroll";
			SetProcess(false);
		}

		public override void _Input(InputEvent @event)
		{
			switch (@event)
			{
				// Do toque (celular) vem o gesto; o mouse emulado a partir dele é o mesmo gesto, e não conta de novo.
				case InputEventScreenTouch { Index: 0 } touch:
					if (touch.Pressed)
						Begin(touch.Position, fromTouch: true);
					else
						End();
					return;

				case InputEventScreenDrag { Index: 0 } drag:
					Move(drag.Position);
					return;

				case InputEventMouseButton { ButtonIndex: MouseButton.Left } click when click.Device != InputEvent.DeviceIdEmulation && !_touching:
					if (click.Pressed)
						Begin(click.Position, fromTouch: false);
					else
						End();
					return;

				case InputEventMouseMotion motion when motion.Device != InputEvent.DeviceIdEmulation && _armed && !_touching:
					// Soltou fora da janela: o "soltou" não chega, e a lista não pode seguir o mouse sem botão.
					if (!motion.ButtonMask.HasFlag(MouseButtonMask.Left))
						End();
					else
						Move(motion.Position);
					return;
			}
		}

		public override void _Process(double delta)
		{
			var scroll = _coasting;
			if (scroll == null || !IsInstanceValid(scroll) || !scroll.IsVisibleInTree())
			{
				StopCoasting();
				return;
			}

			_position += _velocity * (float)delta;
			_velocity *= Mathf.Pow(Friction, (float)delta);
			var before = new Vector2(scroll.ScrollHorizontal, scroll.ScrollVertical);
			Apply(scroll, _position);
			var after = new Vector2(scroll.ScrollHorizontal, scroll.ScrollVertical);

			// Chegou na ponta, ou ficou lenta demais: acabou.
			if (_velocity.Length() < StopSpeed || after == before)
				StopCoasting();
		}

		private void Begin(Vector2 point, bool fromTouch)
		{
			StopCoasting();
			_touching = fromTouch;
			_scroll = null;
			_armed = false;
			_candidates.Clear();
			_button = null;
			_dragging = false;
			_velocity = Vector2.Zero;

			var hit = TopControlAt(GetTree().Root, point);
			if (hit == null)
				return;

			BaseButton? button = null;
			for (Node? node = hit; node != null; node = node.GetParent())
			{
				// Quem tem gesto próprio fica com ele.
				if (node is ScrollBar or Slider or LineEdit or TextEdit)
				{
					_candidates.Clear();
					return;
				}

				if (node is BaseButton found && button == null)
					button = found;
				if (node is ScrollContainer candidate && Scrolls(candidate))
				{
					_candidates.Add(candidate);
					// O arrasto de dentro do Godot rolaria junto: fica desligado (zona morta que o dedo nunca alcança).
					candidate.ScrollDeadzone = int.MaxValue / 2;
				}
			}

			if (_candidates.Count == 0)
				return;

			_armed = true;
			_button = button;
			_start = _last = point;
			_lastTime = Time.GetTicksMsec();
		}

		private void Move(Vector2 point)
		{
			if (!_armed)
				return;

			var moved = point - _start;
			if (!_dragging)
			{
				if (moved.Length() < Slop)
					return;

				// A lista que anda no sentido do arrasto; se nenhuma anda, o gesto não é de rolagem.
				var horizontal = Math.Abs(moved.X) > Math.Abs(moved.Y);
				_scroll = _candidates.Find(c => IsInstanceValid(c) && Moves(c, horizontal));
				if (_scroll == null)
				{
					_armed = false;
					return;
				}

				_origin = new Vector2(_scroll.ScrollHorizontal, _scroll.ScrollVertical);
				_dragging = true;
				CancelPress();
			}

			if (_scroll == null || !IsInstanceValid(_scroll))
				return;

			var now = Time.GetTicksMsec();
			var elapsed = Math.Max(1, now - _lastTime) / 1000f;
			var instant = (_last - point) / elapsed;
			_velocity = _velocity.Lerp(instant, 0.4f);
			_last = point;
			_lastTime = now;
			_position = _origin - moved;
			Apply(_scroll, _position);
		}

		private void End()
		{
			var scroll = _scroll;
			var dragged = _dragging;
			_scroll = null;
			_armed = false;
			_candidates.Clear();
			_button = null;
			_dragging = false;
			_touching = false;
			if (scroll == null || !dragged || !IsInstanceValid(scroll))
				return;

			if (Time.GetTicksMsec() - _lastTime > RestMs || _velocity.Length() < StopSpeed)
				return;

			_coasting = scroll;
			_position = new Vector2(scroll.ScrollHorizontal, scroll.ScrollVertical);
			SetProcess(true);
		}

		private void StopCoasting()
		{
			_coasting = null;
			_velocity = Vector2.Zero;
			SetProcess(false);
		}

		/// <summary>A lista rola para algum lado e tem para onde ir.</summary>
		private static bool Scrolls(ScrollContainer scroll) => Moves(scroll, horizontal: false) || Moves(scroll, horizontal: true);

		/// <summary>A lista anda no sentido pedido: o modo deixa e o conteúdo é maior que o que ela mostra.</summary>
		private static bool Moves(ScrollContainer scroll, bool horizontal)
		{
			if (horizontal)
				return scroll.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled && scroll.GetHScrollBar().MaxValue > scroll.GetHScrollBar().Page + 1;
			return scroll.VerticalScrollMode != ScrollContainer.ScrollMode.Disabled && scroll.GetVScrollBar().MaxValue > scroll.GetVScrollBar().Page + 1;
		}

		private static void Apply(ScrollContainer scroll, Vector2 position)
		{
			if (scroll.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled)
				scroll.ScrollHorizontal = Mathf.RoundToInt(position.X);
			if (scroll.VerticalScrollMode != ScrollContainer.ScrollMode.Disabled)
				scroll.ScrollVertical = Mathf.RoundToInt(position.Y);
		}

		/// <summary>
		/// Desligar e religar o botão desfaz o aperto (soltar não vira clique); o "soltou" avisado à mão desfaz o
		/// que começou no aperto (o salto do <see cref="Animations.Juice"/>, o toque longo do <see cref="Press"/>).
		/// </summary>
		private void CancelPress()
		{
			if (_button == null || !IsInstanceValid(_button) || _button.Disabled)
				return;

			_button.Disabled = true;
			_button.Disabled = false;
			_button.EmitSignal(BaseButton.SignalName.ButtonUp);
		}

		/// <summary>
		/// O controle que recebe o toque em <paramref name="point"/>: o de cima, pela ordem de desenho (o índice Z
		/// que soma pelos pais e, empatado, o que vem depois na árvore). Controle que ignora o mouse não conta,
		/// nem o que está fora do recorte de quem o contém.
		/// </summary>
		public static Control? TopControlAt(Node root, Vector2 point)
		{
			Control? best = null;
			var bestZ = int.MinValue;
			Visit(root, 0, point, ref best, ref bestZ);
			return best;
		}

		private static void Visit(Node node, int parentZ, Vector2 point, ref Control? best, ref int bestZ)
		{
			var z = parentZ;
			if (node is CanvasItem item)
			{
				if (!item.Visible)
					return;
				z = item.ZAsRelative ? parentZ + item.ZIndex : item.ZIndex;
				if (item is Control control)
				{
					var rect = control.GetGlobalRect();
					var inside = rect.HasPoint(point);
					if (inside && control.MouseFilter != Control.MouseFilterEnum.Ignore && z >= bestZ)
					{
						best = control;
						bestZ = z;
					}

					// O que está fora do recorte não recebe toque, nem os filhos.
					if (!inside && control.ClipContents)
						return;
				}
			}

			foreach (var child in node.GetChildren())
				Visit(child, z, point, ref best, ref bestZ);
		}
	}
}
