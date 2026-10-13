# Prompt — Reestruturação Geral do Jogo: Constelações, Alquimia e Sigilos

## 1. Objetivo e direção do projeto

Reestruture o jogo para a versão **0.1**, estabelecendo uma nova direção artística, conceitual e de design. A proposta do jogo mudou significativamente, e esta versão deve representar uma nova base de desenvolvimento, com sistemas, nomenclaturas, mecânicas e identidade visual alinhados à nova visão.

A nova identidade do jogo será fundamentada em cinco pilares:

- **Constelações:** identidade temática dos homúnculos, progressão e organização do universo do jogo.
- **Alquimia:** transformação, criação e manipulação de elementos.
- **Transmutação:** sistema de obtenção e transformação de entidades.
- **Sigilos:** conjuntos de efeitos, atributos e personalização dos homúnculos.
- **Símbolos:** linguagem visual central da interface, dos sistemas e da identidade do jogo, utilizando simbologia planetária, zodiacal e alquímica.

O objetivo não é apenas renomear elementos existentes. É alinhar a experiência completa à nova visão do projeto, preservando os sistemas que continuam relevantes e preparando uma base consistente para as próximas versões.

### Diretrizes obrigatórias
Analise a arquitetura atual antes de modificar o projeto.
Reutilize os componentes, sistemas e recursos existentes sempre que forem compatíveis com a nova direção.
Não crie habilidades novas. Todas as skills serão desenvolvidas manualmente em outro momento.
Elementos removidos nesta versão devem ser **desativados ou removidos da experiência jogável, sem serem apagados dos arquivos do projeto**.
Preserve os recursos antigos para avaliação, reaproveitamento ou reintrodução em versões futuras.
Evite implementar conteúdo, funcionalidades ou sistemas que não estejam especificados neste documento.
Mantenha consistência entre regras de jogo, dados, interface, nomenclatura, símbolos e sistemas de progressão.
Não introduza mecânicas duplicadas ou regras contraditórias. Quando houver conflito entre o comportamento atual e as especificações deste documento, prevalecem as novas especificações.
---

## 2. Versionamento e escopo da versão 0.1

Regresse a versão do jogo para **0.1**, pois o projeto está passando por uma reformulação substancial de sua visão artística e de design.

A versão 0.1 deverá estabelecer uma base funcional e coerente com a nova proposta, contemplando:

- A nova nomenclatura global.
- A nova identidade visual.
- A reorganização dos homúnculos disponíveis.
- A revisão dos conjuntos de sigilos.
- As alterações nas mecânicas de masmorras e chefes.
- A nova organização do grimório.
- A atualização das regras de recompensa e transmutação.
- A remoção lógica dos conteúdos temporariamente fora do escopo.

Não interprete a regressão de versão como uma autorização para apagar o histórico ou destruir implementações antigas. Preserve o código e os recursos existentes sempre que possível, limitando a remoção ao conteúdo efetivamente disponibilizado ao jogador.

---

## 3. Padronização da nomenclatura

Faça uma revisão global da nomenclatura do jogo, incluindo textos da interface, menus, descrições, componentes visuais, identificadores apresentados ao jogador e demais referências pertinentes.

| Nomenclatura anterior | Nova nomenclatura |
|---|---|
| Monstros | Homúnculos |
| Pergaminhos | Transmutação Alquímica |
| Glifos | Sigilos |
| Runas | Sigilos |
| Cripta | Remover da experiência jogável |
| Santuário | Nova forja e sistema de masmorra associado |

### Regras para a migração de nomenclatura

- Substitua os termos antigos por seus equivalentes novos em todos os locais em que sejam apresentados ao jogador.
- Evite que a interface misture as nomenclaturas antiga e nova.
- Não altere indiscriminadamente nomes de classes, métodos, recursos internos, arquivos ou identificadores técnicos quando isso puder quebrar referências ou integrações existentes.
- Diferencie a nomenclatura de apresentação da nomenclatura interna quando necessário.
- Se a estrutura técnica exigir uma migração de identificadores, mantenha compatibilidade com os dados existentes e preserve os recursos antigos.
- O termo **Sigilo** deverá representar o sistema de conjuntos de efeitos anteriormente associado às runas, independentemente da nomenclatura utilizada na implementação interna.

---

## 4. Sistema de recompensas: exclusividade de categorias de drop

Altere o sistema de recompensas para que determinadas categorias de itens sejam mutuamente exclusivas dentro de uma mesma resolução de recompensa.

As três categorias afetadas são:
Núcleo de Infusão.
Pergaminho de Transmutação Alquímica.
Núcleo de Fragmento.
### Regra obrigatória

**Uma mesma resolução de recompensa não pode conceder mais de uma dessas três categorias simultaneamente.**

Exemplos de resultados válidos:

- Núcleo de Infusão.
- Pergaminho de Transmutação Alquímica.
- Núcleo de Fragmento.
- Nenhum desses itens.

Exemplo inválido:

- Núcleo de Infusão e Pergaminho de Transmutação Alquímica na mesma resolução.

