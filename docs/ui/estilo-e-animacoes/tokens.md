# Tokens

`UI/Style/Tokens.cs` · namespace `Sigilos.UI.Style`

As escalas de espaço, raio, tamanho de fonte e transparência, com nome. Uma tela pede `Space.Large`, não `12`. Assim o jogo inteiro respira no mesmo ritmo, e mudar a escala muda todas as telas juntas.

Os valores são os que já estavam em uso antes dos tokens existirem: trocar o número pelo nome não mudou a aparência de nenhuma tela.

## `Space`: separação e margem (px)

| Nome | Valor | Uso |
|---|---|---|
| `None` | 0 | Peças coladas. |
| `Hair` | 2 | Linhas de texto coladas; ícone e número dentro de uma cápsula pequena. |
| `Tight` | 4 | Símbolo e número de um custo; peças de uma ficha. |
| `Small` | 6 | Abas lado a lado; ícone e valor numa cápsula. |
| `Medium` | 8 | Células de grade, linhas de uma lista, abas em pé. |
| `Regular` | 10 | O padrão entre peças de uma fileira (símbolo e texto de um botão). |
| `Large` | 12 | Blocos de uma coluna (o conteúdo de uma janela, a coluna da tela). |
| `Wide` | 14 | Grupos de um cabeçalho; botões de ação de uma janela. |
| `Loose` | 16 | Partes de uma tela. |
| `Section` | 20 | As colunas grandes de uma tela (o corpo da Campanha, o meio do Santuário). |
| `Spacious` | 24 | Vitrines lado a lado (as ofertas da loja, as raridades do Compêndio). |

## `Radius`: cantos (px)

| Nome | Valor | Uso |
|---|---|---|
| `Small` | 6 | Plaquinhas e selos pequenos. |
| `Medium` | 8 | Caixas rebaixadas, abas, opções de lista. |
| `Button` | 10 | Botões de texto. |
| `Tile` | 14 | Cartões de destino. |

As pílulas têm raio igual à metade da altura ([Pills](estados.md#pílulas)).

## `FontSize`: tamanhos de texto (px)

| Nome | Valor | Uso |
|---|---|---|
| `Small` | 12 | Legendas e valores secundários (`GameTheme.SmallSize`). |
| `Caption` | 13 | O nome embaixo de um retrato ou miniatura; a linha de um selo. |
| `Detail` | 14 | O detalhe embaixo de um nome (aba, cartão, habilidade). |
| `Note` | 15 | A nota de um bloco (a recusa de uma masmorra, o número do correio). |
| `Body` | 16 | O texto corrido (`GameTheme.BodySize`) e o custo de um botão. |
| `Compact` | 17 | O texto de um botão baixo; o número de uma cápsula; o nome de uma habilidade. |
| `Label` | 18 | O nome de uma aba. |
| `Subheading` | 19 | O título de um bloco dentro de uma janela. |
| `Strong` | 20 | O texto de um botão da altura de toque; um nome ou número que se destaca na linha. |
| `Large` | 22 | Um número ou nome em destaque fora do cabeçalho. |
| `Emphasis` | 24 | Os números do resultado de uma luta. |

Os tamanhos de vitrine acima de 24 (o título de um cartão grande, o número de uma invocação) ficam escritos no lugar, um por tela. Antes de pôr um tamanho na mão, veja se um papel do tema serve ([Tipografia](tipografia.md)).

## `Fade`: transparência (0 a 1)

| Nome | Valor | Uso |
|---|---|---|
| `Disabled` | 0,45 | O conteúdo de um botão desligado. |
| `Floating` | 0,8 | O que flutua por cima de qualquer tela (o balão do chat, o aviso da Batalha automática): deixa ver o que está embaixo. |

## Valores fora da escala

Um valor fora da escala é uma exceção e leva um comentário no fim da linha dizendo por quê:

- `// Fora da escala: ...` para um número que não está na escala;
- `// Negativo: ...` para uma separação negativa, que cola duas linhas numa peça só.

Hoje há 14 exceções, todas comentadas (as ondas da Campanha, as portas do Mapa, as colunas do Grimório...).

## Exemplo

```csharp
column.AddThemeConstantOverride("separation", Space.Large);
caption.AddThemeFontSizeOverride("font_size", FontSize.Detail);
var box = GameTheme.Box(Palette.Inset, Palette.GoldDark, 1, Radius.Medium, Space.Medium);
column.AddThemeStyleboxOverride("panel", box);
caption.Modulate = new Color(1, 1, 1, Fade.Disabled);
```
