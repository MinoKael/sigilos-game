using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>Uma opção de escolha: o texto, e um símbolo ou um Glifo na frente, na cor dada.</summary>
	public sealed record Choice(string Text, Texture2D? Icon = null, Color? Ink = null, Glyph? Rune = null);

	/// <summary>
	/// O campo de escolha, no lugar da lista suspensa: um botão que diz o campo e o valor atual
	/// ("Conjunto: Violência") e, tocado, abre a <see cref="Choices"/> colada nele.
	/// </summary>
	public partial class ChoiceButton : GameButton
	{
		private readonly string _field;
		private readonly IReadOnlyList<(Choice Choice, int Value)> _options;
		private int _index;

		/// <param name="field">O nome do campo ("Conjunto"); o botão mostra "Conjunto: valor".</param>
		public ChoiceButton(string field, IReadOnlyList<(Choice Choice, int Value)> options, int current, float height = 48)
			: base("", ButtonKind.Secondary, null, height)
		{
			_field = field;
			_options = options;
			_index = Math.Max(0, options.ToList().FindIndex(o => o.Value == current));
			Refresh();
			Pressed += () => Choices.Open(this, field, _options.Select(o => o.Choice).ToList(), _index, index =>
			{
				_index = index;
				Refresh();
				Changed?.Invoke(_options[index].Value);
			});
		}

		/// <summary>O valor da opção escolhida.</summary>
		public event Action<int>? Changed;

		public int Value => _options[_index].Value;

		private void Refresh() => Text = $"{_field}: {_options[_index].Choice.Text}";
	}

	/// <summary>
	/// A lista de opções numa janela contextual colada em quem abriu: um botão por opção (a atual acesa),
	/// em duas colunas quando são muitas. Tocar escolhe e fecha.
	/// </summary>
	public static class Choices
	{
		/// <param name="anchored">Colada em <paramref name="from"/> (o campo tocado); falso abre no centro.</param>
		public static Dialog Open(Control from, string title, IReadOnlyList<Choice> options, int selected, Action<int> chosen, bool anchored = true)
		{
			var columns = options.Count > 8 ? 2 : 1;
			var dialog = Dialog.Open(from, title, columns == 1 ? 420 : 640, anchored ? from : null, "ChoiceDialog");
			var grid = new GridContainer { Name = "Options", Columns = columns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			grid.AddThemeConstantOverride("h_separation", Space.Medium);
			grid.AddThemeConstantOverride("v_separation", Space.Medium);
			for (var i = 0; i < options.Count; i++)
			{
				var index = i;
				grid.AddChild(Row(options[i], i == selected, () =>
				{
					dialog.Close();
					chosen(index);
				}).Named($"Option{i + 1}"));
			}

			dialog.Body.AddChild(grid);
			return dialog;
		}

		/// <summary>Uma opção: o símbolo e o texto num botão de lista, aceso em azul se é a atual.</summary>
		public static SurfaceButton Row(Choice choice, bool current, Action pressed)
		{
			var border = current ? States.Selected : Palette.GoldDark;
			var held = GameTheme.Box(Palette.Inset, Palette.Arcane, 2, Radius.Medium, 6);
			var button = new SurfaceButton
			{
				CustomMinimumSize = new Vector2(0, 52),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			}
				.Boxes(GameTheme.Box(current ? States.LitFill : Palette.Inset, border, current ? 2 : 1, Radius.Medium, 6),
					GameTheme.Box(Palette.PanelLight, Palette.Gold, 1, Radius.Medium, 6), held)
				.Padded(12, 12);

			var row = new HBoxContainer { Name = "Row", MouseFilter = Control.MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", Space.Regular);
			var ink = choice.Ink ?? Palette.Gold;
			if (choice.Rune is { } glyph)
				row.AddChild(new RuneGlyph(glyph, 26, ink) { Name = "Glyph", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
			else if (choice.Icon != null)
				row.AddChild(Doodle.Icon(choice.Icon, 28, ink).Named("Icon"));
			var label = new Label { Name = "Text", Text = choice.Text, VerticalAlignment = VerticalAlignment.Center, SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
			label.AddThemeColorOverride("font_color", current ? Palette.Arcane.Lerp(Colors.White, 0.3f) : Palette.Text);
			row.AddChild(label);
			button.Body.AddChild(row);
			button.Pressed += pressed;
			return button;
		}
	}
}
