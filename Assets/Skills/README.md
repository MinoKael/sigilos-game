# Imagens de habilidades

Uma imagem por habilidade, em PNG colorido (quadrado, de preferência 256x256), com o nome que o dado pede:
o campo `"image"` da habilidade em `Data/summons/*.json` (`"image": "flecha_explosiva"` lê
`Assets/Skills/flecha_explosiva.png`).

- Sem o arquivo (ou com `"image"` vazio), a tela mostra o símbolo de sempre (`Art.Skill`).
- O jogo recorta a imagem na forma do ícone (pedra quadrada na ativa, losango na passiva); deixe uma
  folga de uns 8% nas bordas.
- Depois de adicionar PNGs, abra o projeto no Godot (ou rode `--import`) para importá-los.
- O builder (`docs/summon_family_builder.html`) edita `image` e `flavor` em cada habilidade.
