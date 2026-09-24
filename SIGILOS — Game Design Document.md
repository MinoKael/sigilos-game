# SIGILOS — Game Design Document

Sep 23, 2026 · @Mikael

> **Revisão de 24/09/2026, depois dos primeiros testes do MVP.** O Erudito e todo o Grimório (páginas, Formas, Círculos, Ressonância) saíram do jogo. A base mecânica e visual passa a ser Summoners War: nível de 1 a 40 por invocação, Despertar com nome próprio e estrelas roxas, runas de 6 espaços. O Éter ficou escasso (só Glifo e inimigo derrubado geram) e é exclusivo do modo manual: o automático nunca o gasta. O jogo ganhou a tela de Monstros e o Compêndio, que explica Glifos, elementos, efeitos, runas e regras.

> **Segunda revisão de 24/09/2026: runas e atributos iguais aos de Summoners War.** Runas e atributos das invocações são a parte mais importante do jogo, então seguem Summoners War em tudo: runas de 1 a 6 estrelas, raridade pelo número de subatributos, melhora até +15 com 4 subatributos, atributo inato, as tabelas de números de lá, custo para tirar runa, Pedra de Afiar e Gema Encantada, e os 16 conjuntos (dois por Glifo). A única diferença: **a melhora de runa nunca falha**; em troca, cada nível custa o preço médio de Summoners War contando as falhas, e o Pó de Sigilo ficou escasso. Os atributos passaram para a escala de lá (uma 5★ no nível 40 tem cerca de 9 mil de Vida), com Crítico 15%, Dano crítico 50%, Resistência 15% e Precisão 0% de base, a curva de Defesa de lá, a Liderança sobre a base e o Despertar com os bônus de lá.

## 1. Visão geral

**Sigilos** (título provisório) é um gacha de fantasia para um jogador, offline e sem loja: você coleciona criaturas, grava sigilos nelas e comanda batalhas por turnos em que as magias são montadas símbolo por símbolo.

Em uma frase: Summoners War: Sky Arena como base mecânica e visual (coleção, nível, Despertar, runas, barra de ataque), o recurso compartilhado de Epic Seven e o ritmo de um AFK. A arte de personagem é rabiscada por escolha visual; o mundo é fantasia levada a sério.

**Objetivo do projeto.** Um jogo que você mesmo abra todo dia por 5 a 15 minutos, durante meses, pelo prazer de montar combinações e ver o time crescer. Critério de sucesso: jogar a versão 1.0 por 90 dias seguidos sem se obrigar.

| Item | Decisão |
| --- | --- |
| Jogadores | 1, offline, save local |
| Plataforma | PC primeiro; Android depois, com o mesmo código |
| Sessão típica | 5 a 15 min, 1 ou 2 vezes por dia |
| Equipe | 1 pessoa, cerca de 8 a 10 h por semana (premissa a confirmar) |
| Tamanho da 1.0 | 40 invocações (8 famílias em 5 elementos), 3 regiões (60 fases), Torre de 60 andares |
| Prazo estimado | MVP jogável em cerca de 3 meses; 1.0 em 9 a 12 meses |

**O que o jogo não é.** Sem monetização, servidor, PvP online, stamina, eventos com prazo, cutscenes ou dublagem. Esses sistemas existem para reter pagantes e custam meses; num projeto pessoal só atrapalham.

## 2. Pilares de design

Toda decisão de sistema passa por estes quatro filtros; o que não serve a nenhum deles sai do escopo.

1. **Magia é linguagem.** Habilidades, runas e invocação são feitos dos mesmos oito Glifos. A profundidade vem de combinar poucas peças, não de acumular sistemas.
2. **Velocidade é tática.** Quem age primeiro e quem é atrasado decide a luta; a barra de Ímpeto é a principal camada de estratégia.
3. **Respeite o tempo.** O progresso acontece com o jogo fechado. Nada pune quem ficou dias sem abrir.
4. **Rabisco é estilo, não tema.** A arte é rápida de propósito para o conteúdo crescer; o mundo continua sendo fantasia épica. Nenhum desenho leva mais de 15 minutos.

## 3. O que pegar de cada referência

De cada jogo entra só o que serve aos pilares; o resto é cortado de propósito para caber no tempo de uma pessoa.

