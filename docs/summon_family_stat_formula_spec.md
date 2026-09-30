# Especificação — Sistema de Stats e Famílias de Summons

## 1. Objetivo

Construir um sistema de geração de personagens em que cada **Família** representa um conceito-base de personagem e possui até **5 variações elementares**:

```text
Família
├── Fire
├── Water
├── Wind
├── Light
└── Dark
```

Cada variação deve ser exportada para um **JSON único por família**, já contendo os stats finais calculados.

A intenção é que o jogo não precise recalcular os stats em runtime. O cálculo deve acontecer no editor/generator e o resultado final deve ser persistido no JSON.

---

# 2. Fontes analisadas

## Planilha

`allstats.xlsx`

A planilha contém os stats do jogo de referência e foi utilizada para identificar a relação entre:

- Star Grade
- Role
- HP
- ATK
- DEF
- SPD
- Stats após awakening

## JSONs de exemplo

Foram fornecidas estas variações:

- `dragon_fire.json`
- `dragon_water.json`
- `dragon_light.json`
- `dragon_dark.json`

Esses JSONs definem a estrutura conceitual de uma variação de personagem:

- `id`
- `name`
- `family`
- `element`
- `role`
- `leader`
- `awakening`
- `skills`

Os JSONs de exemplo **não possuem os stats base calculados**. Portanto:

```text
Planilha → fonte para calibração dos stats
JSONs → fonte para estrutura/conteúdo de personagem
```

Não deve ser assumido que um campo inexistente nesses arquivos necessariamente faça parte do contrato final.

---

# 3. Conceito central: Stat Budget / BVP

O sistema deve tratar os stats como um orçamento limitado.

A fórmula base encontrada é:

```text
BVP = (HP / 15) + ATK + DEF + (SPD × 3)
```

Onde:

```text
1 BVP de HP  = 15 HP
1 BVP de ATK = 1 ATK
1 BVP de DEF = 1 DEF
1 BVP de SPD = 3 SPD
```

Portanto, HP e SPD não devem ser comparados diretamente com ATK/DEF.

Exemplo:

```text
150 HP = 10 BVP
10 ATK = 10 BVP
10 DEF = 10 BVP
3 SPD  = 9 BVP
```

A fórmula também pode ser escrita como:

```text
BVP = HPWeightComponent + ATK + DEF + SPDWeightComponent

HPWeightComponent  = HP / HP_WEIGHT
SPDWeightComponent = SPD × SPD_WEIGHT
```

Valores padrão:

```text
HP_WEIGHT  = 15
SPD_WEIGHT = 3
```

---

# 4. Orçamento por Star Grade

A implementação deve utilizar um orçamento de BVP por raridade/Star Grade.

Valores de trabalho calibrados a partir dos dados analisados:

| Star | Base BVP | Awakened BVP |
|------|---------:|-------------:|
| 2★   | 1785     | 1953         |
| 3★   | 1944     | 2118         |
| 4★   | 2115     | 2286         |
| 5★   | 2277     | 2447         |

Esses números devem ser tratados como **parâmetros de balanceamento**, e não como constantes matemáticas imutáveis.

A arquitetura deve permitir alterá-los sem alterar a fórmula.

Estrutura conceitual:

```json
{
  "stat_budget": {
    "2": 1785,
    "3": 1944,
    "4": 2115,
    "5": 2277
  }
}
```

E, separadamente:

```json
{
  "awakened_stat_budget": {
    "2": 1953,
    "3": 2118,
    "4": 2286,
    "5": 2447
  }
}
```

---

# 5. Regra fundamental do sistema

Todo personagem deve fechar aproximadamente o orçamento correspondente à sua Star Grade:

```text
HP / 15 + ATK + DEF + SPD × 3 ≈ BVP
```

Para awakened:

```text
AwakenedHP / 15
+ AwakenedATK
+ AwakenedDEF
+ AwakenedSPD × 3
≈ AwakenedBVP
```

Depois do arredondamento inteiro dos stats, pode existir uma pequena diferença de arredondamento.

O gerador deve sempre validar:

```text
calculated_bvp
target_bvp
difference
```

---

# 6. A Role determina a distribuição

O orçamento não é aumentado quando uma unidade muda de Role.

A Role determina **onde o orçamento é gasto**.

Perfis calibrados usados no gerador:

## 2★

```text
Attack:
  HP  = 29.6%
  ATK = 41.5%
  DEF = 29.6%

Defense:
  HP  = 32.6%
  ATK = 30.4%
  DEF = 37.0%

HP:
  HP  = 35.6%
  ATK = 30.4%
  DEF = 33.0%

Support:
  HP  = 32.6%
  ATK = 31.1%
  DEF = 36.5%
```

