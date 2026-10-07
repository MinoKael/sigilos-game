---
name: sigilos-performance
description: Padrões de performance do Sigilos (Godot 4.7 C#, UI montada em código). Use antes de criar ou mexer em telas, componentes, listas, shaders, assets ou qualquer coisa que rode por frame.
---

# Performance no Sigilos

Referência para todo trabalho novo. A regra geral: usar o que o projeto já tem, mudar aos poucos e só
otimizar o que foi medido.

## Telas e componentes
- Reaproveitar componente existente (`CreatureCard`, `RuneTile`, `RuneCard`, `Dialog`, `GameButton`,
  `Layout.*`) antes de criar outro. Generalizar um que já existe vale mais que um paralelo.
- Árvore rasa: cada `Container` a mais é mais uma passada de layout. Preferir `Layout.Row/Grid` a
  aninhar `MarginContainer` + `PanelContainer` + `VBox` sem motivo.
- `_Process` só em quem anima de verdade (giro, barra que corre, tempo da luta). Atualização de
  estado vem por evento (`Changed`, `Refreshed`) ou `Timer` de 1 s, nunca checando todo frame.
  Desligar com `SetProcess(false)` quando parado ou invisível.
- Tela escondida que volta (`Swap(screen, reshow)`) não remonta tudo: atualiza o que mudou.

## Listas
- Atualizar no lugar: marcar/desmarcar (`SetSelected`, `SetMarked`) em vez de refazer a grade.
- Lista longa (Monstros, Runas): paginar ou montar aos poucos; reaproveitar os cartões que continuam
  na lista em vez de `Layout.Clear` + criar todos de novo a cada filtro.
- Filtrar e ordenar no Core (`MonsterFilter`, `RuneFilter`, `RuneSort`), sobre dados, antes de criar nós.

## Shaders e materiais
- Shaders simples (o `doodle.gdshader` já faz tinta, boil e aura). Nada de shader novo por efeito.
- Material compartilhado: `Doodle.Shared` guarda por tinta/boil/aura/semente; `SetInk` só duplica
  quando precisa. StyleBox igual entre instâncias também pode ser compartilhado.
- Objetos Godot criados no C# (materiais, StyleBox, Tween, timers) só morrem na coleta completa: o
  `GameRoot.Collect` roda o GC a cada 10 s. Não remover, e não criar esses objetos por frame.

## UI
- Não chamar `GetCombinedMinimumSize` em laço nem a cada frame; medir uma vez, ou ouvir
  `MinimumSizeChanged`.
- Evitar `QueueFree` + remontar a tela inteira para mudar um número: guardar o `Label`/nó e trocar o
  texto. `Layout.Clear` é para conteúdo que muda de verdade (outra aba, outro filtro).
- Uma assinatura por evento: assinar no `_Ready`/construtor, não a cada refresh. Quem assina evento de
  objeto que vive mais (GameRoot, `AutoBattleRun`, `AccountSession`) desassina no `_ExitTree`.
- `ScrollContainer` vertical com `ScrollMode.Reserve` (Auto + texto que quebra oscila e trava).
- Grupos de `GameButton` em `GridContainer` com `Columns`, não `HFlow`.

## Assets
- `Art.Icon/Creature/Element` já cacheiam a textura por caminho: sempre passar por eles.
- Nada de carregar todos os SVGs de uma vez: carregar quando o cartão/tela aparece.
- Não duplicar arquivo de arte para outra cor: tingir com `Doodle` (tinta).

## Geral
- APIs nativas do Godot e abstrações do projeto; nada de framework paralelo (outro sistema de
  layout, outro sistema de eventos, outro gerenciador de áudio ou de config).
- Mudança incremental, com o comportamento antigo preservado.
- Micro-otimização só com medida antes e depois (contagem de objetos/`Performance.Monitor`,
  memória do processo, tempo de frame). Sem medida, legibilidade vence.
- Vazamento: comparar `Performance.Monitor.ObjectCount`/`ObjectOrphanNodeCount` e a memória do
  processo ao longo de muitas lutas/aberturas de tela.
