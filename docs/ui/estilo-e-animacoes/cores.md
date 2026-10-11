# Cores

`UI/Style/Palette.cs` · namespace `Sigilos.UI.Style`

Todas as cores do jogo, por papel. A identidade é a de um grimório estelar: céu de noite, couro ameixa, ouro velho e pergaminho. Uma tela pede `Palette.Gold`, nunca `new Color(...)`. As cores de estado (escolhido, marcado) estão em [Estados](estados.md), e as dos botões em [Tons](tons.md).

## Fundo e superfícies

| Nome | RGB | Papel |
|---|---|---|
| `Background` | 12, 10, 22 | O fundo de tela: céu de noite quase preto. |
| `BackgroundGlow` | 38, 29, 64 | O centro do céu, mais claro e violeta. |
| `Panel` | 40, 29, 50 | Couro de capa de grimório: os painéis. |
| `PanelLight` | 58, 43, 72 | O painel aceso (a aba aberta, a opção sob o mouse). |
| `Inset` | 17, 15, 30 | Tinta de noite: listas, barras, cartões, o que fica rebaixado no painel. |

## Texto

| Nome | RGB | Papel |
|---|---|---|
| `Text` | 240, 230, 208 | Pergaminho: o texto. |
| `TextFaded` | 170, 160, 186 | O texto secundário: lavanda fria, para não competir. |
| `Gold` | 216, 178, 104 | Ouro velho: molduras, títulos, ícones. |
| `GoldDark` | 124, 98, 62 | Latão gasto: a moldura em repouso, os filetes. |
| `Grey` | 220, 220, 220 | Cinza neutro. |

## Botões

| Nome | RGB | Papel |
|---|---|---|
| `Button` / `ButtonHover` | 58, 42, 76 / 76, 56, 98 | Couro violeta da lombada: os botões secundários. |
| `Disabled` | 40, 37, 50 | O botão desligado. |
| `Primary` / `PrimaryDark` | 206, 146, 58 / 106, 64, 24 | Âmbar dourado, o único quente: a ação principal. |
| `Danger` / `DangerDark` | 156, 50, 64 / 78, 22, 36 | Carmim de lacre: o que não tem volta. |

## Luz e sentido

| Nome | RGB | Papel |
|---|---|---|
| `Arcane` | 150, 178, 255 | A luz de estrela azul: sob o mouse, sigilo aceso, seleção. |
| `Spirit` | 126, 222, 186 | O verde-aurora: pronto para coletar, o que o jogador deve tocar, o trecho vencido. |
| `Positive` | 143, 214, 124 | Bônus: o que as runas somam, o que sobe. |
| `Negative` | 226, 100, 84 | O que desce. |
| `Awakened` | 196, 146, 255 | Estrelas e nome de invocação desperta. |

## Luta

| Nome | RGB | Papel |
|---|---|---|
| `Health` / `HealthLow` | 112, 186, 96 / 214, 84, 66 | A barra de vida, cheia e baixa. |
| `Shield` | 176, 200, 222 | O escudo. |
| `Damage` | 250, 240, 225 | O número de dano. |
| `Heal` | = `Positive` | O número de cura. |

## Céu e grimório

| Nome | RGB | Papel |
|---|---|---|
| `Violet` | 134, 98, 206 | O violeta místico: sigilos desenhados, selos, os traços das constelações. |
| `Indigo` | 70, 76, 150 | O índigo do céu: a grade do mapa celeste, os anéis do astrolábio. |
| `Starlight` | 232, 236, 255 | O ponto de luz de estrela. |
| `Parchment` / `ParchmentShade` | 226, 210, 174 / 178, 150, 108 | A página do Grimório do Invocador e a mancha das bordas. |
| `Ink` / `InkFaded` | 48, 34, 44 / 98, 78, 84 | A tinta no pergaminho e a das anotações. |
| `Rubric` | 98, 54, 138 | A tinta de destaque da página (títulos de capítulo). |

## Funções

| Função | Devolve |
|---|---|
| `Stars(bool awakened)` | A cor das estrelas: `Awakened` ou `Gold`. |
| `Frame(int stars)` | A moldura por raridade: bronze, prata e ouro a partir da 3★. |
| `Of(RuneRarity rarity)` | A cor da runa: branca, verde, azul, roxa, laranja. |
| `Of(Element element)` | A cor do elemento. |

## Exemplo

```csharp
name.AddThemeColorOverride("font_color", Palette.Of(element));
bonus.AddThemeColorOverride("font_color", Palette.Positive);
var frame = GameTheme.Box(Palette.Inset, Palette.Frame(stars), 2, Radius.Tile, 0);
name.AddThemeStyleboxOverride("normal", frame);
```