| Referência | O que entra | O que fica de fora |
| --- | --- | --- |
| Summoners War: Sky Arena (base mecânica e visual) | Barra de ataque por Velocidade; nível de 1 a 40 por monstro; painéis escuros com moldura dourada; runas em 6 espaços com conjuntos de 2 e 4 peças; famílias de monstros em 5 elementos; Despertar; duplicatas que sobem habilidades; habilidade de líder; masmorras que soltam conjuntos específicos; masmorras secretas por família; arena contra defesa controlada pela IA | Evolução de estrelas com monstros de sacrifício; chance de falha na melhora de runa; conjuntos de runa da Fenda; guerra de guildas, cerco e PvP em tempo real; cristais e pacotes |
| Epic Seven | Almas compartilhadas que turbinam habilidades (viram o Éter); recargas de habilidade; heróis com nome e personalidade; Abismo como torre de desafio | Equipamento separado das runas; artefatos; cutscenes; Labirinto; PvP em tempo real |
| AFK Arena e AFK Journey | Recompensas ociosas; nível compartilhado entre heróis; batalha automática com time preparado; sessões curtas sem stamina | Dezenas de moedas e menus; eventos com prazo; pacotes pagos |
| Tormenta (RPG) | Escolas de magia viram Glifos; pagar mana extra por aprimoramentos | Nomes, deuses, lugares e textos do cenário; grimório e círculos de custo (saíram na revisão de 24/09) |

Summoners War e Epic Seven se sobrepõem muito (ordem de turnos, elementos). Summoners War é a base; de Epic Seven fica o recurso compartilhado (Éter).

O mundo e os nomes são originais. Assim o jogo pode ser mostrado a amigos sem nenhuma dúvida de propriedade intelectual.

## 4. Mundo e tom

Neste mundo tudo o que existe foi escrito com oito Glifos primordiais, e conjurar é redesenhá-los. Um Conjurador não cria criaturas: grava um sigilo que as prende a ele por contrato. Você é um Conjurador recém-sagrado de uma ordem quase extinta.

**Conflito.** O Silêncio, uma força sem forma, está desfazendo os Glifos. Onde ele passa, a magia falha e as criaturas se tornam Profanadas, selvagens e hostis. Cada região tem um Conjurador rival que acredita que o Silêncio é a paz.

**Regiões.** Cada região muda uma regra de batalha, o que dá variedade sem exigir arte nova.

| Região | Paisagem | Regra de batalha | Chefe |
| --- | --- | --- | --- |
| 1 | Planície dos Menires | Nenhuma; ensina o básico | O Mestre de Correntes, rival especialista em Laço |
| 2 | Arquipélago Afogado | Maré: a cada 3 rodadas, unidades de Água ganham +20% de Ímpeto | A Serpe-Mãe |
| 3 | Cidadela do Selo Partido | Glifos instáveis: aprimoramentos custam +1 Éter em rodadas ímpares | O Arauto do Silêncio |
| Pós-jogo | Torre dos Círculos | Andares com regras fixas, uma por andar | — |

**Tom.** Fantasia épica clássica, séria no mundo e com leveza nas falas. No máximo três falas por fase: a história é tempero, nunca obstáculo entre o jogador e a luta.

## 5. Direção de arte

A arte de personagem é rabiscada por escolha visual e de produção: é o que permite 40 invocações com uma pessoa só. O mundo, os textos e a interface continuam fantasia, e os sigilos são a única arte feita com capricho.

| Elemento | Regra |
| --- | --- |
| Personagens | Traço solto, no papel (foto ou scan) ou no tablet, no máximo 15 minutos. As criaturas são tratadas com seriedade, não como piada |
| Famílias | Um desenho por família, recolorido nos 5 elementos, como em Summoners War. 8 desenhos cobrem as 40 invocações |
| Despertar | Um segundo desenho por família, mais detalhado e colorido: 16 desenhos de criatura na 1.0 |
| Animação | Linha tremida: 3 versões do mesmo desenho alternando a cerca de 8 quadros por segundo. Movimento só por interpolação (avançar, recuar, tremer), nunca quadro a quadro |
| Inimigos | Criaturas clássicas de RPG, como slime, goblin, lobo, bandido, troll, dragão, etc. Todos seriam as criaturas de 1 ou 2 estrelas da pool de invocações. |
| Glifos e runas | Vetores geométricos limpos, com brilho: 8 Glifos, que também são os desenhos dos conjuntos de runas |
| Raridade | 1, 2, 3, 4 ou 5 estrelas. Mas do 3 pra frente com moldura de bronze, prata ou ouro. Estrelas douradas; roxas depois do Despertar |
| Interface | Base de Summoners War: painéis escuros com moldura dourada, fonte serifada nos títulos e fonte limpa nos números |
| Som | Pedra, papel e sussurros de conjuração; música orquestral de biblioteca livre |

**Paleta.** Uma cor forte por elemento (vermelho Fogo, azul Água, verde Vento, dourado Luz, violeta Trevas) sobre fundos neutros de pergaminho e pedra. O elemento se lê antes do desenho.

## 6. Glifos e elementos

Os oito Glifos são o vocabulário do mundo. No jogo eles aparecem em três lugares, sempre com o mesmo desenho:

- **Invocações:** cada uma tem um Glifo, que dá o estilo das habilidades dela.
- **Runas:** cada Glifo empresta o desenho a dois dos 16 conjuntos de runas de Summoners War (seção 10).
- **Gacha:** você direciona a Invocação Ritual para Glifos que já conhece (seção 9).

O Compêndio, dentro do jogo, explica cada Glifo, elemento, efeito, runa e regra de combate com os números atuais.

### Os 8 Glifos

| Glifo | Escola de origem | Estilo das habilidades | Conjuntos de runas |
| --- | --- | --- | --- |
| Muralha | Abjuração | Escudos, Égide, enfraquecer o ataque inimigo | Guarda, Escudo |
| Olho | Adivinhação | Empurrar o Ímpeto de aliados, atrasar inimigos, prever críticos | Foco, Lâmina |
| Porta | Convocação | Espíritos e aliados extras (ainda sem invocação) | Violência, Vingança |
| Laço | Encantamento | Provocar e atordoar | Desespero, Vontade |
| Estilhaço | Evocação | Dano direto, ignorar Defesa, agir de novo ao derrubar | Fúria, Fatal |
| Véu | Ilusão | Ficar Oculto, cegar, esquivar | Perseverança, Nêmesis |
| Ossada | Necromancia | Drenar vida, amaldiçoar | Vampiro, Destruição |
| Espiral | Transmutação | Aumentar ou reduzir atributos, Queimadura | Energia, Rapidez |

### Elementos

Toda invocação tem um elemento. Fogo vence Vento, Vento vence Água, Água vence Fogo; Luz e Trevas têm vantagem uma sobre a outra. Vantagem dá +25% de dano; desvantagem, −25%.

## 7. Combate

Batalha por turnos sem tabuleiro, como em Summoners War: 4 invocações contra até 5 inimigos por onda, e cada unidade age quando sua barra de Ímpeto enche. Você vence ao derrotar todas as ondas.

### Montagem do time

Quatro invocações; a primeira é a Líder e aplica sua Liderança ao time, se tiver uma. Cada fase tem até 3 ondas, como as masmorras de Summoners War.

### Turno

```mermaid
flowchart LR
  A[Ímpeto chega a 100%] --> B{Manual?}
  B -- Sim --> C[Básico ou Glifo]
  C --> D{Paga aprimoramento com Éter?}
  D --> E[Resolve a habilidade]
  B -- Não --> F[Automático escolhe, sem Éter]
  F --> E
  E --> G[Glifo usado: +1 Éter]
  G --> H[Ímpeto volta a 0%]
```

A barra enche em proporção à Velocidade. Efeitos empurram ou atrasam barras, e a ordem dos turnos no início da luta costuma decidir o resultado: é o ajuste fino de velocidade que os dois jogos têm em comum.

### Éter

Recurso único do time, equivalente às Almas de Epic Seven. Começa em 0 e vai até 10. É escasso de propósito: numa luta inteira dá para pagar poucos aprimoramentos, então cada um é uma decisão.

- **Ganho:** +1 por habilidade de Glifo e +1 por inimigo derrotado. O básico não gera Éter.
- **Gasto:** só aprimoramentos, e só no modo manual. O básico aprimorado custa 3; o Glifo aprimorado, 5 (6 nas 5★). Nenhum aprimoramento custa menos que 2 — mais do que um turno rende —, então não existe ciclo de usa-e-ganha.
- **Aprimoramento:** a versão aprimorada é clara e forte (mais golpes, chance dobrada, efeito extra), para valer o custo.

### Vitória e derrota

Você vence ao derrotar todas as ondas. Perde se as 4 invocações caírem ou se 30 rodadas passarem.

### Automático e manual

Toda luta pode ser automática: usa o Glifo quando está pronto e mira com vantagem elemental e, no empate, no mais ferido. **O automático nunca gasta Éter.** O aprimoramento é poder reservado a quem joga no manual; assim a automação não fica mais forte com um recurso que existe para premiar decisões.

Campanha e Masmorras são desenhadas para o automático; Torre e Provações, para o manual. Lutas já vencidas ganham o botão Resolver, que simula na hora.

## 8. Invocações

A 1.0 tem 40 invocações: 8 famílias em 5 elementos, como em Summoners War. Cada variante tem kit e Glifo próprios e, depois do Despertar, um nome próprio, como os heróis de Epic Seven.

### Anatomia

| Campo | Conteúdo |
| --- | --- |
| Identidade | Família, elemento, Glifo e papel (Frente, Atacante, Suporte ou Controle) |
| Raridade | 1, 2, 3, 4 ou 5 estrelas naturais, definida pela família |
| Atributos | Os de Summoners War: Vida, Ataque, Defesa, Velocidade, Crítico, Dano crítico, Resistência e Precisão (chance de aplicar efeitos) |
| Básico | Habilidade sempre disponível |
| Glifo | Habilidade forte com recarga de 3 a 5 turnos |
| Assinatura | Passiva; famílias de 4 e 5 estrelas também têm Liderança |
| Despertar | Nome próprio, desenho novo e Assinatura melhorada |

