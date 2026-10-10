# TouchButton

`UI/Components/Actions/TouchButton.cs` · herda de `Button`

A base de todo botão de toque do jogo. Ninguém usa `TouchButton` direto: os botões do jogo herdam dele (`GameButton`, `SigilButton`, `TileButton`, as abas de `TextTabs`, `SurfaceButton`, `ChatBubble`, `AutoBattleBadge`). O que já vem pronto:

- sem foco de teclado, com o cursor de mão e a caixa `focus` vazia;
- afunda ao apertar e soa o clique de reserva ([Juice](../estilo-e-animacoes/animacoes.md));
- o conteúdo fica por cima, não recebe toque e apaga quando o botão desliga (`Fade.Disabled`);
- mede o conteúdo, porque o `Button` da Godot não mede filhos;
- `Highlight` pulsa até o jogador tocar, e o processo só roda enquanto pulsa.

## Quando usar

Herde de `TouchButton` quando o botão tem um desenho próprio que se repete (um sigilo, um cartão). Para uma superfície tocável qualquer, sem classe nova, use [SurfaceButton](surface-button.md). Para uma ação com texto, use [GameButton](game-button.md).

## Construtor

| Parâmetro | Tipo | Padrão | Descrição |
|---|---|---|---|
| `press` | `float` | `0.95` | A escala ao afundar: menor para botão pequeno (`0.93`), maior para cartão grande (`0.97`). |

O construtor é `protected`: só quem herda chama.

## Propriedades

| Nome | Tipo | Acesso | Descrição |
|---|---|---|---|
| `Highlight` | `bool` | público | Pulsa em verde espiritual até o jogador tocar. Liga o `_Process` só enquanto está ligado. |
| `Disabled` | `bool` | público (da Godot) | Desligado: o conteúdo apaga para `Fade.Disabled` e o botão não afunda. |
| `Content` | `MarginContainer` | protegido | Onde vai o desenho do botão (símbolo, texto). Ocupa o botão inteiro e ignora o mouse. |
| `PulsePhase` | `float` | protegido | A fase do pulso agora, de 0 a 1. |

## Composição

O conteúdo vai em `Content`. As margens dele vêm de `Pad`:

| Método | Descrição |
|---|---|
| `Pad(int sides, int top, int bottom)` | A mesma margem dos dois lados, mais a de cima e a de baixo. |
| `Pad(int left, int right, int top, int bottom)` | Uma margem por lado. |

## Pontos de extensão

| Membro | Quando é chamado | O que fazer |
|---|---|---|
| `Vector2? MinimumFor(Vector2 content)` | Quando o conteúdo muda de tamanho, no `_Ready` e em `Fit()`. | Devolver o tamanho mínimo do botão para o conteúdo medido; `null` deixa o tamanho que o chamador pôs. |
| `void OnHighlightChanged()` | `Highlight` mudou. | Voltar a moldura ao repouso. |
| `void OnPulse(float phase)` | A cada quadro com `Highlight` ligado. | Pintar o chamado com a fase (0 apagado, 1 no auge). |
| `void Fit()` | Chame quando algo que `MinimumFor` leva em conta mudar fora do conteúdo. | — |

Quem sobrescreve `_Ready`, `_Process` ou `_Draw` chama a base. O `_Ready` da base desliga o processo quando não há chamado: se a subclasse precisa de `_Process` sempre, ela religa depois de `base._Ready()`.

## Eventos

Os da Godot: `Pressed`, `Toggled`, `ButtonDown`, `ButtonUp`.

## Estados

| Estado | Como aparece |
|---|---|
| Repouso | A caixa `normal`. |
| Sob o mouse | A caixa `hover` (só no PC; nada pode depender disso). |
| Apertado | A caixa `pressed` e o botão afunda para a escala `press`. |
| Desligado | A caixa `disabled` e o conteúdo a `Fade.Disabled` (0,45). |
| Chamado (`Highlight`) | O que `OnPulse` desenhar. |

## Exemplo: um botão novo

```csharp
private partial class Seal : TouchButton
{
    private readonly Label _label = new() { Name = "Label", MouseFilter = MouseFilterEnum.Ignore };
    private readonly StyleBoxFlat _box = GameTheme.Box(Palette.Inset, Palette.GoldDark, 2, Radius.Button, 0);

    public Seal(string text) : base(0.93f)
    {
        _label.Text = text;
        AddThemeStyleboxOverride("normal", _box);
        AddThemeStyleboxOverride("hover", _box);
        AddThemeStyleboxOverride("pressed", _box);
        Pad(Space.Large, Space.Small, Space.Small);
        Content.AddChild(_label);
    }

    protected override Vector2? MinimumFor(Vector2 content) => new Vector2(content.X, Mathf.Max(content.Y, GameTheme.Touch));

    protected override void OnHighlightChanged() => _box.BorderColor = Palette.GoldDark;

    protected override void OnPulse(float phase) => _box.BorderColor = Palette.GoldDark.Lerp(Palette.Spirit, phase);
}
```

```csharp
var seal = new Seal(T("common.continue")) { Name = "Seal", Highlight = true };
seal.Pressed += () => seal.Highlight = false;
parent.AddChild(seal);
```

## Ver também

- [SurfaceButton](surface-button.md): o mesmo sem classe nova.
- [Animações](../estilo-e-animacoes/animacoes.md): `Juice` e `Pulse`.
