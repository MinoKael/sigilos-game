# Biblioteca de UI — auditoria e plano

Este documento cobre a **Fase 1 (auditoria)** e a **Fase 2 (arquitetura)** da componentização da interface. Também define o que cada fase seguinte entrega.

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
4. **Mesmo namespace.** Tudo continua em `Sigilos.UI.Components` e `Sigilos.UI.Style`. As subpastas só organizam os arquivos, então nenhum `using` muda.

### Estrutura

```
UI/
├── Components/
│   ├── Actions/      TouchButton (novo), GameButton, SigilButton, BackButton, TileButton, ChoiceButton, TextTabs
│   ├── Overlays/     Dialog, FilterDialog, RuneDialog, PauseMenu, TutorialCoach
│   ├── Layout/       Layout, Pill (novo), DragScroll, Backdrop, ArtMask
│   ├── DataDisplay/  CreatureCard, RuneTile, RuneCard, StatTable, StatusChip, SkillRow, CurrencyBar, ...
│   ├── Battle/       BattleArena, UnitView, BossBar, TurnOrderBar, FloatingText, ImpactLayer, ...
│   └── Art/          Doodle, RuneGlyph, Constellation, StarChart, SigilRing, SummonHalo, Starlight, ...
├── Style/
│   ├── Palette.cs    (cores — já existe)
│   ├── Tokens.cs     (novo: Space, Radius, FontSize, Opacity)
│   ├── Tones.cs      (novo: ButtonKind → preenchimento, borda, tinta, contorno)
│   ├── GameTheme.cs  (tema e caixas — já existe)
│   └── Ornament.cs, StarRule.cs, Art.cs
├── Animations/
│   ├── Juice.cs      (movido: apertar)
│   ├── Pulse.cs      (novo: o chamado em verde, tirado de SigilButton/TileButton)
│   └── Motion.cs     (novo: durações e curvas nomeadas)
└── Screens/          (sem mudança de lugar)
docs/ui/
├── PLANO.md                      (este arquivo)
├── componentes/                  "UI Components": uma página por componente genérico
└── estilo-e-animacoes/           "Style and Animations": tokens, tons, estados, animações
```

### Peças novas (todas ligadas a uma duplicação da auditoria)

| Peça | Resolve | Responsabilidade |
|---|---|---|
| `TouchButton : Button` | D1 | Base de todo botão de toque: <ul><li>sem foco, cursor de mão, `focus` vazio;</li><li>`Juice`, e apaga o conteúdo quando desligado (`Tokens.Opacity.Disabled`);</li><li>`Fit()` do conteúdo (`Content`);</li><li>`Highlight` via `Pulse`.</li></ul> |
| `Tones` | D1 | Tons por `ButtonKind` num lugar só. A variação do `TileButton` vira parâmetro, não cópia. |
| `ButtonKind.Text` | D1 | O botão sem moldura que parece texto (`HubScreen` "Time", `LoginScreen` "Forgot"). Já existem dois usos reais. |
| `Pulse` | D2 | O pulso verde reutilizável: dá a fase (0..1) para quem desenha. |
| `SelectionFrame` | D3 | A conta repouso/hover/selecionado/marcado sobre um `StyleBoxFlat`. É usada por `CreatureCard`, `RuneTile` e `Choices.Row`. |
| `Pill` | D4 | Uma cápsula pública e opcionalmente tocável (`Layout.Chip`, cápsula de `CurrencyBar`, `ChatBubble`, `AutoBattleBadge`). |
| `Tokens` | D5 | `Space.{None,Hair,Tight,Small,Medium,Large,Wide,Loose}` = 0,2,4,6,8,10,12,16. Também `Radius`, `FontSize` e `Opacity`, com os valores que já estão em uso, para a aparência não mudar. |
| `Motion` | D6 | `Motion.Quick` (0,12 s, Back/Out, o de `Juice`). As outras durações nomeadas entram quando houver um segundo uso. |

### Exemplos corrigidos (a API que vai existir)

O pedido original trazia três exemplos que não batem com o projeto:

- **`ButtonVariant`:** o tipo é `ButtonKind`, e o nome continua esse para não quebrar 88 chamadas.
- **`Size = ComponentSize.Large`:** esse nome **colide** com `Control.Size` (um `Vector2`) e não compila. A altura já é um parâmetro (`height`), e `GameTheme.Touch` = 52 é o padrão.
- **`SigilId = "fire"`:** não existe. O `SigilButton` recebe o nome do ícone em `Assets/Icons`.

```csharp
// Hoje e depois: construtor + fábrica + encadeamento.
var fight = GameButton.Of(T("prep.fight"), StartBattle, ButtonKind.Primary, "crossed_swords")
    .WithCost("energy", "5")
    .Named("Fight");

// Depois da Fase 3: as propriedades também têm set (o inicializador de objeto funciona).
var confirm = new GameButton(T("common.confirm")) { Kind = ButtonKind.Primary, Disabled = !canPay };
confirm.Pressed += OnConfirm;

// SigilButton: especialização de TouchButton (foco, Juice, Highlight vêm da base).
var close = SigilButton.Of("cancel", dialog.Close, 48).Named("Close");
close.Highlight = true;

// Botão-texto (novo ButtonKind.Text), no lugar do Button Flat feito à mão.
var forgot = GameButton.Of(T("account.forgot"), OpenReset, ButtonKind.Text).Named("Forgot");
```

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
| 4 Estilos e animações | <ul><li>`Tokens`, `Motion`, `Pulse`, `SelectionFrame`, `Pill`.</li><li>`Juice` vai para `Animations/`.</li><li>`SigilButton` e `TileButton` usam `Pulse`.</li><li>`CreatureCard` e `RuneTile` usam `SelectionFrame`.</li></ul> | Build, testes, capturas, contagem de `"separation", <número>` em queda. |
| 5 Migração | <ul><li>`Choices.Row`, a cápsula de `CurrencyBar`, a célula de `AvatarPicker`, `ChatBubble` e `AutoBattleBadge` passam para `TouchButton`/`Pill`.</li><li>"Time" e "Forgot" viram `ButtonKind.Text`.</li><li>Os arquivos vão para as subpastas.</li><li>As separações e fontes das telas passam para os `Tokens`, uma tela por commit.</li></ul> | <ul><li>Build e testes a cada commit.</li><li>`grep "new Button"` em `UI/` = 0 fora de `TouchButton`.</li></ul> |
| 6 Documentação | <ul><li>`docs/ui/componentes/`: uma página para cada um de `TouchButton`, `GameButton`, `SigilButton`, `TileButton`, `ChoiceButton`, `TextTabs`, `Dialog`, `Pill`, `Layout`, `Press`.</li><li>`docs/ui/estilo-e-animacoes/`: `tokens`, `cores`, `tons`, `tipografia`, `estados`, `animacoes`.</li><li>Cada página tem tabelas de propriedades, composição e eventos, e exemplos tirados do código.</li></ul> | Cada exemplo é conferido contra a API: um teste de compilação em `Tests/` com os trechos. |
| 7 Validação | <ul><li>Capturas de todas as telas e janelas tocadas.</li><li>A lista de mudanças visuais intencionais.</li><li>As implementações antigas, apagadas.</li><li>As pendências.</li></ul> | Build, testes, `check_texts.py` sem problema novo. |
