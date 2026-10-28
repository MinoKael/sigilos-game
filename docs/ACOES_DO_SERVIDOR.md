# Servidor autoritativo: o aparelho só mostra

Toda mudança no jogo de uma conta acontece no servidor: o que gasta, rende, sorteia, organiza ou depende da hora. O
aparelho guarda uma cópia do JSON para as telas e a muda só com o que o servidor devolve. Ele não envia o save: `PUT /save`
e `PUT /save/delta` devolvem 403 `save_readonly`. No aparelho ficam as telas, as regras de tela, os assets e os dados
(`Data/*.json`) em cópia só de leitura.

## O caminho único: `GameApi`

- `GameEntry/GameApi.cs` é o único caminho do jogo até as regras que mudam a conta: `Act` (ações), `Start`/`Finish` (lutas),
  `Rename`, `Sync` e `Resync`. O `GameRoot` chama só ele (`Act`, `Do`, `Pref`, `Request`, `StartBattle`, `FinishBattle`).
- Um pedido por vez (`SemaphoreSlim`): a cópia tem uma revisão só, então um pedido espera o anterior voltar.
- As runas e os monstros que vêm na resposta são trocados pelos da cópia com o mesmo `Id` (as telas comparam por referência).
- No servidor, o par é `Server/GameApi.cs` (`Run`): confere sessão e revisão, lê o save guardado, aplica a regra do `Core`
  com o sorteio e a hora UTC do servidor, grava a revisão seguinte e devolve o `Patch`.
- Rotas: `POST /game/init`, `/game/action`, `/game/battle/start`, `/game/battle` e `/game/rename`.

## Ações (`POST /game/action`)

- `Core/Progression/ServerAction.cs`: `ActionRequest` (o que o jogador escolheu), `ServerActions.Apply` (a regra, que chama
  os serviços do `Core`) e `ActionDelta` (o que mudou: `Patch`, `Count`, `Gained`, `Idle`, `Monsters`, `Rune`, `Runes`).
- `ServerActionKind`:
  - Sigilos: `UpgradeRune`, `UpgradeRunes` (a melhora seletiva: todas até o alvo, ou nenhuma), `GrindRune`,
    `EnchantRune`, `ReappraiseRune`, `SellRunes`, `EquipRunes`, `UnequipRunes`, `OpenRuneKit`.
  - Monstros: `Summon`, `Fuse`, `Release`, `Evolve`, `Awaken`, `Infuse`, `Shelve`, `ExchangeFragments`.
  - Recursos: `ClaimMail`, `CollectIdle` (a canalização e a recarga de Mana), `BuyShop`, `CheckIn`.
  - Organização: `Team`, `SetAvatar`, `Toggle` (travas e favoritos), `Preference` (visto, idioma, tutorial, foco do chefe,
    Batalha automática).
  - Arenas: `ArenaOpen`, `ArenaRefresh`, `ArenaBuy`, `VeilBuy`.
- `ClaimMail`: o servidor confere se a carta existe, é da conta e não venceu, paga e a marca como coletada. As cartas do
  próprio jogo (`Letters`: prêmio da temporada da Arena, marcos de nível da conta, marco de 6★) ficam no save (`PlayerState.Inbox`, id
  `auto:N`), vencem em 7 dias e saem dele ao coletar; o check-in do dia (e abrir a conta) tira as vencidas. As do servidor (Guid) seguem no banco.
- `CheckIn` (`Core/Progression/DailyCheckIn.cs`, prêmios em `Data/checkin.json`): conta o dia de hoje e manda a carta dele
  (`Letters.CheckIn`, 7 dias de prazo). É um ciclo de 28 dias contados por entradas: o dia avança na primeira chamada de
  cada dia UTC, faltar dias não tira nada, e depois do 28 volta o 1. Quem decide é o relógio do servidor; o aparelho pede
  ao voltar ao Santuário (e ao voltar do segundo plano ou da falta de conexão) e a ação é recusada se hoje já contou. A
  janela do check-in abre sozinha uma vez por dia por aparelho (`AccountStore.CheckInShown`, só do aparelho); tocar no
  botão do calendário também conta o dia, se ainda não contou. Limite: sem tocar, o dia que vira com o jogo parado no
  Santuário só conta na próxima volta a ele. O pedido automático nunca mostra erro (falhou, fica para a próxima vez); o do
  botão mostra.
- `Evolve` também paga o marco de 6★ (`Core/Progression/SixStar.cs`): ao chegar a 6★, manda uma carta (`Letters.SixStarFirst`, o primeiro
  6★ do elemento, ou `Letters.SixStar`, os seguintes) com `Milestones.FirstSixStar`/`NextSixStar`; o elemento fica em
  `PlayerState.SixStarElements` e o Sigilo Lendário de 6★ da primeira carta é sorteado ao coletar (`rune:6:Legendary`).
  O servidor e o save de teste (`debugger`) usam a mesma regra, então a carta chega igual nos dois.
