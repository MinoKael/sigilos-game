# Servidor próprio na instância Oracle — projeto

Alternativa ao Firebase de [SAVE_NUVEM.md](SAVE_NUVEM.md): o mesmo resultado (contas, save em nuvem,
um aparelho por vez), num servidor pequeno na instância Oracle que já existe, com o domínio dela.

**Este é o caminho escolhido e está implementado:**
- o servidor (`Sigilos.Server`, outro repositório) roda em `https://sigilos.minopavel.duckdns.org`, atrás
  do Caddy e do PM2;
- o jogo fala com ele por `GameEntry/Account/` (seção [No jogo](#no-jogo)).

## É viável?

Sim, e é leve: um grupo de amigos gera algumas requisições por minuto e guarda poucos KB por conta.
Qualquer instância Always Free dá conta: a Ampere A1 sobra, e até a AMD micro (1 GB) basta.

O que se ganha:
- sem cota diária;
- o aparelho derrubado sabe na hora (o servidor manda);
- tudo em C#, a mesma linguagem do jogo;
- os dados ficam com você.

O que se paga é **operação**: backup, segurança, atualizações e a máquina no ar. O código é a parte
fácil.

**Não escreva um banco de dados.** "NoSQL simples" aqui é usar um banco de documentos *embutido*, que
mora num arquivo dentro do próprio servidor:
- **LiteDB** (documentos, 100% .NET, um arquivo): é a recomendação.
- **SQLite** com colunas JSON: faz o mesmo e é ainda mais testado; vale se preferir o backup mais
  simples.

Nenhum dos dois precisa de outro processo rodando.

## Arquitetura

```
Godot (Android/Windows)
   │  HTTPS (JSON)
   ▼
Caddy (porta 443, certificado automático do Let's Encrypt para o domínio)
   │  http://localhost:5080
   ▼
Sigilos.Server (ASP.NET Core 8, minimal API) ──► sigilos.db (LiteDB, um arquivo)
                                              └─► backup diário (Object Storage da Oracle)
```

- **Servidor:** ASP.NET Core 8 com minimal APIs, num projeto novo (`Server/Sigilos.Server.csproj`),
  fora do projeto do Godot.
  - Guarda o save como um bloco **opaco** (o JSON compactado) com os metadados ao lado. Assim o servidor
    não muda quando o formato do save mudar.
  - Pacotes: `LiteDB`, `Microsoft.AspNetCore.Authentication.JwtBearer`; o limitador de requisições e
    o `PasswordHasher` já vêm no ASP.NET Core.
- **Caddy** na frente: HTTPS automático e renovação do certificado. O Android recusa HTTP sem TLS por
  padrão, então HTTPS não é opcional. Ele também serve, direto do disco, a pasta `/releases/` com as
  versões do executável do Windows (a atualização automática, [ATUALIZACOES.md](ATUALIZACOES.md)).
- **Implantação:** `dotnet publish -c Release -r linux-arm64 --self-contained` (ou `linux-x64` na AMD).
  No ar, quem mantém o processo é o PM2 que a instância já usa (o README do servidor tem os passos).

## Dados (coleções do LiteDB)

| Coleção | Documento | Observação |
| --- | --- | --- |
| `users` | `_id`, `email` (único, minúsculo), `passwordHash`, `createdAt`, `name`, `nameKey` | senha com PBKDF2 (`PasswordHasher`). O nome é único: `nameKey` é ele sem acentos e em minúsculas |
| `refreshTokens` | `_id` (hash do token), `userId`, `deviceId`, `expiresAt` | o token em si nunca é guardado; troca a cada uso |
| `sessions` | `_id` = userId, `sessionId`, `deviceId`, `deviceName`, `lastSeen` | a trava: um por conta |
| `saves` | `_id` = userId, `revision`, `savedAt`, `data` (bytes gzip) | ~15 KB por conta |
| `invites` | `_id` = código, `usedBy` (o id da conta), `usedAt` | só entra quem tem convite: o servidor não fica aberto para estranhos |

## API

| Método e rota | Corpo | Resposta |
| --- | --- | --- |
| `POST /auth/register` | e-mail, senha, convite, nome | 201; 400 se o convite ou o nome não valem; 409 se o e-mail ou o nome já existem |
| `POST /auth/login` | e-mail, senha, `deviceId` | token de acesso (JWT, 15 min), de renovação (30 dias) e o nome da conta |
| `POST /auth/refresh` | token de renovação | um par novo (o velho deixa de valer) e o nome da conta |
| `PUT /account/name` | nome | 200 com o nome; 400 se não vale; 409 se é de outra conta |
| `POST /session/claim` | `deviceId`, `deviceName`, `force` | `sessionId`; ou 409 com o aparelho e quando foi visto, se outro está ativo e `force` é falso |
| `POST /session/heartbeat` | `sessionId` | 204; ou 409 "sessão tomada" (o jogo volta para o login) |
| `POST /session/release` | `sessionId` | 204 (ao sair da conta) |
| `GET /save` | — | revisão, data e o save; ou 404 na conta nova |
| `PUT /save` | `sessionId`, `baseRevision`, `revision`, save | 204; 409 se a sessão não é mais desse aparelho ou se a nuvem mudou desde `baseRevision` |
| `GET /mail` | — | as cartas do correio da conta ainda não coletadas: id, título, texto, recompensas, data e prazo |
| `POST /mail/{id}/claim` | — | 204 (repetir não faz mal); 404 se a carta não é da conta |
| `POST /admin/mail` | cabeçalho `X-Admin-Key`; `to` (nome, e-mail ou `*` para todas), `title`, `text`, `rewards`, `expiresInDays` | 201 com o id; 401 sem a chave; 400 `invalid_mail` ou `invalid_reward`; 404 `user_not_found` |
| `GET /admin/mail`, `DELETE /admin/mail/{id}` | cabeçalho `X-Admin-Key` | as cartas enviadas, com quantas contas coletaram; retirar uma carta |
| `GET /chat` (WebSocket) | token de acesso no cabeçalho | o Chat global ao vivo: falas e feitos, repassados a todos com o nome da conta; nada fica guardado |
| `GET /friends`, `POST /friends/requests`, `POST /friends/{id}/accept`, `DELETE /friends/{id}` | nome da conta (no convite) | amigos e convites (até 50, contando os enviados); de amigo, se está jogando |
| `GET /health` | — | 200, para um monitor externo |

**A trava fica no servidor**, que é quem decide:
- `claim` troca o `sessionId` da conta: o aparelho antigo passa a receber 409 no próximo batimento
  (a cada 30 s, já que não há cota) e no próximo envio do save.
- Uma sessão que passa 30 s sem bater libera a conta: o próximo aparelho entra sem pergunta.
- A mesma regra de conflito de [SAVE_NUVEM.md](SAVE_NUVEM.md) vale no cliente: local primeiro, backup
  `.old-` antes de trocar e pergunta quando os dois lados mudaram.
- Avisar na hora, em vez de no próximo batimento, é opcional: um endpoint de eventos (SSE) por onde o
  servidor manda "sessão tomada".

## Segurança e operação (o checklist)

- **Instância na Oracle:**
  - Abrir 80/443 na *Security List* da VCN **e** no `iptables` da instância. As imagens Ubuntu da
    Oracle só liberam a 22 por padrão, e esquecer disso é o tropeço clássico.
  - Usar IP público reservado, para o domínio não perder o endereço.
  - Instâncias Always Free paradas podem ser recuperadas pela Oracle (uso baixo de CPU, rede e memória
    por 7 dias, em conta só Free Tier). Como a sua já roda outras coisas, confira. Passar a conta para
    Pay As You Go evita isso e continua sem custo dentro dos limites gratuitos.
- **Senhas e tokens:**
  - Hash forte (nunca a senha em texto) e chave de assinatura do JWT fora do repositório, em variável de
    ambiente ou arquivo com permissão restrita.
  - Refresh token guardado só como hash e trocado a cada uso.
- **Abuso:**
  - Limitador de requisições em `/auth/*` (por IP).
  - Tamanho máximo do save (1 MB).
  - Cadastro só com convite.
- **Backup:**
  - Um serviço dentro do próprio servidor, uma vez por dia, exporta as coleções para JSON compactado e
    manda para o Object Storage da Oracle (há cota gratuita), guardando 7–14 dias.
  - Copiar o arquivo do LiteDB com o servidor gravando não é seguro.
  - Com SQLite, `VACUUM INTO` faz a cópia consistente com tudo rodando.
- **Atualizações:** `unattended-upgrades` no Ubuntu; o servidor sobe de novo sozinho pelo `systemd`.
- **Monitor:** um verificador gratuito batendo em `/health` a cada poucos minutos avisa se cair.
- **Privacidade:** você passa a guardar e-mail e senha (com hash) de pessoas: guarde o mínimo e deixe
  apagar a conta (LGPD).

## No jogo

`GameEntry/Account/`, com o endereço do servidor em `AccountSession.DefaultServer`
(`-- --server=url` troca, para testar contra um servidor local):
- `AuthClient`: cadastro, login e renovação. O token de acesso fica só na memória; o de renovação,
  em `user://sigilos.account.json`, e a senha nunca. Duas renovações ao mesmo tempo viram uma só,
  porque o token de renovação troca a cada uso.
- `SessionLock`: tomar, bater a cada 20 s e soltar a sessão.
- `CloudSave`: baixar e enviar o save, em Base64.
- `CloudSync`: quem ganha entre o aparelho e a nuvem, comparando o hash do save e a revisão com o último
  ponto de sincronização. Gravar sem mudar nada não conta como mudança.
- `AccountStore`: o arquivo da conta no aparelho.
- `AccountSession`: o nó que junta tudo, com o batimento e o envio a cada 60 s.
- `ChatLink`: a conexão do Chat global, aberta com a conta conectada e reaberta no batimento se cair.
- `CloudFriends`: a lista de amigos e os convites.

O fluxo, no `GameRoot`:
1. **Abrir o jogo.**
   - Quem escolheu jogar sem conta abre direto no save local.
   - Quem tem conta lembrada entra sozinho. Sem internet, ela abre com o save deste aparelho (se ele
     tem um desta conta) e o Santuário mostra "Sem conexão".
   - Os outros veem a tela de login: Entrar, Criar conta (com o nome da conta e o convite) e Jogar sem
     conta.
2. **Entrar.**
   - Toma a sessão. Se outro aparelho bateu há menos de 30 s, pergunta "Entrar aqui fecha a conta
     lá?".
   - Depois baixa o save e decide:
     - só um lado mudou: fica esse;
     - os dois mudaram: pergunta, mostrando os dois resumos;
     - a conta é nova: o save sem conta do aparelho sobe para ela.
   - O que perde vira backup `.old-` no aparelho.
3. **Jogando.**
   - O save local é a verdade. Ele sobe a cada 60 s se mudou, ao pausar no celular e ao fechar a janela;
     fechar também solta a sessão, esperando no máximo uns segundos.
   - Sem rede, nada muda para o jogador, a não ser o "Sem conexão" no Santuário. O batimento continua
     tentando. Na volta, o jogo toma a sessão sem forçar e sincroniza: o que se jogou sem rede sobe, ou,
     se a conta também mudou em outro aparelho, o jogo pergunta qual progresso fica.
   - Outro aparelho entrou com este conectado: no próximo batimento este volta para o login com o
     aviso, e o progresso que não subiu vira backup.
   - Outro aparelho está com a conta quando a rede volta: este volta para o login, mas o que se jogou
     sem rede fica no aparelho e entra na conta na próxima entrada aqui.
4. **Nome da conta:** aparece no Santuário, no lugar de "Conta" ao lado do nível, e troca nos Ajustes.
   Numa conta sem nome (criada antes de ele existir), o jogo pede um logo depois de entrar.
5. **Sair da conta** (Ajustes): envia, solta e volta para o login. Sem rede, pergunta antes, e o
   progresso fica no aparelho até a próxima entrada.

Cada conta tem o seu save no aparelho (`sigilos.account-<id>.json`); o `sigilos.json` continua sendo o
do jogo sem conta. Com `-- --save=nome`, cada nome é um aparelho à parte, com o seu id: dá para testar
dois aparelhos na mesma máquina.

**Android:** o preset de exportação precisa da permissão **Internet** (Permissions → Internet), que hoje
está desligada. Sem ela, todo pedido falha como "sem conexão".

### Notas sobre o servidor

Coisas vistas ao ligar o jogo no servidor. Nenhuma impede o uso; ficam para quando mexer nele.

- **A janela da sessão é de 30 s** no `claim` e no batimento (o `claim` era de 2 min e foi igualado).
  Um celular que passa mais de 30 s em segundo plano volta recebendo "sessão tomada" sem ninguém ter
  entrado. O jogo contorna: toma de novo sem forçar e segue, e só cai se outro aparelho estiver ativo.
- **Limitador de `/auth/*` por IP:** 10 pedidos por minuto para cada IP. O Caddy manda o IP de quem
  chamou em `X-Forwarded-For`, e o `UseForwardedHeaders` só aceita esse cabeçalho vindo de loopback. Sem
  isso, todos os pedidos chegariam como 127.0.0.1 e dividiriam a mesma cota.
- **Convite e nome:** o cadastro confere o convite antes de tudo (sem convite, a resposta não diz se o
  e-mail ou o nome existem), cria a conta e só então gasta o convite, gravando nele o id da conta. A
  conferência e a gravação ficam sob uma trava, para dois cadastros ao mesmo tempo não pegarem o mesmo
  convite ou o mesmo nome. O índice de `nameKey` não é único de propósito: contas de antes do nome
  existir não têm `nameKey`, e o LiteDB contaria os nulos como repetidos.
- **LGPD:** ainda não há rota para apagar a conta.

## Etapas e esforço

1. **Servidor** (4–6 dias):
   - projeto, LiteDB, cadastro e login com JWT, sessão, save, limitador;
   - testes de integração (`WebApplicationFactory`): dois "aparelhos" disputando a conta e um save
     com revisão velha.
2. **Infra** (1–2 dias): Caddy e domínio, `systemd`, portas, backup no Object Storage, monitor.
3. **Cliente** (5–7 dias): telas de login e cadastro, sessão, sincronização, aparelho derrubado; testes
   com dois aparelhos de verdade.

Total: **2 a 3 semanas** de trabalho. É o mesmo tempo do caminho Firebase, trocando "configurar o
serviço dos outros" por "cuidar do seu".

## Firebase ou servidor próprio?

| | Firebase | Servidor na Oracle |
| --- | --- | --- |
| Custo | zero dentro da cota | zero (instância que você já tem) |
| Limite | ~270 h de jogo/dia somando todos | o da máquina: sobra para muito mais |
| Aparelho derrubado | em até 1 min | na hora (ou em 30 s) |
| Quem cuida de backup, segurança, máquina no ar | o Google | você |
| Linguagem do servidor | regras do Firestore + REST | C#, como o jogo |
| Melhor se | quer zero manutenção | já cuida da instância e quer controle |