### Requisitos de implementação

- Centralize a exclusividade no sistema responsável pela geração das recompensas, evitando depender apenas de verificações na interface.
- Preserve os demais drops que não pertençam às categorias restritas.
- Não altere desnecessariamente as probabilidades individuais nem a quantidade dos itens.
- Caso o sistema atual possua múltiplas fontes de recompensa para uma mesma conclusão, garanta que a regra seja aplicada ao escopo correto da resolução, impedindo que fontes paralelas contornem a restrição.
- Mantenha a geração de recompensas determinística em relação aos resultados sorteados, quando aplicável, e compatível com o sistema atual.
- O Núcleo de Fragmento vale 30 fragmentos

---

## 5. Reestruturação das masmorras

As masmorras deverão apresentar mecânicas passivas próprias, capazes de definir a identidade estratégica de cada encontro.

As passivas devem ser implementadas utilizando os sistemas existentes de efeitos, atributos, turnos e estados de combate. Não crie habilidades novas para os chefes ou inimigos.

### 5.1. Golem Rúnico

**Passiva: Núcleo Rúnico**

O Golem Rúnico deverá funcionar como um chefe defensivo, cuja resistência aumenta progressivamente durante o combate.

Requisitos:

- Aumentar a defesa a cada rodada.
- Possuir menos pontos de vida do que a configuração atual equivalente, compensando parcialmente a redução por meio da defesa elevada.
- Fazer com que a progressão defensiva seja perceptível durante o encontro.
- Garantir que o aumento de defesa aconteça conforme a regra de rodada estabelecida pelo sistema de combate, sem acumulações acidentais causadas por múltiplos eventos do mesmo turno.

A mecânica deve favorecer combates nos quais o jogador precisa considerar o tempo necessário para derrotar o chefe, evitando que a defesa crescente transforme o encontro em uma luta indefinida.

### 5.2. Serpe

**Passiva: Tesouro da Serpe**

A Serpe deverá funcionar como um chefe que explora os benefícios acumulados pelos adversários e pune equipes excessivamente fortalecidas.

Requisitos:

- Implementar a mecânica de roubo de buffs dos inimigos utilizando o sistema existente de efeitos positivos.
- Fazer com que a Serpe cause mais dano ao utilizar seus ataques contra alvos beneficiados por buffs, conforme a lógica de combate existente.
- Aplicar Slow constantemente durante o encontro.
- Garantir que o roubo de buffs respeite as regras existentes de remoção de efeitos positivos, duração e quantidade de efeitos.
- Evitar que o roubo remova efeitos negativos ou estados que não sejam classificados como buffs.

A frequência, a duração e os valores numéricos devem utilizar parâmetros centralizados, sem introduzir habilidades novas.

### 5.3. Santuário

O Santuário substituirá a Cripta e passará a integrar a nova identidade temática do jogo.

**Passiva: Afogamento**

Requisitos:

- Todos os monstros afetados pela mecânica perdem 10% da vida a cada rodada.
- A mecânica também aplica Slow como debuff.
- O efeito deve funcionar de maneira consistente com o sistema de turnos e com a classificação atual dos efeitos.
- A perda percentual de vida deverá respeitar as regras de dano e sobrevivência do combate, incluindo eventuais proteções existentes.
- A aplicação do efeito deve evitar execuções duplicadas na mesma rodada.

O percentual deverá ser configurável para ajustes futuros, mantendo 10% como valor inicial especificado para esta versão.

### 5.4. Substituição da Cripta

- Remova a Cripta da experiência jogável desta versão.
- Utilize o Santuário como a nova forja e o sistema de masmorra associado.
- Preserve os arquivos, recursos visuais, scripts e dados antigos da Cripta.
- No botão de Masmorras, mantenha o ícone do Rei Ossudo, mesmo após a remoção da Cripta.
- Não substitua o ícone do Rei Ossudo por outro símbolo nesta versão, salvo se for tecnicamente necessário para manter a compatibilidade do componente.

---

## 6. Regras globais dos chefes

Revise o comportamento de todos os chefes para padronizar o funcionamento dos efeitos, das imunidades e dos ataques.

### 6.1. Aflição

A Aflição deverá continuar funcionando normalmente contra chefes.

- Não tornar a Aflição universalmente ineficaz contra chefes.
- Preservar o comportamento atual desse efeito, respeitando as regras já existentes para aplicação, duração, resistência e resolução.
- Não confundir Aflição com os efeitos explicitamente listados como imunes.

### 6.2. Ataques em área

Todos os golpes dos chefes deverão ser ataques em área.

Requisitos:

- Nenhum golpe de chefe deverá permanecer exclusivamente direcionado a um único alvo.
- Reutilize o sistema de seleção e resolução de alvos em área existente.
- Preserve os efeitos, danos e demais propriedades dos golpes atuais sempre que possível, alterando o escopo de alvos.
- Não crie habilidades novas.
- Verifique a compatibilidade com os sistemas de provocação, efeitos de estado, redução de dano e seleção de alvos.