## 3★

```text
Attack:
  HP  = 32.0%
  ATK = 38.3%
  DEF = 29.3%

Defense:
  HP  = 34.7%
  ATK = 29.3%
  DEF = 36.0%

HP:
  HP  = 37.3%
  ATK = 30.7%
  DEF = 32.0%

Support:
  HP  = 35.3%
  ATK = 32.0%
  DEF = 32.7%
```

## 4★

```text
Attack:
  HP  = 32.7%
  ATK = 38.2%
  DEF = 29.7%

Defense:
  HP  = 32.7%
  ATK = 30.9%
  DEF = 35.8%

HP:
  HP  = 37.6%
  ATK = 32.4%
  DEF = 30.3%

Support:
  HP  = 35.2%
  ATK = 31.5%
  DEF = 32.7%
```

## 5★

```text
Attack:
  HP  = 32.2%
  ATK = 38.3%
  DEF = 29.4%

Defense:
  HP  = 32.8%
  ATK = 30.6%
  DEF = 37.2%

HP:
  HP  = 37.2%
  ATK = 32.8%
  DEF = 30.3%

Support:
  HP  = 34.9%
  ATK = 32.8%
  DEF = 32.8%
```

As porcentagens acima representam a distribuição do orçamento **sem considerar SPD**.

---

# 7. SPD deve ser tratado separadamente

SPD é diferente dos outros atributos.

Ele tem peso:

```text
SPD × 3
```

e, nos dados analisados, tende a variar menos que HP/ATK/DEF.

Valores de referência para SPD por Role:

| Star | Attack | Defense | HP | Support |
|------|-------:|--------:|---:|--------:|
| 2★ | 110 | 98 | 100 | 98 |
| 3★ | 101 | 98 | 99 | 102.5 |
| 4★ | 102 | 95 | 99 | 102 |
| 5★ | 100 | 100 | 100 | 100 |

A fórmula de SPD deve ser:

```text
SPD =
    RoleSPD
  + FamilySPD
  + ElementSPD
  + ManualSPD
```

Para awakened:

```text
AwakenedSPD = SPD + AwakeningSPD
```

Valor padrão de trabalho:

```text
AwakeningSPD = +1
```

Não usar SPD livremente para aumentar o poder total. Qualquer aumento de SPD consome BVP.

---

# 8. Cálculo completo de HP/ATK/DEF

Primeiro separar o orçamento de SPD:

```text
NonSPD_BVP = BVP - (SPD × SPD_WEIGHT)
```

Depois obter o perfil da Role:

```text
shareHP
shareATK
shareDEF
```

Aplicar os biases opcionais da variação:

```text
weightedHP  = shareHP  × biasHP
weightedATK = shareATK × biasATK
weightedDEF = shareDEF × biasDEF
```

Somar:

```text
TotalWeighted =
    weightedHP
  + weightedATK
  + weightedDEF
```

Distribuir o orçamento:

```text
hpBVP =
    NonSPD_BVP × weightedHP / TotalWeighted

atkBVP =
    NonSPD_BVP × weightedATK / TotalWeighted

defBVP =
    NonSPD_BVP × weightedDEF / TotalWeighted
```

Converter para stats reais:

```text
HP  = hpBVP × HP_WEIGHT
ATK = atkBVP
DEF = defBVP
```

Com os pesos padrão:

```text
HP  = hpBVP × 15
ATK = atkBVP
DEF = defBVP
```

---

# 9. Normalização é obrigatória

O elemento/família pode alterar a distribuição, mas não deve criar BVP grátis.

Exemplo:

```text
Fire:
  biasHP  = 0.97
  biasATK = 1.05
  biasDEF = 0.98
```

Isso significa:

```text
Fire tende a receber mais ATK
Fire tende a receber menos HP/DEF
```

Mas o processo precisa redistribuir e normalizar o orçamento.

Nunca simplesmente:

```text
ATK = baseATK × 1.05
```

e parar aí.

O cálculo correto é:

```text
1. calcular pesos modificados
2. recalcular a participação relativa
3. distribuir o mesmo NonSPD_BVP
4. converter novamente para HP/ATK/DEF
```

Dessa forma:

```text
Role = define identidade estatística principal
Element = muda o viés
Family = pode adicionar personalidade
Star = define o poder bruto total
```

---

# 10. Hierarquia de geração

A ordem recomendada é:

```text
STAR GRADE
    ↓
BVP TOTAL
    ↓
ROLE
    ↓
PERFIL HP / ATK / DEF
    ↓
SPD
    ↓
ELEMENT BIAS
    ↓
FAMILY BIAS
    ↓
NORMALIZAÇÃO
    ↓
STATS FINAIS
```

