# Changelog — Sigilos

## 08/10/2026
- **Runas: a prévia antes de equipar.** Escolher uma runa que não está no monstro já mostra a ficha de agora ao lado da que ele teria com ela, e a diferença pronta: + em verde, − em vermelho, apagado o que não muda. Conta o valor fixo, o percentual e os conjuntos (os que fecham e os que abrem), e a runa aparece no espaço dela no círculo, em azul.
  - **Provar** guarda a runa numa prova, uma por espaço, e a prévia soma a prova à runa escolhida: dá para montar a combinação inteira e comparar outras sem perder a referência, porque a coluna "Agora" segue sendo o monstro como está. **Equipar N** põe a prova toda de uma vez; **Limpar** larga.
  - Tocar numa runa não remonta mais a grade: só troca o destaque, a coluna da esquerda e a ficha da runa. Com centenas de runas, a lista fica onde estava.
  - Botões mais baixos (Filtros, Ordenar, Onde, as ações da runa), e a coluna da esquerda com a ficha sempre à vista.
- **Monstros: os novos na frente.** Os que chegaram desde a última visita à tela vêm antes de todos, com a faixa "Novo!", e a aba diz quantos ("90/100 · Novos: 1"). Abrindo a tela, o mais novo já vem escolhido. Ao sair, deixam de ser novos (o save guarda até onde a conta viu).
- **Monstros, filtro por situação:** Desbloqueados.
- **Cadeado bem à vista:** o monstro bloqueado leva um disco de ouro, maior, com o cadeado escuro.
- **Baú: cópias iguais num cartão só.** Monstros iguais em estrelas, nível, Despertar, habilidades, bloqueio, favorito e runas viram um cartão com "×7"; os que diferem em algo disso ficam em cartões separados (bloqueadas de um lado, novas de outro).
  - Tocar no cartão abre as cópias uma a uma: marcar algumas ou todas e, só com as marcadas, **Tirar do Baú** (até onde a coleção tiver vaga), **Fundir em…** (a escolha do monstro que as recebe, e a fusão abre com elas já marcadas) ou **Soltar**.
  - Depois de cada ação a janela mostra as que sobraram; sobrando uma, o cartão volta a ser normal.
  - Em Selecionar vários, tocar num grupo marca as cópias todas; o cartão diz quantas estão marcadas ("3/7").
- **Santuário:** a constelação fica no meio exato do quadro, em qualquer altura dele.

- **Sons próprios.** Uma biblioteca de 62 efeitos sonoros, toda sintetizada para Sigilos, sem nenhum áudio de fora: interface, grimório, constelações, invocação, recompensas, combate (acertos, os cinco elementos, o chefe) e progressão.
  - O som do jogo é o de um grimório mágico entre constelações, feito para morar dentro da música: harpa, celesta, sinos macios, taça e feltro, no mesmo tom dela (Mi menor / Sol maior), com a assinatura Ré–Lá–Mi. Nada de estalo duro, agudo agressivo, distorção ou explosão.
  - Volume em degraus, sempre por baixo da música: o que se repete é o mais baixo, a navegação um pouco acima, a luta comum no meio, as habilidades grandes e as recompensas raras acima, e a invocação 5★ no topo. Importância vem das camadas (acorde maior, coro, a subida antes), não de volume.
  - **Os sons tocam no jogo.** Interface (botões, abas, janelas, telas, voltar), luta, resultado, invocação, Monstros, Runas, Equipes, Loja, Santuário, correio, Grimório, Exploração e Batalha automática. O que é secundário divide o som de outro: bloquear e favoritar soam como ligar e desligar; afiar, encantar e reavaliar, como a tinta mágica.
  - **Na luta, pouco som:** um acerto por golpe (o impacto do elemento na habilidade, o grande feitiço na de recarga longa, a voz do chefe), a queda e, na ação de apoio, um som de bem ou de mal. A corrida, o crítico, o Ímpeto, o Veneno e os efeitos pequenos ficam calados. No máximo dois sons por momento: uma luta longa de Masmorra toca uns 35 sons por minuto em 1× (eram 100), quase sem nenhum por cima do outro.
  - O mesmo som não empilha: no máximo duas vezes junto, e não repete em menos de 80 ms.
  - A ação com som próprio cala o clique do botão que a pediu: o Evoluir soa a estrela, não o clique.
