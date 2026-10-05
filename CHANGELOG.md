# Changelog — Sigilos

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
