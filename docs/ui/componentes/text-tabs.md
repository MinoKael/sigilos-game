# TextTabs

`UI/Components/Actions/TextTabs.cs` · herda de `BoxContainer`

Abas escritas. Cada aba diz o nome e, se quiser, um detalhe embaixo ("5/5", "19"), com um símbolo opcional na frente. A aberta fica clara, com moldura de ouro; as outras ficam na pedra escura. As abas podem ser deitadas (no alto de um painel) ou em pé (a régua da ficha de Monstros).

`TextTabs` só troca a aba. Quem a usa mostra o conteúdo certo no evento `Changed`.

## Variantes

| Variante | Como se pede | Uso |
|---|---|---|
| Deitadas | `vertical: false` (padrão) | Abas no alto de um painel; separação `Space.Small`. |
| Em pé | `vertical: true` | Uma régua do lado; separação `Space.Medium`. |
| Compactas | `compact: true` | Letra `FontSize.Body` em vez de `FontSize.Label`, e menos folga: abas que dividem pouco espaço. |

## Construtor

| Parâmetro | Tipo | Padrão | Descrição |
|---|---|---|---|
| `vertical` | `bool` | `false` | Em pé. |
| `height` | `float` | `52` | A altura de cada aba. |
| `compact` | `bool` | `false` | Letra menor e menos folga. |

## Propriedades, métodos e eventos

| Membro | Descrição |
|---|---|
| `Button Add(string text, string detail = "", string? icon = null, bool enabled = true)` | Uma aba nova, no fim. O nó se chama `Tab0`, `Tab1`...; quem cria pode dar o nome do conteúdo. Nasce aberta a aba de índice `Selected` (a primeira, se ninguém chamou `Select`). |
| `void Select(int index)` | Abre a aba sem avisar (`Changed` não dispara): para quando a tela já sabe. |
| `int Selected` | A aba aberta agora (só leitura). |
| `event Action<int> Changed` | O jogador abriu outra aba; recebe o índice. |
| `Alignment` | Da Godot: `Center` centraliza as abas na fileira. |

Cada aba é um botão de ligar (`ToggleMode`) de um mesmo `ButtonGroup`, com a largura do próprio conteúdo. Trocar de aba soa `ui.tab_switch`.

## Estados de uma aba

| Estado | Como aparece |
|---|---|
| Fechada | `Palette.Inset` com borda de latão 1 px; letra `Palette.TextFaded`. |
| Aberta | `Palette.PanelLight` com borda de ouro 2 px; letra de ouro claro. |
| Sob o mouse | Fundo um pouco mais claro e borda de ouro (só no PC). |
| Desligada (`enabled: false`) | Fundo mais escuro, borda `Palette.Disabled`, conteúdo a `Fade.Disabled`; não abre. |

## Exemplo

```csharp
var tabs = new TextTabs(height: 48, compact: true) { Name = "Tabs" };
tabs.Add(T("destination.Campaign"), "12/20", "fight").Name = "Campaign";
tabs.Add(T("destination.Dungeons"), enabled: false).Name = "Dungeons";
tabs.Changed += show;
tabs.Select(0);
page.AddChild(tabs);
```
