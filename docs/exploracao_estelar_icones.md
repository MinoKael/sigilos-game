# Exploração Estelar: ícones e desenhos novos

Ids que o código e os dados já usam e que ainda não têm arte. Sem o arquivo, o jogo roda: o orbe da constelação mostra a estrela que pisca, e a ficha da constelação fica sem o desenho. Os ícones da interface (a porta `star_exploration`, `influence` e as faixas `hemisphere_*`) já existem. Os ícones vão em `Assets/Icons/<id>.svg` e os desenhos de criatura em `Assets/Creatures/<id>.svg`; depois, `Tools/art/render_png.py` gera os PNG de `Assets/Rendered`.

## Desenhos de criatura (`Assets/Creatures`)

Os chefes novos de `Data/enemies.json` (guardiões das constelações que nenhuma família representa).

| Id | Chefe | Constelações |
| --- | --- | --- |
| `bear` | Urso Celeste | Ursa Maior |
| `ram` | Carneiro de Ouro | Áries |
| `bull` | Touro Celeste | Touro |
| `hunter` | Caçador das Estrelas | Órion |
| `unicorn` | Unicórnio Celeste | Unicórnio |
| `crab` | Caranguejo Celeste | Câncer |
| `lion` | Leão de Nemeia | Leão Menor, Leão |
| `hydra` | Hidra de Lerna | Hidra, Hidra Macho |
| `scales` | Balança de Astreia | Libra |
| `scorpion` | Escorpião Celeste | Escorpião |
| `sea_goat` | Cabra-Marinha | Capricórnio |
| `centaur` | Centauro Celeste | Sagitário, Centauro |

## Ícones das constelações (`Assets/Icons`)

Um por constelação, no orbe do mapa e no cabeçalho da ficha (o desenho da constelação).


### Boreais

| Andar | Id | Constelação |
| --- | --- | --- |
| 1 | `constellation_ursa_minor` | Ursa Menor (Ursa Minor) |
| 2 | `constellation_camelopardalis` | Girafa (Camelopardalis) |
| 3 | `constellation_auriga` | Cocheiro (Auriga) |
| 4 | `constellation_lynx` | Lince (Lynx) |
| 5 | `constellation_ursa_major` | Ursa Maior (Ursa Major) |
| 6 | `constellation_leo_minor` | Leão Menor (Leo Minor) |
| 7 | `constellation_canes_venatici` | Cães de Caça (Canes Venatici) |
| 8 | `constellation_coma_berenices` | Cabeleira de Berenice (Coma Berenices) |
| 9 | `constellation_bootes` | Boieiro (Boötes) |
| 10 | `constellation_corona_borealis` | Coroa Boreal (Corona Borealis) |
| 11 | `constellation_hercules` | Hércules (Hercules) |
| 12 | `constellation_lyra` | Lira (Lyra) |
| 13 | `constellation_vulpecula` | Raposa (Vulpecula) |
| 14 | `constellation_cygnus` | Cisne (Cygnus) |
| 15 | `constellation_draco` | Dragão (Draco) |
| 16 | `constellation_cepheus` | Cefeu (Cepheus) |
| 17 | `constellation_lacerta` | Lagarto (Lacerta) |
| 18 | `constellation_cassiopeia` | Cassiopeia (Cassiopeia) |
| 19 | `constellation_triangulum` | Triângulo (Triangulum) |
| 20 | `constellation_perseus` | Perseu (Perseus) |
| 21 | `constellation_andromeda` | Andrômeda (Andromeda) |

### Equatoriais

