# GameButton

`UI/Components/Actions/GameButton.cs` · herda de [TouchButton](touch-button.md)

O botão de texto do jogo. Diz o que faz, com um símbolo opcional na frente e, numa segunda linha, o custo ("⚡ 5"). Tem a altura do dedo (`GameTheme.Touch`), afunda ao apertar e apaga quando desligado. Toda ação que possa gerar dúvida usa este botão; o de só símbolo ([SigilButton](sigil-button.md)) fica para fechar, voltar e pausar.

## Variantes: `ButtonKind`

| Valor | Aparência | Quando usar |
|---|---|---|
| `Primary` | Âmbar aceso, letra creme com contorno âmbar escuro. | A ação principal da tela: Lutar, Invocar, Comprar, Iniciar. Uma por tela. |
| `Secondary` (padrão) | Couro violeta com moldura de latão, letra creme, símbolo de ouro. | As outras ações. |
| `Danger` | Carmim, letra creme com contorno escuro. | O que não tem volta: Soltar, Vender, Parar. |
| `Text` | Sem caixa nem contorno; letra ouro, uma linha só. | Um link no meio do texto ("Esqueci a senha"). Use com `height: GameButton.TextHeight`. |

As cores de cada peso vêm de [Tons](../estilo-e-animacoes/tons.md).

## Construtores e fábrica

| Assinatura | Descrição |
|---|---|
| `GameButton(string text, ButtonKind kind = Secondary, string? icon = null, float height = GameTheme.Touch)` | O botão sem ação. Assine `Pressed`. |
| `GameButton()` | Vazio, para o inicializador de objeto. |
| `static GameButton Of(string text, Action onPressed, ButtonKind kind = Secondary, string? icon = null, float height = GameTheme.Touch)` | O botão pronto, com a ação. |

`icon` é o nome de um arquivo de `Assets/Icons`, sem extensão (`"fight"`, `"confirm"`).

## Propriedades

| Nome | Tipo | Padrão | Descrição |
|---|---|---|---|
| `Text` | `string` | `""` | O texto. Esconde o `Button.Text` da Godot: quem desenha é o rótulo de dentro. |
| `Kind` | `ButtonKind` | `Secondary` | O peso. Trocar refaz as caixas, a tinta e a medida. |
| `IconName` | `string?` | `null` | O símbolo na frente do texto; `null` tira. |
| `Disabled` | `bool` | `false` | Da Godot: caixa escura e conteúdo apagado. |
| `TextHeight` | `const float` | `36` | A altura de um botão `Text`. |

A altura (`height`) só se escolhe no construtor.

## Métodos (encadeáveis)

| Método | Descrição |
|---|---|
| `WithCost(string icon, string value)` | A segunda linha: símbolo e número. `value` vazio esconde. |
| `Wide(float width)` | Largura mínima, para botões lado a lado ficarem iguais. |
| `Named(string name)` | Extensão de `Layout`: dá o nome ao nó e devolve o botão. |

## Composição

O conteúdo é do próprio botão: `Content/Row` com `Icon`, `Text` (`Label` e `Cost`). Não ponha filhos no botão; para um conteúdo próprio, use [SurfaceButton](surface-button.md).

## Medida

O botão mede o próprio conteúdo:

- altura: a pedida, ou a do conteúdo se for maior;
- largura: a de `Wide`, ou pelo menos as margens, o símbolo e a palavra mais longa (o rótulo quebra linha);
- no peso `Text`, a largura é a do texto inteiro, sem quebra.

Numa célula de grade mais larga, o botão preenche a célula.

## Eventos

`Pressed` (da Godot). `Of` já assina a ação.

## Estados

| Estado | Como aparece |
|---|---|
| Repouso | O preenchimento e a borda do peso. |
| Sob o mouse | Preenchimento 12 % mais claro, borda 20 % mais clara e a luz de estrela acesa. |
| Apertado | Preenchimento 18 % mais escuro; o botão afunda. |
| Desligado | Caixa `Palette.Disabled`, conteúdo a `Fade.Disabled`. |

O peso `Text` não tem caixa em nenhum estado: só afunda e apaga. `GameButton` não desenha o chamado (`Highlight`); para guiar o jogador, use [TileButton](tile-button.md) ou [SigilButton](sigil-button.md).

## Exemplos

Os quatro pesos:

```csharp
var fight = GameButton.Of(T("common.fight"), startBattle, ButtonKind.Primary, "fight")
    .WithCost("mana", "5")
    .Named("Fight");

var release = new GameButton(T("monsters.release")) { Name = "Release", Kind = ButtonKind.Danger, Disabled = !canRelease };
release.Pressed += releaseSelected;

var forgot = GameButton.Of(T("account.forgot"), openReset, ButtonKind.Text, height: GameButton.TextHeight).Named("Forgot");
```

Dois botões iguais lado a lado (numa grade, nunca num `HFlowContainer`):

```csharp
var actions = Layout.Grid(2).Named("Actions");
actions.AddChild(GameButton.Of(T("common.cancel"), cancel).Wide(200).Named("Cancel"));
actions.AddChild(GameButton.Of(T("common.continue"), next, ButtonKind.Primary, "confirm").Wide(200).Named("Continue"));
page.AddChild(actions);
```

Mudar depois de criar:

```csharp
fight.Text = T("common.continue");
fight.Kind = ButtonKind.Secondary;
fight.IconName = null;
fight.WithCost("mana", "");
```

## Ver também

- [Tons](../estilo-e-animacoes/tons.md): as cores de cada peso.
- [Dialog](dialog.md): `AddAction` cria um `GameButton` na fileira de baixo.
