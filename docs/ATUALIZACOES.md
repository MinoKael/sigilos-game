# Atualização automática (Windows)

O `Sigilos.exe` se atualiza sozinho a partir do servidor próprio. Ao abrir, ele pergunta se há versão
nova; se o jogador aceitar, baixa, confere, troca o próprio executável e abre de novo, já atualizado.
No Android não: um app não troca o próprio APK (ver "Android", no fim).

## Como funciona

1. Ao abrir, o jogo lê `https://sigilos.minopavel.duckdns.org/releases/windows/latest.json`, sem
   segurar nada: a resposta chega com o jogo já na tela. Sem rede, ou sem nada publicado, segue o
   jogo como está.
2. O `latest.json` só vale assinado (ver "A chave"). Ele diz a versão nova, a mínima que ainda pode
   jogar, o nome do executável, o tamanho e o SHA-256.
3. Se a versão do servidor é mais nova que a do jogo (`application/config/version`), abre a janela
   "Nova versão", com Atualizar e Depois. Abaixo da mínima, a janela é "Atualização obrigatória", sem
   Depois: é atualizar ou sair do jogo.
4. Atualizar baixa `Sigilos.exe.new` ao lado do executável, com a barra de progresso e Cancelar. O
   arquivo só fica se o tamanho e o SHA-256 baterem com o manifesto.
5. O jogo grava, envia o que falta para a nuvem e solta a sessão da conta, como ao fechar. Só então
   troca os arquivos: o executável atual vira `Sigilos.exe.old` (o Windows não deixa apagar um
   executável aberto, mas deixa renomear) e o `.new` toma o lugar dele.
6. Um PowerShell escondido espera este processo terminar e abre o executável novo. Abrir antes daria
   errado: o Godot extrai o .NET do jogo para `%LOCALAPPDATA%\data_Sigilos_windows_x86_64` e, numa
   versão nova, apaga a extração velha, que fica presa enquanto o processo velho existe.
7. O jogo novo apaga o `.old` ao abrir e entra na conta lembrada como sempre.

Só no executável exportado do Windows: no editor, o executável seria o do Godot.

O código: `GameEntry/Update/` (`UpdateManifest`, a leitura e a assinatura; `Updater`, a pergunta ao
servidor, o download, a troca e a reabertura), `UI/Screens/UpdateDialog.cs` (a janela) e a seção
"Atualização" do `GameRoot`. Testes em `Tests/UpdateTests.cs`.

## A chave

O manifesto é assinado com ECDSA P-256. A chave pública está no jogo (`Updater.PublicKey`); a privada
fica só no computador de quem publica, em `%USERPROFILE%\.sigilos\update-key.pem`, e nunca entra no
repositório nem no servidor. Assim, quem invadir o servidor consegue trocar arquivos, mas não
consegue assinar: o jogo recusa e segue como está.

- **Faça uma cópia da chave privada** fora deste computador (um pendrive, um gerenciador de senhas).
  Perdida, as cópias do jogo que já estão por aí nunca mais aceitam uma versão nova: todo jogador
  teria de baixar a próxima à mão.
- Criar a chave é uma vez só (`dotnet run --project Tools/release -- keygen`). A ferramenta recusa se
  já existe uma.

## Publicar uma versão

1. Suba `application/config/version` no `project.godot` (por exemplo, `0.4.0`) e o caminho do preset
   Windows (`Releases/v0.4.0/Windows/Sigilos.exe`).
2. Exporte o Windows.
3. Monte o pacote:

   ```bash
   dotnet run --project Tools/release -- windows
   ```

   Ele lê a versão do `project.godot`, pega o executável de `Releases/v<versão>/Windows/` e cria
   `Releases/v<versão>/Update/` com `Sigilos-<versão>.exe` e o `latest.json` assinado. Antes, confere
   que a chave privada é o par da pública que está no jogo.
   - `--minimum 0.4.0` torna a atualização obrigatória para quem está abaixo dessa versão. Use quando
     a versão velha não puder mais jogar, por exemplo se o save mudou de formato e uma cópia velha
     estragaria o save da nuvem. Sem ele, ninguém é obrigado.
   - `--exe caminho` usa outro executável; `--key caminho` (ou a variável `SIGILOS_UPDATE_KEY`), outra
     chave.
4. Envie para o servidor, **o executável antes do manifesto**. Um manifesto apontando para um arquivo
   que ainda não chegou daria erro no download de quem abrisse o jogo nesse meio tempo:

   ```bash
   dotnet run --project Tools/release -- windows --upload ubuntu@servidor:/home/ubuntu/sigilos/releases/windows/
   ```

   O `--upload` usa o `scp` e já envia nessa ordem. Sem ele, a ferramenta mostra os dois comandos.
5. Os executáveis velhos podem ser apagados do servidor: o jogo só baixa o que o `latest.json` atual
   aponta.

A versão que o jogo compara é a que foi exportada dentro dele. Exportar sem subir a versão publica
um executável que se acha velho e se oferece para atualizar de novo.

## O servidor

O Caddy serve a pasta direto, sem passar pelo servidor ASP.NET. No `Caddyfile`, antes do proxy:

```caddy
sigilos.minopavel.duckdns.org {
    encode gzip
    handle_path /releases/* {
        root * /home/ubuntu/sigilos/releases
        file_server
    }
    handle {
        reverse_proxy 127.0.0.1:5080
    }
}
```

E a pasta: `mkdir -p /home/ubuntu/sigilos/releases/windows`. O mesmo trecho está no README do
repositório do servidor.

## Testar sem publicar

O executável exportado aceita `-- --updates=url`: procura versões em outra pasta. Um servidor local
de arquivos com um `latest.json` assinado (versão maior que a do jogo) e um executável qualquer ao
lado basta para ver a janela, o download, a troca e o jogo abrindo de novo. Rode o executável numa
pasta sua, nunca o de `Releases/`, que seria trocado.

## Cuidados

- **Pasta com permissão de escrita.** Em `Arquivos de Programas`, o jogo não consegue gravar o
  `.new`: a janela explica e oferece "Baixar no navegador".
- **Tamanho.** Cada atualização baixa o executável inteiro, uns 190 MB. Se pesar, o caminho é a
  atualização por diferença do Velopack, com um lançador à parte.
- **SmartScreen.** O aviso do Windows aparece no primeiro download pelo navegador, não no que o
  próprio jogo baixa. Um certificado de assinatura de código (pago) tira o aviso de vez.
- **Antivírus.** Um executável que baixa outro e abre um PowerShell pode chamar a atenção de algum
  antivírus. Assinar o executável também ajuda aqui.

## Android

O app não troca o próprio APK. Os caminhos são a Play Store (mesmo só no teste fechado, que atualiza
sozinha) ou baixar o APK e abrir o instalador do sistema, com a confirmação do jogador e a permissão
de instalar apps de fontes desconhecidas.
