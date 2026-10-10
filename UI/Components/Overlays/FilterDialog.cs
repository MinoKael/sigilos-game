using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A janela dos filtros (Runas, Monstros): um campo por linha, cada um abre a lista das opções, e a
	/// primeira opção é "Todos". Muda na hora: cada escolha avisa quem abriu e refaz os campos. Embaixo,
	/// Limpar filtros e Pronto.
	/// </summary>
	public sealed class FilterDialog
	{
		/// <summary>O valor da opção "Todos".</summary>
		public const int All = -1;

		private readonly Dialog _dialog;
		private readonly Action<FilterDialog> _build;
		private readonly Action _changed;
		private VBoxContainer _fields = null!;

		private FilterDialog(Control from, string title, Action<FilterDialog> build, Action changed)
		{
			_dialog = Dialog.Open(from, title, 560, null, "FilterDialog");
			_build = build;
			_changed = changed;
		}

		/// <param name="build">Põe os campos (<see cref="Field"/>) com os valores de agora.</param>
		/// <param name="changed">Um filtro mudou: quem abriu se atualiza.</param>
		/// <param name="clear">Volta os filtros ao começo; depois dele vem o <paramref name="changed"/>.</param>
		public static void Open(Control from, string title, Action<FilterDialog> build, Action changed, Action clear)
		{
			var filters = new FilterDialog(from, title, build, changed);
			filters.Build();
			filters._dialog.AddAction(T("filter.clear"), () =>
			{
				clear();
				changed();
			}, ButtonKind.Secondary, true, "cancel").Named("Clear");
			filters._dialog.AddAction(T("filter.done"), null, ButtonKind.Primary).Named("Done");
		}

		/// <summary>Um campo: o botão com o valor de agora (<see cref="All"/> é "Todos").</summary>
		public void Field(string name, string title, IEnumerable<(Choice Choice, int Value)> options, int current, Action<int> chosen)
		{
			var list = new List<(Choice Choice, int Value)> { (new Choice(T("filter.all")), All) };
			list.AddRange(options);
			var field = new ChoiceButton(title, list, current, 52) { Name = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			field.Changed += value =>
			{
				chosen(value);
				_changed();
				Callable.From(Build).CallDeferred();
			};
			_fields.AddChild(field);
		}

		private void Build()
		{
			if (!GodotObject.IsInstanceValid(_dialog))
				return;

			Layout.Clear(_dialog.Body);
			_fields = new VBoxContainer { Name = "Fields" };
			_fields.AddThemeConstantOverride("separation", Space.Regular);
			_build(this);
			_dialog.Body.AddChild(_fields);
		}
	}
}