- Feitos no Chat global (sigilo a +15, invocação rara) são anunciados pelo servidor, nunca pelo aparelho.
- Trocar o nome (`POST /game/rename`): de graça, uma vez a cada 7 dias corridos (UTC, `Core/Progression/AccountRename.cs`).
  O servidor confere o nome e a semana, grava o nome e marca `PlayerState.LastRename` no mesmo pedido; antes da semana
  passar, `rename_cooldown`. O primeiro nome de uma conta sem nome não conta. A Loja não vende mais a troca; o botão fica
  nos Ajustes, apagado com o dia em que abre.
- `Core/Progression/StatePatch.cs`: a diferença entre dois estados em JSON. Itens com `Id` (runas, monstros) mudam no lugar;
  moedas somam como diferença. Serve a qualquer ação, sem classe de resultado por ação.

## Lutas em dois pedidos

`Core/Progression/ServerBattle.cs` (`BattleTickets`, `ServerBattles`). `BattleKind`: Stage, Floor, Constellation, Arena.

- **Começo** (`POST /game/battle/start`): confere a porta (aberta, Mana da vitória, vaga de runa), monta a equipe pelo save
  guardado e sorteia a semente. Devolve `Seed`, `Ticket` e `Clock` (a hora UTC do servidor). Não paga nada. Um bilhete por
  conta, de 2 h; começar outra luta troca o bilhete.
- **Batalha automática**: o começo já luta (piloto automático) e guarda o resultado no bilhete; o aparelho mostra a mesma luta
  com a semente. O fim só vale depois do tempo da luta na tela (`ReadyShare` 0,9 do tempo, menos `ReadySlack` 1 s); antes,
  o servidor devolve `ReadyIn` e o `GameApi` espera e pede de novo (até 3 vezes).
- **Luta jogada**: ao fim o aparelho manda as decisões (`BattleScreen.Actions`) com o bilhete; o servidor refaz a luta
  (`Core/Battle/BattleReplay.cs`) com a semente e a equipe do começo. Decisão inválida ou faltando vira o piloto automático.
- **Fim** (`POST /game/battle`): só a vitória paga. A porta é conferida de novo na hora do fim (`Problem`, por exemplo
  `NoMana`) e a recompensa sai com o sorteio do servidor. O tempo do recorde (`BattlePace.Seconds`) e `Records.Submit` são
  do servidor. Bilhete que não confere: `battle_ticket` (o jogo mostra `serverBattle.ticket`).
- **Parar a Batalha automática** descarta a luta em andamento: ela não paga nem cobra. Cada luta paga só no fim dela.
- **Exploração Estelar**: o encontro e o recorde são os do mês do começo (`Clock` do bilhete); o percurso e a recompensa,
  os do mês do fim.
- **Arena Astral**: o começo renova a semana, gasta o Convite e marca a pendência (`Arena.Begin`); o fim joga a luta com o
  bônus de sequência da hora do começo e fecha com `Arena.Finish` (pontos, sequência, Astrarium). Os oponentes saem das defesas
  guardadas no servidor.
- **Arena do Véu** (RTA): a partida é do servidor; ele espelha perfil e resultado no save guardado (`StoredPlayer.Mirror`) e
  manda `"synced"`; o jogo chama `GameApi.Resync`.

## Relógio

Tudo em UTC. A hora vem do servidor; o aparelho nunca a envia. A semana da Arena vira no domingo às 22h UTC (19h de Brasília).

## Sem conexão

Com conta, sem servidor nada do jogo abre (`serverAction.offline`, `serverBattle.offline`). No save de teste
(`-- --save=debugger`, local e offline), o `GameApi` roda as mesmas regras no aparelho, com o próprio livro de bilhetes e a
hora UTC, e grava o save a cada mudança. Só para teste.

## Versão e publicação

- `Protocol.Minimum = 3` (3: cartas do jogo no save). Um aparelho com protocolo menor recebe 426 `update_required` no `/session/claim` (o jogo mostra
  `account.update_required` e não entra).
- Conteúdo (`Data/*.json`) muda pelo servidor sem build novo; código novo (efeito, passiva, tela) exige build e sobe o
  `protocol`. Nada de código remoto: o servidor só envia dados.
- Servidor e jogo saem juntos (o `Core` é compartilhado). No Windows o aviso de atualização (`latest.json`) roda na abertura;
  no Android é preciso instalar o APK novo.

## Limites conhecidos

- A semente sai no começo: um cliente adulterado pode espiar o resultado da Batalha automática ou planejar a luta jogada.
  A decisão e o pagamento são sempre do servidor, e a derrota não custa nada.
- Os bilhetes ficam em memória: reiniciar o servidor perde a luta em andamento (o fim recebe `battle_ticket` e nada é pago).
- Cada toque custa uma ida ao servidor (decisão de produto): latência perceptível em rede lenta.
- O `Mirror` do Véu e as ações sobem a mesma revisão: um toque com revisão velha recebe 409 `save_conflict`, o jogo baixa a
  cópia (`SyncNeeded`) e o jogador toca de novo.
- `StatePatch` leva remoções, itens novos e campos, mas não a ordem das listas com `Id`; as telas ordenam por conta própria.