| Andar | Id | Constelação |
| --- | --- | --- |
| 22 | `constellation_pisces` | Peixes (Pisces) |
| 23 | `constellation_cetus` | Baleia (Cetus) |
| 24 | `constellation_aries` | Áries (Aries) |
| 25 | `constellation_taurus` | Touro (Taurus) |
| 26 | `constellation_orion` | Órion (Orion) |
| 27 | `constellation_lepus` | Lebre (Lepus) |
| 28 | `constellation_canis_major` | Cão Maior (Canis Major) |
| 29 | `constellation_monoceros` | Unicórnio (Monoceros) |
| 30 | `constellation_gemini` | Gêmeos (Gemini) |
| 31 | `constellation_canis_minor` | Cão Menor (Canis Minor) |
| 32 | `constellation_cancer` | Câncer (Cancer) |
| 33 | `constellation_leo` | Leão (Leo) |
| 34 | `constellation_sextans` | Sextante (Sextans) |
| 35 | `constellation_hydra` | Hidra (Hydra) |
| 36 | `constellation_crater` | Taça (Crater) |
| 37 | `constellation_corvus` | Corvo (Corvus) |
| 38 | `constellation_virgo` | Virgem (Virgo) |
| 39 | `constellation_libra` | Libra (Libra) |
| 40 | `constellation_serpens` | Serpente (Serpens) |
| 41 | `constellation_ophiuchus` | Serpentário (Ophiuchus) |
| 42 | `constellation_scorpius` | Escorpião (Scorpius) |
| 43 | `constellation_sagittarius` | Sagitário (Sagittarius) |
| 44 | `constellation_scutum` | Escudo (Scutum) |
| 45 | `constellation_aquila` | Águia (Aquila) |
| 46 | `constellation_sagitta` | Flecha (Sagitta) |
| 47 | `constellation_delphinus` | Delfim (Delphinus) |
| 48 | `constellation_equuleus` | Cavalo Menor (Equuleus) |
| 49 | `constellation_capricornus` | Capricórnio (Capricornus) |
| 50 | `constellation_aquarius` | Aquário (Aquarius) |
| 51 | `constellation_pegasus` | Pégaso (Pegasus) |

### Austrais

| Andar | Id | Constelação |
| --- | --- | --- |
| 52 | `constellation_piscis_austrinus` | Peixe Austral (Piscis Austrinus) |
| 53 | `constellation_sculptor` | Escultor (Sculptor) |
| 54 | `constellation_fornax` | Fornalha (Fornax) |
| 55 | `constellation_eridanus` | Erídano (Eridanus) |
| 56 | `constellation_caelum` | Buril (Caelum) |
| 57 | `constellation_columba` | Pomba (Columba) |
| 58 | `constellation_puppis` | Popa (Puppis) |
| 59 | `constellation_pyxis` | Bússola (Pyxis) |
| 60 | `constellation_antlia` | Máquina Pneumática (Antlia) |
| 61 | `constellation_centaurus` | Centauro (Centaurus) |
| 62 | `constellation_lupus` | Lobo (Lupus) |
| 63 | `constellation_norma` | Esquadro (Norma) |
| 64 | `constellation_ara` | Altar (Ara) |
| 65 | `constellation_corona_australis` | Coroa Austral (Corona Australis) |
| 66 | `constellation_telescopium` | Telescópio (Telescopium) |
| 67 | `constellation_microscopium` | Microscópio (Microscopium) |
| 68 | `constellation_grus` | Grou (Grus) |
| 69 | `constellation_phoenix` | Fênix (Phoenix) |
| 70 | `constellation_horologium` | Relógio (Horologium) |
| 71 | `constellation_reticulum` | Retículo (Reticulum) |
| 72 | `constellation_dorado` | Dourado (Dorado) |
| 73 | `constellation_pictor` | Pintor (Pictor) |
| 74 | `constellation_carina` | Quilha (Carina) |
| 75 | `constellation_vela` | Vela (Vela) |
| 76 | `constellation_volans` | Peixe Voador (Volans) |
| 77 | `constellation_mensa` | Mesa (Mensa) |
| 78 | `constellation_hydrus` | Hidra Macho (Hydrus) |
| 79 | `constellation_tucana` | Tucano (Tucana) |
| 80 | `constellation_indus` | Índio (Indus) |
| 81 | `constellation_pavo` | Pavão (Pavo) |
| 82 | `constellation_octans` | Oitante (Octans) |
| 83 | `constellation_apus` | Ave-do-Paraíso (Apus) |
| 84 | `constellation_triangulum_australe` | Triângulo Austral (Triangulum Australe) |
| 85 | `constellation_circinus` | Compasso (Circinus) |
| 86 | `constellation_chamaeleon` | Camaleão (Chamaeleon) |
| 87 | `constellation_musca` | Mosca (Musca) |
| 88 | `constellation_crux` | Cruzeiro do Sul (Crux) |
