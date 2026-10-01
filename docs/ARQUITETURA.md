# Arquitetura

## Camadas

```
Data/*.json ──texto──▶ GameEntry ──▶ Core   (regras, sem Godot)
                          │
                          └──▶ UI    (telas; lê Core, nunca GameEntry)
```

- **Core/** não conhece o Godot. Recebe texto (não caminhos), `Random` e `DateTime` como parâmetro.
  Por isso roda igual no jogo e no console de testes, e o combate é determinístico por semente.
- **UI/** recebe o que mostra no construtor e avisa por evento C# (`FightRequested`, `AwakenRequested`...).
  Nenhuma tela muda o `PlayerState` nem salva. Nenhuma tela tem texto escrito no código: tudo vem de
  `Data/texts/pt-BR.json` por chave (`Locale.T("runes.upgrade_to", ...)`); `UI/Texts.cs` só monta nomes
  por enum e as descrições geradas das regras. A interface é para o toque: botões de ação com texto
  (`GameButton`), símbolo sozinho só no ✕ e na seta de voltar, nenhuma dica de mouse, e toda
  explicação numa janela (`Dialog`) colada no elemento tocado.
- **GameEntry/** é a raiz de composição: o `GameRoot` assina os eventos das telas, chama as regras do
  Core, salva e troca de tela. É o único lugar que junta tudo.

Dentro do Core:

| Namespace | Papel | Depende de |
| --- | --- | --- |
| `Core.Content` | Definições lidas de `Data/`, o vocabulário (Glifo, conjunto, raridade, efeitos), o modelo de atributos (`StatModel`) e o `GameDatabase` | — |
| `Core.Runes` | Runa, conjuntos, pedras, tabelas de Summoners War, sorteio, busca e bônus | Content |
| `Core.Progression` | Crescimento por estrelas e nível, experiência (tabela de Summoners War), Evolução, Despertar, Fusão (nível de habilidade), ficha de atributos, ociosidade, Campanha, Masmorras, Mana, nível da conta, Loja | Content, Runes, Player |
| `Core.Battle` | Combate: Ímpeto, recarga das habilidades, a ordem de um golpe, automático. O que cada efeito, status, Passiva e conjunto faz são estratégias nas subpastas `Effects`, `Statuses`, `Passives` e `Sets` | Content, Runes, Progression |
| `Core.Player` | O save (`PlayerState`), coleção e Baú (`Roster`), equipes por conteúdo (`Teams`), inventário de runas e a ponte para a batalha | Content, Runes, Battle |
| `Core.Summoning` | Gacha | Content, Player, Progression |

`Core.Battle` não conhece `PlayerState`: o simulador monta um `BattleTeam` à mão. A tela de Monstros e
a batalha calculam atributos pelo mesmo `SummonStats`, então o número que o jogador vê é o que luta.

## Onde mexer

| Quero... | Mexa em |
| --- | --- |
| Nova família ou nova variante | `docs/summon_family_builder.html` (abra no Chrome ou no Edge e use "Abrir pasta do projeto"): ele calcula os atributos e grava `Data/summons/<familia>_family.json`, um arquivo por família com até 5 variantes. Família nova pede também dois SVG em `Assets/Creatures/` (normal e desperto; ver "Novo símbolo"). Nenhum código, a não ser que a Passiva dela ainda não exista (ver "Nova Passiva") |
| Habilidades de uma variante | `"skills"` da variante, no construtor ou direto no arquivo da família: a primeira é a básica (sem `cooldown`), as outras ativas têm `cooldown`, no máximo uma é `passive`; `levels` são as melhorias por cópia fundida (`Damage`, `Recovery`, `EffectRate`, `Cooldown`); `awakenedEffects` troca os efeitos ao despertar |
| Habilidade fora do padrão (golpes diferentes entre si) | só dados: os efeitos acontecem na ordem da lista, então "3 golpes comuns e um quarto que ignora a Defesa" são dois efeitos `Damage` (`{"hits": 3}` e `{"ignoreDefense": 1}`). Ver "Combate: regras como estratégias" |
| O que o Despertar dá | `"awakening"` da variante: `stat` (bônus de atributo) ou `skill` (habilidade nova, que só existe desperta); os atributos maiores são os `"awakened_stats"`, que o construtor calcula |
| Atributos de uma invocação ou de um chefe | ninguém escreve à mão: saem do modelo (`Data/stat_model.json`) pelas estrelas naturais e pelo papel. Para puxar uma variante para um lado, o viés dela ou o da família, no construtor. Ver "Atributos: o modelo de BVP" |
| Recalibrar o jogo inteiro (orçamentos, perfis de papel, pesos, viés de elemento) | aba Modelo do construtor, "Salvar no projeto" e "Recalcular todas": regrava `Data/stat_model.json`, todas as famílias e os chefes. Depois, `dotnet run --project Tests` (confere) e `-- --simulate` (o efeito no balanceamento) |
| Inimigos de fase ou andar | ondas em `Data/stages.json` e `Data/dungeons.json`: `"summon"` é uma variante de invocação, `"enemy"` um chefe de `Data/enemies.json`; `stars` e `level` de cada fase e andar |
| Reforço dos inimigos-invocação | `BattleFactory.FoeScale` (por estrelas naturais) e `scale` do andar |
| Novo efeito de status | o nome em `Core/Content/StatusKind.cs`, a estratégia em `Core/Battle/Statuses/` (uma das prontas, `StatChange` e `DamageOverTime`, ou uma classe nova que herda de `StatusBehavior`), a linha em `StatusBehaviors`, os textos `effect.Nome.name` e `.info` em `Data/texts` e o símbolo em `Assets/Effects/` |
| Nova Passiva | o nome em `Core/Content/PassiveKind.cs`, uma classe em `Core/Battle/Passives/` que herda de `UnitBehavior` e sobrescreve o momento dela, a linha em `PassiveBehaviors`, o texto `passive.Nome` em `Data/texts` e o símbolo em `Art.Skill`. Com a pasta do projeto aberta, o construtor já oferece o nome novo |
| Novo tipo de efeito de habilidade | o nome em `Core/Content/EffectKind.cs`, uma classe em `Core/Battle/Effects/` que herda de `SkillEffect`, a linha em `SkillEffects`, a descrição em `UI/Texts.cs` e o texto em `Data/texts` |
| Novo momento da luta (algo que nenhuma regra consegue ouvir hoje) | um método vazio em `Core/Battle/UnitBehavior.cs` e a chamada no ponto em que ele acontece (`BattleSession` para turno e onda, `EffectResolver` para habilidade e golpe) |
| Mexer no código do combate sem mudar a regra | `dotnet run --project Tests -- --digest > antes.txt`, mexa, rode de novo e compare: mais de mil lutas com semente fixa, uma linha por luta |
| Ritmo da luta na tela e da Batalha automática | `UI/BattlePace.cs`; quantas lutas em `AutoBattle.RepeatRuns` |
| Campo de batalha (onde ficam as unidades, o oval, a corrida até o alvo, o respingo) | `UI/Components/BattleArena.cs` (os arcos de cada lado em graus, `Slots`, a corrida e a volta), `ImpactLayer` (o respingo), o ritmo em `UI/BattlePace.cs` (`Approach`, `Hit`, `Return`); o cartão da unidade em `UnitView`; os cantos e a pausa em `BattleScreen` e `PauseMenu` (a pausa é `SceneTree.Paused`, e a espera entre eventos usa timer que para junto) |
| Nova Masmorra ou andar | `Data/dungeons.json` (andares, conjuntos, drop, `mana`, `firstClearGold`, `scale` de força); regras em `Core/Progression/Dungeons.cs` |
| Custo em Mana de fase | `Data/stages.json` (`mana`) |
| Mana máxima e recarga | `Core/Progression/Mana.cs`; a recarga entra pela canalização em `Core/Progression/Idle.cs` |
| Nível da conta e Ouro por nível | `Core/Progression/Account.cs` |
| Loja | ofertas em `Data/shop.json`; regra em `Core/Progression/Shop.cs` |
| Qualquer texto da interface | `Data/texts/pt-BR.json` (a base) e a mesma chave em `Data/texts/en.json`, depois `py Tools/texts/check_texts.py`. Chave nova não pode ter o nome de um grupo que já existe (`filter.order` apagaria `filter.order.*`) |
| Novo símbolo (ícone) | o SVG do acervo de game-icons.net copiado para a pasta dele em `Assets/` (ou baixado do Commons: `py Tools/art/fetch_commons_assets.py --chrome ...`), a linha de crédito em `Tools/art/commons_assets.csv`, `py Tools/art/fetch_commons_assets.py` (refaz `Assets/CREDITOS.md`), `py Tools/art/render_png.py` (os PNG) e `Art.Icon("nome")` |
| Símbolo de um efeito de batalha | `Assets/Effects/<efeito>.svg` (o nome do `StatusKind` em minúsculas) + render; no texto rico, `Texts.Term(StatusKind)` põe o símbolo na frente |
| Símbolo de uma habilidade | `Art.Skill` (o efeito que ela aplica, senão o Glifo do que ela faz) |
| Tamanho da runa em miniatura | `RuneTile.Side` (quadrada; cada lugar passa a escala) |
| Como uma runa aparece (a ficha) | `UI/Components/RuneCard.cs`: a mesma em todo lugar (tela de Runas, vitória, Batalha automática e o toque longo de todo `RuneTile`); por cima da tela, `RuneDialog.Show` (uma `Dialog` com a ficha, e quem abre põe os botões) |
| Resultado da luta (vitória e derrota) | `UI/Screens/BattleResultPanel.cs` (a barra de experiência de cada monstro em `ResultMonster.StepsOf`); o melhor tempo de cada fase e andar em `Core/Progression/Records.cs`, guardado em `PlayerState.BestTimes` |
| Novo botão | `GameButton.Of(T("texto"), ação, ButtonKind.Primary, "ícone")`: texto sempre, o símbolo acompanha; `Primary` (laranja) é a ação da tela, `Secondary` a comum, `Danger` (vermelho) vender e liberar; `WithCost` põe o preço numa segunda linha. Destino grande (tela inicial, tela Batalha, barra de baixo) é `TileButton`. `SigilButton` (só símbolo) fica para o universal: fechar, voltar, pausa, velocidade, habilidade de batalha |
| Explicar algo (o que antes era dica) | `Dialog.Info(elemento, título, texto)`: abre colada no elemento, com a seta apontando para ele. Janela com botões: `Dialog.Open(...)` + `AddAction`; pergunta: `Dialog.Confirm` |
| Toque e toque longo | `Press.On(controle, toque, segurar)` (ou `Press.Feed` dentro do `_GuiInput`, `Press.OnButton` num botão): segurar é 0,45 s parado; arrastar cancela, para a rolagem funcionar no dedo. Botão direito do mouse conta como segurar |
| Resumo de um monstro (toque longo) | `UI/Components/MonsterSummary.cs`: `Open(de, variante, cópia)` para o que o jogador tem ou vê no catálogo, `Open(de, BattleUnit)` para quem está lutando (Vida, efeitos e recargas de agora). `CreatureCard`, `TeamStrip`, `UnitView`, `TurnOrderBar`, os inimigos da fase e o resultado da luta já abrem |
| Escolher entre opções | `ChoiceButton` (mostra "Campo: valor" e abre a lista numa `Dialog`) ou `Choices.Open` direto; escolher um monstro é o `MonsterPicker` |
| Abas | `TextTabs` (escritas, com detalhe opcional embaixo, em pé ou deitadas); o Compêndio e o Grimório usam `Layout.Tab` num `TabContainer` |
| Grade de runas ou de monstros | `TileGrid`: cabem quantas colunas a largura deixar, e a sobra vira espaço entre elas (a grade vai de borda a borda em qualquer tela) |
| Bloquear (runa ou monstro) | `Rune.Locked` e `OwnedSummon.Locked`, salvos com o resto; quem recusa é a regra: `RuneInventory.Sell` (e `SellAll`) não vende runa bloqueada, `Fusion.CanFuse` não aceita material bloqueado e `Fusion.Release` não libera. Na tela, o cadeado é do `RuneTile` e do `CreatureCard` (aparece em todo lugar), e o botão Bloquear/Desbloquear fica nas fichas de Runas, de Monstros e na runa e no monstro que caem na Batalha automática; a runa que cai na vitória tem Bloquear ao lado de Vender e Guardar |
| Gasto grande de uma vez (subir até o nível máximo, melhorar a runa até o próximo marco) | pergunta antes, com o gasto e aonde chega: `Dialog.Confirm` (o da runa é `RuneDialog.ConfirmUpgrade`) |
| A constelação da Canalização (tela inicial) | `UI/Components/Constellation.cs`: ocupa o painel; o sigilo do centro (`SetCenter`), o anel do tempo acumulado, os orbes desenhados em volta (`Places`, `Radii`) e o que vai logo embaixo do centro (`Attach`) |
| Versão do jogo (canto de baixo à esquerda do Santuário) | `application/config/version` no `project.godot`; numa build de depuração aparece "· depuração" |
| Destinos da barra de baixo e da tela Batalha | `UI/Screens/Destination.cs` (símbolo e nome) e `GameRoot.Go` (qual tela abre e para onde volta) |
| Voltar (seta, Esc e o Voltar do celular) | `BackButton` e `Layout.Header`; o Voltar do Android vira a ação `ui_cancel` no `GameRoot`. Quem fecha ou volta ouve `ui_cancel` em `_UnhandledInput`, que corre do último nó para o primeiro: a janela de cima fecha antes da tela voltar |
| Idiomas oferecidos na Configuração | um arquivo por idioma em `Data/texts`; `ContentLoader.Languages()` lista, `PlayerState.Language` guarda a escolha |
| Traduzir | copie `Data/texts/pt-BR.json` com outro nome, troque os textos, ponha no grupo `names` o nome de cada coisa dos dados (pelo nome em português; o `check_texts.py` diz quais faltam) e rode com `-- --language=nome` |
| Nome de invocação, habilidade, fase, Masmorra, chefe ou oferta da Loja | o `"name"` (ou `"base_name"`) no arquivo de `Data/`, em português, e a tradução no grupo `names` do `en.json` (a chave é o nome em português: renomear pede renomear lá também) |
| Balancear números do combate | `Core/Battle/BattleRules.cs`, depois `dotnet run --project Tests -- --simulate` |
| Atributos por papel e estrelas naturais | `Data/stat_model.json` (orçamento de BVP e perfis de papel), pelo construtor |
| Atributos por estrelas e nível de agora | faixas por estrela em `Core/Progression/Growth.cs`: a fração do 6★ nível 40 |
| Crítico, Dano crítico, Resistência e Precisão de base | `"base_stats"` em `Data/stat_model.json` |
| Experiência por nível e valor da Essência | `Core/Progression/Leveling.cs` (tabela de Summoners War por estrela, `ExperiencePerEssence`); experiência por fase e andar em `Data/stages.json` e `Data/dungeons.json` |
| Evolução (custo por estrela) | `Core/Progression/Evolution.cs` |
| Despertar (custo por estrelas naturais, bônus de atributo) | `Core/Progression/Awakening.cs`; o orçamento desperto em `Data/stat_model.json` |
| Fusão de cópias (sobe uma habilidade sorteada), Fragmentos ao liberar | `Core/Progression/Fusion.cs` |
| Runas: tabelas, custos, espaços | `Core/Runes/RuneRules.cs` (uma tabela por atributo, de 1★ a 6★) |
| Conjuntos de runas | `Core/Runes/RuneSets.cs`; o efeito em combate em `Core/Battle/Sets/` (uma estratégia por `RuneSetEffect`) |
| Pedras (Afiar, Gema) | `Core/Runes/RuneForge.cs` (regras), `Core/Runes/RuneRules.cs` (faixas), grau por andar em `Data/dungeons.json` |
| Glifo de um conjunto ou de um atributo | `Core/Runes/RuneSets.cs` (conjunto → Glifo); atributo → Glifo em `UI/Texts.cs` (`GlyphOf`); a letra que desenha cada Glifo na fonte das runas em `Texts.Rune`; na tela, `RuneGlyph` |
| Fontes | `Assets/Fonts`: a SFC Wezards é a do jogo (com reserva do sistema para ★ × ⟳), a Kehdrai a das runas; carregadas em `GameTheme` |
| Batalha automática (quantas lutas) | o padrão e o máximo em `Core/Battle/AutoBattle.cs` (`RepeatRuns`, `MaxRuns`); a escolha é o `AutoBattleSetup` |
| Batalha automática por trás | `GameEntry/AutoBattleRunner.cs` (o nó que corre as lutas, mesmo com a tela trocada), `UI/AutoBattleRun.cs` (o estado que a interface lê, com o evento `Changed`), `UI/Components/AutoBattleBadge.cs` (o selo no alto) e `UI/Screens/AutoBattleDialog.cs` (a janela; as ações dela são o `AutoBattleActions` que o `GameRoot` monta) |
| Coleção, Baú, equipes | `Core/Player/Roster.cs`, `Core/Player/Teams.cs`, `PlayerState.CollectionCapacity`/`TeamSize` |
| Vagas do inventário de runas | `RuneInventory.Capacity` (runas equipadas, inclusive em monstro do Baú, não contam) |
| Taxas do gacha | `Core/Summoning/SummonRates.cs` |
| Ociosidade | `Core/Progression/Idle.cs` |
| Compêndio (regras) e Grimório (catálogo) | textos em `Data/texts` (`compendium.*`, `grimoire.*`); cartões em `UI/Screens/CompendiumScreen.cs` e `GrimoireScreen.cs` |
| Cores, fontes, molduras, rolagem | `UI/Style/Palette.cs` (cores), `UI/Style/GameTheme.cs` (estilos por tipo de controle), `UI/Style/Ornament.cs` (texturas geradas: couro com moldura, gema da rolagem, sigilo de marcar), `Assets/Shaders/backdrop.gdshader` (o fundo) |
| Nova tela | `UI/Screens/` + o `Show...` correspondente em `GameEntry/GameRoot.cs` e, se for destino de navegação, um valor em `Destination` |
| Ver contornos, nomes, valores e origem dos nós com o jogo rodando | Ctrl+F1 a Ctrl+F4: o addon `addons/visual_debugger` (autoload `VisualDebug`, não conhece o jogo), opções em `visual_debug/*` nas Configurações do Projeto |
| Nome dos nós (o caminho na árvore e o rótulo do modo de depuração) | todo nó tem nome: quem cria dá o do papel dele no pai, em PascalCase (`Name = "Body"`, `Layout.Row(8).Named("Actions")`), e o que se repete leva número ou id (`Slot3`, `Rune17`, `Ally2`). A tela ganha o nome da classe em `GameRoot.Swap`, as janelas por cima o nome passado a `Dialog.Open` (`RuneDialog`, `MonsterSummary`, `AutoBattleDialog`, `ConfigDialog`...), e os componentes nomeiam as próprias peças. Nome padrão: `SigilButton.Of`, `Layout.Chip` e `Doodle.Icon` usam o do símbolo (`level_max` → `LevelMax`), célula de grade usa `Layout.NextCell` (`R2C3`), aba usa o nome passado a `Layout.Tab`. Para remontar, `Layout.Clear` ou `Layout.Discard`, nunca `QueueFree` direto: o nó novo entraria com o nome ainda ocupado e viraria `@Nome@123` |

`GameDatabase.Validate()` confere referências e faixas dos dados; o teste `DataTests` falha se algo
estiver quebrado (inclusive básica com recarga, ativa sem recarga, duas Passivas ou atributos que não
são os do modelo), e o jogo mostra os problemas no console ao abrir.

## Combate: regras como estratégias

A luta não sabe o que é Veneno, Contragolpe nem dano: ela só conhece **momentos**, e avisa quem estiver
ouvindo. Quem ouve são estratégias (padrão Strategy), uma classe pequena por regra.

```
Core/Battle/
  BattleSession.cs     o fluxo: de quem é a vez, ondas, vitória e derrota
  EffectResolver.cs    a ordem de uma habilidade e de um golpe + as ações (ferir, curar, pôr efeito...)
  UnitBehavior.cs      a base das estratégias: um método vazio por momento da luta
  UnitRule.cs          uma regra em vigor numa unidade: a estratégia + os números desta aplicação
  Cast.cs, Strike.cs   a habilidade em curso e um golpe dela
  Effects/             o que cada tipo de efeito de habilidade faz   (SkillEffect, tabela SkillEffects)
  Statuses/            o que cada efeito de status faz               (StatusBehavior, tabela StatusBehaviors)
  Passives/            o que cada Passiva faz                        (tabela PassiveBehaviors)
  Sets/                o que cada conjunto de runas faz em combate   (SetBehaviors)
```

Uma unidade carrega as regras em vigor nela (`BattleUnit.Rules()`): os efeitos de status, na ordem em
que chegaram, depois os conjuntos de runas e a Passiva. Cada regra é um `UnitRule` (o `StatusEffect` é
um `UnitRule` com duração) que aponta para a estratégia dela. A estratégia não guarda estado: o valor,
quem pôs e os turnos que faltam vêm na regra que todo método recebe.

Os momentos (métodos de `UnitBehavior`; a estratégia sobrescreve só os dela):

| Momento | Quando | Quem usa hoje |
| --- | --- | --- |
| `Modify` | alguém pergunta um atributo de agora | Ataque+, Ataque−, Defesa+, Quebra de Defesa, Velocidade+, Passiva dos Diabretes |
| `SkipsTurn`, `HidesOwner`, `BlocksHarmful`, `ForcedTarget` | perguntas de sim ou não | Atordoamento, Oculto, Imunidade, Provocação |
| `OnWaveStart` | começou uma onda | conjuntos Tenacidade e Baluarte, Passiva dos Bandidos |
| `OnTurnStart` | começou o turno do dono | Queimadura, Veneno, Bomba, Passivas dos Trolls, Magos, Paladinos e Pixies |
| `OnAttack` → `OnDefend` | antes do dano de um golpe | Cegueira (erra), Presságio (crítico), Sifão e Passiva dos Vampiros (dreno) → Passiva dos Pássaros (esquiva), Égide (anula) |
| `DamageDealt`, `DamageTaken`, `Absorb` | a conta do dano | Passivas dos Goblins e Lobos; Maldição e Passiva dos Limos; Escudo |
| `AfterHurt`, `AfterHit` | o golpe acertou e o alvo ficou de pé | Perdição e Passivas dos Druidas e das Gárgulas; Tormento e Passivas dos Dragões e dos Corvos |
| `AfterEffect`, `AfterSkill`, `AfterStruck` | fim de um efeito e da habilidade | Presságio (some); Oblívio; Contragolpe |
| `AfterAction` | o dono acabou de agir | Frenesi |
| `OnDeath` | o dono caiu | Passivas dos Cavaleiros e da Fênix |

A ordem de um golpe está em `EffectResolver.Land`: quem ataca pode errar, quem apanha pode esquivar ou
anular, o crítico, o dano, o escudo, o dreno, a queda e, se o alvo ficou de pé, o que o golpe dispara
nos dois. O alvo pode devolver dano (a Passiva dos Druidas): se quem ataca cai com isso, os golpes e os
efeitos que faltavam da habilidade não acontecem.

**Um status novo.** "Congelado: perde o turno e recebe 20% a mais de dano":

```csharp
// Core/Battle/Statuses/FreezeStatus.cs
internal sealed class FreezeStatus : StatusBehavior
{
	public override bool Harmful => true;
	public override bool SkipsTurn => true;
	public override double DamageTaken(UnitRule rule) => 1.2;
}
```

Mais `Freeze` em `StatusKind`, `[StatusKind.Freeze] = new FreezeStatus()` em `StatusBehaviors`, os
textos e o símbolo. Nada em `BattleSession`, `EffectResolver`, `BattleUnit` ou `DamageFormula`. O teste
`SkillTests.EveryKindHasItsStrategy` acusa o nome que ficou sem estratégia.

Os casos comuns nem pedem classe: `new StatChange(Stat.Attack, -0.3)` é um "Ataque −30%" e
`new DamageOverTime(0.04, 3)` é um dano por turno de 4% que acumula até 3.

**Uma habilidade fora do padrão.** Uma habilidade é uma lista de efeitos, e cada um age sozinho, na
ordem. Quatro golpes, os três primeiros comuns e o último ignorando a Defesa:

```json
"effects": [
  {"kind": "Damage", "target": "Target", "power": 1.2, "hits": 3},
  {"kind": "Damage", "target": "Target", "power": 1.2, "ignoreDefense": 1}
]
```

Dois golpes no alvo e o terceiro em todos é a mesma coisa, com `"target": "AllEnemies"` no segundo
efeito. `onKill` num efeito faz ele só acontecer se a habilidade derrubou alguém. O que não cabe nos
campos de `EffectDefinition` vira um tipo novo de efeito: uma classe em `Effects/` que monta os golpes
como quiser (`new Strike(cast, alvo, força) { IgnoreDefense = ..., Drain = ... }`) e os entrega com
`cast.Resolver.Land`.

## Atributos: o modelo de BVP

Todo monstro das mesmas estrelas naturais gasta o mesmo orçamento de BVP, e o papel decide onde:
`BVP = Vida / 15 + Ataque + Defesa + Velocidade × 3`. A explicação inteira está em
`docs/summon_family_stat_formula_spec.md`.

| Arquivo | O que tem |
| --- | --- |
| `Data/stat_model.json` | os parâmetros: pesos, orçamento por estrelas (de base e desperto), perfis de papel (fatias e Velocidade), viés por elemento, Velocidade do Despertar e os quatro atributos de base |
| `Data/summons/<familia>_family.json` | a família e as variantes, com `"stats"` e `"awakened_stats"` **já calculados** no 6★ nível 40; o viés da família (`family_bias`) e o da variante (`bias`, `awakening_spd`) só aparecem quando não são neutros |
| `Data/enemies.json` | os chefes, com `"stats"` do papel e das estrelas deles |
| `docs/summon_family_builder.html` | o construtor: edita família e modelo, calcula, confere e grava |
| `Core/Content/StatModel.cs` | a mesma conta no jogo, só para conferir |

O jogo não calcula atributo na hora: lê o número pronto e o encolhe para as estrelas e o nível do
monstro (`Growth`). A conta existe em dois lugares, no construtor (JavaScript) e em `StatModel.cs`, e
um confere o outro: `GameDatabase.Validate` refaz a conta de cada variante e de cada chefe e acusa o
que não bate. Mudou um parâmetro e esqueceu de recalcular, ou mexeu num número à mão? O teste de dados
falha e diz qual variante. Para consertar, "Recalcular todas" no construtor.

Com a pasta do projeto aberta, o construtor também lê os nomes válidos de efeito, status, Passiva e
papel direto dos enums de `Core/Content`: um status novo no código aparece lá sem mexer no HTML. Sem a
pasta (outro navegador), ele usa o modelo e as listas que traz embutidos e trabalha com Importar e
Baixar.

O que o contrato do projeto tem de diferente da especificação:

| Da especificação | No projeto |
| --- | --- |
| Perfis de papel (§6) e Velocidade por papel (§7) | uma tabela só, `role_profiles`: por estrelas e papel, `{"hp", "atk", "def", "spd"}` |
| Viés de elemento (§12) | `element_bias` no modelo, igual para o jogo inteiro |
| Viés de família (§11) | `family_bias` no arquivo da família |
| Viés e Velocidade manuais da variante (§7, §8) | `bias` (`{"hp", "atk", "def", "spd"}`) e `awakening_spd` na variante |
| Campos a mais na família | `name` (o nome no Grimório), `image` e `awakened_image`: vieram do antigo `families.json` |
| `validation` (§22) | não vai para o arquivo: o construtor mostra ao vivo e o jogo confere |
| Arredondamento (§25) | a Vida sai em múltiplos do peso dela (15) e a sobra do arredondamento vai para a Defesa: o BVP fecha o orçamento exato |
| Atributo do Despertar (§13) | fora do orçamento: o jogo soma por cima de `awakened_stats` (`Awakening.Bonus`) |

## Decisões (SOLID sem abstração prematura)

- **Uma responsabilidade por classe.** `BattleSession` cuida do fluxo de turnos; `EffectResolver` do
  que cada efeito faz; `Targeting` de quem é atingido; `DamageFormula` do número; `AutoPilot` das
  decisões automáticas; `RuneForge` cria e transforma runas; `RuneInventory` cobra (Pó e pedras) e
  guarda. As telas só desenham.
- **Aberto para conteúdo, sem código novo.** Habilidades são listas de efeitos em JSON; uma família
  nova é um arquivo.
- **Nenhuma interface.** Não há `IRandom`, `IClock` nem `ISaveRepository`: o Core recebe `Random` e
  `DateTime` como parâmetro, e o save é texto. Com o Erudito fora do jogo, `ITurnTaker` perdeu a
  segunda implementação e saiu junto.
- **Regras de combate são estratégias, não `switch`.** Enquanto eram seis efeitos e dez status, um
  `switch` e uns `if` espalhados davam conta. Veneno, Bomba e Quebra de Defesa mostraram o custo: cada
  regra nova pedia mexer em quatro arquivos, e a Bomba quebrava a luta. Agora cada regra é uma classe
  que responde aos momentos da luta, e o fluxo não conhece nenhuma pelo nome. A troca foi conferida
  luta a luta (`--digest`): as mesmas sementes dão os mesmos eventos.
- **Atributo é dado pronto, conferido.** O arquivo da família guarda o número final, não os
  ingredientes; o jogo só escala por estrelas e nível. Para o número não envelhecer, o jogo refaz a
  conta do modelo na validação e compara.
- **Eventos em vez de callbacks.** O combate devolve `BattleEvent` e não sabe que existe tela; a mesma
  luta roda animada (`BattleScreen`) ou instantânea (`AutoBattle`: Batalha automática e simulador).
- **Uma fonte da verdade por dado.** A runa guarda em qual monstro está (`Rune.EquippedOn`, o id da
  cópia); o monstro não guarda lista de runas. A equipe guarda ids de monstro; o monstro não sabe em
  que equipe está. O valor do principal, a raridade e o total de cada subatributo não são salvos
  (`[JsonIgnore]`): saem das tabelas e dos sorteios guardados (`RuneSubstat.Rolls`).
- **Monstros são cópias.** Cada invocação cria um `OwnedSummon` com id próprio; a variante é só o
  `SummonId`. Por isso duas cópias iguais podem ter estrelas, nível, níveis de habilidade, Despertar,
  runas e equipes diferentes.
- **Uma batalha, vários conteúdos.** Fase e andar de Masmorra viram um `Encounter` (estrelas, nível,
  ondas, força); a mesma `BattleFactory`, a mesma tela e a mesma Batalha automática servem aos dois. O que muda é a
  equipe (por conteúdo) e a recompensa (`Campaign` ou `Dungeons`, as duas devolvem `VictoryReward`).
- **O jogo nasce em português; ids em inglês.** Os textos da interface (`Data/texts/pt-BR.json`, a
  base) e os nomes em `Data/` (invocações, habilidades, fases, Masmorras, chefes, Loja) são em
  português; ids, chaves, enums e argumentos ficam em inglês e nunca mudam (o save guarda ids). O
  `en.json` é uma tradução: as mesmas chaves, mais o grupo `names` com o nome de cada coisa dos dados
  pelo nome em português. O `ContentLoader` troca os nomes ao montar o `GameDatabase` (`Locale.Name`),
  então o Core e a batalha já recebem tudo no idioma escolhido; trocar de idioma remonta o banco.
- **Celular primeiro.** A tela é pensada para o dedo: nada depende do mouse (sem dica; o brilho sob
  o mouse é só enfeite), alvos de toque com 56 px de altura (`GameTheme.Touch`), texto de 18 px, e
  toda explicação numa `Dialog` colada no que foi tocado (elemento → toque → janela). Segurar um
  monstro abre o resumo dele, segurar uma runa a ficha, em toda tela. A janela tampa o jogo, mas não o
  para: a Batalha automática segue por baixo.
- **Desenhado em código.** Não há cena `.tscn` nem imagem de interface pronta:
  o `Theme` sai de `GameTheme.Build()`, as molduras são texturas geradas uma vez por cor
  (`Ornament`, viram `StyleBoxTexture` de 9 partes, com o miolo repetido para o grão não esticar) e os
  sigilos se desenham em `_Draw` (`SigilButton`, `SigilRing`, `Constellation`, `EnergyRing`). Os
  estados se leem pela luz: azul arcano ligado, verde espiritual chamando; o peso
  (afundar ao apertar) é o `Juice`. `Button` do Godot não mede os filhos: `GameButton`, `TileButton`
  e as abas acertam o `CustomMinimumSize` pelo conteúdo (`Fit`). Os desenhos do Commons são pretos e o
  `Doodle` os pinta pelo shader, então um ícone serve em qualquer cor. O número de um sigilo fica
  numa plaquinha escura com contorno, por cima da borda de baixo.
- **PNG em vários tamanhos, escolhido pelo tamanho na tela.** Os SVG são só a fonte: o
  `Tools/art/render_png.py` gera PNG de 32 a 512 px, importados com mipmaps. `Art` entrega o de 128 e
  o `Doodle`, ao mudar de tamanho, troca pelo menor que cobre o tamanho na tela (com a escala da
  janela) — a redução que sobra é pequena e suavizada. Todo desenho fica em "contain" (proporção
  mantida, inteiro no espaço) e, dentro de um componente com forma, recortado nela pela `ArtMask`
  (`ClipChildren`): o símbolo no sigilo, o retrato no medalhão, a criatura no cartão.
- **O fundo segue o foco da tela.** O anel do `Backdrop` fica no centro do portal de invocar; nas
  outras telas, no meio (a batalha não tem anel: o oval é o campo).
- **Camadas por cima vão no alto da árvore.** O `GameRoot` põe a tela num contêiner (`Screens`), o
  selo da Batalha automática depois dele e as janelas (`Dialog`) no controle mais alto (`Layout.Host`),
  que tem o tema; assim cobrem a tela inteira de qualquer botão e sobrevivem à troca de tela. Ordem
  de desenho: resultado da luta 60, selo 70, janela 80, pausa 99.
- **Texto fora do código.** As telas pedem texto por chave ao `Locale`; o que depende de regra
  (descrição de habilidade, conjunto, efeito) é montado em `Texts` a partir das mesmas regras que o
  combate usa, então a explicação nunca desatualiza. `Tools/texts/check_texts.py` confere as chaves
  literais; as que vêm de enum o jogo confere ao abrir.
- **Números de Summoners War como tabela, não como fórmula inventada.** Principal, subatributo,
  pedras e custo de melhora são as tabelas de lá, uma linha por estrela, em `RuneRules`. O custo sem
  falha é derivado delas (custo ÷ chance de sucesso), então mudar o preço em Essência é mudar
  `CostPerEssence`.
- **Inimigo comum é invocação.** A mesma variante que o jogador invoca, nas estrelas e no nível do
  encontro, com as habilidades no nível 1, sem runas nem Despertar, com Vida e Ataque reforçados por estrelas (`BattleFactory.FoeScale`). Só o que
  não existe como invocação (os chefes) mora em `Data/enemies.json`. Assim cada criatura nova serve aos
  dois lados.
- **A Batalha automática espera o tempo da tela.** As lutas são resolvidas na hora (`AutoBattle.Run`
  guarda os eventos), mas a recompensa só entra depois de `BattlePace.Seconds(eventos, 2×)`, que soma os
  momentos de `BattlePace.Beats` (corrida, golpes que caem juntos, volta): a mesma
  conta que a tela de batalha usa para esperar entre eventos.
- **Habilidade é uma lista, não slots fixos.** Cada variante tem a básica, as ativas com recarga e
  talvez uma Passiva (`SkillDefinition.Passive`). O nível e o Despertar não mudam o dado: a batalha pede
  `SkillDefinition.At(nível, desperta)`, que devolve a habilidade já com as melhorias e os efeitos do
  Despertar. A habilidade que o Despertar dá entra no fim da lista (`SummonDefinition.AllSkills`), então
  o índice do nível salvo (`OwnedSummon.SkillLevels`) nunca muda.
- **Estrelas e nível pelas tabelas de Summoners War.** A fração do atributo de 6★ nível 40 em cada
  estrela e nível (`Growth.Bands`) e a experiência de cada nível (`Leveling`) são as de lá. A
  diferença: evoluir mantém o nível (lá volta ao 1), então o monstro entra na faixa da estrela nova no
  mesmo nível, os atributos sobem na hora e só faltam os 5 níveis novos.
- **Confere na entrada, cobra na vitória.** `Campaign.Check` e `Dungeons.Check` devolvem um
  `EntryProblem` (fechado, sem Mana, inventário de runas cheio) sem mudar nada; a Mana só sai no
  `ApplyVictory`, então a derrota (ou sair e recomeçar pela pausa) não custa nada.

## Simplificações do MVP em relação ao GDD

- Passivas simples (dezessete tipos, uma por família), habilidades novas do Despertar só nas 2★ e nas 3★.
- Viés de elemento neutro: os cinco elementos repartem o orçamento igual (o modelo aceita o viés).
- Sem Tiques, traçado do sigilo, troca por Fragmentos, regras de região, Torre, Provações e Portais
  Secretos (o Despertar ainda não pede Provação: só Essência).
- Masmorras com 5 andares cada; chefes reaproveitam o desenho de criaturas do Commons.

## Save

`PlayerState.CurrentVersion` marca o formato (hoje 7: estrelas, níveis de habilidade e a experiência por
estrela; o 6 trouxe ids em inglês; o 5, Mana, Ouro e nível da conta). Um save de outro formato não é lido: o `SaveStore` guarda o arquivo como
`nome.old-AAAAMMDD-HHMMSS.json` (a data evita apagar um backup mais velho) e começa uma conta nova.

## Exportar

`Data/` são arquivos de texto lidos com `FileAccess`: no preset de exportação, inclua `*.json` em
"Filters to export non-resource files".