- **Ajustes:** volume de **Efeitos**, ao lado de Geral e Música.
- **Masmorras: o andar 5 virou o das Lendárias.** Antes, o andar 4 rendia mais runas boas que o 5 (30,4% de Lendária contra 3%): umas 15 runas 6★ Lendárias por dia no 4 e 1,8 no 5.
  - Andar 4: Rara 60%, Heroica 32%, Lendária 8% (era 46,5 / 23,1 / 30,4). Continua o andar das runas 6★ (71,8%), agora com mais Heroicas: é com ele que o time se prepara para o 5.
  - Andar 5: Rara 46,5%, Heroica 23,1%, Lendária 30,4% (era 61,8 / 35,2 / 3,0). Quem o vence rende mais nele em tudo: por dia de Mana, 60 runas 6★ e 18 Lendárias 6★, contra 49 e 3,9 no andar 4.
  - Forja: o andar 5 solta 1 pedra por vitória (eram 2), com a mesma tabela de grau das outras Masmorras.
  - O andar 5 de toda Masmorra dá o dobro de Essência: 1.760 por vitória (eram 880), 105.600 num dia de Mana.
- **Conferência do drop das Masmorras** (`dotnet run --project Tests -- --drops`): cada andar vence 20 000 vezes pelo jogo de verdade e o que caiu é comparado com a tabela do andar (estrelas, raridade, conjunto, pedras da Forja, Pergaminho Místico, Núcleo de Infusão, Mana, Essência, experiência e o Ouro e o marco da primeira vitória). No fim, o que cada andar rende por dia de Mana. O teste `EveryFloorDropsByItsTable` faz a mesma conferência em toda Masmorra.
- **Nível da conta até 100.** A experiência para o próximo nível segue 500 × 1,075^(nível − 1): suave no começo, bem maior nos níveis altos.
  - A Mana máxima vai de 100 no nível 1 a 300 no nível 100 (+2 por nível).
  - Os marcos dos níveis passam a somar 20 Núcleos de Infusão do 1 ao 100.
  - **No nível máximo, a experiência não se perde:** um terço dela vira Essência. A tela da vitória já mostra essa Essência junto da outra.
- **Canalização da Mana:** 20 por hora, uma a cada 3 minutos (era 12). A Mana máxima do nível 1 enche do zero em 5 horas.
- **Loja:** Frasco de Mana com 120 por 75 de Ouro e Cântaro com 300 por 150.
- **Invocação:** os botões ficam travados do começo do ritual até os cartões aparecerem, mesmo trocando a aba do pergaminho. Antes, trocar a aba no meio do ritual liberava outra invocação, e a segunda leva somava cartões à primeira e empurrava os de cima (a 5★ da garantia, por exemplo) para fora do palco.

## 07/10/2026
- **Visual: o grimório do invocador.** A interface inteira virou o gabinete de um invocador que estuda constelações.
  - Paleta sóbria: violeta (místico), índigo (céu), ouro só no que é precioso, couro e pergaminho.
  - Fundo de céu noturno com nebulosa e astrolábio; separadores com estrela; páginas de pergaminho escritas a tinta.
  - A Canalização ganhou a carta do céu por baixo, o mapa da Exploração a grade do céu, e os sigilos grandes a moldura graduada.
  - Ícones novos da Exploração Estelar: a porta, a Influência e as três faixas do céu.
  - Títulos numa fonte serifada de destaque (Cinzel); o texto e os números continuam na fonte do jogo.
  - O Santuário perdeu o anel girando no fundo: a constelação da Canalização já faz esse papel.
  - A estrela que pisca nos orbes do mapa da Exploração é a estrela polar desenhada, lisa, no lugar do polígono serrilhado.
