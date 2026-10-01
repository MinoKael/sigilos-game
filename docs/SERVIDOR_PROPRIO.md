# Servidor próprio na instância Oracle — projeto

Alternativa ao Firebase de [SAVE_NUVEM.md](SAVE_NUVEM.md): o mesmo resultado (contas, save em nuvem,
um aparelho por vez), num servidor pequeno na instância Oracle que já existe, com o domínio dela.
Ainda não implementado.

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
  padrão, então HTTPS não é opcional.
- **Implantação:** `dotnet publish -c Release -r linux-arm64 --self-contained` (ou `linux-x64` na AMD)
  e um serviço `systemd`. Docker Compose (caddy + server) também serve, se você já usa.

## Dados (coleções do LiteDB)

| Coleção | Documento | Observação |
| --- | --- | --- |
| `users` | `_id`, `email` (único, minúsculo), `passwordHash`, `createdAt` | senha com PBKDF2 (`PasswordHasher`) ou Argon2id |
| `refreshTokens` | `_id` (hash do token), `userId`, `deviceId`, `expiresAt` | o token em si nunca é guardado; troca a cada uso |
| `sessions` | `_id` = userId, `sessionId`, `deviceId`, `deviceName`, `lastSeen` | a trava: um por conta |
| `saves` | `_id` = userId, `revision`, `savedAt`, `data` (bytes gzip) | ~15 KB por conta |
| `invites` | `_id` = código, `usedBy` | só entra quem tem convite: o servidor não fica aberto para estranhos |

## API

| Método e rota | Corpo | Resposta |
| --- | --- | --- |
| `POST /auth/register` | e-mail, senha, convite | 201, ou 409 se o e-mail existe |
| `POST /auth/login` | e-mail, senha, `deviceId` | token de acesso (JWT, 15 min) e de renovação (30 dias) |
| `POST /auth/refresh` | token de renovação | um par novo (o velho deixa de valer) |
| `POST /session/claim` | `deviceId`, `deviceName`, `force` | `sessionId`; ou 409 com o aparelho e quando foi visto, se outro está ativo e `force` é falso |
| `POST /session/heartbeat` | `sessionId` | 204; ou 409 "sessão tomada" (o jogo volta para o login) |
| `POST /session/release` | `sessionId` | 204 (ao sair da conta) |
| `GET /save` | — | revisão, data e o save; ou 404 na conta nova |
| `PUT /save` | `sessionId`, `baseRevision`, `revision`, save | 204; 409 se a sessão não é mais desse aparelho ou se a nuvem mudou desde `baseRevision` |
| `GET /health` | — | 200, para um monitor externo |

**A trava fica no servidor**, que é quem decide:
- `claim` troca o `sessionId` da conta: o aparelho antigo passa a receber 409 no próximo batimento
  (a cada 30 s, já que não há cota) e no próximo envio do save.
- Um arrendamento de ~2 min sem batimento libera a conta sem perguntar.
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

Igual ao desenho com Firebase, só que mais simples, porque a API é sua:
- `GameEntry/Account/`, com a URL do servidor numa configuração do projeto:
  - `AuthClient`: cadastro, login, renovação;
  - `SessionLock`: claim, batimento, release;
  - `CloudSave`: baixar e enviar.
- O `GameRoot` mostra o login antes do Santuário e derruba o jogador no 409.
- O Core não muda.
- O `HttpClient` do .NET funciona no Godot .NET em Windows e Android.

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
