# Sigilos

Gacha de fantasia offline para um jogador, com base mecânica e visual em Summoners War: colecione
invocações, suba o nível delas até 40, desperte, equipe runas e comande batalhas por turnos com barra
de Ímpeto. O design completo está em [SIGILOS — Game Design Document.md](SIGILOS%20—%20Game%20Design%20Document.md).

Este repositório é o **MVP**: combate 5 contra 5 com Ímpeto e Éter (Éter só no manual), gacha com
garantia em que cada invocação é uma cópia nova, coleção com Baú, Ecos por fusão, uma equipe por
conteúdo, ociosidade, nível 1–40, Despertar, runas e atributos iguais aos de Summoners War (1 a 6
estrelas, +15, 4 subatributos, 16 conjuntos com um Glifo cada, Pedra de Afiar e Gema Encantada; só a
melhora nunca falha), a região 1 (20 fases), cinco Masmorras, Mana para entrar nas lutas, nível da
conta, Ouro e Loja, Compêndio (regras) e Grimório (tudo o que existe). Todo texto da interface mora em
`Data/texts/pt-BR.json`.

## Rodar

Precisa do Godot 4.7 **.NET** e do .NET SDK 8+.

- Jogo: abra `project.godot` no editor e aperte F5 (ou `Godot --path .`).
- Testes e simulador (sem Godot), na raiz:

```bash
dotnet run --project Tests
```

```bash
dotnet run --project Tests -- --simular
```

```bash
dotnet run --project Tests -- --luta=10
```

`--simular` roda cada fase e cada andar de Masmorra 40 vezes no automático e mostra vitórias,
rodadas e vida que sobra: é a ferramenta de balanceamento. `--luta=N` imprime uma luta da fase N
turno a turno.

Argumentos de desenvolvimento do jogo (depois de `--`): `--save=nome` usa outro save;
`--idioma=nome` usa `Data/texts/nome.json`;
`--tela=campanha|masmorras|invocar|loja|monstros|equipes|runas|compendio|grimorio|batalha` abre essa tela direto.

## Textos e tradução

Todo texto da interface está em `Data/texts/pt-BR.json`, por chave. Para traduzir, copie o arquivo
(`en.json`...), troque os textos e rode com `-- --idioma=en`. Nomes de invocações, inimigos, fases e
Masmorras ficam nos arquivos de `Data/`. Para conferir se falta ou sobra alguma chave:

```bash
py Tools/texts/check_texts.py
```

## Pastas

| Pasta | O que tem | Depende de |
| --- | --- | --- |
| `Core/` | Regras do jogo em C# puro, sem Godot | nada |
| `Data/` | Conteúdo em JSON: papéis, famílias, invocações, inimigos, fases, Masmorras e os textos da interface | — |
| `UI/` | Telas e componentes Godot. Mostram e avisam por evento | Core |
| `GameEntry/` | Nó raiz: carrega dados e save, troca telas, aplica regras | Core, UI |
| `Assets/` | SVGs do Wikimedia Commons e o shader de traço | — |
| `Tests/` | Console app: testes e simulador, compila `Core/` por link | Core |
| `Tools/` | Scripts: baixar a arte do Commons, conferir os textos | — |

Detalhes, regras de dependência e onde mexer para cada tipo de mudança: [docs/ARQUITETURA.md](docs/ARQUITETURA.md).

## Arte

Toda imagem vem do Wikimedia Commons: os 16 Glifos (letras rúnicas do futhark antigo) e símbolos
alquímicos (elementos) em domínio público, criaturas (normais e despertas) e ícones do game-icons.net (CC BY 3.0). Lista e autores em
[Assets/CREDITOS.md](Assets/CREDITOS.md). O shader `doodle` e parte dos SVGs vieram de Rabiscos&Runas.

Para baixar de novo (o Python do Inkscape não tem certificados SSL, use `py`; se o Wikimedia
responder 429, use o Chromium):

```bash
py Tools/art/fetch_commons_assets.py --chrome C:/Chromium/Application/chrome.exe
```