A implementação deverá considerar o comportamento dos ataques em área diante de efeitos que normalmente alterariam a escolha do alvo.

### 6.3. Imunidades dos chefes

Crie uma lista centralizada de imunidades aplicáveis aos chefes, contemplando os seguintes efeitos:

- Atordoamento.
- Sono.
- Esquecimento.
- Silêncio.
- Bomba.

Essas imunidades devem ser verificadas pelo sistema de combate, e não apenas ocultadas na interface.

Requisitos:

- Centralizar a lista de imunidades para facilitar alterações futuras.
- Garantir que as imunidades sejam respeitadas por todas as fontes de aplicação dos respectivos efeitos.
- Preservar o funcionamento normal de efeitos não listados, incluindo Aflição.
- Evitar duplicação de verificações em cada habilidade ou componente.
- Manter a representação visual dos efeitos coerente com a imunidade efetivamente aplicada.

---

## 7. Reestruturação do efeito Provocar

Altere o funcionamento do efeito Provocar para que ele passe a ser um **buff aplicado a um aliado**, em vez de um debuff aplicado a um inimigo.

### Novo comportamento

Enquanto um personagem possuir o buff Provocar:

- Todos os inimigos deverão direcionar seus ataques contra o personagem que possui o efeito.
- A seleção de alvos deverá priorizar obrigatoriamente o personagem provocado.
- A provocação deverá permanecer ativa conforme a duração definida pelo sistema de efeitos.
- A expiração ou remoção do buff deverá restaurar o comportamento normal de seleção de alvos.

### Requisitos de implementação

- Reclassifique Provocar como um efeito positivo.
- Atualize sua apresentação visual, descrição e classificação nos componentes de interface.
- Integre a provocação diretamente ao sistema central de seleção de alvos.
- Não permita que um inimigo escolha livremente outro alvo enquanto existir um alvo válido sob provocação, respeitando apenas as restrições técnicas e as regras de combate já estabelecidas.
- Ataques em área continuam atingindo os alvos determinados pela regra de área; a provocação não deve converter esses ataques em golpes de alvo único.
- Se houver mais de um personagem com Provocar ativo, utilize uma regra determinística de prioridade, aproveitando a lógica existente quando possível.

---

## 8. Balanceamento dos buffs de Ataque e Defesa

Revise os bônus de Ataque e Defesa para evitar que seus benefícios superem excessivamente os efeitos negativos equivalentes.

### Regra de balanceamento

Os bônus positivos de Ataque e Defesa deverão ter magnitude inferior à dos debuffs negativos correspondentes.

Use os valores de referência de **+50% para o buff e -70% para o debuff**, quando os efeitos forem percentuais e utilizarem a mesma base de cálculo.

A relação desejada é:

| Efeito | Valor de referência |
|---|---:|
| Buff de Ataque | +50% |
| Debuff de Ataque | -70% |
| Buff de Defesa | +50% |
| Debuff de Defesa | -70% |

Os valores apresentados são referências para a configuração inicial, não uma autorização para alterar indiscriminadamente efeitos que utilizem escalas diferentes.

Requisitos:

- Centralizar os valores em parâmetros de balanceamento.
- Garantir que os buffs sejam menos impactantes do que os debuffs equivalentes.
- Verificar como os percentuais são aplicados no cálculo real de atributos.
- Evitar que o mesmo modificador seja aplicado duas vezes.
- Preservar efeitos que possuam uma lógica própria, desde que não contradigam a regra estabelecida.

---

## 9. Reorganização dos conjuntos de Sigilos

O sistema anteriormente denominado Runas deverá passar a utilizar o nome **Sigilos**.

Os conjuntos serão definidos pelo efeito que concedem, e não pela nomenclatura rúnica anterior.

### 9.1. Sigilos disponíveis nesta versão

Mantenha somente os seguintes conjuntos funcionais:

- Vigor.
- Pressa.
- Sutileza.
- Agredir.
- Contragolpe.
- Fúria.
- Letal.
- Amparo.

Os efeitos desses conjuntos deverão corresponder às funções existentes no sistema atual, respeitando o novo mapeamento de atributos e a distribuição especificada para as masmorras.

### 9.2. Sigilos temporariamente removidos

Remova os seguintes conjuntos da experiência jogável:

- Baluarte.
- Oblívio.
- Perdição.
- Tenacidade.
- Frenesi.
- Sifão.
- Tormento.
- Vigia.

### Requisitos para a remoção

- Os conjuntos listados não devem aparecer nas interfaces de obtenção, inventário, gerenciamento, equipagem ou recompensas.
- Não devem ser gerados por novos drops nem disponibilizados para equipagem nesta versão.
- Não devem interferir nos cálculos de atributos dos homúnculos.
- Preserve suas implementações, definições, ícones, recursos e dados nos arquivos do projeto.
- Não apague os arquivos originais, mesmo que seus efeitos ainda estejam conectados a sistemas antigos.
- Evite referências inválidas ao desativar conjuntos e mantenha o carregamento dos dados ativos funcional.

