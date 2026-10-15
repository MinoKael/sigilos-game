# Estudo: experiência, Campanha e primeira vitória contra a referência

Data: 2026-10-15. Nada do jogo foi alterado por este estudo: é só leitura e comparação.

Fontes (como estavam nesta data):

- Tabela de experiência por nível e estrela: <https://summonerswarskyarena.info/experience-table/> ("site XP").
- Cenários, uma página por área: <https://summonerswar.fandom.com/wiki/Scenario> e as 13 páginas de área ligadas nela ("wiki").
- O jogo: `Core/Progression/Leveling.cs` (tabela), `Data/stages.json` (as 50 fases), `Core/Progression/Campaign.cs`
  (o que a vitória dá) e os atributos dos inimigos tirados do próprio `BattleFactory` (inimigo de cada fase já com
  estrelas, nível e `scale`).

Limites do que foi lido:

- A página do wiki lista 20 áreas, mas só 13 têm dados completos (Garen Forest a Charuka Remains); as outras 7 não
  têm tabela de experiência na página principal.
- As recompensas de primeira vitória do wiki são as do modo Normal. As de Hard e Hell estão na página "Challenges",
  que não foi lida.
- O wiki é mantido por jogadores: a página principal e as páginas de área discordam em alguns prêmios (ver seção 4).
- Os atributos de inimigo do wiki só existem em Faimon Volcano, Ferun Castle e Charuka Remains (poucas linhas).

---

## 1. Tabela de experiência por nível: igual ao site, vezes 3

Cada célula da tabela do jogo (`Leveling.Table`) é **exatamente 3 × a do site**, nas 6 estrelas e em todos os níveis
(conferido célula a célula; 14, 19, 24, 29, 34 e 39 entradas, o mesmo número de níveis).

| Estrelas | Nível máximo | Site: XP total até o máximo | Jogo: XP total até o máximo | Razão |
|---|---|---|---|---|
| 1★ | 15 | 15.120 | 45.360 | 3,0 |
| 2★ | 20 | 35.822 | 107.466 | 3,0 |
| 3★ | 25 | 82.182 | 246.546 | 3,0 |
| 4★ | 30 | 189.750 | 569.250 | 3,0 |
| 5★ | 35 | 446.647 | 1.339.941 | 3,0 |
| 6★ | 40 | 1.005.420 | 3.016.260 | 3,0 |

Então, **o custo de subir de nível não difere em forma**: só a escala (×3) muda. O que decide se a progressão está
"igual" é quanto XP as vitórias dão em relação a essa escala. Isso é a seção 2.

Observações do código (não são divergências):

- Evoluir mantém o nível, e de cada estrela acima da natural só se sobem os 5 últimos níveis (comentário em
  `Leveling.cs`): um 3★ natural chega ao 6★ nível 40 com 858.795 de experiência. Isso é regra do jogo, não está no site.
- Cada Essência vira 10 de experiência (`ExperiencePerEssence`). O wiki não tem equivalente: lá a experiência só vem
  de lutar ou de itens.

## 2. Experiência que cada homúnculo recebe por vitória

Regra de cada lado:

| | Referência (wiki) | Jogo |
|---|---|---|
| Como divide | O total da fase é **dividido** entre os monstros que lutaram (até 4); menos monstros, mais para cada | **Cada** monstro da equipe recebe o valor inteiro da fase (`Leveling.GiveExperience`) |
| Muda com | Área, dificuldade (Normal/Hard/Hell) e número da fase 1 a 7 | Só a fase (1 a 50): `experience` em `stages.json` |
| Derrota | Dá menos, conforme o avanço | Não dá nada |
| Conta | — | A conta recebe o mesmo valor |

### 2.1 Quanto vale uma vitória (por monstro, fase 6 de cada área, Normal / Hard / Hell)