Regra conceitual:

```text
Star  → quanto poder existe
Role  → como o poder é distribuído
Element → pequena especialização
Family  → personalidade específica
```

---

# 11. Family Bias

Family Bias deve ser pequeno e opcional.

Exemplo:

```text
family_bias:
  hp: 1.00
  atk: 1.00
  def: 1.00
  spd: 0
```

Uma família mais resistente poderia usar:

```text
hp: 1.03
atk: 0.99
def: 1.02
```

Uma família agressiva:

```text
hp: 0.98
atk: 1.03
def: 0.99
```

Mesmo assim, o resultado final precisa ser normalizado para fechar o BVP.

---

# 12. Element Bias

Os cinco elementos podem ter tendências sistemáticas.

Sugestão conceitual:

```text
Fire:
  ATK ↑

Water:
  HP / DEF ↑

Wind:
  SPD ↑

Light:
  DEF / HP ↑

Dark:
  ATK / dano ↑
```

Esses são **vieses de design**, não regras obrigatórias.

Importante:

```text
Element não muda o BVP total.
Element muda a distribuição do BVP.
```

---

# 13. Awakening

Awakening possui pelo menos dois conceitos separados:

```text
Awakening Name
Awakening Stat
```

Nos exemplos:

### Fire

```json
{
  "awakening": {
    "name": "Pyrrhax",
    "stat": "Crit"
  }
}
```

### Water

```json
{
  "awakening": {
    "name": "Maelstra",
    "stat": "Accuracy"
  }
}
```

### Light

```json
{
  "awakening": {
    "name": "Aurelion",
    "stat": "Resistance"
  }
}
```

### Dark

```json
{
  "awakening": {
    "name": "Umbrax",
    "stat": "Accuracy"
  }
}
```

O awakening stat é uma mecânica adicional e não deve ser automaticamente confundido com HP/ATK/DEF/SPD.

Caso o projeto queira que despertar também aumente stats básicos, esse aumento deve ser aplicado contra um **Awakened BVP próprio**, não simplesmente multiplicando todos os stats.

---

# 14. Leader Skill

Os JSONs de exemplo usam:

```json
"leader": {
  "stat": "Attack",
  "value": 0.18
}
```

ou:

```json
"leader": {
  "stat": "Health",
  "value": 0.20
}
```

ou:

```json
"leader": {
  "stat": "Defense",
  "value": 0.25
}
```

ou:

```json
"leader": {
  "stat": "CritDamage",
  "value": 0.30
}
```

O valor é armazenado como decimal:

```text
18% = 0.18
20% = 0.20
25% = 0.25
30% = 0.30
```

Leader Skill não deve entrar no BVP dos stats básicos.

---

# 15. Skills

Os exemplos usam um array:

```json
"skills": [
  { ... },
  { ... },
  { ... }
]
```

Cada skill pode conter:

```text
name
cooldown
effects
levels
passive
```

Nem todos os campos precisam existir em todas as skills.

---

# 16. Estrutura de Effect

Os efeitos observados usam objetos flexíveis.

Exemplo:

```json
{
  "kind": "Damage",
  "target": "AllEnemies",
  "power": 3.15
}
```

Status:

```json
{
  "kind": "Status",
  "target": "AllEnemies",
  "status": "Burn",
  "chance": 0.5,
  "turns": 2
}
```

Cura:

```json
{
  "kind": "Heal",
  "target": "AllAllies",
  "power": 0.2
}
```

Shield:

```json
{
  "kind": "Shield",
  "target": "AllAllies",
  "power": 0.1,
  "turns": 2
}
```

Outros exemplos presentes:

```text
Impeto
Drain
Burn
Stun
Curse
Recovery
Cooldown
```

O generator deve permitir inserir `effects` como JSON livre para não limitar o design das skills.

---

# 17. Skill Levels

Os exemplos usam:

```json
"levels": [
  {"kind": "Damage", "value": 0.10},
  {"kind": "Damage", "value": 0.10},
  {"kind": "Cooldown", "value": 1}
]
```

Isso representa modificações aplicadas a cada melhoria da skill.

O editor deve permitir um array arbitrário:

```json
"levels": [
  {
    "kind": "Damage",
    "value": 0.1
  },
  {
    "kind": "Cooldown",
    "value": 1
  }
]
```

---

# 18. Passive Skill

Os exemplos também usam:

```json
"passive": {
  "kind": "BurnOnHit",
  "value": 0.2
}
```

