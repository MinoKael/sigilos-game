# Tipografia

`UI/Style/GameTheme.cs`, `UI/Style/Tokens.cs` · namespace `Sigilos.UI.Style`

O texto é pensado para o dedo primeiro: celular deitado, 1280×720 de base, texto grande. As telas pedem os papéis do tema pelo nome de variação (`ThemeTypeVariation`), nunca por cor e tamanho soltos.

## Fontes

| Fonte | Membro | Uso |
|---|---|---|
| A fonte do jogo (Wezards, com reserva do sistema para ★, ×, ⟳) | `GameTheme.Serif` (e `Sans`, a mesma) | Texto, números, botões. É a fonte padrão do tema. |
| Cinzel 600 | `GameTheme.Display` | Só títulos: `Title`, `Heading`, `PageHeading`. |
| Kehdrai | `GameTheme.Runes` | Os Glifos: cada runa é uma letra, nítida em qualquer tamanho. |

## Papéis do tema

| Papel | Constante | Fonte | Tamanho | Cor |
|---|---|---|---|---|
| Texto corrido | (padrão) | Serif | 16 (`BodySize`) | `Palette.Text` |
| Título de tela | `GameTheme.Title` | Display | 32 | `Palette.Gold` |
| Cabeçalho de painel | `GameTheme.Heading` | Display | 23 | `Palette.Gold` |
| Valor secundário | `GameTheme.Faded` | Serif | 12 (`SmallSize`) | `Palette.TextFaded` |
| Número em destaque | `GameTheme.Number` | Serif | 19 | `Palette.Text` |
| Capítulo do Grimório | `GameTheme.PageHeading` | Display | 22 | `Palette.Rubric` |
| Texto do Grimório | `GameTheme.PageText` | Serif | 16 | `Palette.Ink` |
| Anotação do Grimório | `GameTheme.PageFaded` | Serif | 13 | `Palette.InkFaded` |
| Botão da Godot | (tipo `Button`) | Serif | 19 | — |

Outros nomes de variação do tema: `InsetPanel` e `PagePanel` (painéis), `PageBar` (barra) e `PageRule` (divisor do Grimório).

## Tamanhos fora dos papéis

Quando nenhum papel serve, o tamanho vem da escala [`FontSize`](tokens.md#fontsize-tamanhos-de-texto-px) (12 a 24 px). Os componentes usam:

| Onde | Tamanho |
|---|---|
| Texto de `GameButton` na altura de toque, e do peso `Text` | `FontSize.Strong` (20) |
| Texto de `GameButton` baixo | `FontSize.Compact` (17) |
| Custo de `GameButton` | `FontSize.Body` (16) |
| Nome de aba / aba compacta | `FontSize.Label` (18) / `FontSize.Body` (16) |

## Exemplo

```csharp
panel.AddChild(Layout.Text(T("auto.title"), GameTheme.Heading));
panel.AddChild(Layout.Text(T("auto.setup_note"), GameTheme.Faded, 420));
panel.AddChild(Layout.Text("1.250", GameTheme.Number));

var name = new Label { Name = "Name", Text = T("hub.battle") };
name.AddThemeFontOverride("font", GameTheme.Serif);
name.AddThemeFontSizeOverride("font_size", FontSize.Strong);
panel.AddChild(name);
```
