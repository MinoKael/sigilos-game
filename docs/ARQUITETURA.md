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
| `Core.Progression` | Crescimento, nível 1–40, Despertar, Fusão (Ecos), ficha de atributos, ociosidade, Campanha, Masmorras, Mana, nível da conta, Loja | Content, Runes, Player |
| `Core.Battle` | Combate: Ímpeto, Éter, efeitos, automático | Content, Runes, Progression |
| `Core.Player` | O save (`PlayerState`), coleção e Baú (`Roster`), equipes por conteúdo (`Teams`), inventário de runas e a ponte para a batalha | Content, Runes, Battle |
| `Core.Summoning` | Gacha | Content, Player, Progression |

`Core.Battle` não conhece `PlayerState`: o simulador monta um `BattleTeam` à mão. A tela de Monstros e
a batalha calculam atributos pelo mesmo `SummonStats`, então o número que o jogador vê é o que luta.

## Onde mexer

| Quero... | Mexa em |
| --- | --- |
| Nova variante de invocação | um arquivo em `Data/summons/` (nenhum código) |
| Nova família | `Data/families.json` + dois SVG em `Assets/Creatures/` (normal e desperto) + 5 arquivos em `Data/summons/` |
| Novo inimigo ou fase | `Data/enemies.json`, `Data/stages.json` |
| Nova Masmorra ou andar | `Data/dungeons.json` (andares, conjuntos, drop, `mana`, `firstClearGold`, `scale` de força); regras em `Core/Progression/Dungeons.cs` |
| Custo em Mana de fase | `Data/stages.json` (`mana`) |
| Mana máxima e recarga | `Core/Progression/Mana.cs`; a recarga entra pela canalização em `Core/Progression/Idle.cs` |
| Nível da conta e Ouro por nível | `Core/Progression/Account.cs` |
| Loja | ofertas em `Data/shop.json`; regra em `Core/Progression/Shop.cs` |
| Qualquer texto da interface | `Data/texts/en.json` (a base) e a mesma chave em `Data/texts/pt-BR.json`, depois `py Tools/texts/check_texts.py` |
| Traduzir | copie `Data/texts/en.json` com outro nome e rode com `-- --language=nome` |
| Balancear números do combate | `Core/Battle/BattleRules.cs`, depois `dotnet run --project Tests -- --simular` |
| Éter (ganho, teto, custo mínimo) | `Core/Battle/BattleRules.cs`; custo de cada aprimoramento em `Data/summons/` |
| Atributos por papel e nível | `Data/roles.json` (valores de nível 40, escala de Summoners War), `Core/Progression/Growth.cs` |
| Experiência, Despertar | `Core/Progression/Leveling.cs`, `Core/Progression/Awakening.cs` |
| Runas: tabelas, custos, espaços | `Core/Runes/RuneRules.cs` (uma tabela por atributo, de 1★ a 6★) |
| Conjuntos de runas | `Core/Runes/RuneSets.cs`; o efeito em combate em `Core/Battle/EffectResolver.cs` ou `BattleSession.cs` |
| Pedras (Afiar, Gema) | `Core/Runes/RuneForge.cs` (regras), `Core/Runes/RuneRules.cs` (faixas), grau por andar em `Data/dungeons.json` |
| Glifo de um conjunto, de um atributo ou de um efeito | `Core/Runes/RuneSets.cs` (conjunto → Glifo); atributo e efeito → Glifo em `UI/Texts.cs` (`GlyphOf`) |
| Coleção, Baú, equipes | `Core/Player/Roster.cs`, `Core/Player/Teams.cs`, `PlayerState.CollectionCapacity`/`TeamSize` |
| Vagas do inventário de runas | `RuneInventory.Capacity` (runas equipadas, inclusive em monstro do Baú, não contam) |
| Taxas do gacha | `Core/Summoning/SummonRates.cs` |
| Ociosidade | `Core/Progression/Idle.cs` |
| Novo tipo de efeito | `Core/Content/EffectKind.cs` + um `case` em `Core/Battle/EffectResolver.cs` + a descrição em `UI/Texts.cs` e o texto em `Data/texts` |
| Nova Assinatura | `Core/Content/PassiveKind.cs` + o gancho em `BattleSession` ou `BattleUnit` |
| Compêndio (regras) e Grimório (catálogo) | textos em `Data/texts` (`compendium.*`, `grimoire.*`); cartões em `UI/Screens/CompendiumScreen.cs` e `GrimoireScreen.cs` |
| Cores, fontes | `UI/Style/Palette.cs`, `UI/Style/GameTheme.cs` |
| Nova tela | `UI/Screens/` + o `Show...` correspondente em `GameEntry/GameRoot.cs` |

`GameDatabase.Validate()` confere referências e faixas dos dados; o teste `DataTests` falha se algo
estiver quebrado (inclusive aprimoramento abaixo do custo mínimo de Éter), e o jogo mostra os
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
  `SummonId`. Por isso duas cópias iguais podem ter nível, Despertar, runas e equipes diferentes.
- **Uma batalha, vários conteúdos.** Fase e andar de Masmorra viram um `Encounter` (nível, ondas,
  força); a mesma `BattleFactory`, a mesma tela e o mesmo Resolver servem aos dois. O que muda é a
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
- **Confere na entrada, cobra na vitória.** `Campaign.Check` e `Dungeons.Check` devolvem um
  `EntryProblem` (fechado, sem Mana, inventário de runas cheio) sem mudar nada; a Mana só sai no
  `ApplyVictory`, então a derrota (ou o Recuar) não custa nada.

## Simplificações do MVP em relação ao GDD

- Assinaturas por família: Diabretes, Cavaleiros e Fênix.
- Sem Tiques, traçado do sigilo, troca por Fragmentos, regras de região, Torre, Provações e Portais
  Secretos (o Despertar ainda não pede Provação: só Essência).
- Masmorras com 5 andares cada; chefes reaproveitam o desenho de criaturas do Commons.

## Save

`PlayerState.CurrentVersion` marca o formato (hoje 6: ids em inglês; o 5 trouxe Mana, Ouro e nível da
conta). Um save de outro formato não é lido: o `SaveStore` guarda o arquivo como
`nome.old-AAAAMMDD-HHMMSS.json` (a data evita apagar um backup mais velho) e começa uma conta nova.

## Exportar

`Data/` são arquivos de texto lidos com `FileAccess`: no preset de exportação, inclua `*.json` em
"Filters to export non-resource files".