### Famílias

| Família | Estrelas | Conceito |
| --- | --- | --- |
| Diabretes de Selo | 3 | Pequenos demônios presos em sigilos de contenção |
| Menires Despertos | 3 | Golens de pedra rúnica que guardam estradas antigas |
| Harpias do Véu | 3 | Caçadoras que se escondem na névoa |
| Cavaleiros Juramentados | 4 | Armaduras vazias movidas por um voto gravado no peito |
| Serpes | 4 | Dragões menores, de voo baixo e humor pior |
| Oráculos de Vidro | 4 | Videntes cujo corpo virou cristal de tanto olhar o futuro |
| Fênix de Cinza | 5 | Aves que renascem das cinzas de um sigilo queimado |
| Sábios Sem Rosto | 5 | Arquimagos que trocaram o próprio rosto por um Glifo |

### Exemplos (um por Glifo)

| Invocação | Ficha | Básico | Glifo (recarga) | Assinatura |
| --- | --- | --- | --- | --- |
| Menir Desperto de Água | 3★ · Muralha · Frente | Golpe de Pedra: 90% de dano e 30% de chance de reduzir o Ataque do alvo. +1 Éter: chance de 60% | Círculo de Pedra (4): escudo de 20% da Vida do Menir em todos os aliados por 2 turnos. +2 Éter: remove também um efeito negativo | Pedra não se move: imune a atraso de Ímpeto e a atordoamento |
| Diabrete de Selo de Fogo | 3★ · Estilhaço · Atacante | Faísca: 2 golpes de 55%. +1 Éter: cada golpe tem 30% de chance de Queimadura | Selo Rompido (3): 180% num alvo; se ele cair, o Diabrete age de novo. +2 Éter: ignora 30% da Defesa | +15% de Velocidade enquanto for o aliado com menos Vida |
| Harpia do Véu de Vento | 3★ · Véu · Atacante | Rasante: 100% de dano e 25% de chance de Cegueira. +1 Éter: chance de 50% | Penas de Névoa (4): fica Oculta por 1 turno e ganha +30% de Ímpeto. +2 Éter: o aliado com menos Vida também fica Oculto | Ao esquivar, contra-ataca com Rasante |
| Cavaleiro Juramentado de Luz | 4★ · Laço · Frente | Juramento: 100% de dano e 30% de chance de Provocar. +1 Éter: Provocar garantido | Voto de Escudo (4): provoca todos os inimigos por 1 turno e recebe −40% de dano por 2 turnos. +2 Éter: provocação de 2 turnos | Liderança: +20% de Defesa. Ao cair, dá escudo de 15% da Vida a todos os aliados |
| Serpe de Trevas | 4★ · Ossada · Controle | Mordida Vil: 110% de dano, drena 30% como Vida. +1 Éter: −50% de cura no alvo por 2 turnos | Hálito de Túmulo (3): 80% em todos, 50% de chance de Maldição (+25% de dano recebido) por 2 turnos. +2 Éter: chance de 100% | Sempre que um inimigo cai, recupera 15% da Vida e gera 1 Éter |
| Oráculo de Vidro de Água | 4★ · Olho · Suporte | Visão Fria: 90% de dano e atrasa o Ímpeto do alvo em 15%. +1 Éter: atrasa 30% | Profecia (4): todos os aliados ganham +25% de Ímpeto. +2 Éter: e +15% de Velocidade por 2 turnos | Liderança: +15% de Velocidade. Aliados não sofrem críticos na primeira rodada |
| Fênix de Cinza de Fogo | 5★ · Espiral · Atacante | Chama Espiral: 120% de dano e converte um efeito positivo do alvo em Queimadura. +1 Éter: converte dois | Voo Rubro (4): 90% em todos e Queimadura por 1 turno. +3 Éter: dano ×1,5 | Na primeira vez que cai, renasce com 40% da Vida no turno seguinte dela |
| Sábio Sem Rosto de Trevas | 5★ · Porta · Controle | Toque do Limiar: 100% de dano e 30% de chance de atordoar | Abrir a Porta (5): invoca uma cópia de um inimigo, que luta do seu lado por 2 turnos com 50% dos atributos. +3 Éter: 100% dos atributos | Sempre que um aliado usa uma habilidade aprimorada, ganha +20% de Ímpeto |

