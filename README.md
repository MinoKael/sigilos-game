# Sigilos

Gacha de fantasia offline para um jogador, com base mecânica e visual em Summoners War: colecione
invocações, suba o nível e as estrelas delas até 6★ nível 40, desperte, equipe runas e comande batalhas por turnos com barra
de Ímpeto. O design completo está em [SIGILOS — Game Design Document.md](SIGILOS%20—%20Game%20Design%20Document.md).

Este repositório é o **MVP**: combate 5 contra 5 com Ímpeto e habilidades com recarga (básica, ativas
e Passiva), gacha com garantia em que cada invocação é uma cópia nova, coleção com Baú, cópias fundidas
sobem o nível de uma habilidade sorteada, uma equipe por conteúdo, ociosidade, estrelas e experiência
pela tabela de Summoners War (Evolução até 6★ com Essência e Fragmentos), Despertar que dá habilidade
nova (2★ e 3★), habilidade mais forte (4★) ou atributo (5★), interface de símbolos (botões são sigilos, o nome
vem na dica; texto explicativo só no Compêndio), runas e atributos iguais aos de Summoners War (1 a 6
estrelas, +15, 4 subatributos, 16 conjuntos com um Glifo cada, Pedra de Afiar e Gema Encantada; só a
melhora nunca falha), 17 famílias de invocação de 2★ a 5★ naturais (85 variantes; os inimigos comuns
são invocações reforçadas), a região 1 (20 fases), cinco Masmorras, Batalha automática (30 lutas seguidas), Mana para
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
turno a turno. `--digest` resume mais de mil lutas de semente fixa, uma por linha: rode antes e depois
de mexer no código do combate para conferir que nenhuma luta mudou.

Argumentos de desenvolvimento do jogo (depois de `--`): `--save=nome` usa outro save;
`--language=nome` usa `Data/texts/nome.json` (padrão: `en`; `pt-BR` para português);
`--screen=map|bag|campaign|dungeons|summon|shop|monsters|teams|runes|compendium|grimoire|battle` abre essa tela direto.
O idioma também pode ser trocado no jogo, na engrenagem do Santuário (fica salvo).

Depuração visual da interface, com o jogo rodando: Ctrl+F1 contornos, Ctrl+F2 nomes, Ctrl+F3 valores
ao vivo, Ctrl+F4 origem no código (a mesma tecla desliga). É o addon `addons/visual_debugger`, que só
liga em build de depuração. Nó sem nome aparece em cinza, e a barra do topo conta quantos há na tela.

## Invocações novas

Abra [docs/summon_family_builder.html](docs/summon_family_builder.html) no Chrome ou no Edge e clique
em "Abrir pasta do projeto". O construtor edita uma família (até 5 variantes, uma por elemento), calcula
os atributos pelo modelo de BVP e salva `Data/summons/<familia>_family.json`. Os parâmetros do modelo
(orçamento por estrelas, perfis de papel, pesos, vieses) são editáveis na aba Modelo e moram em
`Data/stat_model.json`; depois de mudar, "Recalcular todas" refaz os atributos do jogo inteiro. O jogo
não calcula atributo: lê o número pronto e confere contra o modelo (os testes acusam o que não bate).
A fórmula está em [docs/summon_family_stat_formula_spec.md](docs/summon_family_stat_formula_spec.md).

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
| `Data/` | Conteúdo em JSON: o modelo de atributos, as famílias de invocação (um arquivo por família), chefes, fases, Masmorras e os textos da interface | — |
| `UI/` | Telas e componentes Godot. Mostram e avisam por evento | Core |
| `GameEntry/` | Nó raiz: carrega dados e save, troca telas, aplica regras | Core, UI |
| `Assets/` | SVGs do Wikimedia Commons e o shader de traço | — |
| `Tests/` | Console app: testes e simulador, compila `Core/` por link | Core |
| `Tools/` | Scripts: baixar a arte do Commons, conferir os textos | — |
| `addons/visual_debugger/` | Overlay de depuração visual (Ctrl+F1 a Ctrl+F4), independente do jogo | nada |

Detalhes, regras de dependência e onde mexer para cada tipo de mudança: [docs/ARQUITETURA.md](docs/ARQUITETURA.md).

## Arte

Toda imagem vem do Wikimedia Commons: os 16 Glifos (letras rúnicas do futhark antigo) e símbolos
alquímicos (elementos) em domínio público, criaturas (normais e despertas), ícones e os símbolos dos
efeitos de batalha do game-icons.net (CC BY 3.0). Os desenhos mais novos foram copiados do acervo
baixado de game-icons.net, que são os mesmos do Commons. Lista e autores em
[Assets/CREDITOS.md](Assets/CREDITOS.md). O shader `doodle` e parte dos SVGs vieram de Rabiscos&Runas.

Fontes em `Assets/Fonts`: SFC Wezards (a do jogo) e Kehdrai, de Neale Davidson (as runas); cada uma
com a sua licença.

Os SVG são a fonte; o jogo usa PNG renderizados pelo Inkscape em 32, 64, 128, 256 e 512 px
(`Assets/Rendered`), porque o SVG rasterizado pelo Godot e depois escalado fica serrilhado. Depois de
baixar ou trocar um SVG:

```bash
py Tools/art/render_png.py
```

Para baixar de novo (o Python do Inkscape não tem certificados SSL, use `py`; se o Wikimedia
responder 429, use o Chromium):

```bash
py Tools/art/fetch_commons_assets.py --chrome C:/Chromium/Application/chrome.exe
```
