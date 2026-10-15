using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>Uma opção de escolha: o texto, e um símbolo ou um planeta na frente, na cor dada.</summary>
	public sealed record Choice(string Text, Texture2D? Icon = null, Color? Ink = null, Planet? Symbol = null);

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
	/// O campo de vários valores (os filtros: fogo e luz): como o <see cref="ChoiceButton"/>, mas a lista
	/// (<see cref="Choices.OpenMany"/>) deixa ligar várias opções e só avisa quando fecha. Nenhuma ligada é "Todos".
	/// </summary>
	public partial class MultiChoiceButton : GameButton
	{
		/// <summary>Até quantos nomes o botão escreve; com mais, só quantos são.</summary>
		private const int NamesShown = 2;

		private readonly string _field;
		private readonly IReadOnlyList<(Choice Choice, int Value)> _options;
		private List<int> _selected;

		/// <param name="field">O nome do campo; o botão mostra "Campo: A, B".</param>
		/// <param name="current">Os valores ligados agora; os que não estão nas opções ficam de fora.</param>
		public MultiChoiceButton(string field, IReadOnlyList<(Choice Choice, int Value)> options, IEnumerable<int> current, float height = 48)
			: base("", ButtonKind.Secondary, null, height)
		{
			_field = field;
			_options = options;
			_selected = options.Select(o => o.Value).Where(current.ToList().Contains).ToList();
			Refresh();
			Pressed += () => Choices.OpenMany(this, field, _options.Select(o => o.Choice).ToList(),
				_selected.Select(v => _options.ToList().FindIndex(o => o.Value == v)).ToList(), indexes =>
				{
					_selected = indexes.Select(i => _options[i].Value).ToList();
					Refresh();
					Changed?.Invoke(_selected);
				});
		}

		/// <summary>A lista fechou com outra escolha: recebe os valores ligados (vazia é "Todos").</summary>
		public event Action<IReadOnlyList<int>>? Changed;

		public IReadOnlyList<int> Values => _selected;

		private void Refresh()
		{
			var names = _selected.Select(v => _options.First(o => o.Value == v).Choice.Text).ToList();
			var value = names.Count == 0 ? Locale.T("filter.all")
				: names.Count <= NamesShown ? string.Join(", ", names)
				: Locale.T("filter.selected_n", names.Count);
			Text = $"{_field}: {value}";
		}
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

		/// <summary>
		/// A lista de várias escolhas: tocar liga ou desliga a opção (acesa em azul) e a janela continua aberta;
		/// "Todos" desliga tudo. <paramref name="changed"/> recebe os índices ligados uma vez, quando a janela
		/// fecha (Pronto ou um toque fora) e a escolha não é a do começo: quem abriu não se refaz a cada toque.
		/// </summary>
		public static Dialog OpenMany(Control from, string title, IReadOnlyList<Choice> options, IReadOnlyCollection<int> selected, Action<IReadOnlyList<int>> changed, bool anchored = true)
		{
			var columns = options.Count > 8 ? 2 : 1;
			var dialog = Dialog.Open(from, title, columns == 1 ? 420 : 640, anchored ? from : null, "ChoiceDialog");
			var on = new SortedSet<int>(selected);
			var start = on.ToList();
			var grid = new GridContainer { Name = "Options", Columns = columns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			grid.AddThemeConstantOverride("h_separation", Space.Medium);
			grid.AddThemeConstantOverride("v_separation", Space.Medium);

			void Fill()
			{
				Layout.Clear(grid);
				grid.AddChild(Row(new Choice(Locale.T("filter.all")), on.Count == 0, () =>
				{
					on.Clear();
					Fill();
				}).Named("All"));
				for (var i = 0; i < options.Count; i++)
				{
					var index = i;
					grid.AddChild(Row(options[i], on.Contains(i), () =>
					{
						if (!on.Remove(index))
							on.Add(index);
						Fill();
					}).Named($"Option{i + 1}"));
				}
			}

			Fill();
			dialog.Body.AddChild(grid);
			dialog.AddAction(Locale.T("filter.done"), null, ButtonKind.Primary).Named("Done");
			dialog.Closed += () =>
			{
				if (!on.SequenceEqual(start))
					changed(on.ToList());
			};
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
			if (choice.Symbol is { } planet)
				row.AddChild(new PlanetSymbol(planet, 26, ink) { Name = "Planet", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
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