| Área | Nível dos inimigos N / H / Hell | XP por monstro N | H | Hell |
|---|---|---|---|---|
| Garen Forest | 3 / 18 / 40 | 377 | 586 | 960 |
| Mt. Siz | 7 / 20 / 40 | 392 | 608 | 960 |
| Kabir Ruins | 13 / 24 / 40 | 539 | 727 | 1.080 |
| Mt. White Ragon | 16 / 26 / 40 | 630 | 843 | 1.680 |
| Telain Forest | 19 / 27 / 40 | 726 | 952 | 1.320 |
| Hydeni Ruins | 21 / 29 / 40 | 824 | 1.080 | 1.440 |
| Tamor Desert | 24 / 31 / 40 | 872 | 1.119 | 1.440 |
| Vrofagus Ruins | 27 / 33 / 40 | 919 | 1.163 | 1.440 |
| Faimon Volcano | 29 / 34 / 40 | 1.040 | 1.290 | 2.184 |
| Aiden Forest | 32 / 36 / 41 | 1.101 | 1.346 | 2.236 |
| Ferun Castle | 35 / 40 / 45 | 1.200 | 1.493 | 1.734 |
| Mt. Runar | 37 / 42 / 47 | 1.231 | 1.524 | 1.760 |
| Charuka Remains | 39 / 44 / 49 | 1.275 | 1.560 | 1.794 |

(Valores "XP per Monster" do wiki. Chefe, fase 7, paga bem menos: de 203 a 759 em Normal e de 700 a 1.595 em Hell.
Note que o wiki usa denominadores diferentes por dificuldade, então o valor por monstro é o que a página publica,
não uma divisão minha.)

Jogo, por fase (XP por monstro, mana, nível e estrelas dos inimigos):

| Fase | Estrelas / nível dos inimigos | `scale` | XP | Mana |
|---|---|---|---|---|
| 1 | 3★ / 1 | 1,0 | 550 | 2 |
| 5 | 3★ / 14 | 1,25 | 1.900 | 2 |
| 10 | 4★ / 25 | 1,0 | 3.500 | 3 |
| 15 | 5★ / 24 | 0,75 | 5.200 | 4 |
| 20 | 5★ / 23 | 0,9 | 7.600 | 5 |
| 25 | 5★ / 35 | 0,85 | 8.350 | 5 |
| 30 | 6★ / 40 | 0,8 | 9.100 | 5 |
| 35 | 6★ / 42 | 0,7 | 9.850 | 6 |
| 40 | 6★ / 50 | 0,68 | 10.600 | 6 |
| 45 | 6★ / 52 | 0,7 | 11.350 | 6 |
| 50 | 6★ / 60 | 0,53 | 12.100 | 6 |

O XP sobe de 550 a 12.100 e nunca cai de uma fase para a seguinte (passos de +150 a +450).

### 2.2 O que isso dá em "vitórias para subir um nível" (o número que importa)

Como as tabelas têm escalas diferentes (×3), a comparação justa é quantas vitórias um monstro precisa para subir
**um nível**, na altura do nível dos inimigos da fase, com a tabela da própria fonte:

| Jogo: fase (nível do inimigo, XP) | 3★ | 4★ | 5★ | 6★ |
|---|---|---|---|---|
| 1 (nível 1, 550) | 3,6 | 4,3 | 5,2 | 6,3 |
| 10 (nível 25, 3.500) | 8,5 | 11,7 | 14,0 | 16,9 |
| 20 (nível 23, 7.600) | 3,4 | 4,1 | 4,9 | 6,0 |
| 30 (nível 40, 9.100) | 3,3 | 7,7 | 18,9 | 36,9 |
| 40 (nível 50, 10.600) | 2,8 | 6,6 | 16,2 | 31,7 |
| 50 (nível 60, 12.100) | 2,5 | 5,8 | 14,2 | 27,8 |

(Acima do nível máximo da estrela usa-se o último nível da tabela. "36,9" é o último nível de um 6★.)

| Referência: área (nível Normal, XP) | 3★ | 4★ | 5★ | 6★ |
|---|---|---|---|---|
| Garen (3, 377) | 2,2 | 2,7 | 3,2 | 3,8 |
| Kabir (13, 539) | 4,9 | 5,9 | 7,0 | 8,5 |
| Hydeni (21, 824) | 8,3 | 9,9 | 11,9 | 14,3 |
| Tamor (24, 872) | 11,4 | 13,7 | 16,4 | 19,8 |
| Faimon (29, 1.040) | MAX | 22,6 | 27,0 | 32,6 |
| Charuka (39, 1.275) | MAX | MAX | MAX | 87,9 |