Uma skill pode ser uma passiva em vez de possuir cooldown/effects convencionais.

Não forçar todos os campos para todas as skills.

---

# 19. Família unificada

O objetivo final é:

```text
1 família = 1 JSON
```

Em vez de:

```text
dragon_fire.json
dragon_water.json
dragon_wind.json
dragon_light.json
dragon_dark.json
```

usar:

```text
dragon_family.json
```

Formato:

```json
{
  "family_id": "dragon",
  "base_name": "Dragon",
  "star_grade": 5,

  "variations": {
    "fire": {},
    "water": {},
    "wind": {},
    "light": {},
    "dark": {}
  }
}
```

---

# 20. Contrato recomendado do JSON final

Exemplo estrutural:

```json
{
  "family_id": "dragon",
  "base_name": "Dragon",
  "star_grade": 5,

  "variations": {
    "fire": {
      "variation_id": "dragon_fire",
      "name": "Fire Dragon",
      "element": "Fire",
      "role": "Attack",

      "stats": {
        "hp": 9608,
        "atk": 753,
        "def": 583,
        "spd": 100
      },

      "awakened_stats": {
        "hp": 10000,
        "atk": 810,
        "def": 620,
        "spd": 101
      },

      "leader": {
        "stat": "Attack",
        "value": 0.18
      },

      "awakening": {
        "name": "Pyrrhax",
        "stat": "Crit"
      },

      "skills": [
        {
          "name": "Claw",
          "effects": [],
          "levels": []
        },
        {
          "name": "Breath",
          "cooldown": 4,
          "effects": [],
          "levels": []
        },
        {
          "name": "Dragonfire",
          "passive": {
            "kind": "BurnOnHit",
            "value": 0.2
          }
        }
      ]
    }
  }
}
```

Os valores numéricos acima são apenas exemplo estrutural. O generator deve recalculá-los com base nos parâmetros atuais.

---

# 21. Regras de implementação

## Regra 1 — Nunca calcular stats manualmente

O agente/editor deve calcular:

```text
BVP
→ SPD
→ NonSPD_BVP
→ Role shares
→ Element/Family bias
→ normalização
→ HP/ATK/DEF
```

## Regra 2 — Nunca quebrar o orçamento

Depois do arredondamento:

```text
abs(calculatedBVP - targetBVP)
```

deve permanecer dentro de uma tolerância pequena.

## Regra 3 — Role não adiciona poder

Attack, Defense, HP e Support recebem o mesmo orçamento de Star, mas o distribuem de maneira diferente.

## Regra 4 — Elemento não adiciona poder

Element bias altera distribuição, mas deve ser normalizado.

## Regra 5 — SPD custa mais

```text
1 SPD = 3 BVP
```

Portanto +10 SPD consome:

```text
30 BVP
```

## Regra 6 — HP tem escala própria

```text
15 HP = 1 BVP
```

## Regra 7 — Leader e Skills não entram no BVP básico

O BVP é exclusivamente dos stats básicos:

```text
HP
ATK
DEF
SPD
```

## Regra 8 — Awakening deve ser separado

Stats despertados possuem orçamento próprio.

## Regra 9 — Não inventar campos

O contrato do JSON deve seguir os campos efetivamente adotados pelo projeto.

---

# 22. Validação automática

Cada variação deve gerar algo equivalente a:

```json
{
  "stats": {
    "hp": 9608,
    "atk": 753,
    "def": 583,
    "spd": 100
  },
  "validation": {
    "calculated_bvp": 2277,
    "target_bvp": 2277,
    "difference": 0
  }
}
```

`validation` pode ser usado internamente no editor, mas pode ser removido do JSON de produção.

Também validar:

```text
HP >= 0
ATK >= 0
DEF >= 0
SPD > 0
BVP dentro da tolerância
```

---

# 23. Outliers dos dados

A planilha possui registros anômalos/inconsistentes.

Um exemplo identificado foi um registro com ATK extremamente alto em relação à faixa da sua categoria, acompanhado de mudança muito grande após awakening.

Portanto:

```text
não usar média simples de todos os registros
```

O processo de calibração deve preferir:

```text
mediana
percentis
média aparada
remoção de outliers
```

O objetivo é capturar a distribuição normal do jogo de referência, não reproduzir erros ou casos extremos isolados.

---

# 24. O que não deve ser feito

Não fazer:

```text
Fire = base × 1.30 ATK
Water = base × 1.30 HP
```

sem normalizar.

Não fazer:

```text
Attack = mais BVP
Defense = menos BVP
```

apenas por causa da Role.

Não fazer:

```text
Awakening = todos os stats × 1.20
```

