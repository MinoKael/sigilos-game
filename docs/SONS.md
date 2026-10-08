# Sons: a biblioteca de Sigilos

Todos os efeitos sonoros do jogo saem de `Tools/sounds`, sintetizados do zero: nenhuma gravação, banco
de sons ou arquivo de áudio de fora. Cada efeito é uma receita em C# (camadas de osciladores, ruído e
corpos que vibram), e a ferramenta gera os `.wav` em `Assets/Audio` e o catálogo `sounds.json`. A
mesma semente dá sempre os mesmos bytes, então a biblioteca pode ser refeita a qualquer momento.

O jogo ainda não toca esses sons; o catálogo já está pronto para a integração (ver "O catálogo").

## Gerar

Da raiz do repositório:

```bash
dotnet run --project Tools/sounds -c Release
```

Gera os 223 efeitos (330 arquivos, uns 17 MB) em 2 segundos e apaga os `.wav` que não são mais de
nenhum efeito. Opções:

| Opção | O que faz |
| --- | --- |
| `--only=ui` | só uma pasta (exata: `combat` não leva `combat/damage`) |
| `--only=combat/` | a pasta e as de dentro |
| `--only=ui/button_click,combat.damage.fire` | efeitos soltos, com `/` ou com `.` (o nome lógico) |
| `--seed=7451` | a semente (7451 é a da biblioteca; trocar muda todos os arquivos) |
| `--out=pasta` | outra pasta de saída (o catálogo sai com caminhos relativos a ela) |
| `--rate=44100` | taxa de amostragem |
| `--list` | lista os efeitos, sem gerar |
| `--report` | por arquivo: duração, pico, volume percebido, alvo, energia abaixo de 150 Hz e acima de 6 kHz, e os avisos |

Gerar só uma parte refaz o catálogo inteiro (lendo os `.wav` do disco), apaga as variações que
sobraram dos efeitos escolhidos (um efeito que tinha 3 e passou a 2) e não toca no resto.

Depois de gerar, o Godot importa os arquivos ao abrir o editor (ou
`Godot_v4.7.1-stable_mono_win64_console.exe --headless --path . --import`). Os `.import` vão para o
git junto com os `.wav`; a ferramenta apaga o `.import` junto de um `.wav` que sai.

## Identidade

Grimório arcano, constelações, invocação e fantasia medieval mística. Na prática:

- **Interface:** delicada, tátil, quase de desenho animado: toques de madeira, papel e uma notinha
  de marimba ou celesta. Curta e baixa, porque se repete o tempo todo.
- **Grimório:** couro, papel, pena e tinta na frente; a magia entra por cima, em brilhos.
- **Constelações:** cristal, sinos e ar, no modo lídio (o "céu").
- **Invocação:** o ritual cresce em camadas (círculo, sigilo, energia, portal) e a revelação cresce
  com a raridade: a 5★ e a lendária têm coro, sinos e cauda mais longa.
- **Combate:** magia mais densa e escura; os golpes têm o corpo nos médios, para ler em volume baixo
  e no alto-falante do celular.
- **Chefe:** voz própria (rosnado, tambor, gongo e batimento), no modo frígio.
- **Sintetizador discreto:** serras sempre filtradas e com coro, nada de varredura de ficção
  científica. Madeira, papel e couro ficam em segunda camada, dando matéria à magia.

**Tom:** tudo gira em Ré. Interface e recompensas em Ré maior ou pentatônico, o céu em Ré lídio, a
sombra e o chefe em Ré frígio. A assinatura de Sigilos são as quintas empilhadas Ré–Lá–Mi
(`Arcane.Sigil`): aparece no sigilo da invocação, nos grandes feitiços de cada elemento, no Ímpeto
cheio e nas habilidades.

## As classes de mixagem

Cada efeito tem uma classe (`Mix`), que decide o volume e os cortes; `levelDb` acerta um efeito em
relação aos outros da mesma classe (o foco de botão 7 dB abaixo do clique, por exemplo).

| Classe | Volume percebido | Pico máximo | Corte grave | Corte agudo | Cauda até | Usada em |
| --- | --- | --- | --- | --- | --- | --- |
| `ui` | −25 dB | −6 dBFS | 160 Hz | 10 kHz | −48 dB | interface |
| `soft` | −22 dB | −4 dBFS | 90 Hz | 13 kHz | −54 dB | grimório, constelações, progressão |
| `reward` | −19 dB | −2 dBFS | 70 Hz | 14 kHz | −54 dB | invocação, recompensas |
| `combat` | −18 dB | −1,5 dBFS | 55 Hz | 11 kHz | −50 dB | golpes, dano, elementos, efeitos |
| `epic` | −18 dB | −1 dBFS | 40 Hz | 15 kHz | −60 dB | lendários, chefe, vitória, derrota |

O volume percebido é o da janela de 50 ms mais forte, com uma curva parecida com a K da EBU R128
(grave pesa menos, presença acima de 1,5 kHz pesa mais): assim um clique e uma explosão da mesma
classe soam do mesmo tamanho. A masterização (`Mastering/Master.cs`) corta grave e agudo da classe,
apara a cauda, suaviza as pontas, leva ao volume da classe e passa pelo limitador (com antecipação de
2 ms), que nunca deixa passar do pico máximo. Os arquivos são 16 bits, mono, com dither.