Leitura:

- **Começo (fases 1 a 10 / Garen a Kabir): equivalente.** 3,6 a 6,3 vitórias por nível no jogo contra 2,2 a 3,8 na
  referência. O jogo pede um pouco mais; é o tutorial, onde o nível é 1.
- **Meio (fases 15 a 25 / Hydeni a Tamor): o jogo é 2 a 3 vezes mais generoso.** Fase 20: 6★ sobe um nível em 6
  vitórias; Hydeni na referência pede 14,3. Aqui, nível de monstro "gratuito" aparece mais cedo que no original.
- **Fim (fases 30 a 50 / Faimon a Charuka): o jogo é de 2,5 a 3 vezes mais rápido.** O último nível de um 6★
  custa 27,8 a 36,9 vitórias na fase 30-50; na referência, 87,9 em Normal (Charuka) e 62 em Hell.
- **Conjunto inteiro:** 6★ do 1 ao 40 = 3.016.260 de XP. Na melhor fase do jogo (12.100) são **249 vitórias**
  (1.494 de Mana). Na referência, 1.005.420 / 1.794 (Hell de Charuka) são **560 vitórias** (2.800 de energia, com 5
  por fase). O jogo leva cerca de **2,2 vezes menos vitórias** e **1,9 vez menos custo**.
- **A Essência é um segundo caminho barato.** 3.016.260 de XP = 301.626 de Essência. A fase 50 paga 6.475 de
  Essência por vitória: **47 vitórias**. Isso é o que realmente decide a velocidade, e não existe na referência.

### 2.3 Pontos que merecem decisão (não corrigi nada)

1. **O tempo para chegar ao 6★ nível 40 é bem menor que na referência.** Se a intenção é "demorar", a alavanca é a
   Essência (10 XP por unidade) e o XP das fases altas, não a tabela por nível (que já é fiel ×3).
2. **A curva do nível do inimigo não é monótona.** O nível dos inimigos cai em 12 passos (fase 13: 28→22; 16: 24→22;
   17: 22→21; 26: 35→30; 28: 33→31; 29: 31→30; 31: 40→37; 33: 41→36; 34: 36→35; 37: 44→40; 41: 50→49; 42: 49→48).
   A `scale` compensa (0,53 a 1,3), mas o XP é sempre crescente: nas fases 13 a 19 e 26 a 29 o jogo paga mais por
   inimigos de nível menor.
3. **XP cheio para cada monstro** (e não dividido) torna equipes grandes mais fortes por si só; a referência divide
   para equilibrar. Com 5 monstros na equipe, o jogo paga 5 vezes o XP total da luta.
4. **Derrota não dá XP no jogo.** Na referência, perder dá parte. É consistente com "a derrota não custa Mana".

## 3. Dificuldade: nível e atributos dos inimigos

### Nível

| | Referência | Jogo |
|---|---|---|
| Progressão | 13 áreas × 3 modos × 7 fases. Normal 3→39, Hard 18→44, Hell 40→49 (e chefe +1) | 50 fases, um modo só: 1→60 |
| Estrelas dos inimigos | não listadas | 3★ nas fases 1 a 8, 4★ em 9 a 14, 5★ em 15 a 25, 6★ de 26 em diante |
| Custo por luta | 3 de energia (fases 1 a 6) e 4 (chefe) em Normal; 4/5 em Hard; 5/6 em Hell | 2 a 6 de Mana (só na vitória), crescente por fase |

O jogo cobre, em 50 fases, o que o original cobre em 273: ele **não tem Hard nem Hell**, então o equivalente é
estreito. A fase 50 (nível 60, `scale` 0,53) é mais nível que Hell de Charuka (49), mas com poder reduzido pela
`scale`.

### Atributos (inimigo mais forte da última onda)

Do wiki (só onde a página publica a linha):

