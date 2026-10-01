# Save em nuvem e contas — escopo

Escopo curto para levar o save do Sigilos para a nuvem **sem manter servidor próprio** e, depois,
para ter contas com login em qualquer aparelho, **um aparelho por vez** (seção "Contas").

**O caminho escolhido foi o servidor próprio** (a instância Oracle), que já está no ar e ligado ao jogo:
ver [SERVIDOR_PROPRIO.md](SERVIDOR_PROPRIO.md). O desenho daqui (local primeiro, metadados de
sincronização, conflito, trava de sessão) é o que o jogo segue. As partes sobre Firebase ficam como
registro da alternativa.

## Ponto de partida

- O save é um JSON de ~75 KB (`PlayerSave.ToJson`), gravado por `GameEntry/SaveStore.cs` em
  `user://sigilos.json`. Um save ilegível vira `sigilos.old-<data>.json` e não se perde.
- Plataformas exportadas: Android e Windows.
- O jogo é offline e de um jogador só: não precisa de anticheat nem de validar nada no servidor. A
  nuvem serve para **trocar de aparelho, jogar em dois e não perder o progresso**.

## Opções sem servidor próprio

| Opção | Plataformas | Custo | Login | Esforço | Observação |
| --- | --- | --- | --- | --- | --- |
| A. Exportar/importar (texto ou arquivo) | todas | zero | nenhum | 0,5–1 dia | O jogador copia um código (JSON compactado em Base64) e cola no outro aparelho. Não sincroniza sozinho, mas resolve a troca de aparelho já |
| B. Google Play Games — Jogos salvos | só Android | grátis (conta do Play Console, taxa única) | conta Google do Play Games | 3–5 dias | Precisa de um plugin do Play Games Services para Godot 4 (Android) e do app no Play Console (teste interno serve). O próprio Google guarda e mostra os saves |
| C. Steam Cloud (Auto-Cloud) | só PC, na Steam | taxa de publicação da Steam | conta Steam | horas | Sem código: no Steamworks, aponta a pasta do save. Só faz sentido se o jogo for para a Steam |
| D. Firebase (Auth + Firestore) | todas | plano gratuito | anônimo, depois Google | 3–5 dias | Um documento por jogador com o save; acesso pela API REST. Regras do Firestore garantem que cada um só lê e grava o seu |
| E. Supabase (Auth + banco/Storage) | todas | plano gratuito | anônimo, e-mail ou Google | 3–5 dias | Parecido com o Firebase, em Postgres. Atenção: projeto gratuito parado por dias é pausado |
| F. PlayFab (Microsoft) | todas | plano gratuito para começar | anônimo por aparelho, depois vincula | 3–5 dias | Feito para jogos ("Player Data"); tem SDK em C# |
| G. Google Drive do próprio jogador (pasta oculta do app) | todas | zero | conta Google | 4–6 dias | O save fica no Drive do jogador. O fluxo de OAuth é o mais trabalhoso e o escopo pode exigir revisão do Google |

Os limites dos planos gratuitos mudam: confira antes de escolher. Para um jogo deste tamanho (um
documento de ~10 KB compactado por jogador, poucas gravações por sessão), qualquer um deles sobra.

## Recomendação

1. **Agora: A (exportar/importar) e preparar o save** (metadados abaixo). É barato, não depende de
   conta e já resolve a troca de aparelho.
2. **Depois: D (Firebase)** — uma implementação só cobre Android e Windows, sem servidor, com login
   anônimo que pode virar conta Google sem perder o save.
3. **Se o jogo for só para a Play Store**, B é a opção nativa (o jogador já tem a conta). **Se for para
   a Steam**, C entra junto, quase de graça.

## Desenho (vale para qualquer opção)

- **Local primeiro.** O `SaveStore` continua sendo a fonte da verdade: o jogo grava no aparelho como
  hoje e a nuvem é uma cópia. Sem internet, nada muda para o jogador.
- **Metadados no save** (`PlayerState`, campos novos com padrão, sem quebrar saves antigos):
  - `Revision`: sobe a cada gravação.
  - `SavedAtUtc`: quando foi gravado.
  - `DeviceId`: qual aparelho gravou.
  - `LastSyncedRevision`: a última versão que foi para a nuvem.
- **Sincronizar:**
  - Ao abrir o jogo, baixa a cópia da nuvem.
  - Se só um dos lados mudou desde o último envio, fica o mais novo, sem perguntar.
  - Se os dois mudaram, pergunta numa `Dialog` mostrando os dois resumos (nível da conta, monstros, fase, data).
  - Ao pausar, ao fechar, e a cada poucos minutos se houver mudança, envia.
  - Falhou: tenta de novo depois, em silêncio.