---

## 10. Nova simbologia planetária dos Sigilos

Substitua a simbologia e a nomenclatura rúnica anterior por uma identidade baseada nos planetas.

**O nome do conjunto deverá ser o nome do efeito.** O planeta funcionará como símbolo visual de identificação, sem alterar a função mecânica associada ao conjunto.

| Efeito / Nome do conjunto | Símbolo | Nome anterior |
|---|---|---|
| Vida | Terra | Uruz |
| Velocidade | Mercúrio | Sowilo |
| Precisão | Saturno | Kauna |
| Crítico | Netuno | Pertho |
| Contragolpe | Vênus | Thurisaz |
| Dano Crítico | Júpiter | Raido |
| Ataque | Marte | Tiwaz |
| Resistência | Urano | Iwaz |

### Diretrizes visuais

- Utilize os símbolos planetários correspondentes para representar os conjuntos.
- Os nomes apresentados na interface devem refletir os efeitos, e não os nomes rúnicos antigos.
- Preserve a associação entre cada efeito e seu símbolo.
- Não altere o funcionamento dos atributos apenas porque sua representação visual mudou.
- Não apresente os nomes antigos como nomes principais dos conjuntos na interface.
- Mantenha a correspondência visual consistente entre inventário, gerenciamento, equipagem, recompensas e detalhes dos Sigilos.

---

## 11. Distribuição dos Sigilos nas masmorras

Cada masmorra deverá possuir uma seleção específica de Sigilos associados à sua identidade temática.

### Golem Rúnico

Disponibilizar os seguintes conjuntos:

- Vida.
- Crítico.
- Velocidade.
- Ataque.

### Serpe

Disponibilizar os seguintes conjuntos:

- Contragolpe.
- Dano Crítico.
- Resistência.
- Precisão.

### Requisitos

- A distribuição deverá ser configurada no sistema de recompensas das respectivas masmorras.
- Não permita que os conjuntos exclusivos de uma masmorra sejam gerados indevidamente por outra.
- Preserve a estrutura atual de raridades e probabilidades, salvo quando uma alteração for necessária para garantir a distribuição especificada.
- Garanta que os conjuntos removidos temporariamente não sejam disponibilizados por nenhuma dessas fontes.
- Utilize os novos nomes e símbolos na apresentação das recompensas.

---

## 12. Reorganização do elenco de Homúnculos

Reduza o elenco disponível nesta versão aos homúnculos explicitamente listados abaixo.

Nenhum homúnculo adicional deverá aparecer como conteúdo selecionável, invocável ou disponível em sistemas de progressão.

### 12.1. Homúnculos de 2 estrelas

- Fada — renomear Pixie.
- Cogumelo.
- Planta Carnívora.
- Fada
- Gosma — Antigo Limo. 

### 12.2. Homúnculos de 3 estrelas

- Golfinho — novo homúnculo.  (delapouite/dolphin.svg)
- Raposa — substitui o Diabrete.  (caro-asercion/fox.svg)
- Bode — substitui o Limo.  (darkzaitzev/ham.svg)
- Lobo.
- Corvo.

### 12.3. Homúnculos de 4 estrelas

- Bandido.
- Pirata.
- Minerador. (apenas remova o Anão do nome e arquivos)
- Cavaleiro.

### 12.4. Homúnculos de 5 estrelas

- Lich.
- Mago.
- Druida.

### 12.4. Ajustes de raridade

| Homúnculo | Raridade anterior | Nova raridade |
|---|---:|---:|
| Gosma | 3 estrelas | 2 estrelas |
| Bandido | 3 estrelas | 4 estrelas |
| Corvo | 4 estrelas | 3 estrelas |

### Requisitos de implementação

- Ajuste as listas de seleção, os dados de raridade, os filtros, o grimório, os sistemas de obtenção e as demais referências que dependam do elenco ativo.
- Diabrete deverá ser substituído por Raposa na experiência jogável.
- Limo deverá ser substituído por Bode na experiência jogável.
- Não mantenha os homúnculos substituídos como opções disponíveis ao jogador nesta versão.
- Preserve os arquivos e dados dos homúnculos antigos.
- O Golfinho deverá ser adicionado como novo homúnculo, utilizando os sistemas e componentes genéricos existentes.
- Não crie habilidades para o Golfinho nem para qualquer outro homúnculo. As skills serão configuradas manualmente posteriormente.
- Não invente estatísticas, habilidades, passivas ou comportamentos que não estejam definidos nesta especificação ou nos dados já existentes.

---

## 13. Grimório: organização temática dos Homúnculos

Adicione ao Grimório uma aba, filtro ou mecanismo equivalente para organizar os homúnculos por temática.

O objetivo é permitir que o jogador navegue pelo elenco de maneira mais clara, agrupando entidades com identidade conceitual semelhante.

### Grupos temáticos

#### 13.1. Constelações

Grupo destinado aos homúnculos associados à temática das constelações.

