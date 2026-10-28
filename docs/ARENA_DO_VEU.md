# Arena do Véu: o duelo em tempo real

Duelo entre dois jogadores de verdade, 4 contra 4, com montagem do time, banimento, Líder e prazo de decisão a
cada turno. Convive com a Arena Astral (que enfrenta defesas guardadas): usa os mesmos Graus, com o **Áster** no
lugar dos pontos, e tem moeda própria, os **Ecos do Véu**, que pagam a **Loja do Véu**. Abre pelo botão "Arena do
Véu" da tela da Arena Astral.

## Regras

- **Entrada:** conta conectada ao servidor e pelo menos 10 homúnculos diferentes (o baú que vai para a fila é a
  melhor cópia de cada um, até 40, das mais fortes às mais fracas).
- **Montagem** (`Core/Rta/RtaDraft.cs`): quem abre é sorteado. Cada lado escolhe 5, em turnos de 1, 2, 2, 2, 2 e 1;
  o que um lado escolheu o outro não pode escolher. Depois cada um bane 1 dos 5 do rival (o primeiro bane antes) e
  indica a Líder entre os 4 que sobraram (a Liderança vale para o time, como nas outras lutas).
- **Prazos** (`RtaClock`): 30 s por decisão da montagem e 15 s por turno. Estourou, o automático decide e o prazo
  seguinte cai pela metade até 3 s; na luta, estourar já no mínimo recomeça de 15. Agir a tempo devolve o prazo inteiro.
- **Luta** (`RtaMatch`): a sessão de duelo comum, com cada jogador decidindo só os turnos do próprio time. Depois de
  20 turnos, e a cada 15, todos ganham 10% de Ataque e perdem 10% da Vida máxima, somando (`RtaRules.Pressure*`).
  O limite de 99 rodadas dá empate. O turno extra já era de um por turno (`ExtraTurnSet`).
- **Áster e Ecos:** vitória e derrota mudam o Áster pela mesma conta da Arena Astral (`pointsBase`, `pointsSlope`...
  de `Data/arena.json`); o Grau de cada um paga os Ecos da vitória (`ecos` em cada Grau). Empate e Treino não mudam nada.
- **Treino contra um fantasma:** a mesma sala rodando no próprio jogo (`GameEntry/Account/LocalRta.cs`), sem servidor
  nem Áster, contra um time pronto do Grau. Funciona offline.
- **Fora do que existe por enquanto:** temporadas e zerada semanal do Áster, partidas de colocação, decaimento por
  inatividade, amistosos com regras próprias, replays e espectadores.

## Como funciona

- **O servidor roda a luta.** `F:\GameDev\sigilos-server` compila o `Core/` do jogo (projeto `GameCore`, caminho pela
  propriedade `SigilosGame`) e lê o `Data/` dele. Cada jogador manda só decisões (`act`: habilidade, lado e posição do
  alvo). O servidor responde com a decisão aplicada e uma impressão digital da luta (`RtaMatch.Hash`); cada jogador
  roda a mesma luta no aparelho (`RtaReplica`) e confere. Divergiu, vale o resultado do servidor.
- **A sala não conhece rede** (`Core/Rta/RtaRoom.cs`): recebe mensagens (`Post`), manda mensagens (`RtaPlayer.Send`) e
  vigia os prazos. O servidor liga a sala a WebSockets (`Server/Rta/RtaHub.cs`, rota `/rta`), e o Treino liga a mesma
  sala ao jogo. Os testes (`Tests/RtaTests.cs`, `Tests/RtaRoomTests.cs`) rodam salas inteiras sem rede.
- **O jogador de qualquer lado vê o próprio time embaixo.** A sessão tem sempre o primeiro a escolher como `Allies`;
  o segundo vê os `Enemies` embaixo (`BattleScreen`, campo `_mine`). Os alvos viajam por lado e posição, nunca por objeto.
- **O automático não usa o sorteio da luta** (`RtaMatch.Automatic`): o piloto dos inimigos sorteia o alvo com a
  semente da luta, o que só o servidor faria e desviaria a cópia de cada jogador.

## Limites conhecidos

- O baú vem do save que o servidor guarda (o jogo o sobe antes de entrar na fila), e as runas dele passam pela
  conferência do `PUT /save` (`docs/ACOES_DO_SERVIDOR.md`). Falta conferir estrelas, nível e habilidades dos monstros:
  um save forjado nisso vale. O mesmo vale para a defesa da Arena Astral, que o servidor também monta pelo save
  (os pontos dela ainda vêm do aparelho).
- O Áster e as vitórias moram no servidor (`rtaProfiles`); os Ecos que o servidor deu a uma luta que acabou com a
  conexão caída chegam ao conectar de novo (`VeilState.EcosSeen`).
- A hora do prazo usa o relógio do servidor; o relógio da tela é uma estimativa a partir de quando a mensagem chega.
- O Android roda .NET 9 e o servidor .NET 10 (Linux ARM64): contas de ponto flutuante transcendentes podem diferir
  entre eles. A impressão digital existe por isso, e arredonda a duas casas.

## Provar no jogo

Treino, sem servidor: `godot -- --save=debugger --screen=veil`, "Treinar contra um fantasma". Pelo servidor, siga a
receita de duas contas locais de `docs/SERVIDOR_PROPRIO.md` (duas janelas, `--save=acc_a` e `--save=acc_b`, ambas
com `--server=http://127.0.0.1:5080/`).
