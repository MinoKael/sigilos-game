# Dialog

`UI/Components/Overlays/Dialog.cs` · herda de `ColorRect`

A janela por cima da tela. Ela substitui toda dica e toda pergunta: um painel de couro com o título, o ✕ de fechar, o conteúdo (`Body`, com rolagem quando não cabe) e os botões de ação embaixo.

Tocar fora, o ✕, Esc e o botão Voltar do celular fecham a janela. Fechar não mexe em nada do jogo: quem precisa saber assina `Closed`. Cada janela abre uma camada acima das que já estão abertas, então uma pergunta aberta de dentro de outra janela fica por cima dela.

## Variantes

| Variante | Como se abre | Aparência |
|---|---|---|
| Contextual | `Open(from, title, anchor: elemento)` | Colada no elemento tocado (embaixo dele, ou em cima quando não cabe), com uma seta apontando para ele. A tela escurece pouco. |
| Central | `Open(from, title)` | No centro; a tela escurece mais. |
| Tela cheia | `Full(from, title, name)` | Cobre a tela. O conteúdo ocupa o espaço entre o cabeçalho e a fileira de baixo, num chão escuro (o Chat global). |
| Informação | `Info(anchor, title, text)` | Contextual, só com texto: o lugar do que seria uma dica. |
| Confirmação | `Confirm(from, title, text, confirm, onConfirmed, kind)` | Central: o texto, Cancelar e o botão que confirma, com o peso dado. |

## Fábricas

| Assinatura | Descrição |
|---|---|
| `static Dialog Open(Control from, string title, float width = DefaultWidth, Control? anchor = null, string name = "Dialog")` | Abre por cima da tela de `from`. `name` é o nome do nó (`RuneDialog`). |
| `static Dialog Full(Control from, string title, string name)` | Em tela cheia. |
| `static Dialog Info(Control anchor, string title, string text, float width = 460)` | Só texto, colada em `anchor`. |
| `static Dialog Confirm(Control from, string title, string text, string confirm, Action onConfirmed, ButtonKind kind = Primary)` | A pergunta de confirmação. |
| `static bool CloseTop(Node root)` | Fecha a janela de cima que aceita fechar; `false` se não havia nenhuma. |

`from` é qualquer controle da tela: a janela abre no controle mais alto acima dele (`Layout.Host`), que tem o tema do jogo.

## Propriedades

| Nome | Tipo | Padrão | Descrição |
|---|---|---|---|
| `DefaultWidth` | `const float` | `560` | A largura padrão do painel. |
| `Body` | `VBoxContainer` | — | O conteúdo, entre o título e os botões. |
| `Dismissable` | `bool` | `true` | Falso esconde o ✕ e ignora tocar fora, Esc e Voltar: só os botões de ação fecham (escolha obrigatória). |
| `AtEnd` | `bool` | — | O conteúdo está rolado até o fim, ou cabe sem rolar. |

## Composição

| Ponto | O que vai | Como |
|---|---|---|
| Conteúdo | Qualquer controle. | `dialog.Body.AddChild(...)` |
| Ações | Um [GameButton](game-button.md) na fileira de baixo. | `AddAction(text, onPressed, kind, closes, icon)` |
| Rodapé | Um controle na fileira de baixo, na ordem em que chega (o campo de texto do chat antes do Enviar). | `AddFooter(control)` |
| Cabeçalho | Um nome no meio da linha do título. | `SetCaption(text, color)` |

## Métodos

| Método | Descrição |
|---|---|
| `GameButton AddAction(string text, Action? onPressed, ButtonKind kind = Secondary, bool closes = true, string? icon = null)` | Um botão na fileira de baixo. Com `closes`, a janela fecha antes de chamar a ação. `onPressed` nulo só fecha. |
| `void AddFooter(Control control)` | Um controle na fileira de baixo. |
| `void SetCaption(string text, Color color)` | Um nome no meio do cabeçalho. |
| `void UseBackButton()` | Troca o ✕ pela seta de voltar: para a janela que é um lugar para onde se volta, não uma pergunta que se cancela. |
| `void Reveal(Control control)` | Rola o conteúdo até `control` aparecer, depois de ele se arrumar. |
| `void ClearActions()` | Tira os botões de ação, para quem remonta a janela. |
| `void Close()` | Fecha. |

## Eventos

| Evento | Quando |
|---|---|
| `event Action Closed` | A janela fechou, por qualquer caminho. |

## Exemplos

Uma janela com conteúdo e duas ações:

```csharp
var dialog = Dialog.Open(from, T("auto.title"), name: "AutoDialog");
dialog.Body.AddChild(Layout.Text(T("auto.setup_note"), GameTheme.Faded, 480));
dialog.AddAction(T("common.cancel"), null);
dialog.AddAction(T("auto.start"), save, ButtonKind.Primary, icon: "confirm");
dialog.Closed += () => GD.Print("fechou");
```

Os atalhos de informação e de confirmação:

```csharp
Dialog.Info(capsule, T("auto.victories"), T("auto.monsters_hint"));
Dialog.Confirm(from, T("auto.stop_title"), T("auto.stop_confirm"), T("auto.stop"), stop, ButtonKind.Danger);
```

Uma escolha obrigatória:

```csharp
var dialog = Dialog.Open(from, T("account.conflict_title"));
dialog.Dismissable = false;
dialog.AddAction(T("common.continue"), keep, ButtonKind.Primary);
```

## Cuidados

- A rolagem do conteúdo reserva o lugar da barra. Sem isso, um texto no limite mudava de quebra quando a barra aparecia, e a janela trocava de tamanho sem parar.
- Abrir e fechar soam como reserva (`Sfx.Fallback`), acima do clique que abriu.
