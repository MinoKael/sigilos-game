---
name: sigilos-ui
description: Regras visuais e de interface do Sigilos (Godot 4.7 C#, UI montada em código). Use antes de criar ou alterar qualquer tela, componente, botão, diálogo, grade ou texto de interface, e depois, para verificar o resultado no jogo.
---

# Interface do Sigilos: consistência visual

Estas regras mantêm o jogo reconhecível: o jogador aprende onde cada coisa fica e nada se mexe sozinho.
Vêm do que as telas já fazem (`Layout`, `GameButton`, `TileButton`, `SigilButton`, `Dialog`, `CreatureCard`,
`RuneTile`, `Pills`, `Space`, `GameTheme`, `Palette`). Performance fica em `sigilos-performance`; aqui é aparência e uso.

## O jeito do jogo
- **Retângulos em colunas.** A tela é um corpo em duas colunas (lista à esquerda, detalhe à direita; ou 30/70 como a
  Arena), painéis retangulares de cantos suaves (`Ornament.Panel`, `GameTheme.InsetPanel`), fileiras e grades.
  Não inventar layout livre, sobreposto ou posicionado à mão. Posição fixa só em desenho (constelação, mapa).
- **Botões pequenos, agradáveis de ver e de tocar.** Altura de toque padrão (`GameTheme.Touch`; 44–56 nas
  telas, 40 nos redondos). Texto curto, ícone opcional à esquerda, custo (`WithCost`). Nada de botão gigante que
  ocupa a coluna inteira sem motivo; botões largos têm largura fixa (`Wide(n)`) e `ShrinkEnd`/`ShrinkCenter`.
- **Redondo = `SigilButton`** (o ✕ de fechar, o ? de ajuda). Retangular com texto = `GameButton`. Bloco de
  atalho = `TileButton`. Abas = `TextTabs`. Não criar um tipo novo de botão.
- **Tema pelas fontes do projeto**: cores só de `Palette`, variações de `GameTheme` (`Heading`, `Title`, `Faded`,
  `Number`), espaços só de `Space` (exceção escrita e justificada). Nada de cor, fonte ou número solto.
- **Reaproveitar**: antes de criar, procurar o componente que já faz (`Layout.Labeled/Medal/Grid/Row/Text/Scroll`,
  `CreatureCard`, `RuneCard`, `Dialog.Info/Confirm`, `MonsterSearch`, `GainPop`...). Generalizar um existente vale mais
  que um paralelo.
- **Nomear todo nó criado em código** (`.Named("Papel")`) pelo papel dele: o dono depura pelo overlay.
- **Ícones** vêm de `Art.Icon(nome)` (`Assets/Icons/*.svg`); arte nova só do `F:\GameDev\GameIconsSVG` ou Commons, com
  linha em `Tools/art/commons_assets.csv` e em `Assets/CREDITOS.md`. Texto sempre por `T("chave")`, em `pt-BR.json` e `en.json`.

## Texto nunca muda o tamanho de um componente
Em hipótese nenhuma. Um texto (nome longo, número maior, tradução, outra língua) não pode alargar, esticar ou empurrar
o que está em volta.
- Todo `Label` que pode crescer: `AutowrapMode = WordSmart`, `CustomMinimumSize` pequeno no X (ex.: `new Vector2(10, 0)`) e
  `SizeFlagsHorizontal = ExpandFill` dentro do espaço que sobra. Nunca deixar um texto definir a largura da linha.
- Botões, cápsulas e medalhas têm tamanho fixo (`CustomMinimumSize`, `Wide(n)`, `SizeFlags.ShrinkEnd/ShrinkCenter`); o texto
  cabe nele, quebra em linhas ou é resumido, e o botão não cresce.
- Número que muda (pontos, contagem, tempo): reservar a largura (`labelMinimumSize`) para o maior valor, ou formato fixo
  (`01:46/12:00`, `8/150`).
- Linhas de lista: nome (ExpandFill, quebra) + peças de largura fixa + botão fixo. Se a soma passa da coluna, o nome encolhe,
  nunca a coluna estoura. Conferir com o nome mais longo do jogo e com o texto em inglês.
- Medalhas e cartões redondos/quadrados: `SizeFlagsVertical = ShrinkCenter` para uma linha alta não esticar a peça em oval.
- Grupos de botões em `GridContainer` com `Columns`, nunca `HFlow`/HBox sem tamanho (o rótulo quebrado colapsa).
- Rolagem vertical com `ScrollMode.Reserve`.

## Tudo visível, sem rolar (exceto listas)
- A tela mostra todo o conteúdo na altura disponível (1280x720 é a medida de teste; o celular é mais estreito). Quando
  falta espaço: compactar (menos texto, peças menores, mais colunas), mover explicação para o help, ou quebrar em abas.
  Só **listas** (monstros, runas, oponentes, andares, coleção) rolam.
- Painel de ficha, formulário, resumo, cabeçalho e rodapé: sem barra de rolagem. Se precisaria rolar, redesenhar.
- Lista que rola leva `Layout.Scroll` (touch + roda) e, se longa, `TileGrid.Virtualize`.

## Explicação vai no botão de ajuda
- **Todo texto que explica** ação, botão, regra ou contexto ("toque num homúnculo para...", "a semana acaba...", regras de
  pontos, chances) sai da tela e vai para um **botão ? (`SigilButton.Of("help", ...)`) que abre um `Dialog`**. A tela fica
  só com rótulos, números e botões.
- O ? fica ao lado do título ou do número a que se refere (Pontos da Arena, Pergaminhos da Transmutação). Um por contexto.
- O que fica na tela: nome, valor, estado e uma linha curta de resultado/erro (aviso de "equipe cheia", falta de Convite).
  Explicar a mecânica nunca é "aviso".
- Chaves de texto de ajuda com prefixo próprio (`arena.help_*`, `summon.rates`), em pt-BR e en.

## Diálogos
- Abrir por `Dialog.Open/Info/Confirm`, sempre com título. **Fecham pelo ✕ do canto superior direito** (o `SigilButton`
  "cancel" do cabeçalho), por Esc/Voltar do celular e pelos botões de ação do rodapé. **Tocar fora não fecha**
  (`OutsideTapCloses` é falso por padrão); não ligar essa opção. `Dismissable = false` esconde o ✕ e é só para escolha
  obrigatória (o jogador tem que usar um botão de ação): usar com parcimônia, nunca num diálogo que é só informação.
- Largura fixa (`Dialog.Open(from, título, largura)`), conteúdo em `Layout.Text(texto, variação, largura)` para quebrar.
  Sem rolagem, a não ser que o conteúdo seja uma lista.
- **Bounce na abertura e no fechamento**: já embutido no `Dialog` (escala 0,9 a 1 com `Motion.Spring`, brilho do escuro; a tela
  cheia só aparece e some). `Closed` dispara na hora, antes da animação de saída acabar. Não criar janela fora do `Dialog`.
- Diálogo aberto de outro diálogo fica por cima (z), com o ✕ dele; fechar volta ao de baixo.

## Animação de toque
- Todo botão afunda e volta com repique (`Juice.Attach`; `TouchButton`, `GameButton`, `SigilButton`, `TileButton` já ligam).
  Peças que não são botão (cartões, medalhas, retratos) levam o mesmo repique pelo `Press` (`Press.On`/`Feed`).
  Botão novo passa por um desses tipos ou chama `Juice.Attach`; não criar botão cru sem repique.
- Sob o mouse não cresce: foco é luz (aura, moldura acesa). Som de clique só como reserva (`Sfx.Fallback`).
- Números que sobem (ganho de runa, +n de habilidade): `GainPop.Show`. Reutilizar, não refazer.

## Botões em grade: ordem e lugar fixos
- **Cada botão tem o seu lugar e ele nunca muda.** Um botão que não se aplica agora (já no máximo, sem recurso, ainda
  bloqueado) fica **no mesmo lugar, apagado (`Disabled`/`Modulate` 0,5)**, não some e não deixa os outros subirem. Nada de
  `Visible = false` nem de não criar o botão para "abrir espaço" ou reorganizar.
- Exemplo a evitar: botões da tela de Runas que aparecem e desaparecem ao evoluir. Eles ficam sempre, na mesma célula;
  só mudam de estado.
- Grade com `Columns` fixo e a ordem declarada no código (lista de `(chave, ação)`), não derivada do que está disponível.
  Células sem botão ficam vazias de propósito (um `Control` vazio), não preenchidas pelo próximo.
- **Botão apagado sempre explica o porquê**: `GameButton.Block(motivo, título)` apaga o botão (visual de desligado) e o
  toque abre uma janela com o motivo; `Block(null)` religa. Não usar `Disabled = ...` cru em botão de ação da tela.
  Parte do jogo fechada (`TileButton.Locked()`) abre o `LockedDialog` ou um `Dialog.Info`.
- Botões lado a lado com o mesmo papel: `FixedHeight()` (o texto não os faz crescer) e todos da mesma altura.
- Lojas: `ShopShelf` (categorias à esquerda, grade de 4 colunas à direita) com `ShopCard`; categoria sem oferta não aparece.
- Telas parecidas usam as mesmas posições para as mesmas ações (Voltar no canto, ? ao lado do título, Lutar no fim da linha).

## Verificar sempre no jogo
Toda mudança visual é conferida **abrindo o jogo na tela alterada**, nunca só pelo build.
1. `dotnet build` (Debug) e, se mexer em export, ExportRelease.
2. Rodar com o save de teste: `-- --save=debugger` (nunca o save real). Para estados específicos (habilidade no nível 1,
   conta nova) copiar `debugger.json` para um nome de rascunho em `%APPDATA%\Godot\app_userdata\Sigilos\` e apagar depois.
3. Capturar a tela com um script de harness (`SceneTree` com `--script`, cliques por `Input.parse_input_event`, ler o
   botão pelo nome do nó, `get_texture().get_image().save_png`) e **olhar a imagem**. Padrão em
   `Godot_v4.7.1-stable_mono_win64_console.exe --path . --script <gd> -- --save=debugger --out=<pasta>/`.
4. Conferir: nada estoura a coluna; nenhum texto mexe no tamanho (testar nome longo e inglês); medalhas redondas; sem barra
   de rolagem fora de lista; ordem dos botões igual antes e depois de mudar o estado (clicar até o estado mudar e capturar
   de novo); diálogos fecham só pelo ✕; ? abre o help.
5. Largura estreita: mesma conferência em 1280x720 e numa janela menor (a do dono costuma ser mais estreita).
6. `py Tools/texts/check_texts.py` (não passar de 205 problemas, todos "FALTA NOME") e os testes (`dotnet run --project Tests`: todos
   passam).
7. Nunca commitar; o dono faz isso.

## Antes de entregar, perguntar
- Algum texto pode alargar algo? Alguma explicação ficou na tela em vez de no ?? Algum botão some ou muda de lugar?
  Algo rola que não é lista? O diálogo fecha por fora? Vi a tela rodando?
