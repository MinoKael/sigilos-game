# TileButton

`UI/Components/Actions/TileButton.cs` · herda de [TouchButton](touch-button.md)

O cartão grande de destino: o símbolo em cima (ou à esquerda, deitado), o nome escrito e uma linha de detalhe ("Fase 12 de 20"). É o jeito de entrar numa parte do jogo: a barra de baixo do Santuário, os cartões de Batalha e Invocar, as Masmorras. Pulsa em verde com `Highlight` para guiar quem está começando.

## Variantes

| Variante | Como se pede | Uso |
|---|---|---|
| Em pé | `horizontal: false` (padrão) | Símbolo em cima, nome embaixo: a barra do Santuário (`Nav`). |
| Deitado | `horizontal: true` | Símbolo à esquerda, nome e detalhe à direita: os cartões grandes. |
| Peso | `kind` (`ButtonKind`) | Os tons de [GameButton](game-button.md), um pouco mais escuros: o âmbar escurece 12 % e a madeira vira `Palette.Panel`, porque o cartão é grande e fica atrás do texto. |

## Construtor e fábrica

| Assinatura | Descrição |
|---|---|
| `TileButton(string title, string detail, Texture2D? texture, Vector2 size, ButtonKind kind = Secondary, bool horizontal = false, float iconSize = 0)` | O cartão. `detail` vazio não mostra a segunda linha. `iconSize` 0 calcula pelo tamanho (62 % da altura deitado, 46 % em pé). Afunda para 0,97. |
| `static TileButton Nav(string title, string icon, float width = 128)` | O botão da barra de baixo do Santuário: 86 px de altura, em pé, sem detalhe. O nó leva o nome do ícone. |

## Propriedades

| Nome | Tipo | Padrão | Descrição |
|---|---|---|---|
| `Highlight` | `bool` | `false` | A moldura e a sombra respiram em verde. |
| `Disabled` | `bool` | `false` | Caixa rebaixada e conteúdo apagado. |

O tamanho do título é calculado pelo tamanho do cartão (30, 23, 19 ou 16 px). Título e detalhe só se escolhem no construtor.

## Eventos

`Pressed` (da Godot).

## Estados

| Estado | Como aparece |
|---|---|
| Repouso | Preenchimento do peso e borda escura. |
| Sob o mouse | 8 % mais claro, borda de ouro. |
| Apertado | 15 % mais escuro; afunda pouco (0,97). |
| Desligado | `Palette.Inset` com borda `Palette.Disabled`. |
| Chamado | Borda e sombra verdes, pulsando. |

## Exemplo

```csharp
var battle = new TileButton(T("hub.battle"), T("hub.battle_detail"), Art.Icon("fight"), new Vector2(380, 170), ButtonKind.Secondary, horizontal: true) { Name = "Battle" };
battle.Pressed += openBattle;
battle.Highlight = true;
hub.AddChild(battle);

var summon = TileButton.Nav(T("destination.Summon"), "summon").Named("Summon");
summon.Pressed += openSummon;
nav.AddChild(summon);
```
