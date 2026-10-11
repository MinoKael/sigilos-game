# SigilButton e BackButton

`UI/Components/Actions/SigilButton.cs`, `BackButton.cs` · herdam de [TouchButton](touch-button.md)

O botão só de símbolo, dentro de um sigilo. Fica para o que todo mundo reconhece sem ler: fechar (✕), voltar (seta), pausar, 2×, os passos + e −. Toda outra ação é um [GameButton](game-button.md), com texto. O único texto aqui é a `Badge`, um número pequeno, ou as `Letters` no lugar do símbolo.

Os estados se leem pela luz, não pela cor de fundo. O redondo grande (a partir de `Bezel`, 56 px) tem a moldura graduada como um astrolábio.

## Variantes: `SigilShape`

| Valor | Uso |
|---|---|
| `Circle` (padrão) | Sigilos soltos: fechar, os nós dos mapas, as habilidades ativas. |
| `Diamond` | O que é diferente do resto da fileira: a habilidade passiva. |
| `Square` | Barras de ferramentas: pausar, velocidade, voltar. |

## Construtor e fábrica

| Assinatura | Descrição |
|---|---|
| `SigilButton(Texture2D? icon, float size = 56, SigilShape shape = Circle)` | O sigilo sem ação; `icon` nulo para usar `Letters` ou `Rune`. Afunda para 0,93. |
| `static SigilButton Of(string icon, Action onPressed, float size = 56, SigilShape shape = Circle)` | O sigilo pronto, pelo nome do ícone em `Assets/Icons`. |
| `BackButton(Action onBack)` | A seta de voltar: quadrada, 52 px. Também responde a `ui_cancel` (Esc, Voltar do celular). |

## Propriedades

| Nome | Tipo | Padrão | Descrição |
|---|---|---|---|
| `Shape` | `SigilShape` | `Circle` | A forma (só leitura; vem do construtor). |
| `Ink` | `Color` | `Palette.Gold` | A cor do símbolo em repouso (a cor do elemento num monstro). |
| `Accent` | `Color?` | `null` | A cor da moldura em repouso, quando não é a de ouro (raridade, elemento, o próximo passo). |
| `Badge` | `string` | `""` | O número na borda de baixo; vazio esconde. |
| `Letters` | `string` | `""` | Letras no lugar do símbolo (`1×`, `EN`, `+3`). |
| `Rune` | `Glyph?` | `null` | Um Glifo no lugar do símbolo, na fonte das runas. |
| `Highlight` | `bool` | `false` | Herdado: pulsa em verde. |
| `ToggleMode` | `bool` | `false` | Da Godot: ligado, o sigilo fica aceso em azul. |

## Métodos

| Método | Descrição |
|---|---|
| `SetIcon(Texture2D? icon)` | Troca o desenho. |
| `SetSymbol(Symbol symbol)` | O símbolo de uma coisa do jogo: um desenho ou um Glifo. |
| `SetLetterSize(int pixels)` | O tamanho das `Letters`, quando o padrão (um terço do sigilo) fica pequeno. |
| `Named(string name)` | Extensão de `Layout`. |

## Eventos

`Pressed` e `Toggled` (da Godot).

## Estados

| Estado | Como aparece |
|---|---|
| Repouso | Moldura de latão (`Palette.GoldDark`, ou `Accent`) e símbolo em `Ink`. |
| Sob o mouse | A moldura vira ouro e a aura acende em azul (só no PC). |
| Apertado | Afunda. |
| Ligado (`ToggleMode`) | Fundo aceso (`States.LitFill`), moldura `States.Selected` e aura azul. |
| Desligado | Fundo `Palette.Inset`, moldura `Palette.Disabled`, conteúdo apagado. |
| Chamado | A aura pulsa em verde. |

## Exemplos

```csharp
var close = SigilButton.Of("cancel", dialog.Close, 48).Named("Close");

var pause = SigilButton.Of("pause", openPause, 56, SigilShape.Square).Named("Pause");
pause.Highlight = true;

var speed = new SigilButton(null, 56, SigilShape.Square) { Name = "Speed", Letters = "2×" };
speed.Badge = "3";
speed.Accent = Palette.Spirit;

var back = new BackButton(goBack) { Name = "Back" };
```

O cabeçalho das telas já põe o `BackButton` ([Layout.Header](layout.md)).
