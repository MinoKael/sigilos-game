using System;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A ficha de uma runa (<see cref="RuneCard"/>) numa janela: colada na runa que o jogador segurou,
	/// ou no centro quando quem abre é o jogo (a runa que caiu na vitória). O cabeçalho tem numa linha só
	/// "Runa", o conjunto e o espaço dela no meio (na cor da raridade) e o ✕. Quem abre põe os botões de
	/// ação que fizerem sentido ali (Vender, Melhorar, Guardar) com <see cref="Dialog.AddAction"/>.
	/// </summary>
	public static class RuneDialog
	{
		/// <summary>O nível que a melhora direta alcança de uma vez, perguntando antes (o botão "Até +12").</summary>
		public const int DirectLevel = 12;

		/// <param name="opened">O nível de antes de uma melhora: o que a runa ganhou desde ele fica em verde.</param>
		/// <param name="animate">A melhora acabou de acontecer: a ficha a refaz na tela, de <paramref name="opened"/> até agora.</param>
		public static Dialog Show(Control from, Rune rune, bool anchored = true, string? note = null, int? opened = null, bool animate = false)
		{
			var dialog = Dialog.Open(from, T("runes.dialog_title"), 440, anchored ? from : null, "RuneDialog");
			dialog.SetCaption(Texts.Title(rune), Palette.Of(rune.Rarity));
			dialog.Body.AddChild(new RuneCard(rune, 400, opened, note: note, title: false, animateFrom: animate ? opened : null));
			return dialog;
		}

		/// <summary>
		/// A ficha de uma janela já aberta (<see cref="Show"/>) refeita no lugar, depois de uma melhora: a janela
		/// continua onde está e a runa nova refaz a melhora na tela, de <paramref name="from"/> até agora, com
		/// o verde do que subiu desde <paramref name="opened"/>.
		/// </summary>
		public static void Replay(Dialog dialog, Rune rune, int opened, int from)
		{
			Layout.Clear(dialog.Body);
			dialog.Body.AddChild(new RuneCard(rune, 400, opened, title: false, animateFrom: from));
		}

		/// <summary>
		/// A melhora até <see cref="DirectLevel"/> gasta muita Essência: pergunta antes, com o gasto e de onde a
		/// runa sai e aonde chega. Nada do que vem é mostrado aqui: o que ela ganhou só aparece depois, em
		/// <see cref="ShowUpgraded"/>. <paramref name="essence"/> é o que o jogador tem.
		/// </summary>
		public static Dialog ConfirmDirect(Control from, Rune rune, int essence, Action confirmed)
		{
			var cost = RuneRules.UpgradeCost(rune, DirectLevel);
			return Dialog.Confirm(from, T("runes.upgrade_confirm_title", DirectLevel),
				T("runes.upgrade_confirm", Texts.Number(cost), Texts.Title(rune), rune.Level, DirectLevel, Texts.Number(essence)),
				T("runes.upgrade_button", DirectLevel), confirmed);
		}

		/// <summary>
		/// A runa antes e depois da melhora, lado a lado, com o que cresceu em verde: aberta quando a melhora
		/// já aconteceu (<paramref name="before"/> é uma cópia tirada antes; <paramref name="after"/> é a runa de agora).
		/// </summary>
		public static Dialog ShowUpgraded(Control from, Rune before, Rune after)
		{
			var dialog = Dialog.Open(from, T("runes.upgraded_title", after.Level), 940, null, "UpgradedDialog");
			var row = Layout.Row(Space.Large).Named("Cards");
			row.Alignment = BoxContainer.AlignmentMode.Center;
			row.AddChild(Side("Before", T("runes.upgrade_before"), before, null));

			// O desenho aponta para baixo. Quem está num container tem a rotação zerada a cada arrumação, e o Doodle
			// refaz a própria (a ondulação): a rotação vai numa caixa solta, dentro de uma caixa que o container mede.
			var slot = new Control { Name = "ArrowSlot", CustomMinimumSize = new Vector2(36, 36), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
			var turn = new Control { Name = "ArrowTurn", Size = new Vector2(36, 36), PivotOffset = new Vector2(18, 18), Rotation = -Mathf.Pi / 2, MouseFilter = Control.MouseFilterEnum.Ignore };
			var arrow = Doodle.Icon(Art.Icon("arrow"), 36, Palette.Gold).Named("Arrow");
			arrow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			turn.AddChild(arrow);
			slot.AddChild(turn);
			row.AddChild(slot);

			row.AddChild(Side("After", T("runes.upgrade_after"), after, before.Level));
			dialog.Body.AddChild(row);
			dialog.AddAction(T("common.continue"), null, ButtonKind.Primary).Named("Continue");
			return dialog;
		}

		/// <summary>Uma das duas runas, com a legenda em cima; <paramref name="opened"/> põe em verde o que cresceu desde esse nível.</summary>
		private static Control Side(string name, string caption, Rune rune, int? opened)
		{
			var column = new VBoxContainer { Name = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			column.AddThemeConstantOverride("separation", Space.Medium);
			var label = new Label { Name = "Caption", Text = caption, ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center };
			column.AddChild(label);
			var panel = new PanelContainer { Name = "Panel", ThemeTypeVariation = GameTheme.InsetPanel };
			// A runa de depois refaz a melhora na tela, do nível de antes até o de agora.
			panel.AddChild(new RuneCard(rune, 360, opened, title: true, animateFrom: opened));
			column.AddChild(panel);
			return column;
		}
	}
}
