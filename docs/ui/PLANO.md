# Biblioteca de UI — auditoria e plano

Este documento cobre a **Fase 1 (auditoria)** e a **Fase 2 (arquitetura)** da componentização da interface. Também define o que cada fase seguinte entrega e registra o que cada uma entregou de fato ([O que foi feito](#o-que-foi-feito)).

A documentação das peças está em [README.md](README.md).

Tudo aqui foi tirado do código atual da branch `revamp-tematica`. Os nomes de tipo, de método e de arquivo são os reais.

---

## Fase 1 — Auditoria

### Ponto de partida

- A interface é montada **em código**. Nenhuma cena (`.tscn`) referencia scripts de `UI/`, então dá para mover arquivos sem mexer em cenas.
- `UI/` tem 18,5 mil linhas:
  - 49 componentes em `UI/Components`;
  - 31 telas e janelas em `UI/Screens`;
  - 5 arquivos de estilo em `UI/Style` (`Palette`, `GameTheme`, `Ornament`, `StarRule`, `Art`).
- **Cores:** as cores já estão centralizadas em `Palette`, e as telas quase não usam cor solta.
- **Papéis de texto:** os papéis vêm do tema (`GameTheme.Title`, `Heading`, `Faded`, `Number`...).

### O que já está certo (as referências)

| Peça | Usos | O que faz bem |
|---|---|---|
| `Dialog` | 83 | É uma fábrica única para toda janela. As variações (`Open`, `Info`, `Confirm`, `Full`) são métodos estáticos. Tem um ponto de composição claro (`Body`, `AddAction`, `AddFooter`), o evento `Closed`, empilhamento por camada e fechamento por fora/✕/Esc. |
| `GameButton` | 88 | Pesos semânticos (`ButtonKind`), fábrica `Of`, encadeamento (`WithCost`, `Wide`, `Named`). Mede o próprio conteúdo (`Fit`). |
| `SigilButton` | 14 (+`BackButton`) | Estados lidos pela luz (hover, ligado, desligado, `Highlight`). Formas (`SigilShape`). |
| `Layout` | — | Fábricas de layout (`Grid`, `Flow`, `Scroll`, `Chip`, `Labeled`, `Section`) e o ciclo de vida seguro (`Discard`, `Reveal`, `KeepScroll`). |
| `Press` / `Juice` | 21 / 3 | Toque curto e longo; o afundar do botão e o som de reserva. |

### Duplicações encontradas

**D1 — A família de botões reimplementa a mesma base.** Onze lugares herdam ou criam `Button` e repetem a infraestrutura:

| Onde | Foco/cursor/`focus` vazio | Caixas por estado à mão | Apaga desligado | Mede conteúdo (`Fit`) | `Juice` (afundar + som) |
|---|---|---|---|---|---|
| `GameButton` | sim | 5 | 0,45 | sim | sim |
| `SigilButton` | sim | desenha | tinta | — | sim |
| `TileButton` | sim | 5 | 0,45 | não | sim |
| `TextTabs.TabButton` | sim | 5 | **0,40** | sim | **não** |
| `Choices.Row` | sim | 4 | não | não | **não** |
| `CurrencyBar` (cápsula) | sim | 4 | não | não | **não** |
| `AvatarPicker` (célula) | sim | 3 | não | não | **não** |
| `ChatBubble` | sim | todos iguais | não | não | não |
| `AutoBattleBadge` | sim | todos iguais | não | não | não |
| `HubScreen` "Time" | sim | 3 vazias | não | sim (à mão) | **não** |
| `LoginScreen` "Forgot" | sim | tema | não | não | **não** |

Consequências:

- **Abas e opções:** não afundam nem soam o clique. As abas soam só `ui.tab_switch`.
- **Desligado:** cada botão apaga com uma transparência diferente (0,40 nas abas, 0,45 nos outros).
- **Tons:** a tabela de tons por `ButtonKind` está repetida em `GameButton.Tones` e no construtor de `TileButton`. A cópia de `TileButton` difere: o âmbar é escurecido em 0,12 e a madeira usa `Panel`.

**D2 — O pulso de `Highlight` está copiado.** `SigilButton` e `TileButton` têm o mesmo pulso (`_time`, `Mathf.Sin(_time * 3.2f)`, `Palette.Spirit`, `SetProcess`). `StarChart` e `SummonHalo` têm outros senos, que são efeitos próprios e não entram aqui.

**D3 — A moldura de seleção dos cartões está copiada.** `CreatureCard` e `RuneTile` fazem a mesma conta de estado em `_box`:

- selecionado: borda `Arcane`, sombra `Arcane` a 0,4;
- sob o mouse: borda clareada;
- em repouso: a cor de raridade ou moldura.

`UnitView` tem uma variante (alvo, ativo, chefe), que é comportamento de luta e fica de fora. O "aceso em azul" (`Inset.Lerp(Arcane, 0.14f)`) aparece em 3 lugares (`SigilButton`, `Choices.Row` e `RuneTile`).

**D4 — As pílulas têm três implementações.**

- `Layout.Capsule` (fechado, privado).
- A cápsula de `CurrencyBar`: a mesma caixa, mas tocável.
- `ChatBubble` e `AutoBattleBadge`: a mesma pílula `Box(Inset 0,94, borda, 2, altura/2)`.

**D5 — Os números de layout estão soltos.** As separações são escritas como número solto, e só 9 valores aparecem:

| Separação (px) | Ocorrências |
|---|---|
| 10 | 28 |
| 8 | 26 |
| 6 | 16 |
| 4 | 14 |
| 12 | 10 |
| 0 | 7 |
| 14 | 6 |
| 2 | 5 |
| 16 | 4 |

- **Tamanhos de fonte:** são escritos à mão em 12+ arquivos, fora do tema.
- **Raios e espessuras de borda:** também são números soltos em ~30 chamadas a `GameTheme.Box`.

**D6 — As animações não têm um lugar próprio.**

- **Peças que já existem:** `Juice` (apertar) é a única animação de interface compartilhada. O pulso (D2) é copiado.
- **O que falta:** não há duração nem curva nomeada. `Dialog` abre e fecha sem transição, o que não é bug.
- **Efeitos de luta e invocação:** as tweens de `BattleArena`, `SummonScreen` e `BattleResultPanel` são efeitos próprios dessas telas, não padrões repetidos.

### O que não é duplicação (fica como está)

- **Desenhos próprios:** `Constellation`, `StarChart`, `SigilRing`, `SummonHalo`, `StagePath`, `EnergyRing` e `BattleArena` são desenhos únicos.
- **`UnitView`, `BossBar` e `TurnOrderBar`:** são ligados à luta. Usam o estilo comum, mas o comportamento é deles.
- **Fichas (`RuneCard`, `MonsterSummary`, `StatTable`):** são componentes do jogo (especializados) que já se compõem com `Layout`.
- **Janelas das telas (`FusionDialog`, `CopiesDialog`...):** já são composições de `Dialog`, que é o caminho certo.

---

## Fase 2 — Arquitetura

### Princípios

1. **Composição por padrão.** A herança vale só quando o componente **é** um botão, como `GameButton`, `TileButton` e as abas.
2. **API idiomática do projeto.** Mantém o que já funciona:
   - construtor com o essencial;
   - fábrica `Of(...)` com a ação;
   - encadeamento (`WithCost`, `Wide`, `Named`);
   - eventos C# (`event Action<T>`).
   As propriedades configuráveis depois de criar (o "props" do Vuetify) passam a ser **propriedades com `set`**. Assim, inicializador de objeto também funciona.
3. **Nada de abstração para peça única.** Só vira genérico o que tem duas ou mais implementações hoje.
4. **Mesmo namespace.** Os componentes continuam em `Sigilos.UI.Components` e o estilo em `Sigilos.UI.Style`. As subpastas só organizam os arquivos. A exceção é a pasta nova `UI/Animations/`, que tem o namespace próprio `Sigilos.UI.Animations` (`Juice` saiu dos componentes).

### Estrutura

```
UI/
├── Components/
│   ├── Actions/      TouchButton, SurfaceButton, GameButton, SigilButton, BackButton, TileButton, ChoiceButton, TextTabs, Press
│   ├── Overlays/     Dialog, FilterDialog, RuneDialog, PauseMenu, TutorialCoach, AvatarPicker, MonsterPicker, MonsterSummary, ChatBubble, AutoBattleBadge
│   ├── Layout/       Layout, DragScroll, TileGrid
│   ├── DataDisplay/  CreatureCard, RuneTile, RuneCard, StatTable, StatusChip, SkillRow, CurrencyBar, MonsterSearch, TeamStrip, StagePath, RichText
│   ├── Battle/       BattleArena, UnitView, BossBar, TurnOrderBar, FloatingText, ImpactLayer, AutoBattleWatch
│   └── Art/          Doodle, RuneGlyph, Constellation, StarChart, SigilRing, SummonHalo, Starlight, ArtMask, Backdrop, EnergyRing
├── Style/
│   ├── Palette.cs        cores
│   ├── Tokens.cs         Space, Radius, FontSize, Fade
│   ├── Tones.cs          ButtonKind → preenchimento, borda, tinta, contorno
│   ├── States.cs         cores de escolhido, marcado, sob o mouse
│   ├── SelectionFrame.cs moldura de seleção dos cartões
│   ├── Pills.cs          caixas de cápsula
│   ├── GameTheme.cs      tema, papéis de texto e caixas
│   └── Ornament.cs, StarRule.cs, Art.cs
├── Animations/       (namespace Sigilos.UI.Animations)
│   ├── Juice.cs      apertar
│   ├── Pulse.cs      o chamado em verde
│   └── Motion.cs     durações e curvas nomeadas
├── Examples/
│   └── UiExamples.cs os exemplos da documentação, compilados com o jogo
└── Screens/          (sem mudança de lugar)
docs/ui/
├── PLANO.md                      (este arquivo)
├── README.md                     índice
├── componentes/                  "UI Components": uma página por componente genérico
└── estilo-e-animacoes/           "Style and Animations": tokens, cores, tons, tipografia, estados, animações
```

### Peças novas (todas ligadas a uma duplicação da auditoria)

| Peça | Resolve | Responsabilidade |
|---|---|---|
| `TouchButton : Button` | D1 | Base de todo botão de toque: <ul><li>sem foco, cursor de mão, `focus` vazio;</li><li>`Juice`, e apaga o conteúdo quando desligado (`Fade.Disabled`);</li><li>`Fit()` do conteúdo (`Content`);</li><li>`Highlight` via `Pulse`.</li></ul> |
| `Tones` | D1 | Tons por `ButtonKind` num lugar só. A variação do `TileButton` vira parâmetro, não cópia. |
| `ButtonKind.Text` | D1 | O botão sem moldura que parece texto (`HubScreen` "Time", `LoginScreen` "Forgot"). Já existem dois usos reais. |
| `Pulse` | D2 | O pulso verde reutilizável: dá a fase (0..1) para quem desenha. |
| `States` e `SelectionFrame` | D3 | As cores de escolhido, marcado e sob o mouse, e a conta repouso/hover/selecionado/marcado sobre um `StyleBoxFlat`. `SelectionFrame` é usada por `CreatureCard` e `RuneTile`; `Choices.Row` e `SigilButton` usam as cores de `States`. |
| `Pills` e `SurfaceButton` | D4 | `Pills` faz as caixas de cápsula (`Carved`, `Lit`, `Floating`). A cápsula tocável é um `SurfaceButton` com essas caixas. Não existe um nó `Pill`: a cápsula fechada (`Layout.Chip`) e a tocável têm comportamento diferente, e o que elas repetiam era só a caixa. |
| `Tokens` | D5 | <ul><li>`Space`: `None` 0, `Hair` 2, `Tight` 4, `Small` 6, `Medium` 8, `Regular` 10, `Large` 12, `Wide` 14, `Loose` 16, `Section` 20, `Spacious` 24.</li><li>`Radius`: `Small` 6, `Medium` 8, `Button` 10, `Tile` 14.</li><li>`FontSize`: de `Small` 12 a `Emphasis` 24.</li><li>`Fade`: `Disabled` 0,45, `Floating` 0,8.</li></ul>Os valores são os que já estavam em uso, para a aparência não mudar. |
| `Motion` | D6 | `Motion.Quick` (0,12 s), `Spring` (Back), `Settle` (Out) e `SpringTo`, o de `Juice`. As outras durações nomeadas entram quando houver um segundo uso. |
| `SurfaceButton` | D1 | O botão genérico sobre `TouchButton`: quem cria dá as caixas (`Boxes`) e o conteúdo (`Body`). Tirou os `Button` crus das telas e dos componentes. |

### Exemplos corrigidos (a API que vai existir)

O pedido original trazia três exemplos que não batem com o projeto:

- **`ButtonVariant`:** o tipo é `ButtonKind`, e o nome continua esse para não quebrar 88 chamadas.
- **`Size = ComponentSize.Large`:** esse nome **colide** com `Control.Size` (um `Vector2`) e não compila. A altura já é um parâmetro (`height`), e `GameTheme.Touch` = 52 é o padrão.
- **`SigilId = "fire"`:** não existe. O `SigilButton` recebe o nome do ícone em `Assets/Icons`.

O `GameButton` como ficou (o mesmo exemplo está em [componentes/game-button.md](componentes/game-button.md) e compila em `UI/Examples/UiExamples.cs`):

```csharp
var fight = GameButton.Of(T("common.fight"), startBattle, ButtonKind.Primary, "fight")
    .WithCost("mana", "5")
    .Named("Fight");

var release = new GameButton(T("monsters.release")) { Name = "Release", Kind = ButtonKind.Danger, Disabled = !canRelease };
release.Pressed += releaseSelected;

var forgot = GameButton.Of(T("account.forgot"), openReset, ButtonKind.Text, height: GameButton.TextHeight).Named("Forgot");
```

Mudanças de nome em relação ao rascunho: o símbolo de `GameButton` é a propriedade `IconName` (o `Button.Icon` da Godot é uma textura e ficaria escondido), e o botão-texto pede `height: GameButton.TextHeight` (36 px), porque sem madeira não precisa da altura de toque inteira.

### Riscos e cuidados

- **Aparência:** os tokens recebem os valores **atuais**. O que mudar de propósito fica listado na Fase 7, com captura de antes e depois:
  - a transparência de desligado das abas (0,40 → 0,45);
  - o afundar nas abas e opções.
- **Ciclo de vida:**
  - `Pulse` liga `SetProcess` só enquanto o chamado está ativo, como hoje (regra de performance: nada de `_Process` parado).
  - `Juice` já mata a tween anterior antes de criar outra.
- **Godot e C#:**
  - `Button` não mede filhos, então `TouchButton.Fit` herda a solução de `GameButton`.
  - Botões lado a lado vão em `Layout.Grid`, nunca em `HFlow` (o rótulo quebrado encolhe a zero).

---

## Entregáveis por fase

| Fase | Entrega | Verificação |
|---|---|---|
| 1 Auditoria | Este documento, seção Fase 1. | — |
| 2 Arquitetura | Este documento, seção Fase 2. | Aprovação do plano. |
| 3 Base | <ul><li>`TouchButton`, `Tones`, `ButtonKind.Text`.</li><li>`GameButton`, `SigilButton`, `TileButton` e `TextTabs.TabButton` passam a herdar de `TouchButton`.</li><li>As cópias de foco, caixas, `Fit` e desligado são apagadas.</li></ul> | <ul><li>Build com 0 erros; 235 testes.</li><li>Capturas antes/depois de Santuário, Preparação, Runas e Monstros.</li></ul> |
| 4 Estilos e animações | <ul><li>`Tokens`, `Motion`, `Pulse`, `States`, `SelectionFrame`.</li><li>`Juice` vai para `Animations/`.</li><li>`SigilButton` e `TileButton` usam `Pulse`.</li><li>`CreatureCard` e `RuneTile` usam `SelectionFrame`.</li></ul> | Build, testes, capturas, contagem de `"separation", <número>` em queda. |
| 5 Migração | <ul><li>`Choices.Row`, a cápsula de `CurrencyBar`, a célula de `AvatarPicker`, `ChatBubble` e `AutoBattleBadge` passam para `TouchButton`/`SurfaceButton` com `Pills`.</li><li>"Time" e "Forgot" viram `ButtonKind.Text`.</li><li>Os arquivos vão para as subpastas.</li><li>As separações e fontes das telas passam para os `Tokens`, uma tela por commit.</li></ul> | <ul><li>Build e testes a cada commit.</li><li>`grep "new Button"` em `UI/` = 0 fora de `TouchButton`.</li></ul> |
| 6 Documentação | <ul><li>`docs/ui/componentes/`: uma página para cada um de `TouchButton`, `GameButton`, `SigilButton`, `TileButton`, `ChoiceButton`, `TextTabs`, `Dialog`, `Layout`, `Press`, mais `SurfaceButton`.</li><li>`docs/ui/estilo-e-animacoes/`: `tokens`, `cores`, `tons`, `tipografia`, `estados`, `animacoes`.</li><li>Cada página tem tabelas de propriedades, composição e eventos, e exemplos tirados do código.</li></ul> | Cada exemplo é conferido contra a API: os trechos estão em `UI/Examples/UiExamples.cs`, que o build compila, e `Tests/UiDocTests.cs` confere que a documentação mostra só o que está lá. |
| 7 Validação | <ul><li>Capturas de todas as telas e janelas tocadas.</li><li>A lista de mudanças visuais intencionais.</li><li>As implementações antigas, apagadas.</li><li>As pendências.</li></ul> | Build, testes, `check_texts.py` sem problema novo. |

---

## O que foi feito

| Fase | Commit | Entregue |
|---|---|---|
| 1–3 | `05bd0ec` | <ul><li>`TouchButton`, `Tones` e `ButtonKind.Text`.</li><li>`GameButton`, `SigilButton`, `TileButton` e as abas de `TextTabs` herdam de `TouchButton`; as cópias de foco, caixas, medida e desligado saíram.</li><li>As abas e as opções afundam e soam o clique; todo desligado apaga a 0,45.</li></ul> |
| 4 | `5844eab` | <ul><li>`Tokens`, `Motion`, `Pulse`, `States` e `SelectionFrame`; `Juice` em `UI/Animations/`.</li><li>`SigilButton` e `TileButton` pulsam com `Pulse`; `CreatureCard` e `RuneTile` usam `SelectionFrame`.</li></ul> |
| 5a | `e2b48ca` | <ul><li>`SurfaceButton` e `Pills`.</li><li>`Choices.Row`, a cápsula de `CurrencyBar`, a célula de `AvatarPicker`, a hora do Santuário, `ChatBubble` e `AutoBattleBadge` passam para a base; "Esqueci a senha" vira `ButtonKind.Text`.</li><li>Nenhum `new Button` fora de `TouchButton`.</li></ul> |
| 5b | `0aa91d3` | Os componentes em subpastas por papel (`Actions`, `Overlays`, `Layout`, `DataDisplay`, `Battle`, `Art`). |
| 5c | `b10a4f6` | As separações das telas e dos componentes pelos tokens `Space`. |
| 5 (pendências) | `8d9f01b`, `e3ff9a5` | <ul><li>O botão-texto não quebra linha.</li><li>As escalas `FontSize` e `Space` completas (`Section`, `Spacious`; de `Caption` a `Emphasis`).</li><li>As 14 exceções fora da escala com o motivo em comentário.</li></ul> |
| 6 | `7237a5c` | A documentação em `docs/ui/` e a conferência dos exemplos (`UiExamples.cs`, `UiDocTests`). |
| 7 | (o commit da validação) | <ul><li>Capturas antes e depois de todas as telas e janelas tocadas.</li><li>Os dois últimos `Button` crus (a conta e o "Sem conexão" do Santuário) passam para `SurfaceButton`.</li><li>Quatro raios soltos passam para `Radius`.</li><li>A [validação](#fase-7--validação) abaixo.</li></ul> |

### Desvios do plano

- **Tokens nas telas num commit só**, e não um por tela. A troca é de número por nome com o mesmo valor, então não muda a aparência; as capturas antes e depois de cada fase confirmam.
- **Sem nó `Pill`.** Ver a tabela de peças novas: o que se repetia era a caixa (`Pills`), e a cápsula tocável é um `SurfaceButton`.
- **`SurfaceButton` é peça nova**, fora do plano original. Ele apareceu na migração: as linhas, as cápsulas e as células precisavam de um botão de caixa livre, e uma subclasse para cada uma seria abstração para peça única.
- **`Opacity` virou `Fade`**, para não confundir com a transparência de `Modulate` da Godot.
- **Os exemplos não compilam em `Tests/`.** O projeto de testes não compila código da Godot. Os exemplos compilam no build do jogo (`UI/Examples/UiExamples.cs`), e o teste confere o texto da documentação contra esse arquivo.

---

## Fase 7 — Validação

### Capturas

As telas e janelas foram capturadas no commit de partida (`5e6e2ab`, antes da biblioteca) e no fim, com o mesmo save, e comparadas pixel a pixel (diferença acima de 0,15 num canal).

| Captura | Resultado |
|---|---|
| Mapa, Runas, Loja, Compêndio, a ordem das Runas | Iguais. |
| Santuário, a explicação dos Pergaminhos, a Canalização, o Grimório do Invocador, a escolha de retrato, o balão do chat, Amigos | Só o relógio e a Essência acumulada, que andam entre as duas capturas. |
| Campanha, Masmorras, Exploração, Invocação, Monstros, a ordem e os filtros dos Monstros, Preparação, Grimório, Batalha | Só a arte animada (os retratos, o sigilo que gira, a luta). |
| Entrada (login) | "Esqueci a senha" (abaixo). |

### Mudanças visuais intencionais

1. **"Esqueci a senha"** é um botão-texto (`ButtonKind.Text`): 36 px de altura, texto em `FontSize.Strong` (20). O texto fica um pouco maior e o cartão de entrada, 3 px mais baixo.
2. **As abas desligadas** apagam a 0,45, como todo botão desligado (antes 0,40). A diferença é pequena demais para o limiar da comparação.
3. **Afundar e clique** ao tocar: as abas, as opções das listas de escolha, as cápsulas de moeda e o correio, a hora da Canalização e o retrato da conta. É movimento, então não aparece numa captura parada.

### Implementações antigas apagadas

- As cópias de foco, caixas, medida, desligado e pulso dentro de cada botão (fases 3 e 4).
- Os `Button` crus das telas e dos componentes. Na fase 7 saíram os dois últimos, no Santuário: o retrato da conta (agora `SurfaceButton` com `Hug`) e o "Sem conexão" (`SurfaceButton` plano).
- Os números soltos de separação, fonte e raio que têm token. Na fase 7 saíram quatro raios (`Radius.Button` e `Radius.Medium`).

Conferido por busca no código:
- nenhuma classe herda de `Button` fora de `TouchButton`;
- `Juice.Attach` só é chamado em `TouchButton`;
- nenhum `Opacity`, nenhuma cópia do seno do pulso.

### Pendências

- **"Sem conexão"** não aparece nas capturas: só surge sem servidor. Conferir o visual na próxima vez que o jogo abrir sem rede.
- **Números fora das escalas**, de propósito:
  - Os raios de círculo (metade do tamanho: 18, 22, 32, 34) e dois raios fora da escala (4 na faixa do Grimório do Invocador, 12 na célula do Armazém).
  - As cinco fontes grandes (28 a 76) dos números de destaque.
  - As fontes têm o motivo em comentário. Os raios de círculo se explicam pelo tamanho; os dois fora da escala ainda não têm comentário.
- **A ferramenta de capturas** (abrir uma tela, tocar nós pelo nome, gravar e comparar) foi usada fora do repositório. Ela pode virar `Tools/` se as capturas antes e depois forem repetir.
- **`check_texts.py`** acusa 346 problemas, todos de antes (nomes sobrando em `en.json`). Nenhum é novo.