#### 13.2. Adversários das Constelações

Nome provisório para o grupo de homúnculos que combatem as constelações ou representam forças contrárias à sua proposta.

Exemplos previstos:

- Bandido.
- Cavaleiro.
- Pirata.

O nome deverá ser tratado como provisório, preferencialmente por meio de uma configuração centralizada, permitindo sua alteração futura sem exigir mudanças estruturais no Grimório.

#### 13.3. Místicos

Grupo destinado aos homúnculos associados à temática mística.

### Requisitos de implementação

- Agrupe os homúnculos por categoria temática.
- Preserve os filtros existentes que continuem relevantes, incluindo raridade e outros critérios já disponíveis.
- Permita que a navegação temática funcione em conjunto com os demais filtros.
- Evite duplicar dados ou manter listas independentes que possam divergir do elenco ativo.
- Utilize uma fonte centralizada de classificação temática.
- Não atribua automaticamente todos os homúnculos a uma categoria com base apenas em seu nome. Utilize os dados existentes quando disponíveis e mantenha a classificação configurável.
- Garanta que apenas os homúnculos ativos nesta versão sejam exibidos.

---

## 14. Nova identidade visual dos Sigilos e dos símbolos alquímicos

Substitua a simbologia visual atualmente associada às runas por símbolos alquímicos.

A alteração deverá contemplar todos os locais relevantes da interface, incluindo:

- Ícones dos Sigilos.
- Encaixes de Sigilos.
- Botões de transmutação.
- Elementos de invocação existentes.
- Ícones de afinidade e identidade.
- Elementos decorativos e indicadores que utilizem a simbologia antiga.

### 14.1. Símbolo de encaixe dos Sigilos

O símbolo utilizado para representar o encaixe de Sigilos deverá ser o **símbolo alquímico de Criação**.

Utilize o mesmo símbolo de maneira consistente em todos os pontos de interação que representem a instalação ou equipagem de um Sigilo.

### 14.2. Símbolo de Trevas

Substitua o símbolo atual de Trevas pelo **símbolo alquímico da Lua**.

A alteração deverá afetar sua representação visual sem modificar automaticamente as regras mecânicas, resistências ou atributos associados a Trevas.

### 14.3. Transmutação no lugar de Invocação

Substitua o símbolo de Invocação atual pelo símbolo de Transmutação.

Atualize também os textos, descrições e referências visuais que apresentem o sistema como Invocação, de acordo com a nova nomenclatura de Transmutação Alquímica.

O texto decorativo ao redor do símbolo de Transmutação deverá ser exatamente:

*sit aliquid boni venire*

Preserve a grafia e a pontuação especificadas.

---

## 15. Reformulação visual de Constellation

Reformule a identidade visual do elemento Constellation para representar a nova direção temática do jogo.

### Composição obrigatória

- Um planeta Terra como símbolo central.
- Doze símbolos zodiacais irradiando a partir do planeta central.
- Os doze símbolos deverão representar os doze signos do zodíaco.
- A composição deverá transmitir a relação entre o planeta central e o sistema zodiacal.
- A distribuição dos símbolos deverá ser visualmente equilibrada, mantendo legibilidade em diferentes tamanhos de interface.

### Diretrizes de implementação

- Preserve o nome Constellation caso ele continue sendo o identificador utilizado pelo sistema.
- Substitua a composição visual anterior pela nova composição planetária e zodiacal.
- Reutilize componentes genéricos de interface e recursos visuais compartilhados.
- Evite criar versões incompatíveis do mesmo símbolo em diferentes telas.
- Caso a composição precise de uma versão compacta, mantenha a Terra como elemento principal e preserve a identidade zodiacal no espaço disponível.

---

## 16. Reformulação do avatar padrão

Altere a representação visual padrão do avatar.

- O avatar padrão deverá passar a utilizar o símbolo de runa atualmente associado ao avatar especial.
- O símbolo atualmente utilizado como avatar padrão deverá ser reclassificado como avatar especial.
- Atualize os identificadores visuais, a seleção de avatar, os previews e as demais referências pertinentes.
- Preserve os recursos originais nos arquivos do projeto.
- Garanta que os jogadores que já possuam um avatar selecionado não sejam afetados por referências inválidas após a mudança.

A troca deverá ser feita pela configuração ou referência dos recursos, sem duplicar desnecessariamente os arquivos de imagem.

---

## 17. Papéis dos Homúnculos

Padronize os papéis dos homúnculos nas seguintes categorias:

- Ofensivo.
- Defensivo.
- Suporte.

Substitua as categorias anteriores nos locais em que forem apresentadas ao jogador.

### Requisitos

- Utilize essas três categorias como classificação de papel.
- Atualize filtros, descrições, indicadores e demais componentes que apresentem os papéis dos homúnculos.
- Preserve a lógica de classificação existente quando compatível.
- Quando não houver uma classificação anterior confiável, mantenha a atribuição configurável em vez de inferir automaticamente o papel.
- Não crie habilidades ou mecânicas adicionais para justificar a classificação.

