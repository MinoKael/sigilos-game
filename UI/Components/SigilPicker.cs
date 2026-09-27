using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Um campo de escolha que é só um sigilo: mostra a opção atual (símbolo ou letras) e, tocado, abre o
	/// <see cref="ArcPicker"/> com todas. É o substituto da lista suspensa nos filtros.
	/// </summary>
	public partial class SigilPicker : SigilButton
	{
		private readonly IReadOnlyList<(ArcItem Item, int Value)> _options;
		private int _index;

		/// <param name="title">O nome do campo, na frente da opção na dica ("Conjunto: Violento").</param>
		public SigilPicker(string title, IReadOnlyList<(ArcItem Item, int Value)> options, int current, float size = 50)
			: base(null, "", size, SigilShape.Square)
		{
			Title = title;
			_options = options;
			_index = Math.Max(0, options.ToList().FindIndex(o => o.Value == current));
			Show();
			Pressed += () => ArcPicker.Open(this, _options.Select(o => o.Item).ToList(), _index, GetGlobalRect().GetCenter(), index =>
			{
				_index = index;
				Show();
				Changed?.Invoke(_options[index].Value);
			});
		}

		/// <summary>O valor da opção escolhida.</summary>
		public event Action<int>? Changed;

		private string Title { get; }

		private new void Show()
		{
			var item = _options[_index].Item;
			if (item.Rune is { } glyph)
			{
				Rune = glyph;
			}
			else
			{
				Rune = null;
				SetIcon(item.Icon);
				Letters = item.Icon == null ? item.Letters : "";
			}

			Ink = item.Ink;
			Accent = item.Accent;
			TooltipText = $"{Title}: {item.Tooltip}";
		}
	}
}
