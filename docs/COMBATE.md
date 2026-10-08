# Regras de combate: onde moram e de onde vêm

As regras do combate conferidas com a referência (as páginas de teoria da wiki de Summoners War: Turn
Order, Attack Bar, Equations, Fixed Damage, Guide: Stat Scaling e Ignore Damage Reduction Effects) e com
a planilha `docs/allstats.xlsx`. Lidas em 2026-10-07 e de novo em 2026-10-08, as páginas renderizadas
pelo Chromium headless (`chrome.exe --headless=new --dump-dom`), com o código conferido outra vez: as
conclusões abaixo continuam valendo. A referência valida e sugere; ela não manda e não é importada. O
que o Sigilos faz diferente de propósito está escrito aqui, com o motivo.

## Onde mora cada coisa

Um número de combate mora em um lugar só; a estratégia lê de lá, nunca guarda o seu.

| O quê | Onde |
| --- | --- |
| Números do combate (Defesa, elemento, efeitos, Bomba, Aflição, limites) | `Core/Battle/BattleRules.cs` |
| A fórmula de dano | `Core/Battle/DamageFormula.cs` (`Compute`) |
| A barra de Ímpeto e a ordem de turno | `BattleSession.Step` (e `PredictOrder`, que usa o mesmo passo) |
| As fases do turno | `BattleSession.BeginTurn` e `FinishTurn` |
| Vantagem de elemento | `ElementChart` (lê os multiplicadores de `BattleRules`) |
| Números dos conjuntos de runa (Contra-ataque, Perdição, Oblívio...) | `Core/Runes/RuneSets.cs` |
| Cada efeito, Passiva e conjunto | a estratégia dele em `Core/Battle/{Statuses,Passives,Sets,Effects}`, com o valor vindo dos dados ou de uma das duas classes acima |
| Força dos inimigos (calibragem, não regra) | `BattleFactory.FoeBoost`, `FoeScale`, `GuardianHealth` e o `scale` das fases e andares |

Os testes que prendem estas regras: `DamageTests` (curva de Defesa, Ignorar Defesa, Passiva de redução,
elemento, crítico, escudo), `TurnOrderTests` (quem age primeiro, Velocidade em dobro, empurrão, rodada) e
`StatusTests` (Atordoamento, Bomba, Bênção e afins).

## Ordem de turno (Ímpeto)

- **O modelo.** Cada unidade enche a barra na proporção da Velocidade: o tempo até agir é
  t = (100 − I) / VEL, e age quem tiver o menor t (`BattleSession.Step`). A referência anda em passos
  (a cada passo, a barra sobe 7% da Velocidade); o Sigilos usa o tempo contínuo, que é o mesmo relógio
  com passos infinitamente pequenos e não depende de arredondar Velocidade para "passos".
- **Começo da luta e de cada onda.** Todo mundo com Ímpeto 0: age primeiro o mais rápido. Igual à
  referência.
- **Empate.** Na referência, quem passa de 100 no mesmo passo age pela barra mais alta, e a barra
  transborda: no mesmo passo, o mais rápido passa mais. O Sigilos não deixa o Ímpeto passar de 100
  (empurrão e passo param em 100), e no mesmo t (os dois cheios pelo mesmo empurrão, por exemplo) age
  quem vem antes na ordem: aliados antes de inimigos, na ordem do time. Diferença guardada: desempatar
  pela Velocidade foi testado em 2026-10-07 e muda a calibragem (no digest de 8325 lutas, as vitórias
  iam de 7372 para 7384, e o time 6★ nível 40 sem runas passava a vencer a fase 50 da Campanha, que o
  teste `CampaignEndsAtLevelTwentyWithRunes` proíbe). Fica para quando o balanceamento for refeito.
- **Turno extra.** Não anda o relógio: a unidade age de novo na hora (`ExtraTurnPending`).
- **Rodada.** Uma rodada é o tempo que Velocidade 100 leva para encher a barra; a luta perde em
  `BattleRules.RoundLimit`.

## As fases de um turno

Referência: no começo do turno, a cura contínua, a Bomba, os efeitos que tiram o turno e o dano
contínuo; depois a ação; no fim, a contagem dos outros efeitos.

No Sigilos (`BattleSession.BeginTurn` e `FinishTurn`):

1. Começo do turno: cada regra em vigor na unidade responde a `OnTurnStart` (Bênção, Bomba, Aflição,
   Passivas), mesmo atordoada. Quem cai aqui não age.
2. Se alguma regra tira o turno (`SkipsTurn`: Atordoamento e afins), o turno acaba sem ação.
3. A ação, e depois `AfterAction` das regras.
4. Fim do turno: todos os efeitos contam um turno (`TickStatuses`) e as recargas descem.

Diferença guardada: na referência, os efeitos que tiram o turno contam no começo; aqui contam no fim,
junto com os outros. Com 1 turno de Atordoamento o resultado é o mesmo (perde este turno, some no fim
dele). A Bomba já trata o atordoamento dela para acabar no mesmo turno (`BombStatus`).

## Dano

D = ATQ × M × K / (K + DEF) × E × C × (dano recebido pelo alvo) × (dano causado por quem bate),
`DamageFormula.Compute`.

- **Defesa.** A curva é a da referência, 1000 / (1140 + 3,5 × DEF), mas normalizada para Defesa 0
  valer o golpe cheio: K = 1140 / 3,5 ≈ 326 (`BattleRules.DefenseConstant`). Na referência, ignorar a
  Defesa ainda deixa 87,7% do dano; aqui deixa 100%. Decisão do GDD (seção 15): o número na
  habilidade é o dano sem Defesa.
