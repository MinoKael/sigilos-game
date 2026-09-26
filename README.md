# Sigilos

Gacha de fantasia offline para um jogador, com base mecânica e visual em Summoners War: colecione
invocações, suba o nível delas até 40, desperte, equipe runas e comande batalhas por turnos com barra
de Ímpeto. O design completo está em [SIGILOS — Game Design Document.md](SIGILOS%20—%20Game%20Design%20Document.md).

Este repositório é o **MVP**: combate 5 contra 5 com Ímpeto e Éter (Éter só no manual), gacha com
garantia em que cada invocação é uma cópia nova, coleção com Baú, Ecos por fusão, uma equipe por
conteúdo, ociosidade, nível 1–40, Despertar, runas e atributos iguais aos de Summoners War (1 a 6
estrelas, +15, 4 subatributos, 16 conjuntos com um Glifo cada, Pedra de Afiar e Gema Encantada; só a
melhora nunca falha), 9 famílias de invocação (45 variantes; os inimigos comuns são essas invocações
reforçadas), a região 1 (20 fases), cinco Masmorras, Batalha automática (30 lutas seguidas), Mana para
entrar nas lutas, nível da conta, Ouro e Loja, Compêndio (regras) e Grimório (tudo o que existe). O jogo é em inglês: todo texto da
interface mora em `Data/texts/en.json`, com tradução para português em `Data/texts/pt-BR.json`.

## Rodar

Precisa do Godot 4.7 **.NET** e do .NET SDK 8+.

- Jogo: abra `project.godot` no editor e aperte F5 (ou `Godot --path .`).
- Testes e simulador (sem Godot), na raiz:

```bash
dotnet run --project Tests
```

```bash
dotnet run --project Tests -- --simulate
```

```bash
dotnet run --project Tests -- --fight=10
```

`--simulate` roda cada fase e cada andar de Masmorra 40 vezes no automático e mostra vitórias,
rodadas e vida que sobra: é a ferramenta de balanceamento. `--fight=N` imprime uma luta da fase N
turno a turno.

Argumentos de desenvolvimento do jogo (depois de `--`): `--save=nome` usa outro save;
`--language=nome` usa `Data/texts/nome.json` (padrão: `en`; `pt-BR` para português);
`--screen=campaign|dungeons|summon|shop|monsters|teams|runes|compendium|grimoire|battle` abre essa tela direto.

## Textos e tradução

Todo texto da interface está em `Data/texts/en.json`, a base, por chave. Para traduzir, copie o
arquivo (`es.json`...), troque os textos e rode com `-- --language=es`; `pt-BR.json` já é uma tradução
pronta. Ids e nomes de dados são em inglês. Nomes de invocações, inimigos, fases e
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