sem verificar o orçamento awakened.

Não usar:

```text
HP + ATK + DEF + SPD
```

como soma direta, porque os atributos estão em escalas diferentes.

Não criar cinco cópias independentes da família quando o objetivo é um JSON único.

---

# 25. Algoritmo de referência

Pseudocódigo:

```text
function GenerateStats(star, role, elementBias, familyBias, spdOverrides, awakened):

    budget =
        awakened
            ? AWAKENED_BVP[star]
            : BASE_BVP[star]

    roleProfile = ROLE_PROFILE[star][role]

    spd =
        ROLE_SPD[star][role]
        + familySPD
        + elementSPD
        + manualSPD

    if awakened:
        spd += awakeningSPD

    nonSPD_BVP =
        budget - (spd * SPD_WEIGHT)

    weightedHP =
        roleProfile.hp
        * elementBias.hp
        * familyBias.hp

    weightedATK =
        roleProfile.atk
        * elementBias.atk
        * familyBias.atk

    weightedDEF =
        roleProfile.def
        * elementBias.def
        * familyBias.def

    totalWeighted =
        weightedHP
        + weightedATK
        + weightedDEF

    hpBVP =
        nonSPD_BVP
        * weightedHP
        / totalWeighted

    atkBVP =
        nonSPD_BVP
        * weightedATK
        / totalWeighted

    defBVP =
        nonSPD_BVP
        * weightedDEF
        / totalWeighted

    HP =
        round(hpBVP * HP_WEIGHT)

    ATK =
        round(atkBVP)

    DEF =
        round(defBVP)

    return {
        HP,
        ATK,
        DEF,
        SPD
    }
```

---

# 26. Princípio de design

O sistema deve produzir personagens que tenham:

```text
mesmo nível de poder bruto
+
distribuições diferentes
+
identidades diferentes
+
funções diferentes
```

Exemplo:

```text
Fire Attack
→ maior pressão ofensiva

Water Defense
→ maior sobrevivência

Wind
→ velocidade/utilidade

Light Support
→ sustain/defesa

Dark Attack
→ dano especializado
```

A diferença de gameplay deve vir principalmente de:

```text
skills
passivas
leader skill
awakening
elemento
```

e não simplesmente de um orçamento de stats maior.

---

# 27. Relação com os Dragões fornecidos

O material fornecido já exemplifica quatro combinações:

```text
Fire  → Attack
Water → Defense
Light → Support
Dark  → Attack
```

Fire possui leader de Attack +18% e awakening `Pyrrhax / Crit`.

Water possui leader de Health +20% e awakening `Maelstra / Accuracy`.

Light possui leader de Defense +25% e awakening `Aurelion / Resistance`.

Dark possui leader de CritDamage +30% e awakening `Umbrax / Accuracy`.

Os quatro também demonstram que skills podem compartilhar a estrutura básica de dano, status, cura, shield, cooldown, níveis e passivas, mas mudar de função entre elementos.

---

# 28. Resultado esperado do agente

O agente responsável pela criação das famílias deve conseguir receber algo próximo de:

```text
Family:
  Gunslinger

Star:
  5

Element:
  Fire

Role:
  Attack

Element Bias:
  HP  0.97
  ATK 1.05
  DEF 0.98

Leader:
  Attack +18%

Awakening:
  Pyrrhax
  Crit

Skills:
  ...
```

e produzir automaticamente:

```text
Stats calculados
+
Awakened Stats calculados
+
Leader
+
Awakening
+
Skills
+
demais campos do contrato
```

dentro de:

```text
gunslinger_family.json
```

---

# 29. Regra de ouro

A fórmula que governa todo o sistema é:

```text
HP / 15 + ATK + DEF + SPD × 3 = BVP
```

E a arquitetura de geração é:

```text
STAR
  ↓
BVP
  ↓
ROLE
  ↓
DISTRIBUIÇÃO
  ↓
ELEMENT/FAMILY BIAS
  ↓
NORMALIZAÇÃO
  ↓
STATS FINAIS
```

O restante do personagem — skills, passivas, awakening e leader — define sua identidade e função, mas não deve receber BVP gratuitamente nem ser usado para alterar o orçamento de stats básicos.

---

## 30. Observação sobre calibração futura

A fórmula deve ser implementada como um sistema parametrizado.

Não hardcode a lógica em vários lugares.

Centralizar:

```text
HP_WEIGHT
SPD_WEIGHT
BVP por Star
Role Profiles
SPD por Role
Element Bias
Family Bias
Awakening Budget
```

Isso permite recalibrar o jogo inteiro alterando parâmetros, sem reescrever o algoritmo.