- **Nível da habilidade e crítico.** Na referência, o bônus de nível soma com o Dano crítico
  (ATQ × M × (1 + nível + DC)). No Sigilos o bônus entra no M (`SkillDefinition.At`) e o crítico
  multiplica por fora (1 + DC). Os dois jeitos dão o mesmo sem crítico; com crítico, o daqui dá um
  pouco mais. Fica assim: a calibragem de dificuldade (inimigos, fases, masmorras) foi feita em cima
  dele, e trocar mexeria em todo o balanceamento.
- **Elemento.** ×1,25 com vantagem, ×0,75 com desvantagem. Não há golpe de raspão nem esmagador.
- **Ignorar Defesa** (`EffectDefinition.IgnoreDefense`, fração): tira essa parte da Defesa do alvo
  depois dos efeitos. Ignorando tudo, Defesa+ e Quebra de Defesa também deixam de contar, como na
  referência.

## Dano fixo e dano de efeito

Na referência, dano fixo ignora a Defesa, não tem crítico nem acaso, e ainda sofre o que mexe no dano
recebido (Marca) e as Passivas de redução.

Nenhuma habilidade do Sigilos dá dano fixo. O dano que não passa pela fórmula é dano de efeito:

- **Bomba:** ATQ de quem pôs × `BattleRules.BombDamageMultiplier`; sem Defesa, elemento ou crítico;
  conta o dano recebido pelo alvo (Maldição) e o escudo absorve. É o mesmo comportamento do dano fixo
  da referência.
- **Aflição:** uma fração da Vida máxima no começo do turno (`BattleRules.AfflictionFraction`, e no
  chefe só `BossAfflictionShare` disso); não passa por Defesa, escudo nem modificador, como o dano
  contínuo da referência.
- **Espinhos e afins:** uma fração do dano que o golpe causou, de volta em quem bateu; o escudo absorve.

## Em que atributo a habilidade escala

A planilha (`docs/allstats.xlsx`, colunas Skill 1 a 4) tem estes termos nas fórmulas: Ataque (o
grosso, mais de 1600 habilidades), Vida máxima, Defesa, Velocidade, Vida máxima do alvo, Vida atual do
alvo em %, Vida atual em %, Vida atual, Vida perdida, nível de quem ataca, Velocidade relativa,
Velocidade do alvo, aliados vivos em %, inimigos vivos, e as marcas de dano fixo, de vários golpes
(xN), de soma fixa (+N) e de Passiva.

O Sigilos escala só no que o design dele tem, e não importa os outros:

| Efeito | Escala em | Onde |
| --- | --- | --- |
| Dano | Ataque de quem lança (× multiplicador, em N golpes) | `DamageFormula`, `DamageEffect` |
| Cura | Vida máxima do alvo | `HealEffect` |
| Cura do time | Vida máxima de cada alvo | `HealTeam` |
| Escudo | Vida máxima de quem lança | `ShieldEffect` |
| Bomba | Ataque de quem pôs | `BombStatus` |
| Aflição | Vida máxima do dono | `DamageOverTime` |
| Bênção | Vida máxima do dono | `HealOverTime` |
| Espinhos | O dano que o golpe causou | `ThornsPassive` |
| Dreno (Passiva e conjunto) | O dano que o golpe causou | `LifestealPassive`, `DrainSet` |
| Contra-ataque | O golpe básico de quem revida (`RuneSets.CounterDamage`) | `CounterSet` |
| Oblívio | O dano causado, com teto na Vida máxima do alvo (`RuneSets.DestroyShare`, `DestroyLimit`) | `OblivionSet` |
| Perdição | Ímpeto por fatia da Vida máxima do dono perdida num golpe (`RuneSets.BaneStep`) | `BaneSet` |
| Nivelar Vida | Não é dano: a média das frações de Vida | `EqualizeHealthEffect` |

O teto do Oblívio e o passo da Perdição usam a Vida máxima só como limite ou medida, não como dano:
nenhum golpe do Sigilos tira uma fração da Vida máxima do alvo.

Escalar em Defesa, Velocidade ou na Vida do alvo seria uma regra nova de design, não uma correção.
Os filtros de Monstros usam esta mesma tabela (`Core/Player/SkillTraits.cs`).

**Escala nova.** Só entra com uma habilidade que precise dela e com a decisão escrita no GDD. Aí: um
`EffectKind` (ou campo do efeito) que diga a escala, a estratégia que lê o atributo, a linha nesta
tabela, a linha em `SkillTraits` e um teste. Nunca uma fórmula copiada da planilha ou da referência.

## Ignorar redução de dano

Na referência, "ignora os efeitos de redução de dano" quer dizer: os efeitos *positivos* do alvo que
cortam o dano (Invencível, Defender, escudos, Defesa+, Refletir) não contam; Passivas de redução e os
efeitos negativos que aumentam o dano continuam valendo.

O Sigilos não tem essa regra em nenhuma habilidade, e ela não entra sem uma habilidade que a use.
O mais perto é Ignorar Defesa, que já deixa de fora a Defesa+ (veja Dano); a Passiva de redução e a
Maldição continuam valendo contra ela, como na referência (`DamageTests.IgnoringDefenseActsOnTheFinalDefense`
e `ReductionPassiveStillCountsWhenIgnoringDefense`). Nivelar Vida não é dano, então nada reduz.
