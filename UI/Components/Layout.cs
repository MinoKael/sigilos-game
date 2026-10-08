using System;
using System.Linq;
using Godot;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// As peças de layout que toda tela repete: fundo, margem, cabeçalho, painel com título, cápsula e limpeza.
	///
	/// Nome de nó: quem cria e pendura um nó dá a ele o nome do papel que ele tem no pai, em PascalCase
	/// (<c>Body</c>, <c>Slot3</c>, <c>Rune17</c>), com <see cref="Named{T}"/> ou <c>Name = ...</c>. A tela
	/// é montada em código, sem cena, então o nome é o que diz no caminho da árvore (e no modo de
	/// depuração visual) de onde cada nó veio. As peças de dentro de um componente são nomeadas por ele;
	/// estas fábricas dão um nome padrão quando o papel é sempre o mesmo (<c>Page</c>, <c>Header</c>,
	/// <c>Scroll</c>), e o chamador troca quando quiser.
	/// </summary>
	public static class Layout
	{
		public const int ScreenMargin = 24;

		/// <summary>Dá nome ao nó e o devolve, para nomear o que sai de uma fábrica: <c>Layout.Row(8).Named("Actions")</c>.</summary>
		public static T Named<T>(this T node, string name) where T : Node
		{
			node.Name = name;
			return node;
		}

		/// <summary>O nome de nó de um id do jogo: <c>level_max</c> → <c>LevelMax</c>.</summary>
		public static string NodeName(string id) =>
			string.Concat(id.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

		/// <summary>
		/// O fundo de pedra quente com a luz de vela e o anel de sigilo, centrado na tela ou no centro de
		/// <paramref name="focus"/> (o que a tela tem no meio: a constelação, o portal, o círculo).
		/// </summary>
		public static Backdrop Background(Control? focus = null, bool ring = true) => new(focus, ring) { Name = "Backdrop" };

		/// <summary>Põe a margem da tela em <paramref name="screen"/> e devolve a coluna principal.</summary>
		public static VBoxContainer Page(Control screen)
		{
			var margin = new MarginContainer { Name = "Page" };
			margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			foreach (var side in new[] { "left", "right", "top", "bottom" })
				margin.AddThemeConstantOverride($"margin_{side}", ScreenMargin);
			screen.AddChild(margin);

			var column = new VBoxContainer { Name = "Content" };
			column.AddThemeConstantOverride("separation", 12);
			margin.AddChild(column);
			return column;
		}

		/// <summary>O controle mais alto acima de <paramref name="control"/>: onde abrem as camadas por cima da tela (herdam o tema).</summary>
		public static Control Host(Control control)
		{
			var host = control;
			while (host.GetParent() is Control parent)
				host = parent;
			return host;
		}

		/// <summary>Tira todos os filhos: as telas se remontam do zero a cada mudança.</summary>
		public static void Clear(Node node)
		{
			foreach (var child in node.GetChildren())
				Discard(child);
		}

		/// <summary>
		/// Libera o nó no fim do quadro. Antes troca o nome dele por um que não se repete: o substituto entra
		/// na hora com o mesmo nome, e se o antigo ainda o tivesse, o Godot renomearia o novo para "@Nome@123".
		/// Não sai da árvore agora porque quem chama costuma estar no meio de um sinal do próprio nó (o clique
		/// que pediu a remontagem). Mas some na hora: escondido, não conta no tamanho do contêiner, e a
		/// rolagem não pula com o conteúdo dobrado por um quadro (o velho e o novo juntos).
		/// </summary>
		public static void Discard(Node node)
		{
			node.Name = $"Discarded{node.GetInstanceId()}";
			if (node is CanvasItem item)
				item.Visible = false;
			node.QueueFree();
		}

		/// <summary>
		/// Cabeçalho de tela, igual em todas: a seta de voltar e o título à esquerda, a caixa do meio
		/// (<c>Extra</c>, para o que a tela quiser mostrar ao lado do título: contagem, abas) e o cabeçalho
		/// de recursos à direita. A seta também responde ao Esc e ao Voltar do celular.
		/// </summary>
		public static (HBoxContainer Header, HBoxContainer Extra) Header(string title, CurrencyBar? currencies, Action onBack)
		{
			var header = new HBoxContainer { Name = "Header" };
			header.AddThemeConstantOverride("separation", 14);
			header.AddChild(new Label { Name = "Title", Text = title, ThemeTypeVariation = GameTheme.Title, VerticalAlignment = VerticalAlignment.Center });
			header.AddChild(ChatBubble.Slot());
			var extra = new HBoxContainer { Name = "Extra", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Begin };
			extra.AddThemeConstantOverride("separation", 10);
			header.AddChild(extra);
			if (currencies != null)
			{
				currencies.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
				header.AddChild(currencies);
			}
			header.AddChild(new BackButton(onBack) { Name = "Back" });

			return (header, extra);
		}

		/// <summary>Cápsula de símbolo e número: custo, contagem, recompensa. O nó leva o nome do símbolo (<c>Essence</c>).</summary>
		public static PanelContainer Chip(string icon, string value, Color? ink = null, Vector2 labelMinimumSize = default) => Chip(Art.Icon(icon), value, ink, labelMinimumSize);

		/// <summary>Cápsula com um Glifo, escrito na fonte das runas; o nó leva o nome do Glifo.</summary>
		public static PanelContainer Chip(Core.Content.Glyph glyph, string value, Color? ink = null, Vector2 labelMinimumSize = default) =>
			Capsule(glyph.ToString(), new RuneGlyph(glyph, 22, ink ?? Palette.Gold) { Name = "Glyph" }, value, "", labelMinimumSize);

		/// <summary>Cápsula com qualquer desenho (efeito, criatura); o nó leva o nome do desenho.</summary>
		public static PanelContainer Chip(Texture2D? icon, string value, Color? ink = null, Vector2 labelMinimumSize = default) =>
			Capsule(Art.NameOf(icon) is { } name ? NodeName(name) : "Chip", Medal(icon, ink ?? Palette.Gold, 24).Named("Icon"), value, "", labelMinimumSize);

        /// <summary>
        /// Cápsula que se explica sozinha: o símbolo, o número e o que ele é, escrito ("575 Essência",
        /// "★★★★ 50% Runa"). É o jeito de mostrar recompensa e custo sem depender de dica.
        /// </summary>
        public static PanelContainer Labeled(string icon, string value, string caption, Color? ink = null, Vector2 labelMinimumSize = default) =>
            Capsule(NodeName(icon), Medal(Art.Icon(icon), ink ?? Palette.Gold, 24).Named("Icon"), value, caption, labelMinimumSize);

        /// <summary>A cápsula labeled com um Glifo no lugar do símbolo.</summary>
        public static PanelContainer Labeled(Core.Content.Glyph glyph, string value, string caption, Color? ink = null) =>
			Capsule(glyph.ToString(), new RuneGlyph(glyph, 22, ink ?? Palette.Gold) { Name = "Glyph" }, value, caption, default);

		/// <summary>A cápsula labeled com qualquer desenho (efeito, criatura).</summary>
		public static PanelContainer Labeled(Texture2D? icon, string value, string caption, Color? ink = null) =>
			Capsule(Art.NameOf(icon) is { } name ? NodeName(name) : "Chip", Medal(icon, ink ?? Palette.Gold, 24).Named("Icon"), value, caption, default);

		/// <summary>A cápsula: o símbolo, o número (<c>Value</c>) e, se houver, o que ele é (<c>Caption</c>), numa fileira (<c>Row</c>).</summary>
		private static PanelContainer Capsule(string name, Control icon, string value, string caption, Vector2 labelMinimumSize)
		{
			var capsule = new PanelContainer { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
			var box = GameTheme.Carved(Palette.Inset, 4);
			box.SetCornerRadiusAll(16);
			box.ContentMarginLeft = 5;
			box.ContentMarginRight = 12;
			capsule.AddThemeStyleboxOverride("panel", box);
			var row = new HBoxContainer { Name = "Row", MouseFilter = Control.MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", 6);
			row.AddChild(icon);
			if (value.Length > 0)
			{
				var label = new Label { Name = "Value", Text = value, ThemeTypeVariation = GameTheme.Number, MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
				label.AddThemeFontSizeOverride("font_size", 17);
				label.CustomMinimumSize = labelMinimumSize;
				label.HorizontalAlignment = HorizontalAlignment.Right;
				row.AddChild(label);
			}

			if (caption.Length > 0)
			{
				var text = new Label { Name = "Caption", Text = caption, ThemeTypeVariation = GameTheme.Faded, MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
				row.AddChild(text);
			}

			capsule.AddChild(row);
			return capsule;
		}

		/// <summary>Um retrato recortado em círculo, de tamanho fixo.</summary>
		public static Control Medal(Texture2D? art, Color ink, float size)
		{
			var holder = new Control { Name = "Medal", CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore };
			holder.AddChild(Doodle.Masked(art, ink, MaskShape.Circle, boil: false));
			return holder;
		}

		/// <summary>O nome da próxima célula de uma grade: linha e coluna, contadas do que já está nela (<c>R2C3</c>).</summary>
		public static string NextCell(GridContainer grid)
		{
			var index = grid.GetChildCount();
			return $"R{index / grid.Columns + 1}C{index % grid.Columns + 1}";
		}

		/// <summary>Fileira de sigilos, centralizada ou não.</summary>
		public static HBoxContainer Row(int separation = 10, bool centered = false)
		{
			var row = new HBoxContainer { Alignment = centered ? BoxContainer.AlignmentMode.Center : BoxContainer.AlignmentMode.Begin };
			row.AddThemeConstantOverride("separation", separation);
			return row;
		}

		/// <summary>
		/// Grade de botões: <paramref name="columns"/> colunas que dividem a largura toda, e cada
		/// <see cref="GameButton"/> preenche a célula dele. É o jeito de pôr botões lado a lado; a
		/// <see cref="Flow"/> fica para fichas e chips, que têm largura própria.
		/// </summary>
		public static GridContainer Grid(int columns, int separation = 8)
		{
			var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			grid.AddThemeConstantOverride("h_separation", separation);
			grid.AddThemeConstantOverride("v_separation", separation);
			return grid;
		}

		/// <summary>Fileira que quebra linha (fichas, chips, cartões; botões vão na <see cref="Grid"/>).</summary>
		public static HFlowContainer Flow(int separation = 8)
		{
			var flow = new HFlowContainer();
			flow.AddThemeConstantOverride("h_separation", separation);
			flow.AddThemeConstantOverride("v_separation", separation);
			return flow;
		}

		/// <summary>
		/// Rolagem vertical: a gema do tema, sem trilho. Arrastar com o mouse também rola (<see cref="DragScroll"/>).
		/// O lugar da barra fica sempre guardado (<c>Reserve</c>): o conteúdo tem a mesma largura com a barra ou
		/// sem ela, e um texto no limite não fica quebrando linha ora sim, ora não, sem parar.
		/// </summary>
		public static ScrollContainer Scroll(Control content)
		{
			var scroll = new ScrollContainer
			{
				Name = "Scroll",
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
				VerticalScrollMode = ScrollContainer.ScrollMode.Reserve,
				// A roda do mouse anda um bom pedaço por clique (o padrão, um oitavo da altura, se arrasta).
				ScrollVerticalCustomStep = WheelStep,
			};
			content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			scroll.AddChild(content);
			DragScroll.Enable(scroll);
			return scroll;
		}

		/// <summary>Quanto um clique da roda do mouse rola, em px: perto de meia linha de cartões.</summary>
		public const float WheelStep = 72;

		/// <summary>
		/// Rola só o necessário para <paramref name="control"/> aparecer inteiro (o cartão tocado na última
		/// linha, cortado pela borda). Espera o contêiner arrumar os filhos novos, que ele só faz adiado, no fim
		/// do quadro: antes disso a peça recém-criada ainda está no canto de cima da lista, e mostrá-la levaria a
		/// rolagem para o topo. Depois, mais um quadro, para a rolagem já ter medido o conteúdo.
		/// </summary>
		public static async void Reveal(ScrollContainer scroll, Control control)
		{
			if (control.GetParent() is Container parent)
				await parent.ToSignal(parent, Container.SignalName.SortChildren);
			await scroll.ToSignal(scroll.GetTree(), SceneTree.SignalName.ProcessFrame);
			if (GodotObject.IsInstanceValid(scroll) && GodotObject.IsInstanceValid(control) && control.IsInsideTree())
				scroll.EnsureControlVisible(control);
		}

		/// <summary>
		/// Põe a rolagem de uma lista remontada (com um <see cref="ScrollContainer"/> novo) onde a antiga estava.
		/// Espera a rolagem nova medir o conteúdo: antes disso ela não tem para onde ir e o valor voltaria a zero.
		/// </summary>
		public static async void KeepScroll(ScrollContainer scroll, int value)
		{
			if (value <= 0)
				return;
			await scroll.ToSignal(scroll, Container.SignalName.SortChildren);
			if (GodotObject.IsInstanceValid(scroll))
				scroll.ScrollVertical = value;
		}

		/// <summary>Uma aba com rolagem vertical, de nome de nó <paramref name="name"/>; o conteúdo vai na coluna devolvida.</summary>
		public static VBoxContainer Tab(TabContainer tabs, string name, string title)
		{
			var column = new VBoxContainer { Name = "Content" };
			column.AddThemeConstantOverride("separation", 10);
			tabs.AddChild(Scroll(column).Named(name));
			tabs.SetTabTitle(tabs.GetTabCount() - 1, title);
			return column;
		}

		/// <summary>Texto simples que quebra linha.</summary>
		public static Label Text(string text, string? variation = null, float width = 0)
		{
			var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(width, 0) };
			if (variation != null)
				label.ThemeTypeVariation = variation;
			return label;
		}

		/// <summary>Painel com cabeçalho. O conteúdo vai na coluna devolvida.</summary>
		public static (PanelContainer Panel, VBoxContainer Content) Section(string title)
		{
			var panel = new PanelContainer { Name = "Section" };
			var content = new VBoxContainer { Name = "Content" };
			content.AddThemeConstantOverride("separation", 8);
			panel.AddChild(content);
			if (title.Length > 0)
				content.AddChild(new Label { Name = "Heading", Text = title, ThemeTypeVariation = GameTheme.Heading });
			return (panel, content);
		}

		/// <summary>Barra de energia entalhada, na cor dada.</summary>
		public static ProgressBar Energy(Color color, float height = 10)
		{
			var bar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Stop };
			bar.AddThemeStyleboxOverride("fill", GameTheme.Energy(color));
			return bar;
		}
	}
}