---

## 18. Transmutação e taxa de obtenção de Homúnculos LD

Atualize a apresentação e o balanceamento das taxas de obtenção associadas ao Pergaminho Místico, que deverá integrar o sistema denominado Transmutação .
Pergaminho Místico -> Transmutação Alquímica
Pergaminho de Luz e Trevas -> Transmutação 

### Taxa de LD

A taxa base de obtenção de homúnculos LD deverá ser de **0,1%** dentro dos Pergaminhos Místicos, diminuindo da taxa de 3 estrelas original.

A distribuição deverá seguir a regra:

\[
P(\text{LD individual})=\frac{0,1\%}{N_{\text{LDs}}}
\]

Onde (N_{\text{LDs}}) corresponde ao número total de Homúnculos LD elegíveis.

Exemplo: se houver 6 Homúnculos LD elegíveis, cada um terá uma probabilidade individual de (0,016%), totalizando 0,1% de chance de obter qualquer um deles.

Requisitos:

- Exibir a taxa de LD na interface do Pergaminho Místico.
- Apresentar a taxa de LD de 0,1% no Pergaminho Místico.
- Exibir ou disponibilizar de maneira compreensível a regra de distribuição entre os homúnculos LD elegíveis.
- Garantir que a soma das probabilidades individuais dos resultados LD corresponda à taxa definida, respeitando o modelo probabilístico existente.
- Preservar as probabilidades das demais categorias quando possível.
- Centralizar os parâmetros de probabilidade para permitir futuros ajustes de balanceamento.

**Importante:** antes de alterar o sistema probabilístico, identifique como as estrelas naturais, as raridades e a elegibilidade LD são representadas nos dados atuais. Não presuma uma fórmula adicional para o viés de estrela natural se ela ainda não estiver definida. Preserve a regra existente ou deixe esse parâmetro configurável para posterior ajuste.

---

## 19. Arquitetura de componentes e consistência visual

A reformulação deverá aproveitar a arquitetura visual existente e reforçar a reutilização de componentes genéricos.

Sempre que houver elementos visuais repetidos, prefira componentes reutilizáveis a implementações independentes.

Priorize:

- Um sistema consistente de ícones e símbolos.
- Componentes genéricos para exibição de Homúnculos.
- Componentes reutilizáveis para Sigilos.
- Componentes compartilhados para efeitos, buffs e debuffs.
- Componentes de exibição de raridade e papéis.
- Componentes de filtros e agrupamentos do Grimório.
- Componentes reutilizáveis para recompensas.
- Componentes genéricos para diálogos, botões, encaixes e interações.
- Configurações centralizadas para nomes, categorias, raridades, símbolos e regras de balanceamento.

Não transforme cada elemento específico em um componente isolado quando ele puder ser representado por uma configuração de um componente genérico.

Ao modificar a interface, mantenha os dados e as regras de negócio separados da apresentação visual, respeitando a arquitetura existente do projeto.

---
20. Homúnculos de 2 estrelas: despertar e estrelas prateadas
Os Homúnculos de 2 estrelas terão regras específicas de progressão e representação visual.
Despertar:
Homúnculos de 2 estrelas não poderão ser despertados.
Desative a mecânica de despertar exclusivamente para essa categoria.
Oculte ou desative as opções de despertar na interface desses Homúnculos.
Garanta que o sistema de progressão não permita despertar Homúnculos de 2 estrelas, mesmo por chamadas diretas ou outras interfaces.
Preserve o funcionamento atual do despertar para as demais raridades.
Estrelas prateadas:
Exclusivamente os Homúnculos de 2 estrelas possuirão estrelas prateadas.
Homúnculos de outras raridades deverão continuar utilizando estrelas de suas respectivas categorias visuais.
Atualize os indicadores de raridade em todas as interfaces relevantes, incluindo Grimório, detalhes dos Homúnculos, seleção, invocação e gerenciamento.
Centralize a regra de representação visual por raridade para evitar inconsistências entre componentes.

---
Utilização de ícones existentes
Priorize o reaproveitamento de ícones para implementar a nova identidade visual baseada em constelações, alquimia, transmutação, planetas e símbolos.
Fontes prioritárias:
Diretório local `F:\GameDev\GameIconsSVG`.
Wikimedia Commons, para os ícones que não estiverem disponíveis localmente ou não forem adequados ao projeto.
Requisitos:
Antes de criar ou desenhar novos ícones, inspecione o diretório local e identifique os recursos que correspondam à nova identidade visual.
Priorize arquivos SVG existentes, preservando sua qualidade em diferentes resoluções.
Utilize ícones da Wikimedia Commons quando necessário, verificando a licença, a atribuição exigida e as condições de reutilização de cada recurso.
Dê preferência a símbolos alquímicos, planetários, zodiacais e astronômicos visualmente consistentes entre si.
Preserve os arquivos originais e evite modificá-los diretamente quando for possível utilizar referências, configurações ou cópias de trabalho.
Se um ícone existente precisar de adaptação, mantenha uma versão original preservada.
Não invente caminhos nem presuma que arquivos existem: verifique o conteúdo disponível antes de referenciá-los.
Caso o diretório local não esteja acessível no ambiente de desenvolvimento, registre essa limitação e utilize a Wikimedia Commons como alternativa.
Centralize o mapeamento entre os ícones e os elementos do jogo, facilitando substituições futuras.
Resultado esperado: reutilizar o máximo possível dos recursos visuais disponíveis, mantendo uma identidade gráfica consistente e reduzindo a criação desnecessária de novos ícones.

