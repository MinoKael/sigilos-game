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
  Nenhuma tela muda o `PlayerState` nem salva.
- **GameEntry/** é a raiz de composição: o `GameRoot` assina os eventos das telas, chama as regras do
  Core, salva e troca de tela. É o único lugar que junta tudo.

Dentro do Core:

| Namespace | Papel | Depende de |
| --- | --- | --- |
| `Core.Content` | Definições lidas de `Data/` e o `GameDatabase` | — |
| `Core.Runes` | Runa, conjuntos, pedras, tabelas de Summoners War, sorteio e bônus | Content |
| `Core.Progression` | Crescimento, nível 1–40, Despertar, ficha de atributos, ociosidade, campanha | Content, Runes, Player |
| `Core.Battle` | Combate: Ímpeto, Éter, efeitos, automático | Content, Runes, Progression |
| `Core.Player` | O save (`PlayerState`), inventário de runas e a ponte para a batalha | Content, Runes, Battle |
| `Core.Summoning` | Gacha | Content, Player, Progression |

`Core.Battle` não conhece `PlayerState`: o simulador monta um `BattleTeam` à mão. A tela de Monstros e
a batalha calculam atributos pelo mesmo `SummonStats`, então o número que o jogador vê é o que luta.

## Onde mexer

| Quero... | Mexa em |
| --- | --- |
| Nova variante de invocação | um arquivo em `Data/summons/` (nenhum código) |
| Nova família | `Data/families.json` + dois SVG em `Assets/Creatures/` (normal e desperto) + 5 arquivos em `Data/summons/` |
| Novo inimigo ou fase | `Data/enemies.json`, `Data/stages.json` |
| Balancear números do combate | `Core/Battle/BattleRules.cs`, depois `dotnet run --project Tests -- --simular` |
| Éter (ganho, teto, custo mínimo) | `Core/Battle/BattleRules.cs`; custo de cada aprimoramento em `Data/summons/` |
| Atributos por papel e nível | `Data/roles.json` (valores de nível 40, escala de Summoners War), `Core/Progression/Growth.cs` |
| Experiência, Despertar | `Core/Progression/Leveling.cs`, `Core/Progression/Awakening.cs` |
| Runas: tabelas, custos, espaços | `Core/Runes/RuneRules.cs` (uma tabela por atributo, de 1★ a 6★) |
| Conjuntos de runas | `Core/Runes/RuneSets.cs`; o efeito em combate em `Core/Battle/EffectResolver.cs` ou `BattleSession.cs` |
| Pedras (Afiar, Gema) | `Core/Runes/RuneForge.cs` (regras), `Core/Runes/RuneRules.cs` (faixas), grau por fase em `Data/stages.json` |
| Taxas do gacha | `Core/Summoning/SummonRates.cs` |
| Ociosidade | `Core/Progression/Idle.cs` |
| Novo tipo de efeito | `Core/Content/EffectKind.cs` + um `case` em `Core/Battle/EffectResolver.cs` + texto em `UI/Texts.cs` |
| Nova Assinatura | `Core/Content/PassiveKind.cs` + o gancho em `BattleSession` ou `BattleUnit` |
| Textos do Compêndio | `UI/Texts.cs` (Glifos, efeitos) e `UI/Screens/CompendiumScreen.cs` |
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
- **Uma fonte da verdade por dado.** A runa guarda em quem está equipada (`Rune.EquippedOn`); a
  invocação não guarda lista de runas. O valor do principal, a raridade e o total de cada subatributo
  não são salvos (`[JsonIgnore]`): saem das tabelas e dos subatributos.
- **Números de Summoners War como tabela, não como fórmula inventada.** Principal, subatributo,
  pedras e custo de melhora são as tabelas de lá, uma linha por estrela, em `RuneRules`. O custo sem
  falha é derivado delas (custo ÷ chance de sucesso), então mudar a escassez é mudar `ManaPerDust`.

## Simplificações do MVP em relação ao GDD

- Assinaturas por família: Diabretes, Cavaleiros e Fênix.
- Sem Tiques, traçado do Glifo, troca por Fragmentos, regras de região, Masmorras, Torre e Provações
  (o Despertar ainda não pede Provação: só Essência).
- Porta ainda não tem invocação: por ora é só o conjunto de runas de turno extra.

## Save

`PlayerState.CurrentVersion` marca o formato. O formato anterior é convertido em `PlayerSave` (do 2
para o 3, as runas antigas viram runas novas das mesmas estrelas, no mesmo espaço e dono). Um save
mais antigo que isso não é lido: o `SaveStore` guarda o arquivo como `nome.antigo.json` e começa uma
conta nova.

## Exportar

`Data/` são arquivos de texto lidos com `FileAccess`: no preset de exportação, inclua `*.json` em
"Filters to export non-resource files".