- **Exploração:** escolher outra constelação volta a coluna da direita para o topo, como ao entrar na tela.
- **Grimório do Invocador:** tocar na conta, no Santuário, abre o livro da conta, com quatro capítulos, em páginas de pergaminho.
  - I, Invocador: retrato, nome, desde quando a conta existe, nível e os registros (com o mais longe no céu).
  - II, Masmorras: andares vencidos, a equipe do melhor tempo do andar mais fundo e os melhores tempos de cada andar.
  - III, Céu: a Exploração do mês, o mais longe que já chegou e o céu da faixa desenhado a tinta.
  - IV, Selos: doze marcos da jornada e da coleção (dois deles do céu), lacrados em cera quando cumpridos (só registro, sem prêmio).
  - Trocar o retrato passou a ser pelo livro.
- **Recordes com a equipe:** o melhor tempo de cada luta guarda a equipe que o fez, a partir de agora.
  - Nos tempos antigos, o andar mais fundo vencido de cada Masmorra recebe a equipe salva hoje para ela, a mais provável de ter feito o tempo.
  - Os outros andares antigos ficam sem equipe.
- **Retrato da conta em duas abas:** Monstros (os da coleção e os de monstro vindos do correio) e Especiais (o padrão e os de recompensa, que não são monstros). O correio pode mandar um especial (`avatar:sigil`).
- **Chat:** o balão fica onde cada tela marca:
  - logo depois do título (Monstros, Runas, Batalha e as outras telas com cabeçalho);
  - no Santuário, no canto da constelação;
  - na luta, ao lado da rodada.
- **Início da conta:** um save de antes da data de início passa a contar da primeira vez que abre depois desta versão.
- **Guilda:** o save já guarda a data de entrada na guilda atual. Fica reservada, porque ainda não há guildas.
- **Batalha automática, a luta que se assiste:** o alvo só perde Vida quando o golpe chega. Antes, a barra caía no começo do turno, um instante antes de quem ataca correr até ele.
- **Janelas:** a pergunta aberta por cima de outra janela cobre tudo dela. A moldura azul e o "Tocar para ver em tela cheia" da luta pequena ficavam por cima de "Parar a Batalha automática?".
- **Recalibragem do andar 5:** Ninho da Serpe (escala 3,15 → 4,3) e Forja Rachada (2,3 → 1,7), depois das regras de vitória novas e da chance inata menor.
- **Música:** o Plano Celestial toca fora das lutas e a Batalha nas lutas, em laço.
  - Ao entrar em combate, o Plano Celestial afunda (cai o volume e o tom) e a Batalha entra por cima.
  - Ao sair, o Plano Celestial volta de onde parou.
  - Lutas seguidas não reiniciam a Batalha, e a Batalha automática não troca a música.

## 06/10/2026
- **Bancada de balanceamento** (`dotnet run --project Tests -c Release -- --balance`):
  - Testa todas as composições possíveis, ou uma amostra quando são muitas, contra fases e andares de Masmorra.
  - O investimento é configurável: estrelas, nível, Despertar, habilidades e runas.
  - Gera um relatório visual com o ranking das composições, o peso de cada monstro e família e lutas de exemplo com a Vida turno a turno e o log completo.
- **Balanceamento 0.4.4:** ajustes nas famílias e arte dos monstros novos.
- **Música:** faixa de batalha adicionada.

## 05/10/2026
- **Exploração Estelar (antiga Torre):** o modo de onde vêm os recursos, na terceira porta da tela Batalha. Um percurso pelas 88 constelações do céu:
  - Boreais (1 a 21, de Ursa Menor a Andrômeda), Equatoriais (22 a 51, com o zodíaco, de Peixes a Pégaso) e Austrais (52 a 88, até o Cruzeiro do Sul).
  - Abre depois da fase 30 da Campanha. Não custa Mana, tem equipe própria e é feita para o manual.
  - **Influência:** cada constelação tem a sua mecânica, regras que valem a luta inteira nos inimigos, só no guardião, no seu time ou em todos (o Esquecimento não cala). A ficha explica a mecânica e como vencer, com os números; segurar uma unidade na luta mostra as regras dela.
  - **Guardiões:** em quatro de cada cinco desafios, o guardião é uma invocação desperta com as habilidades no máximo e Vida de chefe: as habilidades dela são o desafio. Doze chefes novos (Urso Celeste, Carneiro de Ouro, Touro Celeste, Caçador das Estrelas, Unicórnio Celeste, Caranguejo Celeste, Leão de Nemeia, Hidra de Lerna, Balança de Astreia, Escorpião Celeste, Cabra-Marinha e Centauro Celeste).
  - **Rodízio mensal:** no dia 1 o percurso recomeça e entra a Exploração seguinte (Aurora, Zênite e Crepúsculo), com outros guardiões e escoltas. A recompensa de cada constelação é sempre a mesma e volta todo mês.
  - **Recompensa do mês inteiro:** 91.850 de Essência, 1.980 de Ouro, 22 Pergaminhos Místicos, 8 Núcleos de Infusão, 3 Pergaminhos Lendários e 1 de Luz e Trevas.
  - **Mapa:** o céu de verdade de cada faixa, com os orbes e os fios de luz da constelação da Canalização (as duas desenham com as mesmas peças).