- **Nunca perder:** antes de trocar o save local pelo da nuvem, guarda o local como
  `sigilos.old-<data>.json`, como o `SaveStore` já faz com o save ilegível.
- **Datas em UTC:** a ociosidade depende do relógio (`LastIdleCollect`). Com dois aparelhos, gravar
  em UTC evita juntar horas a mais ou a menos.
- **Onde no código:**
  - O Core não muda.
  - `GameEntry/CloudSave.cs` faz o envio e o download. Uma interface pequena ali se justifica, porque é a fronteira com a plataforma e permite um falso nos testes.
  - `GameEntry/CloudSync.cs` decide quem ganha.
  - O `GameRoot` chama a sincronização ao abrir e ao pausar.
- **Rede:** o `HttpClient` do .NET funciona no Godot .NET em Windows e Android (ou o nó
  `HTTPRequest`). Compactar com gzip leva os ~75 KB para ~10 KB.
- **Segurança:**
  - A chave da API do Firebase (ou a do Supabase) pode ir no cliente: quem protege são as regras de acesso por usuário.
  - Chave de serviço ou de administrador nunca vai no jogo.
- **Loja e privacidade:** login com conta Google pede política de privacidade e o formulário de
  segurança de dados da Play Store (e atenção à LGPD).

## Estimativa total

- Exportar/importar e metadados com conflito: 2–3 dias.
- Firebase em cima disso: mais 3–5 dias, com testes em dois aparelhos.
- Play Games ou Steam Cloud: a configuração nas lojas pesa mais que o código.

## Contas: login em qualquer aparelho, um por vez

A ideia: cada amigo cria uma conta (e-mail e senha), entra de qualquer aparelho, e o progresso vem
junto. A conta só fica aberta num aparelho por vez: entrar num segundo **tira o primeiro**, que volta
para a tela de login. É o save em nuvem (opção D, Firebase) com duas peças a mais: **login** e
**trava de sessão**.

### Peças

- **Login: Firebase Authentication com e-mail e senha.**
  - Não precisa de servidor nem de revisão do Google, e funciona igual no Android e no Windows, pela API REST: criar conta, entrar e renovar o token.
  - O jogo guarda só o token de renovação em `user://`; a senha nunca fica salva.
  - "Entrar com Google" fica para depois: o fluxo OAuth fora de um SDK oficial é a parte cara.
- **Dados: dois documentos por jogador no Firestore**, separados para o batimento não carregar o save:
  - `saves/{uid}`: o save compactado e os metadados da seção anterior;
  - `sessions/{uid}`: a sessão (`deviceId`, nome do aparelho e `heartbeatAt`), coisa de poucos bytes.
  - Regra de acesso: cada um só lê e grava os próprios documentos (`request.auth.uid == userId`).
- **Trava de sessão: arrendamento com batimento.**
  - Ao entrar, o aparelho grava a sessão como dele.
  - Enquanto o jogo está aberto, renova `heartbeatAt` a cada ~60 s. A hora é a do servidor (transformação `REQUEST_TIME`), para relógios diferentes não atrapalharem.
  - Toda gravação na sessão leva a pré-condição `updateTime` que o próprio aparelho recebeu na
    gravação anterior. Se dois aparelhos tentarem ao mesmo tempo, só um ganha. E se outro aparelho
    tomou a sessão, a pré-condição falha: é assim que o batimento descobre, **sem gastar leitura**.
  - O envio do save vai num `commit` junto com essa pré-condição: aparelho sem a sessão não sobrescreve
    a nuvem.

### Fluxo

1. **Entrar.** Login e leitura do documento.
   - Se a sessão é de outro aparelho e o batimento é recente (menos de ~3 min), o jogo pergunta na
     tela: "Sua conta está aberta em <aparelho>. Entrar aqui e desconectar o outro?". Se o batimento
     é velho (o outro fechou ou caiu), entra direto.
   - Ao entrar, baixa o save da nuvem e segue a regra de conflito da seção anterior.
2. **Jogando.**
   - A cada batimento, o aparelho confere se a sessão ainda é dele.
   - Se não for (outro aparelho entrou), a próxima conferência o derruba: guarda o progresso local
     como backup (`.old-`), não envia nada e volta para a tela de login com o aviso "Sua conta foi
     aberta em outro aparelho".