As melhores Assinaturas nascem do conceito da família: a fênix renasce, o menir não se move. Esse é o molde para as outras 32. Os números destes exemplos são os da primeira versão; os atuais estão em Data/summons, com custos de Éter de 3 no básico e 5 ou 6 no Glifo e multiplicadores na escala de Summoners War (o básico da Fênix de Fogo virou 420%, como o da Fênix de lá).

## 9. O gacha: Invocação ritual

Invocar é um ritual: você gasta Pergaminhos, escolhe até 2 Glifos que já conhece e traça o sigilo. As taxas são generosas e a garantia é sempre visível, porque não há ninguém para vender nada.

| Regra | Valor inicial |
| --- | --- |
| Custo | 1 Pergaminho Místico por invocação; 10 por dez |
| Taxas | 3★ 65%, 4★ 28%, 5★ 7% |
| Luz e Trevas | Metade da chance das outras variantes da mesma raridade, como em Summoners War |
| Garantia | 5★ após 60 invocações sem nenhuma, com contador na tela |
| Direcionamento com 1 Glifo | Metade dos resultados vem desse Glifo |
| Direcionamento com 2 Glifos | 30% de cada um; o resto é livre |
| Duplicata além do máximo | Vira Fragmentos: 5 (3★), 10 (4★) ou 20 (5★) |
| Troca por Fragmentos | Qualquer invocação: 30, 60 ou 120 Fragmentos, conforme a raridade |

**O traçado.** Você desenha o Glifo com o mouse ou o dedo, e um reconhecedor de gestos simples (como o $1 Unistroke Recognizer, cerca de 100 linhas de código) avalia o desenho. Não muda as taxas: um traçado limpo só dá 5 de Pó de Sigilo. Invocações de dez usam traçado automático.

**Tiques: surpresa para quem criou o jogo.** Você vai conhecer todas as criaturas, então o gacha precisa de uma surpresa que você não controla. Cada cópia obtida sorteia um Tique entre 12, um traço de personalidade com ganho e custo:

- **Apressado:** +8 de Velocidade, −10% de Vida.
- **Meticuloso:** +15% de Precisão, começa a luta com −10% de Ímpeto.
- **Imprudente:** +20% de Crítico, −10% de Defesa.
- **Teimoso:** +20% de Resistência, −8% de Ataque.

Ao receber uma duplicata, você escolhe manter o Tique antigo ou ficar com o novo. Isso dá motivo para invocar criaturas que você já tem e cria versões diferentes da mesma.

**Convidados.** Peça a amigos o desenho de uma criatura e um Glifo num modelo de uma página; você escreve as habilidades. É a outra fonte de surpresa, e a que dá mais vontade de abrir o jogo.

## 10. Progressão

Há quatro eixos de poder: nível, Ecos, Despertar e runas. Qualquer sistema novo precisa substituir um deles, não somar.

| Eixo | Como sobe | O que dá |
| --- | --- | --- |
| Nível | Experiência das vitórias (para quem lutou) e Essência infundida (1 Essência = 1 de experiência) | Nível de 1 a 40 por invocação; Vida, Ataque e Defesa crescem de 25% a 100% |
| Ecos | Duplicatas, de 0 a 5 | Cada Eco deixa as habilidades 5% mais fortes; no quinto, +10% de atributos |
| Despertar | Essência: 1500 (3★), 3000 (4★), 6000 (5★) | Nome próprio, desenho novo, estrelas roxas, Assinatura melhorada, +20% de Vida, +7% de Ataque e Defesa e o bônus de Summoners War da variante: +15 de Velocidade, +15% de Crítico, +25% de Resistência ou +25% de Precisão |
| Runas | Drop das fases da Campanha (Masmorras na v0.5), melhoradas com Pó de Sigilo; Pedras de Afiar e Gemas a partir da fase 10 | Atributos, conjuntos e o ajuste fino de Velocidade |

**Nível por invocação, como em Summoners War.** Cada invocação sobe de 1 a 40. Os atributos de base do apêndice são os de uma 5★ no nível 40 sem Despertar, na escala de Summoners War: o nível 40 equivale ao 6★ nível 40 de lá, e o nível 1 ao nível 1 da estrela natural (5★: 43% do máximo; 4★: 32%; 3★: 22%). A experiência de vitória vai para quem lutou; a Essência da ociosidade é o atalho para subir quem ficou para trás.

**Despertar.** A invocação ganha um nome próprio (o Diabrete de Selo de Fogo vira Fagulha), um segundo desenho, estrelas roxas no lugar das douradas, a Assinatura melhorada e atributos maiores. É para sempre. Na v0.5 passa a pedir também a vitória na Provação da família.

**Runas: as de Summoners War, sem falha na melhora.** Cada invocação tem 6 espaços dispostos em círculo, o próprio círculo de conjuração. Tudo segue Summoners War, com os números de lá:

- **Espaços.** 1, 3 e 5 têm principal fixo (Ataque, Defesa e Vida fixos). O 2 pode ter Velocidade; o 4, Crítico ou Dano crítico; o 6, Resistência ou Precisão; os três também podem ter Vida, Ataque ou Defesa, fixos ou em porcentagem. O espaço 1 nunca tem Defesa nos subatributos e o 3 nunca tem Ataque.
- **Estrelas e raridade.** De 1 a 6 estrelas, que decidem o tamanho de todos os números. A raridade é o número de subatributos: Normal (0), Mágica (1), Rara (2), Heroica (3), Lendária (4). Às vezes a runa vem com um atributo inato, que nunca cresce.
- **Melhora.** De +0 a +15. Em +3, +6, +9 e +12 entra um subatributo novo (até 4) ou, com 4, um deles cresce; em +15 o principal dá um salto (Velocidade 6★: 31 em +12, 42 em +15).
- **Sem falha, mas cara.** A melhora nunca falha. Cada nível custa o Mana médio de Summoners War contando as falhas (custo ÷ chance), a 100 de Mana por Pó: uma 5★ de +0 a +12 custa cerca de 1.760 Pó; uma 6★ até +15, cerca de 8.950. O Pó é escasso de propósito.
- **Tirar custa.** Tirar uma runa de uma invocação custa Pó (10 para 1★ até 500 para 6★), inclusive quando outra runa entra no lugar. Desfazer uma runa do inventário devolve pouco Pó.
- **Pedras.** A Pedra de Afiar soma um bônus a um subatributo de Vida, Ataque, Defesa ou Velocidade; uma pedra nova troca o bônus antigo. A Gema Encantada troca um subatributo de uma runa +12, e só um por runa. Graus de Mágica a Lendária, com as faixas de Summoners War; servem em qualquer conjunto, como as Imemoriais de lá.
- **Porcentagem sobre a base.** Toda porcentagem de runa, de conjunto e de Liderança é sobre o atributo de base, e o que não fecha número inteiro arredonda para cima.

Os 16 conjuntos são os de Summoners War sem os da Fenda. Cada Glifo empresta o desenho a dois:

| Glifo | Conjunto | Peças | Bônus |
| --- | --- | --- | --- |
| Espiral | Energia | 2 | +15% de Vida |
| Espiral | Rapidez | 4 | +25% de Velocidade |
| Muralha | Guarda | 2 | +15% de Defesa |
| Muralha | Escudo | 2 | No começo de cada onda, escudo de 15% da Vida de base do dono em todos os aliados por 3 turnos |
| Olho | Foco | 2 | +20% de Precisão |
| Olho | Lâmina | 2 | +12% de Crítico |
| Porta | Violência | 4 | 22% de chance de turno extra; cada turno extra seguido multiplica a chance por 0,55 |
| Porta | Vingança | 2 | 15% de chance de contra-atacar com o básico (75% do dano) ao ser atingido |
| Laço | Desespero | 4 | 25% de chance de atordoar cada alvo atingido; a Resistência não barra |
| Laço | Vontade | 2 | Imunidade por 1 turno no começo de cada onda |
| Estilhaço | Fúria | 4 | +40% de Dano crítico |
| Estilhaço | Fatal | 4 | +35% de Ataque |
| Véu | Perseverança | 2 | +20% de Resistência |
| Véu | Nêmesis | 2 | +4% de Ímpeto a cada 7% da Vida máxima perdida num golpe |
| Ossada | Vampiro | 4 | Drena 35% do dano causado |
| Ossada | Destruição | 2 | 30% do dano causado tira Vida máxima do alvo (até 4% por habilidade, 60% no total) |

Com 6 espaços cabem um conjunto de 4 e um de 2, ou três de 2; três conjuntos iguais valem três vezes.

## 11. Loop ocioso e modos de jogo

Uma sessão típica dura de 5 a 15 minutos: coletar o que acumulou, vencer 1 a 3 lutas, invocar, ajustar o time e fechar.

```mermaid
flowchart LR
  A[Abre o jogo] --> B[Coleta a ociosidade]
  B --> C[1 a 3 lutas]
  C --> D[Invoca com Pergaminhos]
  D --> E[Ajusta runas, níveis e time]
  E --> F[Fecha]
  F -- horas depois --> A
```

**Ociosidade.** Com o jogo fechado, seus círculos de invocação continuam canalizando, como as construções da ilha em Summoners War. Acumulam Essência, Pergaminhos, Pó de Sigilo e entradas de Masmorra; a taxa cresce com a fase mais alta vencida, e o acúmulo para em 12 horas. Uma vez por dia, a Canalização Rápida entrega 2 horas de recompensa na hora.

