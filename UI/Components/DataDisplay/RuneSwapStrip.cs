using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Os 6 espaços de um homúnculo em duas colunas, "Antes" (como ele está) e "Depois" (com a troca), cada uma no círculo
	/// de sigilos da ficha do homúnculo (<see cref="SigilRing"/>: 6 1 / 5 2 / 4 3). Na do Depois, o espaço que não muda fica
	/// apagado, para a troca saltar aos olhos. Serve à janela do Auto-equipar e à coluna da esquerda da tela de Sigilos.
	/// O toque num espaço, de qualquer coluna, é <see cref="SlotPressed"/>; o toque longo abre a ficha do sigilo, como em toda pedra.
	/// </summary>
	public partial class RuneSwapStrip : HBoxContainer
	{
		/// <summary>O afastamento das pedras ao centro do círculo (o da ficha do homúnculo).</summary>
		private const float Spread = 0.72f;

		/// <summary>Quanto o círculo é maior que a pedra: dá a ele o tamanho em que duas pedras vizinhas não se tocam.</summary>
		private const float RingPerTile = 0.3f;

		private readonly float _scale;
		private readonly SigilRing _before;
		private readonly SigilRing _after;

		/// <param name="scale">O tamanho das pedras (1 = o da lista); o círculo cresce com elas.</param>
		public RuneSwapStrip(float scale)
		{
			Name = "Swap";
			_scale = scale;
			Alignment = AlignmentMode.Center;
			AddThemeConstantOverride("separation", Space.Regular);
			_before = new SigilRing(Diameter(scale)) { Name = "Ring", Spread = Spread };
			_after = new SigilRing(Diameter(scale)) { Name = "Ring", Spread = Spread };
			AddChild(Column("Before", T("runes.before"), _before));
			AddChild(Column("After", T("runes.after"), _after));
		}

		/// <summary>Um espaço tocado, em qualquer das colunas (a pedra diz o espaço e o sigilo dela).</summary>
		public event Action<RuneTile>? SlotPressed;

		/// <summary>O nível de cada sigilo quando a tela abriu: a ficha do toque longo mostra em verde o que ele ganhou.</summary>
		public Func<Rune, int?>? OpenedLevel { get; set; }

		/// <summary>A largura de uma coluna (as pedras de fora do círculo passam da borda dele): serve para quem divide o espaço.</summary>
		public static float ColumnWidth(float scale) => Diameter(scale) / 1.6f * Spread * 2 + RuneTile.Side * scale;

		/// <summary>Refaz as duas colunas: <paramref name="now"/> é o que o homúnculo usa; <paramref name="next"/>, o que ele usaria.</summary>
		public void Show(IReadOnlyList<Rune> now, IReadOnlyList<Rune> next)
		{
			var before = new List<Control>();
			var after = new List<Control>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var was = now.FirstOrDefault(r => r.Slot == slot);
				var will = next.FirstOrDefault(r => r.Slot == slot);
				before.Add(Tile(was, slot, true));
				after.Add(Tile(will, slot, will?.Id != was?.Id));
			}

			_before.Set(null, before);
			_after.Set(null, after);
		}

		/// <summary>O círculo em que cabem as pedras sem se tocar: a pedra mais um respiro, dividida pelo que o círculo dá de espaço entre vizinhas.</summary>
		private static float Diameter(float scale) => Mathf.Ceil((RuneTile.Side * scale + 8) / RingPerTile);

		private Control Column(string name, string caption, SigilRing ring)
		{
			var column = new VBoxContainer { Name = name };
			column.AddThemeConstantOverride("separation", Space.Small);
			column.AddChild(new Label { Name = "Caption", Text = caption, ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center });
			// As pedras dos lados passam do círculo: quem o centraliza reserva a largura toda.
			var holder = new CenterContainer { Name = "Holder", CustomMinimumSize = new Vector2(ColumnWidth(_scale), 0) };
			holder.AddChild(ring);
			column.AddChild(holder);
			return column;
		}

		private Control Tile(Rune? rune, int slot, bool lit)
		{
			var tile = new RuneTile(rune, slot, _scale) { Name = $"Slot{slot}", OpenedLevel = OpenedLevel };
			tile.Pressed += t => SlotPressed?.Invoke(t);
			if (!lit)
				tile.Modulate = new Color(1, 1, 1, Fade.Disabled);
			return tile;
		}
	}
}
