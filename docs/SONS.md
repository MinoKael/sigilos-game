# Sons: a biblioteca de Sigilos

Todos os efeitos sonoros do jogo saem de `Tools/sounds`, sintetizados do zero: nenhuma gravação, banco
de sons ou arquivo de áudio de fora. Cada efeito é uma receita em C# (camadas de osciladores, ruído e
corpos que vibram), e a ferramenta gera os `.wav` em `Assets/Audio` e o catálogo `sounds.json`. A
mesma semente dá sempre os mesmos bytes, então a biblioteca pode ser refeita a qualquer momento.

O jogo toca pelo nome lógico do catálogo (ver "No jogo").

## Gerar

Da raiz do repositório:

```bash
dotnet run --project Tools/sounds -c Release
```

Gera os 62 efeitos (84 arquivos, uns 5 MB) em menos de um segundo e apaga os `.wav` que não são mais
de nenhum efeito. Opções:

| Opção | O que faz |
| --- | --- |
| `--only=ui` | só uma pasta (exata: `combat` não leva `combat/elements`) |
| `--only=combat/` | a pasta e as de dentro |
| `--only=ui/button_click,combat.elements.fire_impact` | efeitos soltos, com `/` ou com `.` (o nome lógico) |
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

Um grimório mágico num universo de constelações, não uma interface cheia de avisos sonoros. A
referência é a música do jogo: harpa contínua e sininhos delicados de canção de ninar. Os efeitos
moram dentro dela.

- **Menos é mais.** Um som só quando ele diz alguma coisa; o que é secundário divide o som de outro
  ou fica calado, e o redundante não existe.
- **Harmonia antes de quantidade, suavidade antes de impacto.** Harpa, celesta, sinos macios, taça e
  feltro. Nada de ataque duro, agudo agressivo, distorção, vidro, metal ou estouro; o brilho para em
  6 a 8 kHz.
- **Importância não é volume.** O evento importante é reconhecível pelas camadas (mais vozes no
  acorde, coro, a subida antes), não por ser mais alto.

**Tom:** Mi menor / Sol maior, o da música. A melodia fica na pentatônica (Mi Sol Lá Si Ré), que soa
junto com qualquer acorde dela; os acordes são de Mi menor e Sol maior. A assinatura de Sigilos é a
quinta empilhada Ré–Lá–Mi (`Arcane.Sigil`), que resolve em Mi: o sigilo da invocação, a revelação 5★
e os grandes feitiços (no Vento sobre Lá, na Luz e nas Trevas sobre Sol, sempre dentro do tom).

**A hierarquia** (volume percebido na janela de 400 ms, a música fica no meio, em torno de −25 dB):

| Degrau | Sons | Volume |
| --- | --- | --- |
| Recorrente | aba, controle de volume, página, efeito na luta, sua vez | −33 a −38 dB |
| Navegação | clique (a medida de tudo), voltar, janela, painel, equipar | −31 a −33 dB |
| Combate comum | acerto, nocaute, impacto de elemento, nova onda | −29 a −31 dB |
| Habilidades importantes | grande feitiço, chefe, recompensas raras, subir de nível | −26 a −28 dB |
| Invocação e momentos especiais | revelação 4★ e 5★, item lendário, vitória | −24 a −26 dB |

**O cansaço do farm:** o que se repete centenas de vezes é curto (o acerto tem 0,16 s), baixo e
varia de nota a cada vez (o acerto em Mi, Sol, Si ou Lá, sem repetir a última). O mesmo som não
empilha (ver "No jogo").

## As classes de mixagem

Cada efeito tem uma classe (`Mix`), que decide o volume e os cortes; `levelDb` acerta um efeito em
relação aos outros da mesma classe, para ele cair no degrau dele da hierarquia.

| Classe | Volume percebido | Pico máximo | Corte grave | Corte agudo | Cauda até | Usada em |
| --- | --- | --- | --- | --- | --- | --- |
| `ui` | −25 dB | −6 dBFS | 160 Hz | 10 kHz | −48 dB | interface |
| `soft` | −27 dB | −6 dBFS | 90 Hz | 8 kHz | −54 dB | grimório, constelações, progressão, efeitos da luta |
| `reward` | −24 dB | −4 dBFS | 70 Hz | 8 kHz | −54 dB | invocação, recompensas |
| `combat` | −25 dB | −4 dBFS | 60 Hz | 7 kHz | −50 dB | acertos, nocaute, impactos dos elementos |
| `epic` | −25 dB | −3 dBFS | 45 Hz | 8 kHz | −60 dB | grandes feitiços, chefe, vitória, derrota, lendários |

O volume percebido é o da janela de 50 ms mais forte, com uma curva parecida com a K da EBU R128
(grave pesa menos, presença acima de 1,5 kHz pesa mais). A masterização (`Mastering/Master.cs`) corta
grave e agudo da classe, apara a cauda, suaviza as pontas, leva ao volume da classe e passa pelo
limitador (com antecipação de 2 ms), que nunca deixa passar do pico máximo. Os arquivos são 16 bits,
mono, com dither.

