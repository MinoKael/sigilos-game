# Medição de desempenho da interface

Dois scripts do Godot (rodam sem editor, com o save de teste `debugger`):

- `perf.gd`: abre uma tela, espera 2 s e mede o tempo de quadro (média, p50, p99, máximo), as chamadas de
  desenho, os nós, os nós com shader e os objetos do Godot.
- `touch.gd`: injeta um toque de verdade (`Input.parse_input_event`, que liga a emulação do mouse do Android)
  na primeira lista visível, começando sobre uma peça e sobre o vão, e registra quanto a lista rolou.

```bash
# quadro sem limite de taxa, para medir a CPU de verdade
godot --path . --disable-vsync --max-fps 0 --script Tools/perf/perf.gd -- --save=debugger --screen=runes --label=runes --seconds=3

# toque: --target=Rune (peças de nome Rune*) ou --target=@button (o primeiro botão da lista)
godot --path . --script Tools/perf/touch.gd -- --save=debugger --screen=dungeons --target=@button
```

`--tap=Auto` (no `perf.gd`) liga o botão de nome Auto antes de medir (a luta anda sozinha). Para ver o caminho
do celular (sem DragScroll de mouse) no PC, crie um `override.cfg` ao lado do `project.godot` com
`[input_devices]` e `pointing/emulate_touch_from_mouse=true` e apague depois.

Rode cada medida duas vezes e compare médias; o primeiro quadro depois de abrir a tela não conta (o script pula 120).

## gesture.gd

Gestos de toque de verdade sobre a lista (precisa do `override.cfg` acima para a emulação do Android):

```bash
godot --path . --disable-vsync --max-fps 0 --script Tools/perf/gesture.gd -- --save=debugger --screen=runes --mode=fling
```

`--mode=tap` (toque sobre um botão: dispara 1 vez), `drag` (arrasto que começa no botão: 0 disparos e a lista rola),
`axis` (fileira que rola de lado dentro de página que rola para baixo), `fling` (8 arremessos; p50/p99/máx e objetos),
`slider` (arrasto sobre o controle deslizante: ele mexe, a lista não).

Medido em 14/10/2026 no PC: o p99 do arremesso nas Runas e nos Monstros (≈ 15–20 ms) é o custo de criar as peças
que entram (≈ 0,75 ms por RuneTile só para construir e entrar na árvore; com painéis vazios no lugar o p99 cai para
2,5 ms). A máquina de rolagem em si é barata. Cortar esse custo é reduzir o que a peça monta ou reaproveitá-la
(rebind), não mexer na rolagem.
