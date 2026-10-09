# Arquitetura

## Camadas

```
Data/*.json ──texto──▶ GameEntry ──▶ Core   (regras, sem Godot)
                          │
                          └──▶ UI    (telas; lê Core, nunca GameEntry)
```

- **Core/** não conhece o Godot. Recebe texto (não caminhos), `Random` e `DateTime` como parâmetro.
  Por isso roda igual no jogo e no console de testes, e o combate é determinístico por semente.
- **UI/** recebe o que mostra no construtor e avisa por evento C# (`FightRequested`, `AwakenRequested`...).
  Nenhuma tela muda o `PlayerState` nem salva. Nenhuma tela tem texto escrito no código: tudo vem de
  `Data/texts/pt-BR.json` por chave (`Locale.T("runes.upgrade_to", ...)`); `UI/Texts.cs` só monta nomes
  por enum e as descrições geradas das regras. A interface é para o toque: botões de ação com texto
  (`GameButton`), símbolo sozinho só no ✕ e na seta de voltar, nenhuma dica de mouse, e toda
  explicação numa janela (`Dialog`) colada no elemento tocado.
- **GameEntry/** é a raiz de composição: o `GameRoot` assina os eventos das telas, chama as regras do
  Core, salva e troca de tela. É o único lugar que junta tudo. A conta (`GameEntry/Account/`) e a
  atualização do executável (`GameEntry/Update/`) também moram aqui: são a fronteira com a rede, e o
  Core não sabe que elas existem.

Dentro do Core:

| Namespace | Papel | Depende de |
| --- | --- | --- |
| `Core.Content` | Definições lidas de `Data/`, o vocabulário (Glifo, conjunto, raridade, efeitos), o modelo de atributos (`StatModel`) e o `GameDatabase` | — |
| `Core.Runes` | Runa, conjuntos, pedras, tabelas de Summoners War, sorteio, busca e bônus | Content |
| `Core.Progression` | Crescimento por estrelas e nível, experiência (tabela de Summoners War), Evolução, Despertar, Fusão (nível de habilidade), ficha de atributos, ociosidade, Campanha, Masmorras, Mana, nível da conta, Loja | Content, Runes, Player |
| `Core.Battle` | Combate: Ímpeto, recarga das habilidades, a ordem de um golpe, automático. O que cada efeito, status, Passiva e conjunto faz são estratégias nas subpastas `Effects`, `Statuses`, `Passives` e `Sets` | Content, Runes, Progression |
| `Core.Player` | O save (`PlayerState`), coleção e Baú (`Roster`), equipes por conteúdo (`Teams`), inventário de runas e a ponte para a batalha | Content, Runes, Battle |
| `Core.Summoning` | Gacha | Content, Player, Progression |

`Core.Battle` não conhece `PlayerState`: o simulador monta um `BattleTeam` à mão. A tela de Monstros e
a batalha calculam atributos pelo mesmo `SummonStats`, então o número que o jogador vê é o que luta.

## Onde mexer

| Quero... | Mexa em |
| --- | --- |
| Nova família ou nova variante | `docs/summon_family_builder.html` (abra no Chrome ou no Edge e use "Abrir pasta do projeto"): ele calcula os atributos e grava `Data/summons/<familia>_family.json`, um arquivo por família com até 5 variantes. Família nova pede também um SVG em `Assets/Creatures/` (o desperto é o mesmo desenho com a aura; ver "Novo símbolo"). Nenhum código, a não ser que a Passiva dela ainda não exista (ver "Nova Passiva") |
| Habilidades de uma variante | `"skills"` da variante, no construtor ou direto no arquivo da família: a primeira é a básica (sem `cooldown`), as outras ativas têm `cooldown`, no máximo uma é `passive`; `levels` são as melhorias por cópia fundida (`Damage`, `Recovery`, `EffectRate`, `Cooldown`); `awakenedEffects` troca os efeitos ao despertar |
| Habilidade fora do padrão (golpes diferentes entre si) | só dados: os efeitos acontecem na ordem da lista, então "3 golpes comuns e um quarto que ignora a Defesa" são dois efeitos `Damage` (`{"hits": 3}` e `{"ignoreDefense": 1}`). Ver "Combate: regras como estratégias" |
| O que o Despertar dá | `"awakening"` da variante: `stat` (bônus de atributo) ou `skill` (habilidade nova, que só existe desperta); os atributos maiores são os `"awakened_stats"`, que o construtor calcula |
| Atributos de uma invocação ou de um chefe | ninguém escreve à mão: saem do modelo (`Data/stat_model.json`) pelas estrelas naturais e pelo papel. Para puxar uma variante para um lado, o viés dela ou o da família, no construtor. Ver "Atributos: o modelo de BVP" |
| Recalibrar o jogo inteiro (orçamentos, perfis de papel, pesos, viés de elemento) | aba Modelo do construtor, "Salvar no projeto" e "Recalcular todas": regrava `Data/stat_model.json`, todas as famílias e os chefes. Depois, `dotnet run --project Tests` (confere) e `-- --simulate` (o efeito no balanceamento) |
| Inimigos de fase ou andar | ondas em `Data/stages.json` e `Data/dungeons.json`: `"summon"` é uma variante de invocação, `"enemy"` um chefe de `Data/enemies.json`; `stars`, `level` (no 6★, até 60) e `scale` (Vida e Ataque) de cada fase e andar. Depois, `dotnet run --project Tests -- --simulate`: cada fase contra o time de referência dela, cada andar contra o degrau que ele pede (`Tests/ReferenceTeams.cs`) |
| Chefe ou lacaio de Masmorra | aba Chefes do construtor: papel, estrelas, escalas (`healthScale`, `attackScale`, `defenseScale`), `minion` (lacaio: luta ao lado do chefe e não é chefe), as habilidades e a escala de cada andar, com os atributos de luta em cada fase e andar. O Golem, a Serpe, a Cripta e o Santuário são de especialização (GDD, seção 11): confira com `dotnet run --project Tests -- --dungeons` (cada andar contra o time típico, as equipes de especialista de `ReferenceTeams.Specialists`, o ponto doce, a preparação do andar 5 e o time forte genérico) e `--fight=golem5:prepared` |
| Reforço dos inimigos | `BattleFactory.FoeScale` (invocação inimiga, por estrelas naturais), `BattleFactory.FoeBoost` (todo inimigo: Vida, Ataque e Defesa), `scale` da fase e do andar; acima do 6★ nível 40, `Growth.FoeFraction` (só inimigos, até `Growth.MaxFoeLevel`) |
| Limite de efeitos num monstro | `BattleRules.MaxStatuses` (10, somando tudo, cada cópia da Aflição conta), conferido em `EffectResolver.Attach`: cheio, o efeito novo não pega (`StatusBlocked`), o que ele já tem se renova |
| Novo efeito de status | o nome em `Core/Content/StatusKind.cs`, a estratégia em `Core/Battle/Statuses/` (uma das prontas, `StatChange` e `DamageOverTime`, ou uma classe nova que herda de `StatusBehavior`), a linha em `StatusBehaviors`, os textos `effect.Nome.name` e `.info` em `Data/texts` (com o número em `Texts.Explain`), o símbolo em `Assets/Effects/` e a linha em `STATUS_DOCS` do Family Builder. Traços prontos em `UnitBehavior` para o status só declarar: `SkipsTurn` (Atordoamento, Sono), `BlocksHarmful` (Imunidade), `BlocksBeneficial` (Karma), `BlocksHealing` (Ferida), `BlocksCooldownSkills` (Silêncio), `SuppressesPassive` (Esquecimento), `CritTaken` (Resistir Crítico); `StatChange` com `additive` soma pontos (Crítico+) |
| Nova Passiva | o nome em `Core/Content/PassiveKind.cs`, uma classe em `Core/Battle/Passives/` que herda de `UnitBehavior` e sobrescreve o momento dela, a linha em `PassiveBehaviors`, o texto `passive.Nome` em `Data/texts` e o símbolo em `Art.Skill`. Com a pasta do projeto aberta, o construtor já oferece o nome novo |
| Novo tipo de efeito de habilidade | o nome em `Core/Content/EffectKind.cs`, uma classe em `Core/Battle/Effects/` que herda de `SkillEffect`, a linha em `SkillEffects`, a descrição em `UI/Texts.cs`, o texto em `Data/texts`, os alvos que ele aceita em `Core/Content/EffectTargets.cs` e a linha em `EFFECT_DOCS` do Family Builder (o que faz, campos, alvos, modelo). Campos que já existem em `EffectDefinition` e servem a vários tipos: `scope` e `onlyStatus` (que efeitos contam, `StatusFilter`), `from`, `bonus`, `count`, `fallback`. O que um efeito deixa para os seguintes da mesma habilidade fica no `Cast` (`DamageBonus`, `HealBonus`, `CooldownReduction`, `Killed`) |
| Novo momento da luta (algo que nenhuma regra consegue ouvir hoje) | um método vazio em `Core/Battle/UnitBehavior.cs` e a chamada no ponto em que ele acontece (`BattleSession` para turno e onda, `EffectResolver` para habilidade e golpe) |
| Mexer no código do combate sem mudar a regra | `dotnet run --project Tests -- --digest > antes.txt`, mexa, rode de novo e compare: mais de mil lutas com semente fixa, uma linha por luta |
| Chefe em batalha | `BattleUnit.IsBoss` (todo inimigo de `Data/enemies.json` que não é lacaio, marcado em `BattleFactory.EnemyFoe`); a Aflição tira do chefe só `BattleRules.BossAfflictionShare`; quem cai para voltar (o Rei Ossudo, Passiva `Undying`) segura a onda; na tela, o cartão maior (`UnitView.BossScale`), o lugar no meio do arco (`BattleArena.SetEnemies(..., boss)`) e a barra do alto (`UI/Components/BossBar.cs`, que segue `UnitView.Refreshed`) |
| Foco do automático (inimigo tocado e chefe) | o inimigo tocado é o `focus` de `AutoPilot.ForAlly(..., focusBoss, focus)` (vem antes do chefe), guardado na `BattleScreen` (`ToggleFocus`, a mira é `UnitView.SetFocused`) e esquecido quando ele cai ou a onda acaba; o chefe é `AutoPilot.ForAlly(..., focusBoss)` e `AutoBattle.Run(..., focusBoss)`; a escolha é `PlayerState.FocusBoss` (começa ligada), mudada no `PauseMenu` (só em luta com chefe: `BattleSession.HasBoss`) e lida pelo `GameRoot` na luta da tela e na Batalha automática. O simulador e os testes lutam sem foco |
| Efeitos no cartão da luta | `UI/Components/StatusChip.cs`: um quadradinho por efeito em cima do cartão (`UnitView`) e na barra do chefe; vermelho com o símbolo branco (negativo), verde com o preto (positivo), e os turnos num selo escuro no canto |
| Janela do jogo no PC | `GameEntry/WindowAspect.cs`: corrige a janela para 16:9 a cada mudança de tamanho (a borda puxada manda) e, maximizada ou em tela cheia, deixa o conteúdo em 16:9 com faixas. No celular não faz nada (stretch "expand" do project.godot) |
| Ficha de atributos (total e bônus) | `UI/Components/StatTable.cs`: o total (`StatSheet.Total`) em branco e o que as runas somam (`StatSheet.Runes.Stats`) em verde, menor |
| Botões lado a lado | `Layout.Grid(colunas)`: o `GameButton` preenche a célula e quebra o texto, com largura mínima da palavra mais longa (ou a do `Wide`). Numa `Layout.Flow` ou numa fileira sem largura, a grade é que divide o espaço |
| Ritmo da luta na tela e da Batalha automática | `UI/BattlePace.cs`; quantas lutas em `AutoBattle.RepeatRuns` |
| Campo de batalha (onde ficam as unidades, o oval, a corrida até o alvo, o respingo) | `UI/Components/BattleArena.cs` (os arcos de cada lado em graus, `Slots`, a corrida e a volta), `ImpactLayer` (o respingo), o ritmo em `UI/BattlePace.cs` (`Approach`, `Hit`, `Return`); o cartão da unidade em `UnitView` (os efeitos do chefe ficam só na `BossBar`, e quem ficaria com a fileira de efeitos embaixo dela desce: `BattleArena.Ceiling` e `UnitView.Headroom`); os cantos e a pausa em `BattleScreen` e `PauseMenu` (a pausa é `SceneTree.Paused`, e a espera entre eventos usa timer que para junto) |
| Nova Masmorra ou andar | `Data/dungeons.json` (andares, conjuntos, `mana`, `firstClearGold`, `scale` de força e o drop: `grades` e `rarities`, a chance em % de cada estrela e raridade, a mesma tabela em todas, e `scrollChance`, a chance em % de um Pergaminho Místico a cada vitória); regras e sorteio em `Core/Progression/Dungeons.cs`. Confira o drop com `dotnet run --project Tests -- --drops` (cada andar vence 20 000 vezes e o que caiu é comparado com a tabela; no fim, o que cada andar rende por dia de Mana) |
| Custo em Mana de fase | `Data/stages.json` (`mana`) |
| Mana máxima e recarga | `Core/Progression/Mana.cs`; a recarga entra pela canalização em `Core/Progression/Idle.cs` |
| Nível da conta e Ouro por nível | `Core/Progression/Account.cs` |
| Invocação | `Core/Summoning`: os pergaminhos (`ScrollKind`: Místico, Luz e Trevas, Lendário), as taxas e os elementos de cada um e a garantia do Místico em `SummonRates`, o sorteio em `SummonRitual` (`Perform(..., kind)`); a tela escolhe o pergaminho nas abas (`SummonScreen`) |
| Marcos (Pergaminhos especiais e Núcleos de Infusão) | `Core/Progression/Milestones.cs`: o `Prize` da primeira vitória vem dos dados (`firstClearLegendary`, `firstClearLightDark`, `firstClearCores` e `coreChance` em `Data/stages.json` e `Data/dungeons.json`), o dos níveis da conta da tabela de lá (`Account.GiveExperience` entrega). A vitória traz o prêmio em `VictoryReward.Prize`, e a tela mostra (`BattleResultPanel.AddPrize`). A parte da Torre está reservada no GDD, seção 12 |
| Núcleo de Infusão | `Core/Content/InfusionCore.cs`: guardado como monstro (`OwnedSummon.IsInfusionCore`), mas fora de `Data/summons` e da invocação (o `GameDatabase` só o acha pelo id). Funde em qualquer família (`Fusion.CanFuse`); equipe, runas, nível, evolução, Despertar e soltar recusam. Na tela de Monstros, o detalhe dele é `StorageScreen.CoreDetail`, e a fusão o oferece depois das cópias da família (`FusionDialog.Candidates`) |
| Troca de Fragmentos | `Core/Progression/FragmentExchange.cs` (uma 4★ de Fogo, Água ou Vento escolhida); o botão e a grade ficam na tela de invocação |
| Loja | ofertas em `Data/shop.json`; regra em `Core/Progression/Shop.cs`. A Expansão de Coleção (`CollectionExpander`) soma vagas a `PlayerState.CollectionCapacity` (salvo na conta; 50 numa conta nova) até `Account.MaxCollectionCapacity` e esgota lá (`Shop.IsSoldOut`, o aviso na `ShopScreen`). A troca de nome (`RenameAccount`, 10.000 de Ouro) é a única que depende do servidor: o `GameRoot.ShowShop` abre a janela do nome e só chama `Shop.Buy` (que tira o Ouro) depois que o servidor aceitou; nos Ajustes fica só o primeiro nome, de graça |
| Chave de recuperação e "Esqueci a senha" | o servidor cria a chave (quatro palavras) no cadastro e, numa conta de antes dela, na primeira entrada ou renovação, e guarda só o hash. O jogo recebe em `AuthClient` (`RecoveryKeyIssued`), guarda em `AccountStore.RecoveryKey` até o "Já guardei" e mostra pelo `RecoveryKeyDialog` (`GameRoot.ShowRecoveryKey`). A troca de senha é a `PasswordResetDialog` (e-mail, chave, senha nova). Socorro: `POST /admin/accounts/recovery` no servidor (docs/api.md de lá) |
| Qualquer texto da interface | `Data/texts/pt-BR.json` (a base) e a mesma chave em `Data/texts/en.json`, depois `py Tools/texts/check_texts.py`. Chave nova não pode ter o nome de um grupo que já existe (`filter.order` apagaria `filter.order.*`) |
| Novo símbolo (ícone) | o SVG do acervo de game-icons.net copiado para a pasta dele em `Assets/` (ou baixado do Commons: `py Tools/art/fetch_commons_assets.py --chrome ...`), a linha de crédito em `Tools/art/commons_assets.csv`, `py Tools/art/fetch_commons_assets.py` (refaz `Assets/CREDITOS.md`), `py Tools/art/render_png.py` (os PNG) e `Art.Icon("nome")` |
| Ícone do app (Android: launcher e splash; Windows: o `.exe` e a janela) | `py Tools/art/launcher_icons.py`: gera `Assets/Launcher` a partir de `Assets/Icons/rune.svg`, com as cores da paleta no topo do script; o docstring diz que arquivo vai em que campo (presets Android e Windows, e `application/config/windows_native_icon`) |
| Efeito sonoro novo (ou mudar um) | a receita no grupo da pasta em `Tools/sounds/Recipes/` (uma entrada `r.Of(nome, descrição, camadas)`), com os motivos de `Tools/sounds/Motifs/`; depois `dotnet run --project Tools/sounds -c Release -- --only=<pasta>/<nome> --report`. Sai em `Assets/Audio/<pasta>/` e entra no catálogo `Assets/Audio/sounds.json` pelo nome lógico (`combat.elements.fire_impact`). Classes de mixagem, tom e o passo a passo em `docs/SONS.md` |
| Tocar um som (ou mudar onde soa) | `Sfx.Play("rewards.star_up")` no ponto da ação (`UI/Audio/Sfx.cs`); som de reserva dos componentes com `Sfx.Fallback`; a luta em `UI/Audio/BattleSounds.cs`. O nome inteiro no código, sem montar; `SoundTests` confere contra o catálogo (`docs/SONS.md`, "No jogo") |
| Símbolo de um efeito de batalha | `Assets/Effects/<efeito>.svg` (o nome do `StatusKind` em minúsculas) + render; no texto rico, `Texts.Term(StatusKind)` põe o símbolo na frente |
| Símbolo de uma habilidade | `Art.Skill` (o efeito que ela aplica, senão o Glifo do que ela faz) |
| Tamanho da runa em miniatura | `RuneTile.Side` (quadrada; cada lugar passa a escala) |
| O espaço da runa (o selo) | `RuneTile.DrawSeal`: um hexágono atrás do Glifo, cada lado um lugar do `SigilRing` (`RuneTile.SlotAngle`: o 1 em cima, os outros a cada 60° no sentido do relógio), com o lado do espaço aceso na cor da raridade; o número do espaço, o cadeado e a melhora ficam presos pelo centro à linha de baixo (`OnBottomLine`) |
| Correio (cartas com recompensas) | regra em `Core/Progression/Mailbox.cs` (`Claim` soma e anota em `PlayerState.ClaimedMail`), a conversa em `GameEntry/Account/CloudMail.cs` (`AccountSession.FetchMail`/`ClaimMail`), a janela em `UI/Screens/MailboxDialog.cs`, o botão (uma cápsula do `CurrencyBar.Capsule`, só com a carta, colada nas moedas) e o selo em `HubScreen`; o `GameRoot` busca a cada volta ao Santuário e coleta gravando o save antes de avisar o servidor. Enviar: `POST /admin/mail` no servidor (docs/api.md de lá). Presentes que não são moeda (monstro, runa, retrato) vêm na chave da recompensa (`monster:id`, `rune:5:Hero:Vigor`, `avatar:id:awakened`), lidos por `MailGift.Parse` e dados por `Mailbox.Claim` com o `GameDatabase` |
| Memória (objetos do Godot criados pelo C#) | o lado C# de cada estilo, tween ou material segura o objeto até o coletor de lixo passar, e ele quase não faz a coleta completa. Por isso: os `Doodle` dividem materiais (`Doodle.Shared`, um por tinta, tremor, elemento e uma de 6 sementes; quem chama `SetInk` ganha um próprio) e o `GameRoot` faz uma coleta completa em segundo plano a cada `CollectSeconds`. Antes, uma luta juntava milhares de materiais por minuto (um buffer na placa de vídeo cada) e a memória passava de 20 GB |
| Rolar arrastando com o mouse | `UI/Components/DragScroll.cs`, ligado em toda `Layout.Scroll` (e nas abas da `TeamScreen`); fora de aparelho de toque, e cancela o clique do botão sob o mouse quando vira arrasto |
| Barra de rolagem com trilho à vista | `GameTheme.Grooved(bar)` (a da Loja, que rola de lado) |
| Como uma runa aparece (a ficha) | `UI/Components/RuneCard.cs`: a mesma em todo lugar (tela de Runas, vitória, Batalha automática e o toque longo de todo `RuneTile`); por cima da tela, `RuneDialog.Show` (uma `Dialog` com a ficha, e quem abre põe os botões) |
| Resultado da luta (vitória e derrota) | `UI/Screens/BattleResultPanel.cs` (a barra de experiência de cada monstro em `ResultMonster.StepsOf`); o melhor tempo de cada fase e andar em `Core/Progression/Records.cs`, guardado em `PlayerState.BestTimes` |
| Novo botão | `GameButton.Of(T("texto"), ação, ButtonKind.Primary, "ícone")`: texto sempre, o símbolo acompanha; `Primary` (laranja) é a ação da tela, `Secondary` a comum, `Danger` (vermelho) vender e soltar; `WithCost` põe o preço numa segunda linha. Destino grande (tela inicial, tela Batalha, barra de baixo) é `TileButton`. `SigilButton` (só símbolo) fica para o universal: fechar, voltar, pausa, velocidade, habilidade de batalha |
| Explicar algo (o que antes era dica) | `Dialog.Info(elemento, título, texto)`: abre colada no elemento, com a seta apontando para ele. Janela com botões: `Dialog.Open(...)` + `AddAction`; pergunta: `Dialog.Confirm` |
| Toque e toque longo | `Press.On(controle, toque, segurar)` (ou `Press.Feed` dentro do `_GuiInput`, `Press.OnButton` num botão): segurar é 0,45 s parado; arrastar cancela, para a rolagem funcionar no dedo. Botão direito do mouse conta como segurar |
| Resumo de um monstro (toque longo) | `UI/Components/MonsterSummary.cs`: `Open(de, variante, cópia)` para o que o jogador tem ou vê no catálogo, `Open(de, BattleUnit)` para quem está lutando (Vida, efeitos e recargas de agora). `CreatureCard`, `TeamStrip`, `UnitView`, `TurnOrderBar`, os inimigos da fase e o resultado da luta já abrem |
| Escolher entre opções | `ChoiceButton` (mostra "Campo: valor" e abre a lista numa `Dialog`) ou `Choices.Open` direto; escolher um monstro é o `MonsterPicker` |
| Abas | `TextTabs` (escritas, com detalhe opcional embaixo, em pé ou deitadas); o Compêndio e o Grimório usam `Layout.Tab` num `TabContainer` |
| Grade de runas ou de monstros | `TileGrid`: cabem quantas colunas a largura deixar, e a sobra vira espaço entre elas (a grade vai de borda a borda em qualquer tela) |
| Bloquear (runa ou monstro) | `Rune.Locked` e `OwnedSummon.Locked`, salvos com o resto; quem recusa é a regra: `RuneInventory.Sell` (e `SellAll`) não vende runa bloqueada, `Fusion.CanFuse` não aceita material bloqueado e `Fusion.Release` não solta (o botão é Soltar; Liberar é desbloquear). Na tela, o cadeado é do `RuneTile` e do `CreatureCard` (aparece em todo lugar), e o botão Bloquear/Desbloquear fica nas fichas de Runas, de Monstros e na runa e no monstro que caem na Batalha automática; a runa que cai na vitória tem Bloquear ao lado de Vender e Guardar. Na seleção de vários da `StorageScreen`, o bloqueado se marca (vai ao Baú e sai dele), e Soltar conta só os desbloqueados |
| Gasto grande de uma vez (subir até o nível máximo, melhorar a runa até o próximo marco) | pergunta antes, com o gasto e aonde chega: `Dialog.Confirm` (o da runa é `RuneDialog.ConfirmUpgrade`) |
| A constelação da Canalização (tela inicial) | `UI/Components/Constellation.cs`: ocupa o painel; o sigilo do centro (`SetCenter`), o anel do tempo acumulado, os orbes desenhados em volta (`Places`, `Radii`) e o que vai logo embaixo do centro (`Attach`) |
| Versão do jogo (canto de baixo à esquerda do Santuário) | `application/config/version` no `project.godot`; numa build de depuração aparece "· depuração" |
| Destinos da barra de baixo e da tela Batalha | `UI/Screens/Destination.cs` (símbolo e nome) e `GameRoot.Go` (qual tela abre e para onde volta) |
| Voltar (seta, Esc e o Voltar do celular) | `BackButton` e `Layout.Header`; o Voltar do Android vira a ação `ui_cancel` no `GameRoot`. Quem fecha ou volta ouve `ui_cancel` em `_UnhandledInput`, que corre do último nó para o primeiro: a janela de cima fecha antes da tela voltar |
| Idiomas oferecidos na Configuração | um arquivo por idioma em `Data/texts`; `ContentLoader.Languages()` lista, `PlayerState.Language` guarda a escolha |
| Traduzir | copie `Data/texts/pt-BR.json` com outro nome, troque os textos, ponha no grupo `names` o nome de cada coisa dos dados (pelo nome em português; o `check_texts.py` diz quais faltam) e rode com `-- --language=nome` |
| Nome de invocação, habilidade, fase, Masmorra, chefe ou oferta da Loja | o `"name"` (ou `"base_name"`) no arquivo de `Data/`, em português, e a tradução no grupo `names` do `en.json` (a chave é o nome em português: renomear pede renomear lá também) |
| Regras do combate (ordem de turno, Ímpeto, dano, Defesa, dano fixo, escala, ignorar redução) | `docs/COMBATE.md`: o que vale, de onde veio, o que difere da referência de propósito e onde mora cada número. Número novo de combate vai para `BattleRules` (ou `RuneSets`, se é de conjunto), nunca para dentro da estratégia; escala nova só com decisão no GDD |
| Balancear números do combate | `Core/Battle/BattleRules.cs`, depois `dotnet run --project Tests -- --simulate` (os times de referência em `Tests/ReferenceTeams.cs`; os testes `CampaignTests` conferem a fase 50 e que cada andar de Masmorra pede o time do drop dele, `ReferenceTeams.AtFloor`) |
| Atributos por papel e estrelas naturais | `Data/stat_model.json` (orçamento de BVP e perfis de papel), pelo construtor. Uma variante pode ter estrelas naturais próprias (`star_grade` na variante, `SummonDefinition.StarGrade`; `Rarity` já devolve as dela): o orçamento é o dessas estrelas e o viés da família não vale para ela, só o ajuste fino dela e o do elemento (`StatModel.Compute` e `familyBiasFor` no construtor, iguais) |
| Atributos por estrelas e nível de agora | faixas por estrela em `Core/Progression/Growth.cs`: a fração do 6★ nível 40 |
| Crítico, Dano crítico, Resistência e Precisão de base | `"base_stats"` em `Data/stat_model.json` |
| Experiência por nível e valor da Essência | `Core/Progression/Leveling.cs` (tabela de Summoners War por estrela, `ExperiencePerEssence`); experiência por fase e andar em `Data/stages.json` e `Data/dungeons.json` |
| Evolução (custo por estrela) | `Core/Progression/Evolution.cs` |
| Despertar (custo por estrelas naturais, bônus de atributo) | `Core/Progression/Awakening.cs`; o orçamento desperto em `Data/stat_model.json` |
| Fusão de cópias (sobe uma habilidade sorteada), Fragmentos ao soltar | `Core/Progression/Fusion.cs` |
| Runas: tabelas, custos, espaços | `Core/Runes/RuneRules.cs` (uma tabela por atributo, de 1★ a 6★) |
| Conjuntos de runas | `Core/Runes/RuneSets.cs`; o efeito em combate em `Core/Battle/Sets/` (uma estratégia por `RuneSetEffect`) |
| Pedras (Afiar, Gema) | `Core/Runes/RuneForge.cs` (regras), `Core/Runes/RuneRules.cs` (faixas), chance de cada grau por andar (`rarities`) em `Data/dungeons.json` |
| Glifo de um conjunto ou de um atributo | `Core/Runes/RuneSets.cs` (conjunto → Glifo); atributo → Glifo em `UI/Texts.cs` (`GlyphOf`); a letra que desenha cada Glifo na fonte das runas em `Texts.Rune`; na tela, `RuneGlyph` |
| Fontes | `Assets/Fonts`: a SFC Wezards é a do jogo (com reserva do sistema para ★ × ⟳), a Kehdrai a das runas, a Cinzel (serifada, peso 600 por `FontVariation`) a de destaque, só nos títulos (`GameTheme.Display`: `Title`, `Heading`, `PageHeading`); carregadas em `GameTheme` |
| Batalha automática (quantas lutas) | o padrão e o máximo em `Core/Battle/AutoBattle.cs` (`RepeatRuns`, `MaxRuns`); a escolha é o `AutoBattleSetup` |
| Batalha automática por trás | `GameEntry/AutoBattleRunner.cs` (o nó que corre as lutas, mesmo com a tela trocada), `UI/AutoBattleRun.cs` (o estado que a interface lê, com o evento `Changed`), `UI/Components/AutoBattleBadge.cs` (o selo no alto, à esquerda das moedas e do correio: o que estiver no grupo `AutoBattleBadge.CornerGroup`; sem moedas na tela, no centro) e `UI/Screens/AutoBattleDialog.cs` (a janela; as ações dela são o `AutoBattleActions` que o `GameRoot` monta). A runa que caiu e saiu da conta, vendida ali, no inventário ou no resultado, aparece como vendida (`AutoBattleRun.IsSold`) |
| Atualização do executável do Windows | `GameEntry/Update/`: `UpdateManifest` (o `latest.json` assinado) e `Updater` (pergunta ao servidor, download conferido, troca do `.exe` e reabertura, com a chave pública em `Updater.PublicKey`). A janela é o `UpdateDialog`; o fluxo fica no `GameRoot` (seção "Atualização"). Publicar: `dotnet run --project Tools/release -- windows`. Tudo em `docs/ATUALIZACOES.md` |
| Conta, login e save em nuvem | `GameEntry/Account/`: `AuthClient` (cadastro, login, tokens), `SessionLock` (um aparelho por vez: tomar, bater, soltar), `CloudSave` (baixar e enviar), `CloudSync` (quem ganha entre aparelho e nuvem), `AccountStore` (`user://nome.account.json`) e `AccountSession` (o nó que junta tudo, com o batimento e o envio periódico). As telas: `LoginScreen`, `SaveConflictDialog`, `AccountNameDialog` (o nome da conta, único no servidor e mostrado no Santuário) e a conta no `ConfigPanel`; o fluxo fica no `GameRoot` (seção "Conta"). O endereço do servidor é `AccountSession.DefaultServer` (`-- --server=url` troca). O servidor é outro repositório: `docs/SERVIDOR_PROPRIO.md` |
| Regiões da Campanha | `Campaign.RegionStarts` (a primeira fase de cada uma; a última vai até o fim) e os textos `campaign.region.N`; o mapa mostra uma região por vez (`CampaignScreen`, abas `TextTabs`, `StagePath` com a primeira e a última fase) |
| O que o Santuário mostra aos poucos | `Core/Progression/Features.cs`: a tabela parte → fase que abre (as Masmorras vêm de `Data/dungeons.json`). Quem pergunta: a barra do `HubScreen`, a Canalização, o botão Batalha automática da `CampaignScreen`, a porta das Masmorras no `MapScreen` e o Comprar Pergaminhos da `SummonScreen`; a vitória que abre avisa por `BattleOutcome.Opened`. Nada fica salvo: "nova" é a parte da última fase vencida |
| Luta de treino | `Core/Progression/Tutorial.cs` (a equipe emprestada, o encontro e quando abre: `PlayerState.TutorialDone`), `UI/Components/TutorialCoach.cs` (as lições, na ordem, e o que cada uma deixa escolher) e o `coach` da `BattleScreen` (espera as explicações antes da vez do jogador, filtra habilidades e alvos, repassa os eventos). O fluxo é `GameRoot.ShowTutorial`; refazer fica nos Ajustes (`ConfigPanel`). Depois da luta, a primeira invocação: `Tutorial.GuidesFirstSummon` dá à `SummonScreen` o mesmo `TutorialCoach` (`Hint`, `Clear`, `Say`), e o Cavaleiro de Fogo e a 5★ garantidos são de `SummonRitual.Roll` (`SummonRates.FirstSummon`) |
| Fundir cópias (a janela) | `UI/Screens/FusionDialog.cs` (só a mesma família; Marcar as do mesmo elemento; o que vai sumir e a confirmação); a regra em `Fusion.CanFuse` (mesma `FamilyId`) |
| Retrato da conta (Santuário) | a regra em `Core/Progression/Account.cs` (`Avatars`: o padrão, cada variante que a conta tem e o desperto de quem tem cópia desperta, e os liberados; `Current`; `SetAvatar`), guardado em `PlayerState.Avatar` e `AvatarAwakened` (nulo: o padrão). Cada retrato é um `AccountAvatar` com o tipo (`AvatarKind`: Summon ou Special); os especiais, que não são monstros, num catálogo só, `SpecialAvatars` (`Core/Progression/AccountAvatar.cs`: id e ícone; nome em `avatar.special.*`). O retrato e a janela de escolha, com uma aba por tipo, em `UI/Components/AvatarPicker.cs`; a escolha abre do Grimório do Invocador (`SummonerGrimoireDialog.AvatarRequested`), que a conta no `HubScreen` abre. O correio pode liberar um retrato sem o monstro, ou um especial (`Account.UnlockAvatar`, guardado em `PlayerState.AvatarUnlocks`) |
| Grimório do Invocador (o livro da conta) | `UI/Screens/SummonerGrimoireDialog.cs`: capítulos em marcadores romanos (Invocador, Masmorras, Céu, Selos), cada um em duas páginas com fólio; o estilo das páginas vem das variações do tema (`GameTheme.PagePanel`, `PageRule`, `PageHeading`, `PageText`, `PageFaded`, `PageBar`: aqui, o pergaminho `Ornament.Page` e a `StarRule` a tinta) e das cores `Palette.Ink`, `InkFaded`, `Rubric`, `Violet`, `Parchment`. Os desenhos a tinta (anel do retrato, marcas dos andares, selo de cera, o céu da faixa) são controles pequenos dentro dela, em `_Draw`. Em Masmorras, a equipe do melhor tempo do andar mais fundo vencido (`Records.Team`). Os selos em `Core/Progression/Seals.cs` (`Journey`, `Collection`): lidos do save, sem guardar nada e sem prêmio. Desde quando a conta existe: `PlayerState.Started` (posto pelo `NewGame`; num save anterior, pelo `Account.Open`). Textos `book.*` |
| Melhores tempos | `Core/Progression/Records.cs`: o tempo por chave (`StageKey`, `FloorKey`) em `PlayerState.BestTimes` e a equipe que o fez (`RecordMember`: variante e se lutou desperta, a Líder primeiro) em `PlayerState.BestTeams`; o `GameRoot.Fight` grava os dois juntos no `Records.Submit`. Nos tempos de antes da equipe, só o andar mais fundo de cada Masmorra a ganha, ao abrir a conta (`Records.FillDeepestTeams`, com a equipe salva hoje para a Masmorra) |
| Abrir a conta | `Core/Progression/Account.cs` (`Open`, chamado pelo `GameRoot.Play`): completa o que um save anterior aos campos não tem. `Started` passa a ser agora, e o recorde do andar mais fundo ganha a equipe. `PlayerState.GuildJoined` (a entrada na guilda atual) fica reservado no save: ainda não há guildas |
| Chat global | só ao vivo, nada guardado: a conexão em `GameEntry/Account/ChatLink.cs` (WebSocket `/chat`, aberta e fechada pela `AccountSession` com a conta conectada), as linhas na memória em `UI/ChatFeed.cs` (as últimas `MaxLines`), o balão por cima de tudo em `UI/Components/ChatBubble.cs` (no lugar que a tela marca: `ChatBubble.Slot`, que o `Layout.Header` põe logo depois do título e a `BattleScreen` ao lado da rodada, ou `ChatBubble.Dock`, a constelação do `HubScreen`. O `GameRoot.Swap` passa a tela nova ao `Follow`; sem lugar marcado, o balão fica no canto de cima à esquerda da janela. Ao lado, a faixa da última linha de outra conta por `LastSeconds`) e a janela em `UI/Screens/ChatDialog.cs`. Os feitos que o jogo anuncia sozinho (monstro 5★, runa +15 com os subatributos) em `Core/Social/Feat.cs` (`Feats.Of`); o `GameRoot` liga tudo (o `_chat` no `_Ready` e o `Share` depois da invocação e da melhora da runa). O que o servidor aceita e repassa: docs/api.md de lá |
| Amigos | a conversa em `GameEntry/Account/CloudFriends.cs` (`AccountSession.Friends`: listar, convidar pelo nome, aceitar, recusar e desfazer), a janela em `UI/Screens/FriendsDialog.cs` (o destino `Destination.Friends` da barra de baixo) e o fluxo no `GameRoot` (seção "Amigos"). Só o nome da outra conta e, de amigo, se está jogando (o batimento da sessão); o limite (`FriendList.Limit`) conta os convites enviados |
| Números na tela | `Texts.Number`: o inteiro com o separador de milhar do idioma (205.390), sem abreviar em k; de um milhão para cima, em milhões com três casas, cortando o resto (1.167.890 → 1,167M), para nunca mostrar mais do que o jogador tem. Todo recurso e custo na tela passa por ele (moedas, recompensas, preços, os "Você tem"); as cápsulas e os botões crescem com o número |
| Favoritos | `OwnedSummon.Favorite` (salvo); o coração do `CreatureCard`; a ordem (favoritos primeiro) nas telas de Monstros, Equipes e no `MonsterPicker` |
| Gema de Reavaliação | a regra em `Core/Runes/RuneReappraisal.cs` (o que sai e o que volta, com o subatributo original que `RuneForge.Enchant` guarda em `RuneSubstat.Original`), o estoque em `PlayerState.ReappraisalGems`, a oferta em `Data/shop.json` (`ReappraisalGems`) e o botão Reavaliar da `RuneScreen` |
| Visual do Despertar | `Doodle` com `aura` (o elemento) usa `Assets/Shaders/doodle_awakened.gdshader`: borda acesa por dentro na cor do elemento, anel animado próprio de cada elemento e o brilho que corre; sem brilho por fora |
| Ênfase das invocações raras | `UI/Screens/SummonScreen.cs` (a revelação) e `UI/Components/SummonHalo.cs` (raios e halo, nas cores do elemento) |
| Conselhos da derrota | `Core/Player/DefeatAdvice.cs` (o que falta na equipe, na ordem do que mais rende) e a faixa `DefeatBand` do `BattleResultPanel` |
| Envio para a nuvem | `AccountSession.SaveSoon` (dois segundos depois da última mudança; várias viram um envio) é chamado por `GameRoot.Save`; a invocação espera `Flush` antes de mostrar o resultado |
| Coleção, Baú, equipes | `Core/Player/Roster.cs`, `Core/Player/Teams.cs`, `PlayerState.CollectionCapacity`/`TeamSize` |
| Vagas do inventário de runas | `RuneInventory.Capacity` (runas equipadas, inclusive em monstro do Baú, não contam) |
| Taxas do gacha | `Core/Summoning/SummonRates.cs` |
| Ociosidade | `Core/Progression/Idle.cs` |
| Compêndio (regras) e Grimório (catálogo) | textos em `Data/texts` (`compendium.*`, `grimoire.*`); cartões em `UI/Screens/CompendiumScreen.cs` e `GrimoireScreen.cs` |
| Cores, fontes, molduras, rolagem | `UI/Style/Palette.cs` (cores; os tokens do grimório: `Violet`, `Indigo`, `Starlight`, `Parchment`, `Ink`, `Rubric`), `UI/Style/GameTheme.cs` (estilos por tipo de controle e as variações de página: `PageHeading`, `PageText`, `PageFaded`, `PageBar`), `UI/Style/Ornament.cs` (texturas geradas: couro com moldura, pergaminho, gema da rolagem, sigilo de marcar), `UI/Style/StarRule.cs` (o separador: traço com a estrela no meio), `Assets/Shaders/backdrop.gdshader` (o fundo: céu noturno, nebulosa e astrolábio) |
| Nova tela | `UI/Screens/` + o `Show...` correspondente em `GameEntry/GameRoot.cs` e, se for destino de navegação, um valor em `Destination` |
| Ver contornos, nomes, valores e origem dos nós com o jogo rodando | Ctrl+F1 a Ctrl+F4: o addon `addons/visual_debugger` (autoload `VisualDebug`, não conhece o jogo), opções em `visual_debug/*` nas Configurações do Projeto |
| Nome dos nós (o caminho na árvore e o rótulo do modo de depuração) | todo nó tem nome: quem cria dá o do papel dele no pai, em PascalCase (`Name = "Body"`, `Layout.Row(8).Named("Actions")`), e o que se repete leva número ou id (`Slot3`, `Rune17`, `Ally2`). A tela ganha o nome da classe em `GameRoot.Swap`, as janelas por cima o nome passado a `Dialog.Open` (`RuneDialog`, `MonsterSummary`, `AutoBattleDialog`, `ConfigDialog`...), e os componentes nomeiam as próprias peças. Nome padrão: `SigilButton.Of`, `Layout.Chip` e `Doodle.Icon` usam o do símbolo (`level_max` → `LevelMax`), célula de grade usa `Layout.NextCell` (`R2C3`), aba usa o nome passado a `Layout.Tab`. Para remontar, `Layout.Clear` ou `Layout.Discard`, nunca `QueueFree` direto: o nó novo entraria com o nome ainda ocupado e viraria `@Nome@123` |

`GameDatabase.Validate()` confere referências e faixas dos dados; o teste `DataTests` falha se algo
estiver quebrado (inclusive básica com recarga, ativa sem recarga, duas Passivas ou atributos que não
são os do modelo), e o jogo mostra os problemas no console ao abrir.

## Combate: regras como estratégias

A luta não sabe o que é Aflição, Contragolpe nem dano: ela só conhece **momentos**, e avisa quem estiver
ouvindo. Quem ouve são estratégias (padrão Strategy), uma classe pequena por regra.

```
Core/Battle/
  BattleSession.cs     o fluxo: de quem é a vez, ondas, vitória e derrota
  EffectResolver.cs    a ordem de uma habilidade e de um golpe + as ações (ferir, curar, pôr efeito...)
  UnitBehavior.cs      a base das estratégias: um método vazio por momento da luta
  UnitRule.cs          uma regra em vigor numa unidade: a estratégia + os números desta aplicação
  Cast.cs, Strike.cs   a habilidade em curso e um golpe dela
  Effects/             o que cada tipo de efeito de habilidade faz   (SkillEffect, tabela SkillEffects)
  Statuses/            o que cada efeito de status faz               (StatusBehavior, tabela StatusBehaviors)
  Passives/            o que cada Passiva faz                        (tabela PassiveBehaviors)
  Sets/                o que cada conjunto de runas faz em combate   (SetBehaviors)
```

Uma unidade carrega as regras em vigor nela (`BattleUnit.Rules()`): os efeitos de status, na ordem em
que chegaram, depois os conjuntos de runas e a Passiva. Cada regra é um `UnitRule` (o `StatusEffect` é
um `UnitRule` com duração) que aponta para a estratégia dela. A estratégia não guarda estado: o valor,
quem pôs e os turnos que faltam vêm na regra que todo método recebe.

Os momentos (métodos de `UnitBehavior`; a estratégia sobrescreve só os dela):

| Momento | Quando | Quem usa hoje |
| --- | --- | --- |
| `Modify` | alguém pergunta um atributo de agora | Ataque+, Ataque−, Defesa+, Quebra de Defesa, Velocidade+, Passiva dos Diabretes |
| `SkipsTurn`, `HidesOwner`, `BlocksHarmful`, `ForcedTarget` | perguntas de sim ou não | Atordoamento, Oculto, Imunidade, Provocação |
| `OnWaveStart` | começou uma onda | conjuntos Tenacidade e Baluarte, Passiva dos Bandidos |
| `OnTurnStart` | começou o turno do dono | Aflição, Bomba, Passivas dos Trolls, Magos, Paladinos e Pixies |
| `OnAttack` → `OnDefend` | antes do dano de um golpe | Cegueira (erra), Presságio (crítico), Sifão e Passiva dos Vampiros (dreno) → Passiva dos Pássaros (esquiva), Égide (anula) |
| `DamageDealt`, `DamageTaken`, `Absorb` | a conta do dano | Passivas dos Goblins e Lobos; Maldição e Passiva dos Limos; Escudo |
| `AfterHurt`, `AfterHit` | o golpe acertou e o alvo ficou de pé | Perdição e Passivas dos Druidas e das Gárgulas; Tormento e Passivas dos Dragões e dos Corvos |
| `AfterEffect`, `AfterSkill`, `AfterStruck` | fim de um efeito e da habilidade | Presságio (some); Oblívio; Contragolpe |
| `AfterAction` | o dono acabou de agir | Frenesi |
| `OnDeath` | o dono caiu | Passivas dos Cavaleiros e da Fênix |

A ordem de um golpe está em `EffectResolver.Land`: quem ataca pode errar, quem apanha pode esquivar ou
anular, o crítico, o dano, o escudo, o dreno, a queda e, se o alvo ficou de pé, o que o golpe dispara
nos dois. O alvo pode devolver dano (a Passiva dos Druidas): se quem ataca cai com isso, os golpes e os
efeitos que faltavam da habilidade não acontecem.

**Um status novo.** "Congelado: perde o turno e recebe 20% a mais de dano":

```csharp
// Core/Battle/Statuses/FreezeStatus.cs
internal sealed class FreezeStatus : StatusBehavior
{
	public override bool Harmful => true;
	public override bool SkipsTurn => true;
	public override double DamageTaken(UnitRule rule) => 1.2;
}
```

Mais `Freeze` em `StatusKind`, `[StatusKind.Freeze] = new FreezeStatus()` em `StatusBehaviors`, os
textos e o símbolo. Nada em `BattleSession`, `EffectResolver`, `BattleUnit` ou `DamageFormula`. O teste
`SkillTests.EveryKindHasItsStrategy` acusa o nome que ficou sem estratégia.

Os casos comuns nem pedem classe: `new StatChange(Stat.Attack, -0.3)` é um "Ataque −30%" e
`new DamageOverTime(0.04, 3)` é um dano por turno de 4% que acumula até 3.

**Uma habilidade fora do padrão.** Uma habilidade é uma lista de efeitos, e cada um age sozinho, na
ordem. Quatro golpes, os três primeiros comuns e o último ignorando a Defesa:

```json
"effects": [
  {"kind": "Damage", "target": "Target", "power": 1.2, "hits": 3},
  {"kind": "Damage", "target": "Target", "power": 1.2, "ignoreDefense": 1}
]
```

Dois golpes no alvo e o terceiro em todos é a mesma coisa, com `"target": "AllEnemies"` no segundo
efeito. `onKill` num efeito faz ele só acontecer se a habilidade derrubou alguém. O que não cabe nos
campos de `EffectDefinition` vira um tipo novo de efeito: uma classe em `Effects/` que monta os golpes
como quiser (`new Strike(cast, alvo, força) { IgnoreDefense = ..., Drain = ... }`) e os entrega com
`cast.Resolver.Land`.

## Atributos: o modelo de BVP

Todo monstro das mesmas estrelas naturais gasta o mesmo orçamento de BVP, e o papel decide onde:
`BVP = Vida / 15 + Ataque + Defesa + Velocidade × 3`. A explicação inteira está em
`docs/summon_family_stat_formula_spec.md`.

| Arquivo | O que tem |
| --- | --- |
| `Data/stat_model.json` | os parâmetros: pesos, orçamento por estrelas (de base e desperto), perfis de papel (fatias e Velocidade), viés por elemento, Velocidade do Despertar e os quatro atributos de base |
| `Data/summons/<familia>_family.json` | a família e as variantes, com `"stats"` e `"awakened_stats"` **já calculados** no 6★ nível 40; o viés da família (`family_bias`) e o da variante (`bias`, `awakening_spd`) só aparecem quando não são neutros |
| `Data/enemies.json` | os chefes, com `"stats"` do papel e das estrelas deles |
| `docs/summon_family_builder.html` | o construtor: edita família, modelo e chefes (aba Chefes), calcula, confere e grava |
| `Core/Content/StatModel.cs` | a mesma conta no jogo, só para conferir |

O jogo não calcula atributo na hora: lê o número pronto e o encolhe para as estrelas e o nível do
monstro (`Growth`). A conta existe em dois lugares, no construtor (JavaScript) e em `StatModel.cs`, e
um confere o outro: `GameDatabase.Validate` refaz a conta de cada variante e de cada chefe e acusa o
que não bate. Mudou um parâmetro e esqueceu de recalcular, ou mexeu num número à mão? O teste de dados
falha e diz qual variante. Para consertar, "Recalcular todas" no construtor.

Com a pasta do projeto aberta, o construtor também lê os nomes válidos de efeito, status, Passiva e
papel direto dos enums de `Core/Content`: um status novo no código aparece lá sem mexer no HTML. Sem a
pasta (outro navegador), ele usa o modelo e as listas que traz embutidos e trabalha com Importar e
Baixar.

O que o contrato do projeto tem de diferente da especificação:

| Da especificação | No projeto |
| --- | --- |
| Perfis de papel (§6) e Velocidade por papel (§7) | uma tabela só, `role_profiles`: por estrelas e papel, `{"hp", "atk", "def", "spd"}` |
| Viés de elemento (§12) | `element_bias` no modelo, igual para o jogo inteiro |
| Viés de família (§11) | `family_bias` no arquivo da família |
| Viés e Velocidade manuais da variante (§7, §8) | `bias` (`{"hp", "atk", "def", "spd"}`) e `awakening_spd` na variante |
| Campos a mais na família | `name` (o nome no Grimório), `image` e `awakened_image`: vieram do antigo `families.json` |
| `validation` (§22) | não vai para o arquivo: o construtor mostra ao vivo e o jogo confere |
| Arredondamento (§25) | a Vida sai em múltiplos do peso dela (15) e a sobra do arredondamento vai para a Defesa: o BVP fecha o orçamento exato |
| Atributo do Despertar (§13) | fora do orçamento: o jogo soma por cima de `awakened_stats` (`Awakening.Bonus`) |

## Decisões (SOLID sem abstração prematura)

- **Uma responsabilidade por classe.** `BattleSession` cuida do fluxo de turnos; `EffectResolver` do
  que cada efeito faz; `Targeting` de quem é atingido; `DamageFormula` do número; `AutoPilot` das
  decisões automáticas; `RuneForge` cria e transforma runas; `RuneInventory` cobra (Pó e pedras) e
  guarda. As telas só desenham.
- **Aberto para conteúdo, sem código novo.** Habilidades são listas de efeitos em JSON; uma família
  nova é um arquivo.
- **Nenhuma interface.** Não há `IRandom`, `IClock` nem `ISaveRepository`: o Core recebe `Random` e
  `DateTime` como parâmetro, e o save é texto. Com o Erudito fora do jogo, `ITurnTaker` perdeu a
  segunda implementação e saiu junto.
- **Regras de combate são estratégias, não `switch`.** Enquanto eram seis efeitos e dez status, um
  `switch` e uns `if` espalhados davam conta. Veneno, Bomba e Quebra de Defesa mostraram o custo: cada
  regra nova pedia mexer em quatro arquivos, e a Bomba quebrava a luta. Agora cada regra é uma classe
  que responde aos momentos da luta, e o fluxo não conhece nenhuma pelo nome. A troca foi conferida
  luta a luta (`--digest`): as mesmas sementes dão os mesmos eventos.
- **Atributo é dado pronto, conferido.** O arquivo da família guarda o número final, não os
  ingredientes; o jogo só escala por estrelas e nível. Para o número não envelhecer, o jogo refaz a
  conta do modelo na validação e compara.
- **Eventos em vez de callbacks.** O combate devolve `BattleEvent` e não sabe que existe tela; a mesma
  luta roda animada (`BattleScreen`) ou instantânea (`AutoBattle`: Batalha automática e simulador).
- **Uma fonte da verdade por dado.** A runa guarda em qual monstro está (`Rune.EquippedOn`, o id da
  cópia); o monstro não guarda lista de runas. A equipe guarda ids de monstro; o monstro não sabe em
  que equipe está. O valor do principal, a raridade e o total de cada subatributo não são salvos
  (`[JsonIgnore]`): saem das tabelas e dos sorteios guardados (`RuneSubstat.Rolls`).
- **Monstros são cópias.** Cada invocação cria um `OwnedSummon` com id próprio; a variante é só o
  `SummonId`. Por isso duas cópias iguais podem ter estrelas, nível, níveis de habilidade, Despertar,
  runas e equipes diferentes.
- **Uma batalha, vários conteúdos.** Fase e andar de Masmorra viram um `Encounter` (estrelas, nível,
  ondas, força); a mesma `BattleFactory`, a mesma tela e a mesma Batalha automática servem aos dois. O que muda é a
  equipe (por conteúdo) e a recompensa (`Campaign` ou `Dungeons`, as duas devolvem `VictoryReward`).
- **O jogo nasce em português; ids em inglês.** Os textos da interface (`Data/texts/pt-BR.json`, a
  base) e os nomes em `Data/` (invocações, habilidades, fases, Masmorras, chefes, Loja) são em
  português; ids, chaves, enums e argumentos ficam em inglês e nunca mudam (o save guarda ids). O
  `en.json` é uma tradução: as mesmas chaves, mais o grupo `names` com o nome de cada coisa dos dados
  pelo nome em português. O `ContentLoader` troca os nomes ao montar o `GameDatabase` (`Locale.Name`),
  então o Core e a batalha já recebem tudo no idioma escolhido; trocar de idioma remonta o banco.
- **Celular primeiro.** A tela é pensada para o dedo: nada depende do mouse (sem dica; o brilho sob
  o mouse é só enfeite), alvos de toque com 56 px de altura (`GameTheme.Touch`), texto de 18 px, e
  toda explicação numa `Dialog` colada no que foi tocado (elemento → toque → janela). Segurar um
  monstro abre o resumo dele, segurar uma runa a ficha, em toda tela. A janela tampa o jogo, mas não o
  para: a Batalha automática segue por baixo.
- **Desenhado em código.** Não há cena `.tscn` nem imagem de interface pronta:
  o `Theme` sai de `GameTheme.Build()`, as molduras são texturas geradas uma vez por cor
  (`Ornament`, viram `StyleBoxTexture` de 9 partes, com o miolo repetido para o grão não esticar) e os
  sigilos se desenham em `_Draw` (`SigilButton`, `SigilRing`, `Constellation`, `EnergyRing`). A
  identidade é o grimório de um invocador que estuda constelações: couro nos painéis, pergaminho
  escrito a tinta nas páginas, violeta no místico, índigo no céu e ouro só no precioso; as cartas do
  céu (`Constellation`, `StarChart`) e a moldura graduada dos sigilos grandes (`SigilButton.Bezel`)
  guardam os pontos numa vez só, ao mudar de tamanho, e desenham num traço só. Os
  estados se leem pela luz: azul arcano ligado, verde espiritual chamando; o peso
  (afundar ao apertar) é o `Juice`. `Button` do Godot não mede os filhos: `GameButton`, `TileButton`
  e as abas acertam o `CustomMinimumSize` pelo conteúdo (`Fit`). Os desenhos do Commons são pretos e o
  `Doodle` os pinta pelo shader, então um ícone serve em qualquer cor. O número de um sigilo fica
  numa plaquinha escura com contorno, por cima da borda de baixo.
- **PNG em vários tamanhos, escolhido pelo tamanho na tela.** Os SVG são só a fonte: o
  `Tools/art/render_png.py` gera PNG de 32 a 512 px, importados com mipmaps. `Art` entrega o de 128 e
  o `Doodle`, ao mudar de tamanho, troca pelo menor que cobre o tamanho na tela (com a escala da
  janela) — a redução que sobra é pequena e suavizada. Todo desenho fica em "contain" (proporção
  mantida, inteiro no espaço) e, dentro de um componente com forma, recortado nela pela `ArtMask`
  (`ClipChildren`): o símbolo no sigilo, o retrato no medalhão, a criatura no cartão.
- **O fundo segue o foco da tela.** O anel do `Backdrop` fica no centro do portal de invocar; nas
  outras telas, no meio (a batalha não tem anel: o oval é o campo).
- **Camadas por cima vão no alto da árvore.** O `GameRoot` põe a tela num contêiner (`Screens`), o
  selo da Batalha automática depois dele e as janelas (`Dialog`) no controle mais alto (`Layout.Host`),
  que tem o tema; assim cobrem a tela inteira de qualquer botão e sobrevivem à troca de tela. Ordem
  de desenho: resultado da luta 60, selo 70, janela 80, pausa 99. Cada janela aberta por cima de outra
  fica 40 acima dela (`Dialog.Layer`): o conteúdo de uma janela pode subir até 30 dentro dela (a luta
  pequena da Batalha automática e a moldura), e a pergunta de depois ainda cobre tudo.
- **Texto fora do código.** As telas pedem texto por chave ao `Locale`; o que depende de regra
  (descrição de habilidade, conjunto, efeito) é montado em `Texts` a partir das mesmas regras que o
  combate usa, então a explicação nunca desatualiza. `Tools/texts/check_texts.py` confere as chaves
  literais; as que vêm de enum o jogo confere ao abrir.
- **Números de Summoners War como tabela, não como fórmula inventada.** Principal, subatributo,
  pedras e custo de melhora são as tabelas de lá, uma linha por estrela, em `RuneRules`. O custo sem
  falha é derivado delas (custo ÷ chance de sucesso), então mudar o preço em Essência é mudar
  `CostPerEssence`.
- **Inimigo comum é invocação.** A mesma variante que o jogador invoca, nas estrelas e no nível do
  encontro, com as habilidades no nível 1, sem runas nem Despertar, com Vida e Ataque reforçados por estrelas (`BattleFactory.FoeScale`). Só o que
  não existe como invocação (os chefes) mora em `Data/enemies.json`. Assim cada criatura nova serve aos
  dois lados.
- **A Batalha automática espera o tempo da tela.** As lutas são resolvidas na hora (`AutoBattle.Run`
  guarda os eventos), mas a recompensa só entra depois de `BattlePace.Seconds(eventos, 2×)`, que soma os
  momentos de `BattlePace.Beats` (corrida, golpes que caem juntos, volta): a mesma
  conta que a tela de batalha usa para esperar entre eventos.
- **Habilidade é uma lista, não slots fixos.** Cada variante tem a básica, as ativas com recarga e
  talvez uma Passiva (`SkillDefinition.Passive`). O nível e o Despertar não mudam o dado: a batalha pede
  `SkillDefinition.At(nível, desperta)`, que devolve a habilidade já com as melhorias e os efeitos do
  Despertar. A habilidade que o Despertar dá entra no fim da lista (`SummonDefinition.AllSkills`), então
  o índice do nível salvo (`OwnedSummon.SkillLevels`) nunca muda.
- **Estrelas e nível pelas tabelas de Summoners War.** A fração do atributo de 6★ nível 40 em cada
  estrela e nível (`Growth.Bands`) e a experiência de cada nível (`Leveling`) são as de lá. A
  diferença: evoluir mantém o nível (lá volta ao 1), então o monstro entra na faixa da estrela nova no
  mesmo nível, os atributos sobem na hora e só faltam os 5 níveis novos.
- **Força da fase em dado, calibrada fora do jogo.** O nível mostrado de uma fase só sobe, então o que as
  ondas pesam a mais ou a menos vai no `scale` da fase (Vida e Ataque), não num nível que mente. O
  número sai do simulador contra o time de referência daquele ponto (`Tests/ReferenceTeams.cs`); o
  jogo não sabe de calibragem.
- **Só inimigo passa do 6★ nível 40.** `Growth.FoeFraction` continua a reta do 6★ até o 60 só para quem
  luta do outro lado; o jogador, a validação dos dados e as tabelas de nível continuam parando no 40.
- **O jogo aos poucos sem estado novo no save.** Cada parte abre pela fase mais alta vencida
  (`Features`), e "nova" é a parte da última fase vencida. Conta antiga já tem tudo aberto, e não há
  lista de "já vi" para migrar.
- **A luta de treino é uma luta de verdade.** O `TutorialCoach` só olha e fala: a `BattleSession` é a
  mesma de sempre, com uma equipe emprestada que não entra na conta, e a `BattleScreen` só pergunta a
  ele antes da vez do jogador (o que mostrar e o que deixar escolher).
- **Confere na entrada, cobra na vitória.** `Campaign.Check` e `Dungeons.Check` devolvem um
  `EntryProblem` (fechado, sem Mana, inventário de runas cheio) sem mudar nada; a Mana só sai no
  `ApplyVictory`, então a derrota (ou sair e recomeçar pela pausa) não custa nada.

## Simplificações do MVP em relação ao GDD

- Passivas simples (dezessete tipos, uma por família), habilidades novas do Despertar só nas 2★ e nas 3★.
- Viés de elemento neutro: os cinco elementos repartem o orçamento igual (o modelo aceita o viés).
- Sem Tiques, traçado do sigilo, regras de região, Torre, Provações e Portais
  Secretos (o Despertar ainda não pede Provação: só Essência).
- Masmorras com 5 andares cada; chefes reaproveitam o desenho de criaturas do Commons.

## Save

`PlayerState.CurrentVersion` marca o formato (hoje 7: estrelas, níveis de habilidade e a experiência por
estrela; o 6 trouxe ids em inglês; o 5, Mana, Ouro e nível da conta). Um save de outro formato não é lido: o `SaveStore` guarda o arquivo como
`nome.old-AAAAMMDD-HHMMSS.json` (a data evita apagar um backup mais velho) e começa uma conta nova.

**Zerar todas as contas numa versão nova** é subir `PlayerState.CurrentVersion`. Cada aparelho, ao abrir a
versão nova, guarda o save antigo como backup e começa do zero; na nuvem, o save de versão anterior
(`PlayerSave.IsObsolete`) vira backup no aparelho (`nome.old-cloud-...json`) e o jogo novo sobe por cima
dele (`AccountSession.Sync`). As contas (e-mail, senha, nome) ficam. Um save de versão mais nova continua
recusado ("atualize o jogo"), então uma cópia velha do jogo não apaga o progresso novo. Publique a versão
com `--minimum` igual a ela (docs/ATUALIZACOES.md), para ninguém seguir jogando a anterior.

Com conta, cada uma tem o seu save no aparelho (`nome.account-<id>.json`); o `nome.json` é o do jogo sem
conta, que sobe para a primeira conta que entrar sem save na nuvem. O que o aparelho lembra da conta
(id do aparelho, e-mail, token de renovação, idioma e o último ponto de sincronização de cada conta) fica
em `nome.account.json`. Nada se perde na sincronização: o save local trocado pelo da nuvem, o da nuvem
trocado pelo local (`nome.account-<id>.old-cloud-...json`) e o progresso que não subiu quando outro
aparelho tomou a conta viram backup `.old-`.

## Exportar

`Data/` são arquivos de texto lidos com `FileAccess`: no preset de exportação, inclua `*.json` em
"Filters to export non-resource files".