O `--report` avisa: pico acima do teto, volume 3 dB abaixo do alvo (pico demais para pouco corpo),
mais de 45% da energia abaixo de 150 Hz, e mais de 20% acima de 6 kHz em interface e combate (30%
nas outras), que é o que cansa num som repetido.

## Os efeitos

| Pasta | Efeitos |
| --- | --- |
| `ui` | clique, aba, painel, janela (abrir e fechar), voltar, equipar e desequipar, ligar e desligar, controle de volume (subir e descer), ação indisponível |
| `grimoire` | abrir e fechar o livro, virar e escolher página, tinta mágica, criatura registrada |
| `constellation` | estrela acendendo |
| `summon` | começo do ritual, energia subindo, criatura invocada, revelação de 1★ a 5★ |
| `rewards` | Ouro, Essência, experiência, runa, item raro, épico e lendário, subir de nível, subir estrela, recompensa |
| `combat` | acerto, acerto pesado, nocaute |
| `combat/elements` | o impacto e o grande feitiço de Fogo, Água, Vento, Luz e Trevas |
| `combat/status` | o bem (curar, fortalecer, reviver) e o mal (enfraquecer) |
| `combat/boss` | entrada, habilidade e queda do chefe |
| `progression` | sua vez, nova onda, vitória, derrota, Batalha automática ligada e desligada |

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
  "peak_db": -10.5
}
```

Nomes de arquivo: `nome.wav` com uma variação, `nome_01.wav`, `nome_02.wav`... com várias. O nome
lógico não muda quando o número de variações muda; é ele que o jogo deve usar.

## Adicionar um efeito

Antes, veja se um som que já existe serve: um evento secundário divide o som de outro (bloquear e
favoritar usam o ligar e desligar; afiar, encantar e reavaliar, a tinta mágica). Efeito novo só com
uma necessidade clara.

1. Na receita da pasta (`Tools/sounds/Recipes/<Pasta>Sounds.cs`), uma linha nova:

   ```csharp
   yield return r.Of("rune_polish", "Polir runa: o pano na pedra e uma nota de celesta.", p =>
   {
       Foley.Swish(p, 0, 0.18, 900, 2200, 0.2);
       Arcane.Celesta(p, Pick(p, B5, E6), 0.08, 0.4, 0.45);
       p.Reverb(0.12, 0.7);
   }, variations: 2, levelDb: -2);
   ```

   `r.With(Mix.Epic, ...)` troca a classe do efeito; `levelDb:` sobe ou desce. Nome em snake_case,
   descrição em português, notas da pentatônica de Mi.
2. `dotnet run --project Tools/sounds -c Release -- --only=<pasta>/rune_polish --report` e ouvir junto
   com a música. Acerte o `levelDb` para o efeito cair no degrau dele da hierarquia.
3. Pasta nova: a pasta em `SoundCatalog.Categories`, um `Recipes/<Pasta>Sounds.cs` e a linha dele em
   `SoundCatalog.All()`.

O catálogo recusa nome repetido, nome fora de snake_case, pasta que não está na lista e um nome que
pareça variação de outro da mesma pasta (`gold` e `gold_02`).

## No jogo

`UI/Audio/Sfx.cs` é um nó do GameRoot, vivo o tempo todo, com 16 tocadores no barramento `Effects`
(o volume de **Efeitos** nos Ajustes, salvo no aparelho como os outros dois). Lê o catálogo ao abrir,
carrega os sons da interface na hora e o resto na primeira vez que toca. Sorteia a variação sem
repetir a última. Para os sons não empilharem: o mesmo som de novo em menos de 80 ms não toca, e um
som toca no máximo duas vezes junto (a terceira corta a mais antiga dele).

```csharp
Sfx.Play("rewards.star_up");               // agora
Sfx.Play("summon.energy_build", 0.3);      // daqui a 0,3 s
Sfx.PlayIf(Evolution.Evolve(...), "rewards.star_up");   // só se a ação pegou
Sfx.Fallback("ui.button_click");           // reserva (abaixo)
```

**A ação cala o clique.** Os componentes tocam sons de reserva (`Sfx.Fallback`): o botão
(`Juice`: clique, ou ligar e desligar), a aba (`TextTabs`), a janela abrindo e fechando (`Dialog`), a
tela que entra (`GameRoot.Swap`), a seta de voltar e o toque curto (`Press`). A reserva espera o fim
do quadro e só toca se nenhum `Sfx.Play` veio no mesmo quadro; entre as reservas do quadro, ganha a
de maior prioridade (voltar 3, janela 2, aba e tela 1, botão 0). Então o Evoluir soa a estrela, e não
o clique; o botão que abre uma janela soa a janela. O toque longo que abriu um resumo cala o clique
do soltar (`Sfx.Quiet`). A ação pequena sem som próprio (trocar o retrato, escolher o líder) fica com
o clique.

**A luta** (`UI/Audio/BattleSounds.cs`): cada momento vira no máximo 2 sons.

- A corrida até o alvo é calada.
- Um acerto só por golpe, mesmo em área ou crítico. O primeiro golpe da ação diz o que ela é: a
  básica, o acerto (do chefe, o acerto pesado); a habilidade com recarga, o impacto do elemento de
  quem usa (recarga 5 ou mais, o grande feitiço; a do chefe, a voz dele). Os golpes seguintes da
  mesma ação são o acerto simples.
- A queda vem 0,12 s depois do golpe (a do chefe tem a dele).
- A ação que só cura, fortalece, enfraquece ou revive soa uma vez: o bem ou o mal. A Bomba que
  explode é o acerto pesado.
- Calados: o erro e a Égide, o crítico, a vantagem de elemento, o escudo, o efeito que o golpe põe,
  a resistência e a imunidade, o Ímpeto, o turno perdido, o Veneno e o que acontece sozinho no
  começo e no fim do turno.
- A onda (a do chefe tem a entrada dele), a vitória e a derrota.

Os atrasos aceleram com a luta; a vista pequena da Batalha automática é muda. O turno manual soa ao
chegar.

**Onde o resto soa:**

| Onde | Sons |
| --- | --- |
| Interface | clique, ligar/desligar, aba, janela, painel (a tela que entra), voltar, os controles de volume |
| Resultado | a runa que caiu (mais rica quanto mais rara); com as barras, no máximo dois: o destaque (o monstro invocado, ou o prêmio de marco e a página nova do grimório) e a conta que subiu de nível; cada nível de monstro |
| Invocação | o ritual (sigilo e energia subindo) e cada cartão pelas estrelas, em cascata; o monstro novo fecha com o registro no grimório |
| Monstros | Infundir (experiência ou nível), Despertar, Evoluir e Fundir (subir estrela), Soltar (Essência), Guardar e Tirar (desequipar e equipar), Bloquear e Favoritar (ligar e desligar) |
| Runas | Equipar e Desequipar, Melhorar (a runa), Afiar, Encantar e Reavaliar (tinta mágica), Vender (Ouro), Bloquear (ligar e desligar) |
| Preparação da luta, Loja | pôr, tirar e trocar na equipe (equipar e desequipar; cheia, a ação indisponível), escolher um da equipe (ligar e desligar), comprar (Ouro) |
| Santuário e correio | coletar a Canalização (Essência), coletar cartas |
| Grimório e livro do Invocador | abrir, virar página, escolher a família, fechar |
| Exploração | a estrela ao escolher a constelação |
| Batalha automática | começar (e retomar) e parar; as lutas não soam |

**Som novo no código:** gere o efeito (acima), escreva o nome lógico inteiro no código (sem montar
com `$"..."`: uma tabela de nomes quando depende de um valor, como `SummonScreen.RevealSounds`) e rode
os testes. `SoundTests` confere que todo `"categoria.nome"` de `UI/` e `GameEntry/` que não é chave de
texto está no catálogo, que os arquivos do catálogo existem e como a luta vira som.

## Conferir

```bash
dotnet run --project Tests -- --battle-sounds=golem5
```

Joga a luta de verdade (uma fase como `40` ou uma Masmorra como `golem5`) e conta, em cada velocidade,
quantos sons tocam por minuto, quanto do tempo tem som, quantos tocam juntos e os que mais tocam.
`--timeline --speed=2` lista cada som com o tempo. Uma luta longa deve ficar perto de 30 sons por
minuto em 1×, com quase nada sobreposto.

## Como é feito

- **`Synth/`:** o motor. Um `Patch` é a mesa de uma variação: a receita põe camadas
  (`Tone`, `Noise`, `Modal` para sinos, celesta, madeira, taça e pedra, `Pluck` para a harpa,
  `Crackle` para estalos), cada uma com envelope, filtros, glissando, vibrato, FM, coro, tremolo e
  vogal (formantes, para o coro), e o espaço do efeito inteiro (`Reverb`, `Echo`). As notas estão em
  `Notes`, já no tom.
- **`Motifs/`:** o vocabulário, reaproveitado pelas receitas. `Arcane` (harpa, celesta, sinos, taça,
  coro, ar, o sigilo Ré–Lá–Mi), `Foley` (madeira, feltro, papel, couro, moeda), `Strike` (o acerto de
  feltro, o sopro e a florada grave) e `Elemental` (chama, bolhas, onda, vento, sombra).
- **`Recipes/`:** uma classe por pasta, um efeito por entrada.
- **`Mastering/`:** classes de mixagem, volume percebido, limitador e o WAV.
- **Variações e semente:** cada variação tem o próprio sorteio, tirado da semente, do caminho do
  efeito e do número da variação. Mudar uma receita não mexe no som de nenhuma outra, e as variações
  mudam de propósito (outra nota, com `Recipe.Pick`) e um pouco ao acaso (`p.Vary`).