| Fonte | Nível | PV | Ataque | Defesa | Velocidade |
|---|---|---|---|---|---|
| Faimon Normal, chefe | 28 | 4.005 | 528 | 261 | 121 |
| Faimon Hard, chefe | 34 | 6.195 | 816 | 402 | 132 |
| Faimon Hell, chefe | 40 | 11.880 | 1.563 | 771 | 143 |
| Ferun Hard, chefe | 39 | 14.700 | 774 | 781 | 117 |
| Charuka Hell, chefe | 44 | 24.105 | 2.660 | 1.386 | 149 |

Do jogo:

| Fase | Estrelas / nível | PV | Ataque | Defesa | Velocidade |
|---|---|---|---|---|---|
| 10 | 4★ / 25 | 8.849 | 427 | 430 | 95 |
| 20 | 5★ / 23 | 35.426 | 569 | 486 | 98 |
| 30 | 6★ / 40 | 14.367 | 605 | 685 | 99 |
| 40 | 6★ / 50 | 49.458 | 987 | 694 | 101 |
| 50 | 6★ / 60 | 52.028 | 742 | 842 | 103 |

O que se vê:

- **Velocidade:** inimigos do jogo ficam em 95 a 103; na referência 107 a 149 e sobe com o nível. Os inimigos do jogo
  são **10 a 45% mais lentos**. É coerente com `TurnOrder` ("Velocidade 100 age uma vez por rodada"), mas é onde mais
  se afasta do original.
- **Ataque:** no mesmo nível, o ataque do jogo é de **50 a 70%** do da referência (por exemplo, nível 40: 605 contra
  1.563 do Faimon Hell). O inimigo bate mais fraco.
- **PV:** o jogo tem PV mais alto nos chefes de fim de região (fases 20, 40 e 50: 35 mil a 52 mil), mas não nas fases
  comuns (12 a 18 mil, perto de Faimon Hard/Ferun). O Hell de Charuka chega a 24 mil num chefe de fase 1.
- **Resistência** não foi comparada: o jogo não a lista por inimigo.

Conclusão: lutas do jogo são mais **longas e menos perigosas** que as da referência (mais PV, menos ataque, menos
velocidade). Isso combina com o XP generoso: o jogador fica mais tempo na luta, mas morre menos.

## 4. Recompensas de primeira vitória

### Referência (wiki, Normal, uma por área)

| Área | Primeira vitória |
|---|---|
| Garen Forest | Elven Ranger (Luz, 3★) ×1 |
| Mt. Siz | Magic Knight (Água, 4★) ×1 |
| Kabir Ruins | 6 runas Magic Energy (3★) |
| Mt. White Ragon | 30.000 Mana Stone + 30 energia (página da área) / 100.000 Mana Stone + Mystical Scroll (principal) |
| Telain Forest | 6 runas Magic Blade (3★) |
| Hydeni Ruins | Rainbowmon 2★ ×2 (principal: 3★ ×2 + EXP Booster de 1 dia) |
| Tamor Desert | 6 runas Rare Fatal (4★) (principal: 100 energia + Mystical Scroll) |
| Vrofagus Ruins | 100 energia + EXP Booster 1 dia (principal: 6 runas Rare Energy 4★) |
| Faimon Volcano | Scroll of Light & Darkness ×1 |
| Aiden Forest | Ticket de runa Hero 4★/5★ ×1 |
| Ferun Castle | 3 King Angelmon (principal: 100 Summoning Stones) |
| Mt. Runar | 100 Summoning Stones (principal: 6 runas Rare 6★ +15 predeterminadas) |
| Charuka Remains | Legendary Scroll ×1 |

As duas páginas discordam em 6 das 13 áreas; o que vale é o da página de cada área (a fonte primária).

### Jogo (`Campaign.ApplyVictory`, `stages.json`)

Por região (soma da primeira vitória das fases):

| Região | Pergaminhos | Essência | Lendário | Núcleos de Infusão | Mana por vitória |
|---|---|---|---|---|---|
| Planície dos Menires (1–20) | 28 | 63.000 | 1 | 2 | 2 a 5 |
| Arquipélago Afogado (21–40) | 28 | 183.000 | 1 | 2 | 5 e 6 |
| Cidadela do Selo Partido (41–50) | 14 | 136.500 | 1 | 2 | 6 |
| **Total das 50 fases** | **70** | **382.500** | **3** | **6** | |

