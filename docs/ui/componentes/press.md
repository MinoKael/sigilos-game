# Press

`UI/Components/Actions/Press.cs` · classe

Toque curto e toque longo num controle. O jogo não tem dica (tooltip): segurar o dedo é o jeito de pedir o resumo de alguma coisa (o monstro, a runa, a habilidade).

| Gesto | Evento |
|---|---|
| Soltar antes de `HoldSeconds` sem arrastar o dedo. | `Tapped` |
| Segurar parado por `HoldSeconds` (ou o clique direito, no PC). Depois disso, soltar não conta como toque. | `Held` |
| Arrastar mais que 14 px. | Nenhum: é o dedo rolando a lista. |

Por isso o controle deixa o evento seguir para cima (`MouseFilter.Pass`), e a rolagem de quem o contém continua funcionando.

## Membros

| Membro | Descrição |
|---|---|
| `const double HoldSeconds` (0,45) | Quanto segurar para valer como toque longo. |
| `event Action Tapped` | O toque curto. |
| `event Action Held` | O toque longo. |
| `void Feed(Control owner, InputEvent @event)` | Repassa a entrada do controle. Quem tem `_GuiInput` chama daqui. |
| `static Press On(Control control, Action? tapped, Action held)` | Liga os dois toques a um controle qualquer pelo sinal `GuiInput`, para quem não sobrescreve `_GuiInput`. Põe `MouseFilter.Pass` se o controle ignorava o mouse. |
| `static void OnButton(BaseButton button, Action tapped, Action held)` | Para um botão de verdade: o toque curto é o `Pressed` dele, e segurar chama `held` sem disparar o toque ao soltar. |

## Exemplos

Um controle próprio, que repassa a entrada:

```csharp
private partial class Card : PanelContainer
{
    private readonly Press _press = new();

    public Card(Action choose, Action summary)
    {
        MouseFilter = MouseFilterEnum.Pass;
        _press.Tapped += choose;
        _press.Held += summary;
    }

    public override void _GuiInput(InputEvent @event) => _press.Feed(this, @event);
}
```

Um controle qualquer, e um botão:

```csharp
Press.On(portrait, select, describe);
Press.OnButton(skill, select, describe);
```
