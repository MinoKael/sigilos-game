using System;
using Godot;
using Sigilos.UI.Screens;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A luta de agora da Batalha automática, para ver (<see cref="BattleScreen"/> assistindo a
	/// <see cref="AutoBattleRun.Fight"/>):
	///
	/// - pequena (<see cref="Small"/>), na janela da Batalha automática: só o campo e os monstros, a tela da
	///   luta inteira encolhida; tocar abre a tela cheia;
	/// - na tela cheia (<see cref="Open"/>), por cima de tudo, com o cabeçalho, a ordem de turno e a seta
	///   de voltar (também Esc e o Voltar do celular). Volta sozinha quando a Batalha automática para.
	///
	/// As duas seguem a corrida: luta nova, campo novo. Só olham: abrir, fechar ou trocar de vista não
	/// para nem recomeça a Batalha automática, e a luta continua de onde está (quem volta a olhar monta o
	/// campo pelo estado dela).
	/// </summary>
	public partial class AutoBattleWatch : Control
	{
		/// <summary>
		/// A moldura, o convite e o toque da vista pequena ficam por cima até de quem corre para golpear e do
		/// painel de Efeitos, mas abaixo da camada de uma janela: a pergunta aberta por cima os cobre.
		/// </summary>
		private const int OnTop = 30;

		private readonly AutoBattleRun _run;
		private readonly bool _full;
		private AutoBattleFight? _shown;
		private BattleScreen? _screen;
		private bool _closed;

		private AutoBattleWatch(AutoBattleRun run, bool full)
		{
			_run = run;
			_full = full;
		}

		/// <summary>A tela cheia fechou (pela seta, ou porque a Batalha automática parou).</summary>
		public event Action? Closed;

		/// <summary>A vista pequena, de <paramref name="width"/> de largura e na proporção da tela; tocar chama <paramref name="open"/>.</summary>
		public static AutoBattleWatch Small(AutoBattleRun run, float width, Action open)
		{
			var watch = new AutoBattleWatch(run, false) { Name = "Watch", ClipContents = true, MouseDefaultCursorShape = CursorShape.PointingHand };
			watch.CustomMinimumSize = new Vector2(width, width * 9 / 16);
			watch.Resized += watch.Fit;

			// Por cima da luta: a moldura e o convite para a tela cheia, e o toque (que deixa arrastar a janela).
			var frame = new Panel { Name = "Frame", MouseFilter = MouseFilterEnum.Ignore, ZIndex = OnTop };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Colors.Transparent, Palette.Arcane, 2, 10, 0));
			frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			watch.AddChild(frame);

			var hint = new Label { Name = "Hint", Text = T("auto.watch_full"), MouseFilter = MouseFilterEnum.Ignore, ZIndex = OnTop, HorizontalAlignment = HorizontalAlignment.Center };
			hint.AddThemeFontSizeOverride("font_size", 14);
			hint.AddThemeColorOverride("font_color", Palette.Text);
			hint.AddThemeColorOverride("font_outline_color", Palette.Background);
			hint.AddThemeConstantOverride("outline_size", 4);
			hint.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
			hint.GrowHorizontal = GrowDirection.Both;
			hint.GrowVertical = GrowDirection.Begin;
			hint.OffsetBottom = -6;
			watch.AddChild(hint);

			var touch = new Control { Name = "Touch", ZIndex = OnTop };
			touch.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			Press.On(touch, open, open);
			watch.AddChild(touch);
			return watch;
		}

		/// <summary>A tela cheia, por cima da tela de <paramref name="from"/> e das janelas abertas.</summary>
		public static AutoBattleWatch Open(Control from, AutoBattleRun run)
		{
			var watch = new AutoBattleWatch(run, true) { Name = "AutoBattleWatch", ZIndex = 80, MouseFilter = MouseFilterEnum.Stop };
			watch.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			Layout.Host(from).AddChild(watch);
			return watch;
		}

		public void Close()
		{
			if (_closed)
				return;
			_closed = true;
			QueueFree();
			Closed?.Invoke();
		}

		public override void _Process(double delta)
		{
			if (_closed)
				return;
			// Escondida (a tela cheia por cima), a vista pequena não anima nada; ao voltar, monta de novo.
			if (!IsVisibleInTree())
			{
				Drop();
				return;
			}

			if (_run.Fight == _shown)
				return;

			Drop();
			_shown = _run.Fight;
			if (_shown == null)
			{
				if (_full)
					Close();
				return;
			}

			_screen = new BattleScreen(_shown, _run.Title, _full ? Close : null) { Name = "Battle" };
			AddChild(_screen);
			MoveChild(_screen, 0);
			Fit();
		}

		/// <summary>Saindo da árvore (a janela se remonta), larga a vista; de volta, monta outra no ponto em que a luta está.</summary>
		public override void _ExitTree() => Drop();

		private void Drop()
		{
			_shown = null;
			if (_screen == null)
				return;
			Layout.Discard(_screen);
			_screen = null;
		}

		/// <summary>A vista pequena é a tela da luta inteira, encolhida e centrada.</summary>
		private void Fit()
		{
			if (_full || _screen == null || !IsInsideTree())
				return;
			var screen = GetViewportRect().Size;
			var scale = Mathf.Min(Size.X / screen.X, Size.Y / screen.Y);
			_screen.Size = screen;
			_screen.Scale = new Vector2(scale, scale);
			_screen.Position = (Size - screen * scale) / 2;
		}
	}
}