| Modo | Inspiração | Controle | Para que serve | Entra em |
| --- | --- | --- | --- | --- |
| Campanha: 3 regiões de 20 fases | AFK | Automático | Aumenta a ociosidade e o teto de nível | MVP (região 1) |
| Masmorras de Runas: Golem Rúnico, Ninho da Serpe, Cripta do Rei Ossudo, Santuário Afogado | Masmorras de Summoners War e Caçadas de Epic Seven | Automático ou Resolver | Cada uma solta 2 conjuntos de runa | v0.5 |
| Torre dos Círculos: 60 andares com regras fixas | Torre de Summoners War e Abismo de Epic Seven | Manual | Pergaminhos, Pó de Sigilo, desafio de montagem | v0.5 |
| Provações: uma luta fixa por família | Despertar de Summoners War | Manual | Libera o Despertar | v0.5 |
| Portais Secretos: surgem ao vencer Masmorras e ficam até você usar | Masmorras secretas de Summoners War | Automático | Três vitórias no mesmo portal dão uma invocação daquela família | v1.0 |
| Arena dos Aprendizes: rivais gerados com poder parecido com o seu | Arenas de Summoners War e Epic Seven | Automático | Fragmentos e rivais recorrentes | v1.0 |
| Espelho: exporta o time como código de texto para um amigo enfrentar | — | Automático | Social sem servidor | Depois da 1.0 |

Entradas de Masmorra chegam pela ociosidade, uma a cada 30 minutos, até 24 guardadas. Não existe energia que se perde por não jogar.

## 12. Economia

Quatro moedas, e nunca mais que isso. Jogando normalmente entram cerca de 4 Pergaminhos Místicos por dia, ou 4 invocações.

| Moeda | De onde vem | Para onde vai |
| --- | --- | --- |
| Pergaminhos Místicos | Ociosidade, primeira vitória de cada fase, Torre, conquistas | Invocar |
| Essência | Ociosidade, fases | Nível das invocações e Despertar |
| Pó de Sigilo | Ociosidade, fases, Masmorras, Torre, traçado limpo, runas desfeitas | Melhorar e tirar runas. Escasso de propósito: a melhora nunca falha, então o preço é a média de Summoners War |
| Fragmentos | Duplicatas excedentes, Arena | Trocar por uma invocação escolhida |

### Ritmo-alvo

| Marco | Quando deve acontecer |
| --- | --- |
| Primeira 5★ | Primeira sessão, garantida no tutorial |
| Primeiro Despertar | Cerca de 2 semanas |
| Região 1 completa | Cerca de 2 semanas |
| As 40 invocações coletadas | Cerca de 8 semanas |
| Primeira 5★ com 5 Ecos | Cerca de 3 meses |
| Torre no andar 60 | Cerca de 4 meses |

Se o jogo ficar chato no teste, a primeira alavanca é dar mais Pergaminhos. Aqui a generosidade não tem custo comercial.

## 13. Escopo, tecnologia e roadmap

Cada fase termina num jogo que você já consegue jogar; se o projeto parar em qualquer ponto, sobra algo jogável. As fases somam de 7 a 9 meses a 8–10 horas por semana; com folga, de 9 a 12.

### Tecnologia

| Peça | Escolha |
| --- | --- |
| Motor | Godot 4: gratuito, bom em 2D, exporta para PC, Android e web |
| Conteúdo | Invocações, páginas e fases em arquivos de dados. Uma variante nova é um arquivo, sem desenho novo |
| Simulação | Combate separado da tela e determinístico (semente fixa). Permite rodar milhares de lutas sem gráficos para balancear |
| Arte da 1.0 | 16 desenhos de criatura (8 famílias e seus Despertares) e 11 ícones de símbolo |
| Save | Arquivo local, sem conta nem servidor |

### Fases

| Fase | Duração | Entrega | Pergunta que responde |
| --- | --- | --- | --- |
| 0. Simulador em texto | 2 semanas | Combate em linha de comando ou planilha: Ímpeto, Éter, 8 invocações | A matemática de velocidade e Éter é interessante? |
| 1. Núcleo de combate | 6 a 8 semanas | Tela de batalha, ondas, 8 invocações, IA automática, 10 fases | O automático é bom de assistir e o manual é bom de jogar? |
| 2. MVP: loop AFK e gacha | 4 a 6 semanas | Ociosidade, nível 1–40, Despertar, runas, invocação com garantia, 3 famílias (15 invocações), região 1 | Dá vontade de voltar no dia seguinte? |
| 3. v0.5: profundidade | 8 a 10 semanas | Masmorras de Runas, Provações, Torre até 30, traçado, 5 famílias (25) | Existe teorização para semanas? |
| 4. v1.0: conteúdo | 10 a 12 semanas | 8 famílias (40), regiões 2 e 3, Torre 60, Arena, Portais Secretos, Tiques | Você joga 90 dias seguidos? |
| Depois | Contínuo | Uma família nova (5 invocações) quando der vontade, Convidados, Espelho | — |

