# Estados

`UI/Style/States.cs`, `UI/Style/SelectionFrame.cs`, `UI/Style/Pills.cs` · namespace `Sigilos.UI.Style`

As cores dos estados de interação, iguais em todo o jogo, e as caixas prontas que as usam.

- O que está **escolhido** acende em azul arcano.
- O que está **marcado** numa seleção de vários (fundir, soltar, vender) fica esverdeado.
- O que está **sob o mouse** clareia. No celular não existe mouse: nenhum estado pode depender só disso.

## `States`

| Nome | Valor | Uso |
|---|---|---|
| `Selected` | `Palette.Arcane` | A borda do escolhido (o cartão, a opção atual, o sigilo ligado). |
| `SelectedGlow` | `Arcane` a 0,4 | A aura do escolhido. |
| `LitFill` | `Inset` 14 % para `Arcane` | O fundo aceso do escolhido. |
| `MarkedFill` | `Inset` 18 % para `Spirit` | O fundo do marcado. |
| `HoverFill` | `Inset` 6 % mais claro | O fundo rebaixado sob o mouse. |

## `SelectionFrame`

A moldura de um cartão que se escolhe e se marca (o monstro, a runa). Pinta a caixa do cartão conforme o estado. O cartão guarda os estados e chama `Apply` quando um deles muda. Usada por `CreatureCard` e `RuneTile`.

### Construtor

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `box` | `StyleBoxFlat` | A caixa do cartão, que a moldura pinta. |
| `frame` | `Color` | A cor da moldura em repouso (raridade, estrelas). |
| `width` | `int` | A espessura da borda em repouso. |

### Propriedades

| Nome | Tipo | Padrão | Descrição |
|---|---|---|---|
| `Frame` | `Color` | — | A cor da moldura em repouso (muda quando o cartão muda). |
| `Width` | `int` | — | A espessura em repouso. |
| `HoverLighten` | `float` (init) | `0.3` | O quanto a moldura clareia sob o mouse. |
| `GlowSize` | `int` (init) | `5` | A aura do escolhido, em px. |
| `Rest` | `(Color, int, Vector2)` (init) | sem sombra | A sombra em repouso: cor, tamanho e deslocamento. |
| `Hover` | `(Color, int, Vector2)` (init) | sem sombra | A sombra sob o mouse. |

### `Apply(bool selected, bool marked, bool hover)`

| Estado | Borda | Fundo | Sombra |
|---|---|---|---|
| Repouso | `Frame`, `Width` | `Palette.Inset` | `Rest` |
| Sob o mouse | `Frame` clareada | `States.HoverFill` | `Hover` |
| Marcado | — | `States.MarkedFill` | — |
| Escolhido | `States.Selected`, `Width + 1` | — | `SelectedGlow`, `GlowSize` |

O escolhido vence o mouse na borda e na sombra; o marcado vence o mouse no fundo.

## Pílulas

`Pills` faz as caixas de cápsula do jogo. Cada chamada devolve uma caixa nova, então quem muda a borda depois muda só a sua. Elas são estilo, não nó: quem precisa de uma cápsula tocável usa um [SurfaceButton](../componentes/surface-button.md) com estas caixas.

| Função | Devolve |
|---|---|
| `Carved(int radius, int left, int right)` | A pedra entalhada (`GameTheme.Carved`) com as pontas redondas e as margens dos lados: as moedas e o correio do cabeçalho, `Layout.Chip`, `Layout.Labeled`. |
| `Lit(StyleBoxFlat rest)` | A mesma cápsula com o contorno de ouro: a cápsula tocável sob o dedo. |
| `Floating(Color border, float height, int width = 2, bool shadow = true)` | A pílula que flutua por cima da tela (o balão do chat, o aviso da Batalha automática): raio de meia altura, fundo quase opaco e sombra. Sem `shadow` para o que já está colado numa flutuante. O nó fica a `Fade.Floating`. |

## Exemplos

```csharp
var selection = new SelectionFrame(box, rarity, 2) { Hover = (new Color(rarity, 0.3f), 5, Vector2.Zero) };
selection.Apply(selected, marked, hover);

var option = GameTheme.Box(selected ? States.LitFill : Palette.Inset, selected ? States.Selected : Palette.GoldDark, 1, Radius.Medium, 0);
```

```csharp
badge.AddThemeStyleboxOverride("panel", Pills.Floating(Palette.Arcane, 44));
badge.Modulate = new Color(1, 1, 1, Fade.Floating);

var rest = Pills.Carved(16, 5, Space.Large);
capsule.AddThemeStyleboxOverride("normal", rest);
capsule.AddThemeStyleboxOverride("hover", Pills.Lit(rest));
```
