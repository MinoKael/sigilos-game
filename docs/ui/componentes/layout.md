# Layout

`UI/Components/Layout/Layout.cs` · classe estática

As peças de layout que toda tela repete: fundo, margem, cabeçalho, painel com título, cápsula, grade, rolagem e limpeza. São fábricas que devolvem controles da Godot já configurados com o tema e os [tokens](../estilo-e-animacoes/tokens.md).

## Nome de nó

A tela é montada em código, sem cena, então o nome do nó é o que diz, no caminho da árvore e no modo de depuração visual, de onde cada nó veio. Quem cria e pendura um nó dá a ele o nome do papel que ele tem no pai, em PascalCase (`Body`, `Slot3`, `Rune17`).

| Membro | Descrição |
|---|---|
| `T Named<T>(this T node, string name)` | Dá o nome e devolve o nó, para nomear o que sai de uma fábrica: `Layout.Row().Named("Actions")`. |
| `string NodeName(string id)` | Um id do jogo (`"level_max"`) em PascalCase (`LevelMax`). |
| `string NextCell(GridContainer grid)` | O nome da próxima célula de uma grade (`R2C3`). |

As fábricas dão um nome padrão quando o papel é sempre o mesmo (`Page`, `Header`, `Scroll`); o chamador troca quando quiser.

## Tela

| Membro | Devolve | Descrição |
|---|---|---|
| `ScreenMargin` | `const int` (24) | A margem da tela. |
| `Background(Control? focus = null, bool ring = true)` | `Backdrop` | O céu de noite com as estrelas e o astrolábio, centrado na tela ou em `focus`. |
| `Page(Control screen)` | `VBoxContainer` | Põe a margem em `screen` e devolve a coluna principal. |
| `Header(string title, CurrencyBar? currencies, Action onBack)` | `(HBoxContainer Header, HBoxContainer Extra)` | O cabeçalho, igual em todas as telas: o título à esquerda, `Extra` no meio (contagem, abas), as moedas e a seta de voltar à direita. A seta também responde ao Esc e ao Voltar do celular. |
| `Host(Control control)` | `Control` | O controle mais alto acima de `control`: onde abrem as camadas por cima da tela. |

## Arranjo

| Membro | Devolve | Descrição |
|---|---|---|
| `Row(int separation = Space.Regular, bool centered = false)` | `HBoxContainer` | Uma fileira. |
| `Grid(int columns, int separation = Space.Medium)` | `GridContainer` | Uma grade de botões: as colunas dividem a largura toda. Botões lado a lado vão aqui. |
| `Flow(int separation = Space.Medium)` | `HFlowContainer` | Uma fileira que quebra linha, para fichas, chips e cartões. **Nunca para botões.** |
| `Scroll(Control content)` | `ScrollContainer` | Rolagem vertical com a gema do tema, sem trilho e com a barra reservada. Arrastar com o mouse também rola. |
| `Tab(TabContainer tabs, string name, string title)` | `VBoxContainer` | Uma aba com rolagem; o conteúdo vai na coluna devolvida. |
| `Section(string title)` | `(PanelContainer Panel, VBoxContainer Content)` | Um painel com cabeçalho; o conteúdo vai em `Content`. |

## Conteúdo pronto

| Membro | Devolve | Descrição |
|---|---|---|
| `Text(string text, string? variation = null, float width = 0)` | `Label` | Texto que quebra linha, com um papel do tema (`GameTheme.Faded`...). |
| `Chip(icon, string value, Color? ink = null, Vector2 labelMinimumSize = default)` | `PanelContainer` | Uma cápsula de símbolo e número (custo, contagem). `icon` é nome, `Glyph` ou `Texture2D`. |
| `Labeled(icon, string value, string caption, Color? ink = null)` | `PanelContainer` | A cápsula que se explica: símbolo, número e o que ele é ("575 Essência"). |
| `Medal(Texture2D? art, Color ink, float size)` | `Control` | Um retrato recortado em círculo. |
| `Energy(Color color, float height = 10)` | `ProgressBar` | Uma barra de energia entalhada. |

## Ciclo de vida

As telas se remontam do zero a cada mudança. Estas funções evitam os problemas da Godot com nós que saem da árvore.

| Membro | Descrição |
|---|---|
| `Clear(Node node)` | Tira e libera todos os filhos. |
| `Discard(Node node)` | Libera no fim do quadro, trocando o nome antes para o substituto poder usar o mesmo. |
| `Reveal(ScrollContainer scroll, Control control)` | Rola só o necessário para `control` aparecer inteiro, depois de ele se arrumar. |
| `KeepScroll(ScrollContainer scroll, int value)` | Põe a rolagem de uma lista remontada onde a antiga estava. |
| `WheelStep` | `const float` (72): quanto um clique da roda rola. |

## Exemplos

Uma tela com cabeçalho, lista rolável e um painel:

```csharp
var page = Layout.Page(screen);
var (header, extra) = Layout.Header(T("destination.Campaign"), currencies, back);
extra.AddChild(Layout.Labeled("fight", "3/5", T("battle.wave_tip")).Named("Wave"));
page.AddChild(header);

var list = new VBoxContainer { Name = "List" };
list.AddThemeConstantOverride("separation", Space.Medium);
page.AddChild(Layout.Scroll(list).Named("Scroll"));

var (panel, content) = Layout.Section(T("auto.rewards"));
content.AddChild(Layout.Row(Space.Tight).Named("Chips"));
list.AddChild(panel);
```

Remontar uma lista e mostrar o item escolhido:

```csharp
Layout.Clear(list);
list.AddChild(chosen);
Layout.Reveal(scroll, chosen);
```
