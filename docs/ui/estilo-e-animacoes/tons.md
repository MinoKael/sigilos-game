# Tons

`UI/Style/Tones.cs` · namespace `Sigilos.UI.Style`

As cores de cada peso de botão (`ButtonKind`) num lugar só. [GameButton](../componentes/game-button.md) e [TileButton](../componentes/tile-button.md) pedem daqui; antes, cada um tinha uma cópia da tabela.

## Funções

| Função | Devolve |
|---|---|
| `(Color Fill, Color Border) Of(ButtonKind kind)` | O preenchimento e a borda. |
| `Color Ink(ButtonKind kind)` | A tinta do símbolo (e do texto, no peso `Text`). |
| `Color Outline(ButtonKind kind)` | O contorno das letras: um tom escuro da cor do botão. |
| `WoodOutline` | O contorno das letras na madeira: quase preto, puxado para o marrom. |

## Tabela

| `ButtonKind` | `Fill` | `Border` | `Ink` | `Outline` |
|---|---|---|---|---|
| `Primary` | `Palette.Primary` | `Palette.PrimaryDark` | `Palette.Text` | `PrimaryDark` 35 % mais escuro |
| `Secondary` | `Palette.Button` | `Palette.GoldDark` | `Palette.Gold` | `WoodOutline` |
| `Danger` | `Palette.Danger` | `Palette.DangerDark` | `Palette.Text` | `DangerDark` 30 % mais escuro |
| `Text` | transparente | transparente | `Palette.Gold` | `WoodOutline` |

## Variação do cartão

O `TileButton` usa os mesmos tons um pouco mais escuros, porque o cartão é grande e fica atrás do texto: o âmbar escurece 12 % e a madeira vira `Palette.Panel`. A variação é calculada a partir de `Tones.Of`, não copiada.

## Exemplo

```csharp
var (fill, border) = Tones.Of(kind);
label.AddThemeStyleboxOverride("normal", GameTheme.Box(fill, border, 2, Radius.Button, 0));
label.AddThemeColorOverride("font_color", Tones.Ink(kind));
label.AddThemeColorOverride("font_outline_color", Tones.Outline(kind));
```