Mais, em toda primeira vitória, uma runa (até 4★) garantida; em vitória repetida, 50%.

### Comparação

- **Quantidade:** 50 prêmios contra 13 (e 39 de Hard/Hell que não li). O jogo dá prêmio pequeno e frequente; a
  referência dá prêmio grande e raro (um monstro 3★/4★ ou 100 pergaminhos).
- **Pergaminhos de invocação:** 70 contra 2 Mystical + 1 Light & Darkness + 1 Legendary + 100 Summoning Stones (moeda
  de invocação, o valor de cada uma não foi medido aqui) nas 13 áreas Normal. Em pergaminhos, o jogo dá bem
  mais; a comparação com as Summoning Stones depende do preço de cada invocação.
- **Essência:** o jogo paga 382.500 só na primeira vitória; é o suficiente para ~3.8 milhões de XP, ou seja, **um 6★
  nível 40 inteiro** (3.016.260) pela Campanha. Isso é muito acima da referência (sem equivalente).
- **Lendário:** 3 ao longo de 50 fases (20, 40, 50) contra 1 Legendary Scroll (Charuka, a 13ª área).
- **Monstros:** a referência dá monstros 3★ e 4★ de nome; o jogo dá runas e pergaminhos, e 25% de chance de um
  monstro raridade 2 por vitória.

## 5. Resumo em uma tabela

| Item | Referência | Jogo | Veredicto |
|---|---|---|---|
| Tabela de XP por nível | site XP | exatamente 3 × | Fiel. Escala ×3 intencional |
| XP por vitória, começo | 377 a 540 | 550 a 1.900 | Parecido em "vitórias por nível" (3,6 a 6,3 contra 2,2 a 3,8) |
| XP por vitória, meio | 700 a 900 | 5.200 a 8.350 | Jogo 2 a 3× mais generoso |
| XP por vitória, fim | 1.000 a 1.800 | 9.100 a 12.100 | Jogo 2,5 a 3× mais rápido |
| Divisão do XP | dividido pela equipe | cheio por monstro | Diferente (regra) |
| 6★ do 1 ao 40 | 560 vitórias Hell (2.800 de energia) | 249 vitórias (1.494 de Mana) | Jogo 2,2× mais rápido |
| Essência → XP | não existe | 10 XP por Essência | Caminho extra, decide o ritmo |
| Ataque dos inimigos | base | 50 a 70% | Inimigos mais fracos |
| Velocidade dos inimigos | 107 a 149 | 95 a 103 | Inimigos mais lentos |
| Modos de dificuldade | 3 | 1 | Diferente |
| Primeira vitória | 13 prêmios grandes (Normal) | 50 pequenos | Diferente; jogo mais generoso em Essência |
| Curva de nível dos inimigos | sobe sempre | cai em 12 fases | Irregular (compensada pela `scale`) |

## 6. Se for ajustar (nada foi mudado)

Em ordem de efeito para o ritmo:

1. Decidir o alvo: "6★ nível 40 em quantas vitórias". Hoje 249 na melhor fase e 47 por Essência.
2. Ajustar `experience` por fase (ou a conversão Essência→XP) em vez da tabela por nível: a tabela já é fiel.
3. Se quiser a mesma lógica da referência, reduzir o XP das fases 15 a 25 (onde o desvio é maior) e dividir o XP
   entre a equipe (hoje cheio).
4. Revisar o nível dos inimigos nas 12 quedas de nível (seção 2.3): o XP deveria acompanhar o nível ou o `scale`
   deveria cobrir a diferença.
5. Velocidade dos inimigos fica em ~100 por desenho; se quiser lutas mais próximas da referência, é a alavanca
   de dificuldade mais barata de testar.

O que respeitar: a regra do projeto de que os números de balanceamento são do jogador (`Data/stages.json`,
`stat_model.json`). Este estudo só mede; qualquer mudança passa por `dotnet run --project Tests -- --simulate`.