---

## 22. Preservação de arquivos e remoção lógica de conteúdo

Esta é uma regra crítica para toda a implementação.

**Tudo que for removido nesta versão deverá deixar de existir na experiência jogável, mas permanecer preservado nos arquivos do projeto.**

Isso inclui:

- Homúnculos removidos ou substituídos.
- Conjuntos de Sigilos temporariamente desativados.
- Conteúdos antigos da Cripta.
- Símbolos e ícones anteriores.
- Recursos de interface substituídos.
- Definições de dados que ainda não serão utilizadas.
- Implementações de sistemas antigos que poderão ser reavaliadas.

### Procedimento esperado
Identifique os pontos em que cada conteúdo é registrado, disponibilizado ou apresentado ao jogador.
Remova ou desative suas referências nos sistemas ativos.
Preserve os arquivos e os dados originais.
Evite que conteúdos desativados sejam carregados indevidamente por sistemas de obtenção, seleção ou combate.
Não apague recursos apenas porque deixaram de ser utilizados na versão 0.1.
Registre as decisões de remoção lógica e as dependências relevantes para facilitar a reintrodução futura.
A prioridade é manter uma base jogável limpa sem destruir implementações que poderão ser úteis nas próximas versões.

---

## 23. Reestruturação de Passivas

Adicionar mais passivas que recebam objetos relevantes para que os exemplos a seguir possam ser configurados no builder e funcionem em combate

-- Passiva  Corvo Trevas(exemplo)
Aumenta seu dano conforme a quantidade de debuffs que o alvo possui em 1; X% a mais de dano por cada debuff. Quando em conjunto com Corvo Luz(exemplo) também aumenta o tempo de duração do debuff em 1. 
-- Passiva Corvo Luz (exemplo)
todos os alvos de ataque recebem Esquecimento. Se Corvo Trevas(exemplo) estiver vivo em campo, aplica também Karma.
-- Passiva lobo de trevas (exemplo)
Se sua defesa for maior que a do alvo, recebe provocar por 1 rodada. Se  lobo de luz (exemplo)  estiver vivo em campo, recebe contragolpe por 1 rodada

---

## 24. Plano de execução

Execute a reformulação em etapas, verificando a consistência do projeto após cada uma.

### Etapa 1 — Auditoria e mapeamento

- Identificar a arquitetura atual.
- Mapear os sistemas de combate, recompensas, masmorras, Sigilos, homúnculos e transmutação.
- Identificar todas as referências à nomenclatura antiga.
- Mapear dependências entre dados, recursos visuais, interfaces e sistemas de jogo.
- Identificar componentes genéricos que possam ser reutilizados.
- Determinar como o versionamento está implementado.

### Etapa 2 — Dados e nomenclatura

- Atualizar a nomenclatura de apresentação.
- Definir o elenco ativo de Homúnculos.
- Atualizar raridades.
- Definir os conjuntos de Sigilos ativos.
- Centralizar os mapeamentos de símbolos planetários.
- Configurar as categorias temáticas do Grimório.
- Desativar logicamente os conteúdos fora do escopo.

### Etapa 3 — Combate e masmorras

- Implementar as passivas do Golem Rúnico, da Serpe e do Santuário.
- Atualizar as regras globais dos chefes.
- Aplicar a lista centralizada de imunidades.
- Reclassificar e corrigir a mecânica de Provocar.
- Ajustar o balanceamento de Ataque e Defesa.
- Validar a distribuição dos Sigilos por masmorra.

### Etapa 4 — Recompensas e transmutação

- Implementar a exclusividade das categorias de drop.
- Atualizar a nomenclatura de Transmutação Alquímica.
- Atualizar a apresentação das taxas de LD.
- Validar as probabilidades e o viés de estrela natural.
- Garantir a exclusão de conteúdos desativados nas fontes de recompensa.

### Etapa 5 — Interface e identidade visual

- Atualizar a simbologia planetária dos Sigilos.
- Substituir os símbolos rúnicos pelos símbolos alquímicos definidos.
- Atualizar os símbolos de Criação, Lua e Transmutação.
- Reformular o símbolo de Constellation.
- Atualizar o avatar padrão e o avatar especial.
- Reformular os papéis dos Homúnculos.
- Implementar os filtros temáticos do Grimório.
- Revisar as referências visuais à Cripta e ao Rei Ossudo.

### Etapa 6 — Validação e versionamento

