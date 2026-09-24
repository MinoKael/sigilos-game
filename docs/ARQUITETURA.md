# Arquitetura

## Camadas

```
Data/*.json ──texto──▶ GameEntry ──▶ Core   (regras, sem Godot)
                          │
                          └──▶ UI    (telas; lê Core, nunca GameEntry)
```

- **Core/** não conhece o Godot. Recebe texto (não caminhos), `Random` e `DateTime` como parâmetro.
  Por isso roda igual no jogo e no console de testes, e o combate é determinístico por semente.
- **UI/** recebe o que mostra no construtor e avisa por evento C# (`FightRequested`, `Confirmed`...).
  Nenhuma tela muda o `PlayerState` nem salva.
- **GameEntry/** é a raiz de composição: o `GameRoot` assina os eventos das telas, chama as regras do
  Core, salva e troca de tela. É o único lugar que junta tudo.

Dentro do Core:

| Namespace | Papel | Depende de |
| --- | --- | --- |
| `Core.Content` | Definições lidas de `Data/` e o `GameDatabase` | — |
| `Core.Battle` | Combate: Ímpeto, Éter, efeitos, automático | Content |
| `Core.Player` | O save (`PlayerState`) e a ponte para a batalha | Content, Battle |
| `Core.Progression` | Crescimento, nível, ociosidade, campanha | Content, Player |
| `Core.Summoning` | Gacha | Content, Player, Progression |

`Core.Battle` não conhece `PlayerState`: o simulador monta um `BattleTeam` à mão.

## Onde mexer

| Quero... | Mexa em |
| --- | --- |
| Nova variante de invocação | um arquivo em `Data/summons/` (nenhum código) |
| Nova família | `Data/families.json` + um SVG em `Assets/Creatures/` + 5 arquivos em `Data/summons/` |
| Novo inimigo ou fase | `Data/enemies.json`, `Data/stages.json` |
| Nova página pronta | `Data/pages.json` (o efeito sai de `Core/Battle/PageFormula.cs`) |
| Balancear números do combate | `Core/Battle/BattleRules.cs`, depois `dotnet run --project Tests -- --simular` |
| Taxas do gacha | `Core/Summoning/SummonRates.cs` |
| Ociosidade, nível | `Core/Progression/Idle.cs`, `Core/Progression/SharedLevel.cs` |
| Novo tipo de efeito | `Core/Content/EffectKind.cs` + um `case` em `Core/Battle/EffectResolver.cs` + texto em `UI/Texts.cs` |
| Nova Assinatura | `Core/Content/PassiveKind.cs` + o gancho em `BattleSession` ou `BattleUnit` |
| Cores, fontes | `UI/Style/Palette.cs`, `UI/Style/GameTheme.cs` |
| Nova tela | `UI/Screens/` + o `Show...` correspondente em `GameEntry/GameRoot.cs` |

`GameDatabase.Validate()` confere referências e faixas dos dados; o teste `DataTests` falha se algo
estiver quebrado, e o jogo mostra os problemas no console ao abrir.

## Decisões (SOLID sem abstração prematura)

- **Uma responsabilidade por classe.** `BattleSession` cuida do fluxo de turnos; `EffectResolver` do
  que cada efeito faz; `Targeting` de quem é atingido; `DamageFormula` do número; `AutoPilot` das
  decisões automáticas. As telas só desenham.
- **Aberto para conteúdo, sem código novo.** Habilidades são listas de efeitos em JSON; páginas saem de
  fórmula. Uma invocação nova é um arquivo.
- **Interface só com duas implementações reais.** A única é `ITurnTaker` (unidade em campo e
  Conjurador dividem a barra de Ímpeto). Não há `IRandom`, `IClock` nem `ISaveRepository`: o Core
  recebe `Random` e `DateTime` como parâmetro, e o save é texto.
- **Efeitos são dados + `switch`, não uma classe por efeito.** Sete tipos cabem num arquivo legível;
  uma hierarquia de estratégias só pagaria o custo quando houver regra que o `switch` não comporte.
- **Eventos em vez de callbacks.** O combate devolve `BattleEvent` e não sabe que existe tela; a mesma
  luta roda animada (`BattleScreen`) ou instantânea (`AutoBattle`, botão Resolver e simulador).

## Simplificações do MVP em relação ao GDD

- Nível compartilhado único para a conta (o GDD sobe as 5 maiores e nivela o resto pela quinta).
- Grimório com páginas prontas (montagem livre de Glifo, Forma e Círculo entra na v0.5).
- Assinaturas implementadas por família: Diabretes, Cavaleiros e Fênix.
- Sem Sigilos (equipamento), Pó de Sigilo, Tiques, traçado do Glifo, troca por Fragmentos, regras de
  região, Masmorras, Torre e Provações — todos da v0.5 em diante.
- Porta (invocar espírito) ainda não tem fórmula de página nem invocação no MVP.

## Exportar

`Data/` são arquivos de texto lidos com `FileAccess`: no preset de exportação, inclua `*.json` em
"Filters to export non-resource files".
