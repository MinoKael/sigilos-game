# Animações

`UI/Animations/` · namespace `Sigilos.UI.Animations`

As animações de interface compartilhadas. Quem anima um controle pede as durações e as curvas daqui, e o jogo inteiro responde no mesmo ritmo.

Os efeitos próprios de uma tela (as tweens da luta, da invocação, do resultado) e os desenhos animados únicos (`StarChart`, `SummonHalo`) ficam nas telas e nos componentes deles: não são padrões repetidos.

## `Motion`

| Membro | Valor | Uso |
|---|---|---|
| `Quick` | `0.12` s | O retorno de um toque: o botão afunda e volta. |
| `Spring` | `Tween.TransitionType.Back` | A curva do retorno: passa um pouco do ponto e volta (o repique). |
| `Settle` | `Tween.EaseType.Out` | Começa rápido e chega devagar. |
| `Tween SpringTo(Node node, Tween? previous, NodePath property, Variant to, double duration = Quick)` | — | Anima `property` até `to` com o repique, matando a animação anterior. Guarde o retorno e passe-o como `previous` na próxima chamada. |

## `Juice`

O peso de um botão: afunda para a escala dada ao descer o dedo e volta com o repique ao soltar. Sob o mouse não cresce: quem mostra o foco é a luz (a aura do sigilo, a moldura acesa). O centro da escala acompanha o tamanho, e o botão desligado não afunda.

O clique soa como reserva (`Sfx.Fallback`): fica calado quando a ação tem som próprio. O botão normal soa `ui.button_click`; o de ligar e desligar soa `ui.toggle_on` ou `ui.toggle_off`.

| Membro | Descrição |
|---|---|
| `static void Attach(BaseButton button, float press = 0.95f)` | Liga o efeito. Todo [TouchButton](../componentes/touch-button.md) já chama; use direto só num botão que não herda dele. |

## `Pulse`

O pulso do chamado (`TouchButton.Highlight`): uma fase que vai de 0 a 1 e volta, a cada ~2 s. Quem desenha avança o relógio no `_Process` e lê a fase. O `Pulse` não liga o processo sozinho: quem usa liga só enquanto o chamado está aceso (regra de performance: nada de `_Process` parado).

| Membro | Descrição |
|---|---|
| `const float Speed` (3,2) | A velocidade do seno, em radianos por segundo. |
| `float Phase` | De 0 (apagado) a 1 (no auge). |
| `void Advance(double delta)` | Avança o relógio. |

## Exemplos

```csharp
Juice.Attach(button, 0.93f);
var tween = Motion.SpringTo(banner, previous, "scale", Vector2.One * 1.1f);
```

Um desenho próprio que respira no ritmo do chamado dos botões:

```csharp
private partial class Beacon : Control
{
    private readonly Pulse _pulse = new();

    public override void _Process(double delta)
    {
        _pulse.Advance(delta);
        Modulate = Colors.White.Lerp(Palette.Spirit, _pulse.Phase);
    }
}
```