3. **Sair** (Ajustes → Sair da conta). Envia o save, apaga a sessão e volta para o login.

### O ponto delicado: jogar sem internet

O jogo hoje é todo offline, e garantir "um aparelho por vez" exige rede ao menos para entrar.
Como ficou:
- **A primeira entrada num aparelho precisa de internet** (a senha só o servidor confere).
- **Depois, a conta lembrada abre sem internet**, com o save deste aparelho, e mostra "Sem conexão" no
  Santuário. **Se a rede cair no meio**, dá no mesmo: o jogo continua.
- **Ao reconectar**, o jogo toma a sessão sem forçar e sincroniza:
  - só o aparelho mudou: o progresso sobe;
  - a conta também mudou em outro aparelho nesse meio tempo: pergunta qual fica, e o outro vira
    backup no aparelho.
- **Se outro aparelho está com a conta na hora da volta**, este volta para o login, sem perder nada. O
  que se jogou sem rede fica no aparelho e entra na conta na próxima entrada aqui, com a pergunta se a
  conta também mudou.
- **Jogar sem conta** continua existindo, como hoje: tudo local. Ao criar a conta, o save local sobe
  para ela.

### Limites (bons para um grupo de amigos)

- O aparelho tirado descobre em até um batimento (~1 min), porque a API REST do Firestore não avisa
  na hora. Se isso incomodar, o Supabase tem Realtime (WebSocket) e avisa na hora, em troca da pausa
  do plano gratuito.
- O `deviceId` e as regras impedem o descuido, não alguém determinado a burlar: aqui não há trapaça
  que valha a pena barrar.
- Com servidor próprio um dia, o Nakama já tem "uma sessão por conta" pronto.

### Cabe no plano gratuito?

Cota sem custo do Cloud Firestore (edição Standard, conferida em 30/09/2026), por projeto: 1 GiB
guardado, 10 GiB/mês de saída de rede, 20 mil gravações, 50 mil leituras e 20 mil exclusões por dia.

Gasto por **hora de jogo** de uma pessoa, com o desenho acima:

| | Quanto | Conta |
| --- | --- | --- |
| Gravações | ~72 | 60 batimentos + ~12 envios do save (a cada 5 min, se mudou) |
| Leituras | ~0 | só ao entrar (sessão e save: 2) e quando outro aparelho toma a sessão |
| Saída de rede | ~0,1 MB | o save (~15 KB compactado) só desce ao entrar; as respostas das gravações pedem só um campo (`mask`) |
| Guardado | ~15 KB por conta | um save compactado; a sessão é desprezível |

O primeiro limite a pesar são as **gravações**: 20 000 ÷ 72 ≈ **270 horas de jogo por dia**, somando
todo mundo. Por exemplo, 20 amigos jogando 3 h por dia gastam ~4 300 gravações, ~22% da cota. O
resto nem chega perto: 1 GiB guarda dezenas de milhares de contas.

Se o grupo crescer, o batimento a cada 2 min (e arrendamento de 5 min) corta as gravações quase pela
metade, ao custo de o aparelho derrubado demorar até 2 min para perceber.

**Ao estourar a cota no plano gratuito (Spark), o Firestore recusa as operações até a cota virar**
(meia-noite do Pacífico); não cobra nada. Como o jogo é local primeiro, isso só adia a sincronização:
o jogo trata como "sem internet". Só ligar o plano pago (Blaze) se quiser, e com alerta de orçamento.

### Onde no código

- `GameEntry/Account/`, cada peça falando REST com o Firebase:
  - `AuthClient`: criar conta, entrar, renovar o token;
  - `SessionLock`: tomar a sessão, bater, conferir, soltar;
  - `CloudSave`: da seção anterior.
- O `GameRoot` cuida do resto:
  - antes do Santuário, abre a tela de login (Entrar, Criar conta, Jogar sem conta);
  - liga um `Timer` para o batimento;
  - derruba o jogador quando a sessão some.
- `PlayerState` só ganha os metadados do save. O Core não sabe que existe conta.
- Telas novas: login/criar conta e a pergunta "desconectar o outro aparelho?". Em Ajustes: "Conta: e-mail" e "Sair da conta".

### Estimativa

Sobre o save em nuvem (Firebase) pronto:
- login e telas: 3–4 dias;
- trava de sessão, batimento e o aparelho derrubado: 2–3 dias;
- testes com dois aparelhos: 1–2 dias.

No total, contando o save em nuvem: 11 a 17 dias de trabalho desde hoje.
