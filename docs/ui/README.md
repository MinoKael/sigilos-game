# Biblioteca de UI do Sigilos

A interface do jogo é montada em código C# (Godot 4.7, sem cenas `.tscn` para a UI). Esta pasta documenta as peças reutilizáveis: o que cada uma faz, as propriedades, a composição, os eventos, as variantes e os estados, com exemplos reais.

- Os componentes ficam no namespace `Sigilos.UI.Components`.
- O estilo fica em `Sigilos.UI.Style`.
- As animações ficam em `Sigilos.UI.Animations`.

O plano, a auditoria e o que cada fase entregou estão em [PLANO.md](PLANO.md).

## Componentes de UI

| Página | Peça | Para quê |
|---|---|---|
| [TouchButton](componentes/touch-button.md) | `TouchButton` | A base de todo botão: foco, cursor, afundar, apagar desligado, medir, chamado. |
| [SurfaceButton](componentes/surface-button.md) | `SurfaceButton` | Botão genérico: você dá as caixas e o conteúdo (linhas de lista, cápsulas, células). |
| [GameButton](componentes/game-button.md) | `GameButton`, `ButtonKind` | O botão de texto do jogo, com símbolo, custo e peso. |
| [SigilButton](componentes/sigil-button.md) | `SigilButton`, `BackButton`, `SigilShape` | O botão só de símbolo (fechar, voltar, pausar). |
| [TileButton](componentes/tile-button.md) | `TileButton` | O cartão grande de destino (Batalha, a barra do Santuário). |
| [ChoiceButton](componentes/choice-button.md) | `ChoiceButton`, `Choices`, `Choice` | Campo de escolha e a lista de opções numa janela. |
| [TextTabs](componentes/text-tabs.md) | `TextTabs` | Abas escritas, deitadas ou em pé. |
| [Dialog](componentes/dialog.md) | `Dialog` | Toda janela: contextual, central, tela cheia, informação e confirmação. |
| [Layout](componentes/layout.md) | `Layout` | Página, cabeçalho, grade, fileira, rolagem, painel, cápsulas, limpeza. |
| [Press](componentes/press.md) | `Press` | Toque curto e toque longo em qualquer controle. |

## Estilo e animações

| Página | Peça | Para quê |
|---|---|---|
| [Tokens](estilo-e-animacoes/tokens.md) | `Space`, `Radius`, `FontSize`, `Fade` | As escalas de espaço, raio, fonte e transparência. |
| [Cores](estilo-e-animacoes/cores.md) | `Palette` | As cores do jogo, por papel. |
| [Tons](estilo-e-animacoes/tons.md) | `Tones` | As cores de cada `ButtonKind`. |
| [Tipografia](estilo-e-animacoes/tipografia.md) | `GameTheme`, `FontSize` | Os papéis de texto do tema e as fontes. |
| [Estados](estilo-e-animacoes/estados.md) | `States`, `SelectionFrame`, `Pills` | Escolhido, marcado, sob o mouse; as pílulas. |
| [Animações](estilo-e-animacoes/animacoes.md) | `Juice`, `Motion`, `Pulse` | O afundar do toque, as curvas, o pulso do chamado. |

## Regras que valem para todas as peças

- **Dedo primeiro.** Tudo o que se toca tem pelo menos `GameTheme.Touch` (52 px) de altura, exceto o botão-texto (`GameButton.TextHeight`, 36 px). Não existe dica (tooltip): o que precisa de explicação abre um `Dialog`.
- **Nome de nó.** Todo nó criado em código recebe o nome do papel que tem no pai (`Named("Fight")` ou `Name = "Fight"`), em PascalCase.
- **Botões lado a lado vão numa grade** (`Layout.Grid`), nunca num `HFlowContainer`: o rótulo que quebra linha encolhe a zero.
- **Rolagem vertical** com barra reservada (`Layout.Scroll` já faz).
- **Nada de número solto.** Separação, raio, fonte e transparência vêm dos [tokens](estilo-e-animacoes/tokens.md). Um valor fora da escala leva um comentário `// Fora da escala: ...` explicando por quê.

## Os exemplos compilam

Cada bloco `csharp` destas páginas está, linha por linha, em [UI/Examples/UiExamples.cs](../../UI/Examples/UiExamples.cs). O build do jogo compila esse arquivo, e o teste `UiDocTests` (em `Tests/`) confere que nenhum exemplo da documentação está fora dele. Para mudar um exemplo, mude os dois juntos.
