# Ações do servidor: runas e invocação

Melhorar, afiar, encantar e reavaliar uma runa, e invocar monstros (Transmutação), são feitos pelo servidor da conta.
O jogo só pede; quem sorteia, cobra e grava é o servidor. Assim o jogador não consegue editar o save para ter uma
runa +12 perfeita nem a garantia de 5★ na mão.

## Como funciona

- `Core/Progression/ServerAction.cs`: `ActionRequest` (o que o jogador escolheu), `ServerActions.Apply` (a regra:
  chama `RuneInventory`, `RuneReappraisal` e `SummonRitual`) e `ActionDelta` (o que mudou). `ApplyDelta` leva o
  resultado à cópia do jogo sem trocar o estado inteiro (as telas guardam as runas e os monstros).
- Com conta e conexão: `AccountSession.Act` envia antes o que está no aparelho (`Flush`), faz `POST /game/action`
  com a revisão de partida e aplica o resultado. Sem conexão, avisa e nada é gasto (`serverAction.offline`).
- Sem conta e no save de teste (`--save=`): a mesma regra roda no aparelho (`GameRoot.Act`).
- O servidor (`D:\dev\sigilos-server`, `POST /game/action`) lê o save dele, aplica a ação com o próprio sorteio e grava
  a revisão seguinte.
- `PUT /save` passa por `Core/Progression/SaveAudit.cs`: uma runa que já existe não pode mudar (só o cadeado e quem
  a usa), uma runa nova tem que parecer um drop (+0, sorteios dentro das faixas), os contadores da garantia não
  mudam, e uma troca de conta (outra versão ou data de começo) só vale se parece uma conta sem invocações.
  O primeiro envio de uma conta vale como veio (leva o save que já estava no aparelho).

## Limites conhecidos

- Crescimento dos monstros (`Core/Progression/MonsterAudit.cs`): só o que mudou ou é novo desde o save guardado é conferido.
  A estrutura (estrelas entre a natural e 6, nível até o máximo, habilidades e despertar possíveis) vale sempre; o
  crescimento tem que caber no que foi pago: experiência = 3 × Essência gasta mais as vitórias que o tempo permite (uma a cada 0,6 s, a luta mais curta, e nunca mais que a Mana permite; maior
  recompensa do jogo × equipe), estrelas = Fragmentos (`Evolution.Cost`), habilidades = monstros fundidos, despertar =
  Essência. Os pontos da Arena Astral ganhos cabem nos Convites gastos × `pointsMax` (o ciclo novo reinicia). Motivos:
  `monster_structure`, `monster_growth`, `monster_stars`, `monster_skills`, `arena_points`. É um limite por envio, não
  uma conta exata: quem envia muitas vezes seguidas multiplica a folga (20 mil de Essência por envio).
- O servidor monta o baú da Arena do Véu e a defesa da Arena Astral pelo save que guarda, mas ainda não confere
  estrelas, nível e habilidades dos monstros; os pontos da Arena Astral são escritos pelo aparelho.
- Drops de Campanha, Masmorra e Correio continuam sorteados no jogo; só a forma da runa é conferida.
- Moedas (Essência, Transmutações, Gemas) só passam pela conferência de saltos do `SaveValidator`.
- Monstros de fusão, Fragmentos e Correio ainda podem ser forjados pelo envio do save.
- O primeiro envio de uma conta não é conferido.
- Sem conexão, estas ações param (a Batalha automática e o resto do jogo seguem pelos 120 s de jogo sem servidor).
