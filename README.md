# Sigilos

Gacha de fantasia offline para um jogador, com base mecânica e visual em Summoners War: colecione
invocações, suba o nível delas até 40, desperte, equipe runas e comande batalhas por turnos com barra
de Ímpeto. O design completo está em [SIGILOS — Game Design Document.md](SIGILOS%20—%20Game%20Design%20Document.md).

Este repositório é o **MVP**: combate com Ímpeto e Éter (Éter só no manual), gacha com garantia,
ociosidade, nível 1–40 por invocação, Despertar, runas e atributos iguais aos de Summoners War (1 a 6
estrelas, +15, 4 subatributos, 16 conjuntos, Pedra de Afiar e Gema Encantada; só a melhora nunca falha),
tela de Monstros, Compêndio e a região 1 (20 fases).

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

`--simular` roda cada fase 40 vezes no automático e mostra vitórias, rodadas e vida que sobra: é a
ferramenta de balanceamento. `--luta=N` imprime uma luta da fase N turno a turno.

Argumentos de desenvolvimento do jogo (depois de `--`): `--save=nome` usa outro save;
`--tela=campanha|invocar|monstros|runas|compendio|batalha` abre essa tela direto.

## Pastas

| Pasta | O que tem | Depende de |
| --- | --- | --- |
| `Core/` | Regras do jogo em C# puro, sem Godot | nada |
| `Data/` | Conteúdo em JSON: papéis, famílias, invocações, inimigos, fases | — |
| `UI/` | Telas e componentes Godot. Mostram e avisam por evento | Core |
| `GameEntry/` | Nó raiz: carrega dados e save, troca telas, aplica regras | Core, UI |
| `Assets/` | SVGs do Wikimedia Commons e o shader de traço | — |
| `Tests/` | Console app: testes e simulador, compila `Core/` por link | Core |
| `Tools/` | Script que baixa a arte do Commons | — |

Detalhes, regras de dependência e onde mexer para cada tipo de mudança: [docs/ARQUITETURA.md](docs/ARQUITETURA.md).

## Arte

Toda imagem vem do Wikimedia Commons: runas (Glifos) e símbolos alquímicos (elementos) em domínio
público, criaturas (normais e despertas) e ícones do game-icons.net (CC BY 3.0). Lista e autores em
[Assets/CREDITOS.md](Assets/CREDITOS.md). O shader `doodle` e parte dos SVGs vieram de Rabiscos&Runas.

Para baixar de novo (o Python do Inkscape não tem certificados SSL, use `py`; se o Wikimedia
responder 429, use o Chromium):

```bash
py Tools/art/fetch_commons_assets.py --chrome C:/Chromium/Application/chrome.exe
```
