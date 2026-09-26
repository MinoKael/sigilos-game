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
  `Data/texts/en.json` por chave (`Locale.T("runes.upgrade_to", ...)`); `UI/Texts.cs` só monta nomes
  por enum e as descrições geradas das regras.
- **GameEntry/** é a raiz de composição: o `GameRoot` assina os eventos das telas, chama as regras do
  Core, salva e troca de tela. É o único lugar que junta tudo.

Dentro do Core:

| Namespace | Papel | Depende de |
| --- | --- | --- |
| `Core.Content` | Definições lidas de `Data/`, o vocabulário (Glifo, conjunto, raridade, efeitos) e o `GameDatabase` | — |
| `Core.Runes` | Runa, conjuntos, pedras, tabelas de Summoners War, sorteio, busca e bônus | Content |
| `Core.Progression` | Crescimento por estrelas e nível, experiência (tabela de Summoners War), Evolução, Despertar, Fusão (nível de habilidade), ficha de atributos, ociosidade, Campanha, Masmorras, Mana, nível da conta, Loja | Content, Runes, Player |
| `Core.Battle` | Combate: Ímpeto, recarga das habilidades, efeitos, Passivas, automático | Content, Runes, Progression |
| `Core.Player` | O save (`PlayerState`), coleção e Baú (`Roster`), equipes por conteúdo (`Teams`), inventário de runas e a ponte para a batalha | Content, Runes, Battle |
| `Core.Summoning` | Gacha | Content, Player, Progression |

`Core.Battle` não conhece `PlayerState`: o simulador monta um `BattleTeam` à mão. A tela de Monstros e
a batalha calculam atributos pelo mesmo `SummonStats`, então o número que o jogador vê é o que luta.

## Onde mexer

| Quero... | Mexa em |
| --- | --- |
| Nova variante de invocação | um arquivo em `Data/summons/` (nenhum código) |
| Nova família | `Data/families.json` + dois SVG em `Assets/Creatures/` (normal e desperto) + 5 arquivos em `Data/summons/` |
| Habilidades de uma variante | `"skills"` no arquivo dela em `Data/summons/`: a primeira é a básica (sem `cooldown`), as outras ativas têm `cooldown`, no máximo uma é `passive`; `levels` são as melhorias por cópia fundida (`Damage`, `Recovery`, `EffectRate`, `Cooldown`); `awakenedEffects` troca os efeitos ao despertar |
| O que o Despertar dá | `"awakening"` da variante: `stat` (bônus de atributo) ou `skill` (habilidade nova, que só existe desperta) |
| Inimigos de fase ou andar | ondas em `Data/stages.json` e `Data/dungeons.json`: `"summon"` é uma variante de invocação, `"enemy"` um chefe de `Data/enemies.json`; `stars` e `level` de cada fase e andar |
| Reforço dos inimigos-invocação | `BattleFactory.FoeScale` (por estrelas naturais) e `scale` do andar |
| Nova Passiva | `Core/Content/PassiveKind.cs` + o gancho (dano em `DamageFormula`, turno e onda em `BattleSession`, golpe em `EffectResolver`) + texto em `Data/texts` (`passive.*`) |
| Ritmo da luta na tela e da Batalha automática | `UI/BattlePace.cs`; quantas lutas em `AutoBattle.RepeatRuns` |
| Nova Masmorra ou andar | `Data/dungeons.json` (andares, conjuntos, drop, `mana`, `firstClearGold`, `scale` de força); regras em `Core/Progression/Dungeons.cs` |
| Custo em Mana de fase | `Data/stages.json` (`mana`) |
| Mana máxima e recarga | `Core/Progression/Mana.cs`; a recarga entra pela canalização em `Core/Progression/Idle.cs` |
| Nível da conta e Ouro por nível | `Core/Progression/Account.cs` |
| Loja | ofertas em `Data/shop.json`; regra em `Core/Progression/Shop.cs` |
| Qualquer texto da interface | `Data/texts/en.json` (a base) e a mesma chave em `Data/texts/pt-BR.json`, depois `py Tools/texts/check_texts.py` |
| Traduzir | copie `Data/texts/en.json` com outro nome e rode com `-- --language=nome` |
| Balancear números do combate | `Core/Battle/BattleRules.cs`, depois `dotnet run --project Tests -- --simulate` |
| Atributos por papel, estrelas e nível | `Data/roles.json` (valores de 6★ nível 40, escala de Summoners War), faixas por estrela em `Core/Progression/Growth.cs` |
| Experiência por nível e valor da Essência | `Core/Progression/Leveling.cs` (tabela de Summoners War por estrela, `ExperiencePerEssence`); experiência por fase e andar em `Data/stages.json` e `Data/dungeons.json` |
| Evolução (custo por estrela) | `Core/Progression/Evolution.cs` |
| Despertar (custo por estrelas naturais, bônus) | `Core/Progression/Awakening.cs` |
| Fusão de cópias (sobe uma habilidade sorteada), Fragmentos ao liberar | `Core/Progression/Fusion.cs` |
| Runas: tabelas, custos, espaços | `Core/Runes/RuneRules.cs` (uma tabela por atributo, de 1★ a 6★) |
| Conjuntos de runas | `Core/Runes/RuneSets.cs`; o efeito em combate em `Core/Battle/EffectResolver.cs` ou `BattleSession.cs` |
| Pedras (Afiar, Gema) | `Core/Runes/RuneForge.cs` (regras), `Core/Runes/RuneRules.cs` (faixas), grau por andar em `Data/dungeons.json` |
| Glifo de um conjunto, de um atributo ou de um efeito | `Core/Runes/RuneSets.cs` (conjunto → Glifo); atributo e efeito → Glifo em `UI/Texts.cs` (`GlyphOf`) |
| Coleção, Baú, equipes | `Core/Player/Roster.cs`, `Core/Player/Teams.cs`, `PlayerState.CollectionCapacity`/`TeamSize` |
| Vagas do inventário de runas | `RuneInventory.Capacity` (runas equipadas, inclusive em monstro do Baú, não contam) |
| Taxas do gacha | `Core/Summoning/SummonRates.cs` |
| Ociosidade | `Core/Progression/Idle.cs` |
| Novo tipo de efeito | `Core/Content/EffectKind.cs` + um `case` em `Core/Battle/EffectResolver.cs` + a descrição em `UI/Texts.cs` e o texto em `Data/texts` |
| Compêndio (regras) e Grimório (catálogo) | textos em `Data/texts` (`compendium.*`, `grimoire.*`); cartões em `UI/Screens/CompendiumScreen.cs` e `GrimoireScreen.cs` |
| Cores, fontes | `UI/Style/Palette.cs`, `UI/Style/GameTheme.cs` |
| Nova tela | `UI/Screens/` + o `Show...` correspondente em `GameEntry/GameRoot.cs` |

`GameDatabase.Validate()` confere referências e faixas dos dados; o teste `DataTests` falha se algo
estiver quebrado (inclusive básica com recarga, ativa sem recarga ou duas Passivas), e o jogo mostra os
problemas no console ao abrir.

## Decisões (SOLID sem abstração prematura)

- **Uma responsabilidade por classe.** `BattleSession` cuida do fluxo de turnos; `EffectResolver` do
  que cada efeito faz; `Targeting` de quem é atingido; `DamageFormula` do número; `AutoPilot` das
  decisões automáticas; `RuneForge` cria e transforma runas; `RuneInventory` cobra (Pó e pedras) e
  guarda. As telas só desenham.
- **Aberto para conteúdo, sem código novo.** Habilidades são listas de efeitos em JSON; uma invocação
  nova é um arquivo.
- **Nenhuma interface.** Não há `IRandom`, `IClock` nem `ISaveRepository`: o Core recebe `Random` e
  `DateTime` como parâmetro, e o save é texto. Com o Erudito fora do jogo, `ITurnTaker` perdeu a
  segunda implementação e saiu junto.
- **Efeitos são dados + `switch`, não uma classe por efeito.** Seis tipos cabem num arquivo legível;
  uma hierarquia de estratégias só pagaria o custo quando houver regra que o `switch` não comporte.
- **Eventos em vez de callbacks.** O combate devolve `BattleEvent` e não sabe que existe tela; a mesma
  luta roda animada (`BattleScreen`) ou instantânea (`AutoBattle`, botão Resolver e simulador).
- **Uma fonte da verdade por dado.** A runa guarda em qual monstro está (`Rune.EquippedOn`, o id da
  cópia); o monstro não guarda lista de runas. A equipe guarda ids de monstro; o monstro não sabe em
  que equipe está. O valor do principal, a raridade e o total de cada subatributo não são salvos
  (`[JsonIgnore]`): saem das tabelas e dos sorteios guardados (`RuneSubstat.Rolls`).
- **Monstros são cópias.** Cada invocação cria um `OwnedSummon` com id próprio; a variante é só o
  `SummonId`. Por isso duas cópias iguais podem ter estrelas, nível, níveis de habilidade, Despertar,
  runas e equipes diferentes.
- **Uma batalha, vários conteúdos.** Fase e andar de Masmorra viram um `Encounter` (estrelas, nível,
  ondas, força); a mesma `BattleFactory`, a mesma tela e o mesmo Resolver servem aos dois. O que muda é a
  equipe (por conteúdo) e a recompensa (`Campaign` ou `Dungeons`, as duas devolvem `VictoryReward`).
- **O jogo é em inglês; o código fala português.** Ids, nomes em `Data/`, chaves e textos da interface
  (`Data/texts/en.json`), argumentos e o simulador são em inglês. `pt-BR.json` é uma tradução da
  interface, com as mesmas chaves. Comentários, mensagens dos testes e estes docs seguem em português.
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
  guarda os eventos), mas a recompensa só entra depois de `BattlePace.Seconds(eventos, 2×)`: a mesma
  conta que a tela de batalha usa para esperar entre eventos.
- **Habilidade é uma lista, não slots fixos.** Cada variante tem a básica, as ativas com recarga e
  talvez uma Passiva (`SkillDefinition.Passive`). O nível e o Despertar não mudam o dado: a batalha pede
  `SkillDefinition.At(nível, desperta)`, que devolve a habilidade já com as melhorias e os efeitos do
  Despertar. A habilidade que o Despertar dá entra no fim da lista (`SummonDefinition.AllSkills`), então
  o índice do nível salvo (`OwnedSummon.SkillLevels`) nunca muda.
- **Estrelas e nível pelas tabelas de Summoners War.** A fração do atributo de 6★ nível 40 em cada
  estrela e nível (`Growth.Bands`) e a experiência de cada nível (`Leveling`) são as de lá. Evoluir
  volta ao nível 1, como lá.
- **Confere na entrada, cobra na vitória.** `Campaign.Check` e `Dungeons.Check` devolvem um
  `EntryProblem` (fechado, sem Mana, inventário de runas cheio) sem mudar nada; a Mana só sai no
  `ApplyVictory`, então a derrota (ou o Recuar) não custa nada.

## Simplificações do MVP em relação ao GDD

- Passivas simples (nove tipos), habilidades novas do Despertar só nas 3★ e em quatro 4★.
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