- Atualizar a versão para 0.1.
- Verificar erros de compilação, referências inválidas e falhas de carregamento.
- Executar testes de combate e geração de recompensas.
- Verificar a integridade dos dados existentes.
- Confirmar que os conteúdos removidos permanecem nos arquivos.
- Confirmar que somente o elenco e os Sigilos definidos estão disponíveis.
- Validar a consistência visual e terminológica em todas as telas afetadas.

---

## 22. Critérios de aceitação

A reformulação somente estará concluída quando todos os critérios abaixo forem atendidos.

### Sistemas de jogo

- [ ] Uma mesma resolução de recompensa nunca concede mais de uma das três categorias de drop restritas.
- [ ] O Golem Rúnico ganha defesa a cada rodada e possui a configuração reduzida de vida.
- [ ] A Serpe rouba buffs, causa mais dano contra alvos buffados e aplica Slow constantemente.
- [ ] O Santuário aplica perda de 10% de vida por rodada e Slow.
- [ ] A Cripta não está disponível na experiência jogável.
- [ ] O ícone do Rei Ossudo permanece no botão de Masmorras.
- [ ] Aflição continua funcionando normalmente contra chefes.
- [ ] Todos os golpes dos chefes são ataques em área.
- [ ] As imunidades dos chefes estão centralizadas e são aplicadas pelo sistema de combate.
- [ ] Provocar é um buff e direciona os ataques inimigos para seu portador, respeitando as regras de área.
- [ ] Os buffs de Ataque e Defesa possuem magnitude inferior à dos debuffs equivalentes.

### Sigilos e Homúnculos

- [ ] Apenas os oito conjuntos de Sigilos definidos estão disponíveis.
- [ ] Os oito conjuntos removidos estão indisponíveis na experiência jogável.
- [ ] Os conjuntos das masmorras correspondem às listas especificadas.
- [ ] Os símbolos planetários correspondem aos efeitos definidos.
- [ ] Apenas os Homúnculos listados estão disponíveis.
- [ ] Bandido possui raridade de quatro estrelas.
- [ ] Corvo possui raridade de três estrelas.
- [ ] Diabrete foi substituído por Raposa.
- [ ] Limo foi substituído por Bode.
- [ ] O Golfinho foi adicionado sem criação de skills.
- [ ] O Grimório permite agrupar os Homúnculos por temática.
- [ ] Os papéis estão padronizados como Ofensivo, Defensivo e Suporte.

### Identidade visual e nomenclatura

- [ ] Monstros foi substituído por Homúnculos na apresentação ao jogador.
- [ ] Pergaminhos foi substituído por Transmutação Alquímica onde aplicável.
- [ ] Glifos e Runas foram substituídos por Sigilos na apresentação ao jogador.
- [ ] Os símbolos de encaixe, Trevas e Transmutação foram atualizados.
- [ ] O texto *sit aliquid boni venire* está presente junto ao símbolo de Transmutação.
- [ ] Constellation utiliza a composição com a Terra central e os doze símbolos zodiacais.
- [ ] O avatar padrão e o avatar especial foram trocados conforme especificado.
- [ ] A interface não apresenta inconsistências entre nomenclaturas antigas e novas.

### Dados, arquivos e versão

- [ ] A versão do projeto está definida como 0.1.
- [ ] Os arquivos dos conteúdos removidos continuam preservados.
- [ ] Não foram criadas habilidades novas.
- [ ] Não existem referências quebradas nos sistemas ativos.
- [ ] Os sistemas de obtenção respeitam o elenco e os Sigilos disponíveis.
- [ ] Os dados de probabilidade da Transmutação Alquímica estão consistentes com as taxas definidas.
- [ ] O projeto permanece compilável e funcional dentro do escopo da versão.

---

## 23. Resultado esperado

Ao concluir a tarefa, entregue uma versão 0.1 funcional e consistente com a nova identidade de **Constelações, Alquimia, Transmutação e Sigilos**.

O resultado deve priorizar a coerência sistêmica, a clareza visual, a consistência terminológica e a preservação do código existente.

Não amplie o escopo com funcionalidades não solicitadas. Não crie habilidades automaticamente. Não exclua arquivos antigos. Não considere a simples alteração dos textos como conclusão da tarefa.

Implemente as mudanças nos sistemas responsáveis por seu comportamento real, valide as integrações e mantenha todo o conteúdo temporariamente removido preservado para futuras reavaliações.

Ao final, apresente um relatório objetivo contendo:
As mudanças implementadas.
Os sistemas e componentes afetados.
Os conteúdos removidos logicamente e os arquivos preservados.
Os testes executados e seus resultados.
Os requisitos que não puderam ser concluídos e suas justificativas.
Os pontos que ainda exigem decisões manuais, especialmente os relacionados ao balanceamento probabilístico, à classificação temática e aos conteúdos que serão desenvolvidos futuramente.
**A prioridade desta versão é estabelecer uma base sólida para a nova direção do jogo, sem comprometer a integridade do projeto nem antecipar decisões de design que ainda serão tomadas.**