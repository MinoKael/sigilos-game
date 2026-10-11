# ChoiceButton e Choices

`UI/Components/Actions/ChoiceButton.cs`

O jogo não usa lista suspensa (`OptionButton`): no celular ela abre longe do dedo e sem o estilo do jogo. No lugar dela há duas peças.

- **`ChoiceButton`** (herda de [GameButton](game-button.md)) é o campo. Diz o nome do campo e o valor atual ("Ordem: Nível") e, tocado, abre a lista.
- **`Choices`** é a lista: uma janela contextual ([Dialog](dialog.md)) colada em quem abriu, com um botão por opção e a atual acesa em azul. Com mais de 8 opções, usa duas colunas. Tocar escolhe e fecha.

## `Choice`

```
record Choice(string Text, Texture2D? Icon = null, Color? Ink = null, Glyph? Rune = null)
```

| Campo | Descrição |
|---|---|
| `Text` | O texto da opção. |
| `Icon` | Um símbolo na frente (o elemento, por exemplo). |
| `Ink` | A cor do símbolo; o padrão é `Palette.Gold`. |
| `Rune` | Um Glifo no lugar do símbolo, na fonte das runas. |

## ChoiceButton

### Construtor

| Parâmetro | Tipo | Padrão | Descrição |
|---|---|---|---|
| `field` | `string` | — | O nome do campo; o botão mostra "campo: valor". Também é o título da lista. |
| `options` | `IReadOnlyList<(Choice Choice, int Value)>` | — | As opções e o valor de cada uma. |
| `current` | `int` | — | O valor escolhido agora; um valor fora da lista mostra a primeira opção. |
| `height` | `float` | `48` | A altura do campo. |

### Propriedades e eventos

| Nome | Tipo | Descrição |
|---|---|---|
| `Value` | `int` | O valor escolhido agora. |
| `Changed` | `event Action<int>` | O jogador escolheu: recebe o novo valor. |

O peso é sempre `Secondary`. O resto (`Disabled`, `Named`, `Wide`) vem de `GameButton`.

## Choices

| Método | Descrição |
|---|---|
| `static Dialog Open(Control from, string title, IReadOnlyList<Choice> options, int selected, Action<int> chosen, bool anchored = true)` | Abre a lista. `selected` e o valor de `chosen` são **índices** na lista. Com `anchored` falso, abre no centro. |
| `static SurfaceButton Row(Choice choice, bool current, Action pressed)` | Uma linha da lista, para quem monta uma lista própria. |

### Estados da linha

| Estado | Como aparece |
|---|---|
| Repouso | `Palette.Inset`, borda de latão 1 px. |
| Atual | Fundo `States.LitFill`, borda `States.Selected` 2 px e texto azul-claro. |
| Sob o mouse | `Palette.PanelLight` com borda de ouro. |
| Apertado | Borda azul 2 px. |

## Exemplos

O campo de ordem de uma lista:

```csharp
var options = new List<(Choice Choice, int Value)>
{
    (new Choice(T("filter.all")), 0),
    (new Choice(T("filter.element"), Ink: Palette.Arcane), 1),
};
var sort = new ChoiceButton(T("filter.sort"), options, 0) { Name = "Sort" };
sort.Changed += sortBy;
filters.AddChild(sort);
```

A lista aberta por outro botão, quando o campo não serve (a tela mostra o valor de outro jeito):

```csharp
field.Pressed += () => Choices.Open(field, T("filter.sort"), options, current, chosen);
```