A fase 0 é a mais barata e a mais importante: combate por turnos com velocidade é quase só matemática, então dá para testar sem nenhum gráfico.

**Ordem de corte se atrasar:** Arena, depois Portais Secretos, depois a região 3 vira pós-1.0. Nunca cortar: Éter compartilhado, runas e ociosidade.

## 14. Riscos e perguntas em aberto

O maior risco não é técnico: é o escopo crescer até o projeto parar. Os outros têm mitigação simples.

| Risco | Por que preocupa | Mitigação |
| --- | --- | --- |
| Escopo crescente | É o que mais mata projetos pessoais | Sistema novo substitui um eixo, não soma; toda fase termina jogável |
| Runas pesadas | Seis espaços com subatributos é o sistema mais caro de balancear e de interface | Copiar as regras e tabelas de Summoners War em vez de inventar números; melhora sem falha; simulador e testes com os valores de lá |
| Automático forte demais | Se o automático resolve tudo, o manual perde sentido | O automático nunca gasta Éter; aprimorar é decisão do jogador |
| Gacha sem tensão | Você conhece todas as criaturas | Tiques, Convidados, garantia visível e o ritual do traçado |

### Perguntas em aberto

- [x] Os símbolos de conjuração foram lidos como escolas, círculos e aprimoramentos. Se você pensou numa parte específica do lore, os Glifos podem ser trocados por ela. Está correto da forma que está
- [x] Famílias em 5 elementos (Summoners War) ou heróis únicos com nome (Epic Seven)? Famílias poupam arte; heróis únicos têm mais personalidade. Familias
- [x] Plataforma principal: PC ou celular? Muda a interface de batalha e do traçado. PC
- [x] Qual motor você já domina? Godot é sugestão, não requisito. Godot
- [ ] Quantas horas por semana há de verdade? O roadmap supõe 8 a 10.
- [x] Quer mostrar para amigos? Se sim, Convidados e Espelho sobem de prioridade. Sim

## 15. Apêndice: fórmulas e números iniciais

São valores de partida para o simulador, feitos para serem mudados no primeiro teste.

### Dano

```latex
D = ATQ \times M \times \frac{K}{K + DEF} \times E \times C, \quad K = \frac{1140}{3{,}5} \approx 326
```

É a curva de Defesa de Summoners War, 1000 / (1140 + 3,5 × DEF), com Defesa 0 valendo o golpe cheio: Defesa 326 corta o dano pela metade. M é o multiplicador da habilidade, na escala de lá (por exemplo, 4,2 para 420%). E vale 1,25 com vantagem elemental, 0,75 com desvantagem e 1 no neutro. C vale 1 + Dano crítico no crítico (50% de base) e 1 fora dele.

Atributo em combate, como em Summoners War: runas + base × (1 + Liderança + conjuntos), vezes os efeitos (+50% de Ataque, +70% de Defesa, +30% de Velocidade; −50% de Ataque). Efeito negativo pega se passar pela Resistência do alvo menos a Precisão de quem lança, e essa chance de barrar nunca fica abaixo de 15%.

### Ímpeto

```latex
t = \frac{100 - I}{VEL}
```

I é o Ímpeto atual em porcentagem; a unidade com o menor t age primeiro. Empurrar o Ímpeto em 20% soma 20 a I, na hora.

### Atributos de base no nível 40 (5★, sem Despertar)

| Papel | Vida | Ataque | Defesa | Velocidade |
| --- | --- | --- | --- | --- |
| Frente | 11100 | 620 | 700 | 98 |
| Atacante | 9300 | 900 | 500 | 103 |
| Suporte | 10400 | 660 | 640 | 107 |
| Controle | 9900 | 760 | 580 | 102 |

É a escala de Summoners War: a Fênix de Fogo de lá tem, em 6★ nível 40, 9225 de Vida, 834 de Ataque e 527 de Defesa. Todos os papéis começam com Crítico 15%, Dano crítico 50%, Resistência 15% e Precisão 0%, como quase todo monstro de lá; esses quatro não crescem com o nível.

Invocações de 4★ usam 92% desses valores no nível 40 e as de 3★, 85%. Velocidade é fixa desde o nível 1; os outros atributos começam no valor do nível 1 da estrela natural de Summoners War (5★: 43%; 4★: 32%; 3★ e inimigos: 22%) e crescem em linha reta até o 40. Velocidade só muda por runas, Despertar, Tiques e Liderança, para o ajuste fino continuar importando.

Inimigos têm 1 ou 2 estrelas e compensam a raridade baixa com multiplicadores de Vida e Ataque por criatura (Data/enemies.json): não usam runas nem aprimoramentos.