O `--report` avisa: pico acima do teto, volume 3 dB abaixo do alvo (pico demais para pouco corpo),
mais de 45% da energia abaixo de 150 Hz, e mais de 20% acima de 6 kHz em interface e combate (30%
nas outras), que é o que cansa num som repetido.

## As pastas

| Pasta | O quê |
| --- | --- |
| `ui` | botões, abas, menus, janelas, rolagem, arrastar, equipar, alternar, erro |
| `grimoire` | o livro: abrir, folhear, escrever, tinta mágica, descobertas, capítulo completo |
| `constellation` | estrelas, linhas, constelação completa, nós e caminhos, evolução |
| `summon` | o ritual, as revelações por raridade e por estrela, duplicata, Fragmentos e Essência |
| `rewards` | Ouro, Essência, experiência, runas, itens por raridade, nível, baús |
| `combat` | ataques leve a muito pesado, crítico, corte, perfuração, projéteis, área, erro, bloqueio |
| `combat/damage` | dano por tipo e elemento, periódico, crítico, absorvido, reduzido, anulado |
| `combat/elements` | Fogo, Água, Vento, Luz e Trevas, quatro de cada mais o grande feitiço |
| `combat/status` | fortalecer e enfraquecer, cada efeito de batalha, cura, escudo |
| `combat/boss` | chefe: entrada, especial, fúria, mecânica, regeneração, derrota |
| `progression` | Ímpeto, habilidades, turnos, vitória e derrota, fase, onda, Batalha automática |

`dotnet run --project Tools/sounds -c Release -- --list` mostra todos, com a descrição de cada um.

## O catálogo

`Assets/Audio/sounds.json`, refeito a cada geração. A chave é o nome lógico, que é o caminho com
pontos; um efeito com variações lista todas, e o jogo sorteia uma a cada vez:

```json
"ui.button_click": {
  "category": "ui",
  "description": "Clique de botão: toc de madeira com uma notinha de marimba.",
  "mix": "ui",
  "files": [
    "res://Assets/Audio/ui/button_click_01.wav",
    "res://Assets/Audio/ui/button_click_02.wav",
    "res://Assets/Audio/ui/button_click_03.wav"
  ],
  "seconds": 0.09,
  "peak_db": -9.3
}
```

Nomes de arquivo: `nome.wav` com uma variação, `nome_01.wav`, `nome_02.wav`... com várias. O nome
lógico não muda quando o número de variações muda; é ele que o jogo deve usar.

## Adicionar um efeito

1. Na receita da pasta (`Tools/sounds/Recipes/<Pasta>Sounds.cs`), uma linha nova:

   ```csharp
   yield return r.Of("rune_polish", "Polir runa: o pano no cristal e um brilho.", p =>
   {
       Foley.Swish(p, 0, 0.18, 1800, 3200, 0.4);
       Arcane.Glass(p, A5, 0.12, 0.5, 0.6);
       p.Reverb(0.12, 0.7);
   }, variations: 2);
   ```

   `r.With(Mix.Epic, ...)` troca a classe do efeito; `levelDb:` sobe ou desce. Nome em snake_case,
   descrição em português.
2. `dotnet run --project Tools/sounds -c Release -- --only=<pasta>/rune_polish --report` e ouvir.
3. Pasta nova: a pasta em `SoundCatalog.Categories`, um `Recipes/<Pasta>Sounds.cs` e a linha dele em
   `SoundCatalog.All()`.

O catálogo recusa nome repetido, nome fora de snake_case, pasta que não está na lista e um nome que
pareça variação de outro da mesma pasta (`gold` e `gold_02`).

## Como é feito

- **`Synth/`:** o motor. Um `Patch` é a mesa de uma variação: a receita põe camadas
  (`Tone`, `Noise`, `Modal` para sinos, vidro, madeira e metal, `Pluck` para cordas, `Crackle` para
  estalos), cada uma com envelope, filtros, glissando, vibrato, FM, coro, tremolo e vogal (formantes,
  para o coro e o sussurro), e o espaço do efeito inteiro (`Reverb`, `Echo`).
- **`Motifs/`:** o vocabulário, reaproveitado pelas receitas. `Foley` (madeira, papel, couro, pena,
  moeda), `Arcane` (sinos, cristal, coro, ar, o sigilo Ré–Lá–Mi), `Strike` (sopro, impacto, corte,
  estouro, cacos), `Elemental` (chama, bolhas, vento, sombra) e `Colossus` (a voz do chefe).
- **`Recipes/`:** uma classe por pasta, um efeito por entrada.
- **`Mastering/`:** classes de mixagem, volume percebido, limitador e o WAV.
- **Variações e semente:** cada variação tem o próprio sorteio, tirado da semente, do caminho do
  efeito e do número da variação. Mudar uma receita não mexe no som de nenhuma outra, e as variações
  mudam de propósito (outra nota, com `Recipe.Pick`) e um pouco ao acaso (`p.Vary`).
