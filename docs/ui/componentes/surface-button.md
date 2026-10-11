# SurfaceButton

`UI/Components/Actions/SurfaceButton.cs` · herda de [TouchButton](touch-button.md)

O botão genérico: uma superfície tocável em que quem cria dá as caixas de cada estado e o conteúdo. Tudo o que vem de `TouchButton` (afundar, clique, apagar desligado, cursor, sem foco) já está nele.

Existe para não criar `Button` cru nas telas. Hoje fazem isso as linhas da lista de opções (`Choices.Row`), a cápsula de moeda (`CurrencyBar.Capsule`), a célula da escolha de avatar (`AvatarPicker`) e a hora do Santuário (`HubScreen`).

## Quando usar

- Uma linha de lista, uma célula ou uma cápsula que se toca, com um desenho que só aparece ali.
- Um texto que se toca sem parecer botão (`Boxes(null)`).

Para uma ação escrita, prefira [GameButton](game-button.md), que já tem os pesos e a medida.

## Construtores

| Assinatura | Descrição |
|---|---|
| `SurfaceButton(float press = 0.95f)` | A escala ao afundar, como em `TouchButton`. |
| `SurfaceButton()` | O mesmo com `0.95`. Permite o inicializador de objeto: `new SurfaceButton { Name = "Time" }`. |

## Propriedades

| Nome | Tipo | Padrão | Descrição |
|---|---|---|---|
| `Body` | `MarginContainer` | — | Onde vai o conteúdo (é o `Content` da base). |
| `Hug` | `bool` | `false` | O tamanho mínimo acompanha o conteúdo. Sem ele, o tamanho é o que o chamador põe (`CustomMinimumSize`, a célula da grade). |
| `Floor` | `Vector2` | `(0, 0)` | O menor tamanho com `Hug` ligado (a altura de uma cápsula). |
| `Highlight` | `bool` | `false` | Herdado: liga o pulso. `SurfaceButton` não desenha o chamado. |

## Métodos (encadeáveis)

| Método | Descrição |
|---|---|
| `Boxes(StyleBox? rest, StyleBox? hover = null, StyleBox? pressed = null)` | As caixas de cada estado. Sem `hover`, repete `rest`; sem `pressed`, repete `hover`. `rest` nulo não desenha nada parado (`StyleBoxEmpty`). Também põe `hover_pressed`. |
| `Padded(int left, int right, int top = 0, int bottom = 0)` | As margens de `Body`. |

Os dois devolvem o próprio botão, então se encadeiam com o inicializador.

## Composição

| Ponto | O que vai |
|---|---|
| `Body` | Um único filho que organiza o resto (uma fileira, uma coluna, um rótulo). Os filhos devem ignorar o mouse (`MouseFilter = Ignore`), senão roubam o toque. |

## Eventos

Os da Godot: `Pressed`, `Toggled`, `ButtonDown`, `ButtonUp`.

## Estados

| Estado | Como aparece |
|---|---|
| Repouso | `rest` (nada, se nulo). |
| Sob o mouse | `hover`, ou `rest`. |
| Apertado | `pressed`, ou `hover`; o botão afunda. |
| Desligado | A caixa `disabled` do tema; o conteúdo apaga para `Fade.Disabled`. Quem precisa de outra caixa põe `AddThemeStyleboxOverride("disabled", ...)`. |

## Exemplos

Uma linha de lista que acende sob o mouse:

```csharp
var rest = GameTheme.Box(Palette.Inset, Palette.GoldDark, 1, Radius.Medium, 0);
var hover = GameTheme.Box(States.HoverFill, Palette.Gold, 1, Radius.Medium, 0);
var row = new SurfaceButton { Name = "Option1", CustomMinimumSize = new Vector2(0, 52) }
    .Boxes(rest, hover)
    .Padded(Space.Large, Space.Large);
row.Body.AddChild(Layout.Text(T("filter.sort")));
row.Pressed += choose;
list.AddChild(row);
```

Uma cápsula que cresce com o número, com altura mínima, e acende o contorno sob o mouse ([Pills](../estilo-e-animacoes/estados.md#pílulas)):

```csharp
var pill = Pills.Carved(18, Space.Small, Space.Large);
var capsule = new SurfaceButton { Name = "Mana", Hug = true, Floor = new Vector2(0, 36) }
    .Boxes(pill, Pills.Lit(pill));
capsule.Body.AddChild(Layout.Text("120", GameTheme.Number));
capsule.Pressed += explain;
header.AddChild(capsule);
```

Um texto que se toca, sem caixa nenhuma (a hora do Santuário):

```csharp
var time = new SurfaceButton { Name = "Time", Hug = true }.Boxes(null);
time.Body.AddChild(clock);
time.Pressed += openCalendar;
header.AddChild(time);
```

## Ver também

- [TouchButton](touch-button.md): a base.
- [Estados](../estilo-e-animacoes/estados.md): `States` e `Pills` para as caixas.