- **Equipes:** aba da Exploração Estelar. **Compêndio:** cartão da Exploração Estelar.
- **Simulador:** `dotnet run --project Tests -- --exploration` mede cada constelação nas três Explorações contra cinco times.
- **19 famílias novas (95 invocações):**
  - 5★: Unicórnio, Princesa, Anjo e Monge.
  - 4★: Pirata, Dríade, Gorila, Minerador Anão, Múmia, Lich e Samurai.
  - 3★: Arqueiro, Minotauro, Medusa, Esqueleto e Assassino.
  - 2★: Cogumelo, Lagarto e Planta Carnívora.
- **Efeitos mais variados:** os kits novos usam Sono, Silêncio, Karma, Ferida, Bênção, Contragolpe, Reviver e Resistir Crítico, além de encurtar os efeitos positivos do inimigo, ataque em conjunto e turno extra ao derrubar.
- **Minerador Anão de Fogo:** é a peça-chave do Golem. Tira os efeitos positivos, ignora e quebra a Defesa e corta a cura do núcleo. Com ele, a equipe preparada vence o andar 5 em 100% das lutas.
- **Golem andar 3:** um pouco mais difícil (escala 0,8 → 0,95), para seguir pedindo o investimento do andar.
- **Runas:** os marcos de melhora +3, +6, +9 e +12 pedem 1, 2, 3 e 4 subatributos. Uma runa Rara cresce em +3 e +6 e só vira Heroica em +9; antes, virava em +3.
- **Recalibragem:** Santuário andar 5 (escala 3,4 → 3,0) e fase 50 (0,5 → 0,49), depois da mudança nas runas.
- **Evolução:** agora custa 10, 25, 75, 150 e 300 Fragmentos por estrela.
- **Compêndio:** os efeitos positivos ficam na primeira coluna e os negativos na segunda.
- **Coleção:** a ficha do Núcleo de Infusão fica da mesma largura das outras.
- **Monstros e Runas:** tocar nos últimos itens não faz mais a lista voltar ao topo.
- **Testes do Golem:** o andar 5 é medido em três visões. O time gratuito (um 4★ e quatro 3★) vence pelos efeitos, o OK (o especialista) pelo dano bruto e o Spd (sincronia de Velocidade) é o mais rápido.
- **Construtor de famílias:** as habilidades são montadas por listas de escolha (tipo, alvo, status, escopo…), sem digitar JSON.
- **Música:** faixa Plano Celestial adicionada.

## 04/10/2026
- **Masmorras de especialização:** cada chefe pede um time feito para ele. O Golem regenera a cada turno, a Serpe se fortalece por efeitos negativos, o Rei Ossudo (Cripta) impede que qualquer monstro ganhe ou perca Ímpeto, o Santuário Afogado contra-ataca. O andar 5 só cai para uma equipe preparada. A Forja foi recalibrada.
- **Esquecimento:** agora também no Pássaro de Trevas, no Corvo de Água, no Diabrete de Trevas e na Fênix de Luz.
- **Três pergaminhos:**
  - Místico: 1% de 5★, 9% de 4★ e 90% de 3★, com 5★ garantida em 150 invocações.
  - Luz e Trevas: 1% de 5★, 7% de 4★ e 92% de 3★.
  - Lendário: 7% de 5★ e 93% de 4★, só de Fogo, Água e Vento.
- **Núcleo de Infusão:** ocupa espaço na coleção, mas não luta nem usa runa. Serve só para fundir e sobe uma habilidade de monstro de qualquer família.
- **Marcos:**
  - Fim de região (fases 20, 40 e 50): um Lendário e dois Núcleos.
  - Andares 4 e 5 das masmorras: um Lendário e um de Luz e Trevas, respectivamente.
  - Níveis 20 a 60 da conta: pergaminhos de Luz e Trevas, mais um Núcleo a cada 5 níveis.
- **Drops raros nas masmorras:** Núcleo e Pergaminho Místico com 1%, 2% e 3% de chance nos andares 3, 4 e 5.
- **Troca de Fragmentos:** 300 Fragmentos compram uma 4★ escolhida (Fogo, Água ou Vento).
- **Fragmentos:**
  - Soltar um monstro dá 7, 15, 30 ou 50 (até 2★, 3★, 4★ e 5★).
  - Evoluir custa só Fragmentos: 10, 20, 30, 60 e 120 por estrela.
- **Loja:** Expansão de Coleção, +50 espaços por 100 de Ouro, até 500 espaços.
- **Balanceamento:** habilidades repetidas, como Aflição e Maldição, foram recalibradas para que times possíveis de montar vençam o Golem e a Serpe.
- **Ferramentas:** o builder ganhou uma aba de Chefes e passivas genéricas (EffectPassive). Os efeitos aparecem em colunas separadas de positivos e negativos.
- **Testes:** os testes antigos que falhavam foram alinhados ao jogo atual.

## 03/10/2026
- **Monstros:** filtro e ordenação completos (elemento, função, estrelas, raridade, Despertar, condição e atributo).
- **Combate:**
  - Limite de efeitos por unidade.
  - Queimadura e Veneno viraram Aflição, um dano por turno que acumula.
  - Status novos: Sono, Karma, Bênção, Contra-ataque, Reviver e outros.
- **Conta:** opção de renomear a conta, chave de recuperação e presentes no Correio (monstros, runas e retratos).

## 02/10/2026
- **Masmorras:** passam a dar Pergaminhos, e a conta ganhou retrato.
- **Correio:** integrado à conta.
- **Baú:** guardar e retirar vários monstros de uma vez.
- **Lutas:** botão "Continuar" depois da luta.
- **Drops:** passam a seguir tabelas de chance por grade e raridade de runa.
- **Chefes:** barra própria e opção para o automático focar o chefe.
- **Tela no PC:** fixa em 16:9.
- **UI:** ícones novos, rolagem por arraste e refatorações.

## 01/10/2026
- **Campanha:** 50 fases em 3 regiões, com dois chefes novos (Serpente-Mãe e Arauto do Silêncio) e tutorial.
- **Gema de Reavaliação de runas.**
- **Monstros:** favoritos, fusão por família e conselhos após uma derrota.
- **Arte:** a arte desperta foi unificada, com um shader de aura.
- **Windows:** o executável se atualiza sozinho, com manifesto assinado.
- **Save em nuvem:** login, sincronização e um aparelho por vez.

## 30/09/2026
- **Bloqueio:** monstros e runas podem ser bloqueados contra soltura e fusão acidentais.
- **Invocação:** 5★ caiu para 3,5% e 4★ para 14%.
- **Hub:** a constelação foi redesenhada.
- **Idioma:** pt-BR passou a ser o idioma base.
- **Batalha automática.**
- **Campanha:** passa a dar monstros.
- **Conteúdo:** 8 famílias novas (Magos, Paladinos, Druidas, Gárgulas, Vampiros, Corvos, Pássaros e Pixies), com passivas novas.
- **Combate:** refatorado, com um modelo novo de atributos e o builder de famílias.
- **Exportação:** presets para Android e Windows.

## 24–29/09/2026
- **Base do jogo:** commit inicial com runas e combate no estilo Summoners War.
- **Habilidades:** indexadas, com estrelas e evolução.
- **Batalha:**
  - Batalha automática, invocações e passivas.
  - Ritmo novo, efeitos por rodada, arena e menu de pausa.
  - Recordes de tempo e tela de resultado.
- **Refatorações:** runas, status, regras de batalha e bomba. Os sets das masmorras foram corrigidos.
- **UI:** reformulação grande, i18n, pipeline de assets e addon Visual Debug.
