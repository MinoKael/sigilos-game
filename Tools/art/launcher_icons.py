"""Gera os ícones do app (Android e Windows) a partir de Assets/Icons/rune.svg.

A pedra rúnica vira ouro sobre o couro escuro da paleta do jogo (UI/Style/Palette.cs). Saída em
Assets/Launcher, renderizada pelo Inkscape, no tamanho que cada campo pede:

    icon_192.png              Android: launcher_icons/main_192x192 (Android 7: o ícone inteiro, um medalhão)
    icon_foreground_432.png   Android: launcher_icons/adaptive_foreground_432x432 (a runa, fundo transparente)
    icon_background_432.png   Android: launcher_icons/adaptive_background_432x432 (opaco)
    icon_monochrome_432.png   Android: launcher_icons/adaptive_monochrome_432x432 (ícone temático: só o alfa conta)
    splash_icon_432.png       Android: splash_screen/icon (o medalhão, legível sobre qualquer cor de fundo)
    icon_windows.ico          Windows: application/icon (o .exe) e, nas configurações do projeto,
                              application/config/windows_native_icon (a janela e a barra de tarefas).
                              O medalhão, de 16 a 256 px, cada tamanho renderizado à parte

Regras do ícone adaptativo (docs do Godot e do Android): camadas de 108 dp (432 px); a máscara do
aparelho mostra no máximo os 72 dp do meio (288 px) e o que importa fica no círculo de 66 dp
(264 px). Na splash do Android 12+, o ícone é cortado num círculo de 2/3 da imagem (288 px).

Uso (da raiz do repositório):
    py Tools/art/launcher_icons.py
    py Tools/art/launcher_icons.py --inkscape "C:/Program Files/Inkscape/bin/inkscape.exe"
"""

from __future__ import annotations

import argparse
import math
import re
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
SOURCE = ROOT / "Assets" / "Icons" / "rune.svg"
OUTPUT = ROOT / "Assets" / "Launcher"
DEFAULT_INKSCAPE = "C:/Program Files/Inkscape/bin/inkscape.exe"

# Cores de UI/Style/Palette.cs (Panel, Background, Gold). O ouro ganha um degradê e o centro do fundo
# um couro mais aceso, para o ícone não sumir entre os outros na tela do celular.
LEATHER_LIT = "#68482a"
PANEL = "#2e2219"
BACKGROUND = "#140f0b"
GOLD = "#d6ae60"
GOLD_LIGHT = "#f0ce82"
GOLD_DARK = "#b28038"

LAYER = 432
VISIBLE = 288  # os 72 dp que a máscara mostra
SAFE = 264  # os 66 dp que nenhuma máscara corta
RUNE = SAFE - 8  # círculo que contém a runa: dentro da zona segura, com folga
MEDALLION_RUNE = 0.78  # runa no medalhão, em fração do diâmetro do disco

# Tamanhos do .ico: barra de título, barra de tarefas e Explorer, em cada escala de tela (100% a 200%).
WINDOWS = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)


def path_points(d: str, steps: int = 16) -> list[tuple[float, float]]:
    """Pontos do contorno de um path SVG (sem arcos), com as curvas amostradas."""
    tokens = re.findall(r"[MmLlHhVvCcSsQqZz]|-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", d)
    points: list[tuple[float, float]] = []
    x = y = start_x = start_y = 0.0
    control: tuple[float, float] | None = None
    command = ""
    i = 0

    def number() -> float:
        nonlocal i
        i += 1
        return float(tokens[i - 1])

    def curve(p0, p1, p2, p3):
        for step in range(1, steps + 1):
            t = step / steps
            u = 1 - t
            points.append((
                u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1],
            ))

    while i < len(tokens):
        if tokens[i].isalpha():
            command = tokens[i]
            i += 1
            if command in "Zz":
                x, y = start_x, start_y
                control = None
                continue
        relative = command.islower()
        dx, dy = (x, y) if relative else (0.0, 0.0)
        kind = command.upper()
        if kind == "M":
            x, y = number() + dx, number() + dy
            start_x, start_y = x, y
            points.append((x, y))
            command = "l" if relative else "L"
            control = None
        elif kind == "L":
            x, y = number() + dx, number() + dy
            points.append((x, y))
            control = None
        elif kind == "H":
            x = number() + dx
            points.append((x, y))
            control = None
        elif kind == "V":
            y = number() + dy
            points.append((x, y))
            control = None
        elif kind in "CS":
            if kind == "C":
                c1 = (number() + dx, number() + dy)
            else:
                c1 = (2 * x - control[0], 2 * y - control[1]) if control else (x, y)
            c2 = (number() + dx, number() + dy)
            end = (number() + dx, number() + dy)
            curve((x, y), c1, c2, end)
            control = c2
            x, y = end
        elif kind == "Q":
            q = (number() + dx, number() + dy)
            end = (number() + dx, number() + dy)
            curve((x, y), (x + 2 / 3 * (q[0] - x), y + 2 / 3 * (q[1] - y)),
                  (end[0] + 2 / 3 * (q[0] - end[0]), end[1] + 2 / 3 * (q[1] - end[1])), end)
            control = None
            x, y = end
        else:
            raise ValueError(f"comando de path não suportado: {command}")
    return points


class Rune:
    """O desenho de rune.svg, centrado pela caixa que o contém (o que fica equilibrado ao olho) e
    medido pelo ponto mais longe desse centro (o que garante que nada sai do círculo pedido)."""

    def __init__(self, svg: Path):
        self.d = re.search(r'\bd="([^"]+)"', svg.read_text(encoding="utf-8")).group(1)
        points = path_points(self.d)
        xs, ys = [p[0] for p in points], [p[1] for p in points]
        self.x, self.y = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        self.radius = max(math.dist((self.x, self.y), p) for p in points)

    def path(self, center: float, diameter: float, fill: str) -> str:
        """A runa centrada em (center, center), inteira dentro de um círculo com esse diâmetro."""
        scale = diameter / (2 * self.radius)
        return (f'<path transform="translate({center - self.x * scale:.3f} {center - self.y * scale:.3f}) '
                f'scale({scale:.5f})" fill="{fill}" d="{self.d}"/>')


def document(size: int, defs: str, body: str) -> str:
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" '
            f'viewBox="0 0 {size} {size}"><defs>{defs}</defs>{body}</svg>\n')


GOLD_GRADIENT = (f'<linearGradient id="gold" x1="0.15" y1="0" x2="0.85" y2="1">'
                 f'<stop offset="0" stop-color="{GOLD_LIGHT}"/><stop offset="0.5" stop-color="{GOLD}"/>'
                 f'<stop offset="1" stop-color="{GOLD_DARK}"/></linearGradient>')

# No fundo adaptativo, a borda do que a máscara mostra (288 de 432 px) cai em 2/3 do raio.
LEATHER_GRADIENT = (f'<radialGradient id="leather"><stop offset="0" stop-color="{LEATHER_LIT}"/>'
                    f'<stop offset="{VISIBLE / LAYER:.3f}" stop-color="{PANEL}"/>'
                    f'<stop offset="1" stop-color="{BACKGROUND}"/></radialGradient>')

# O disco do medalhão é o que a máscara redonda mostraria do ícone adaptativo.
DISC_GRADIENT = (f'<radialGradient id="disc"><stop offset="0" stop-color="{LEATHER_LIT}"/>'
                 f'<stop offset="1" stop-color="{PANEL}"/></radialGradient>')


def medallion(rune: Rune, size: int, diameter: float, fraction: float = MEDALLION_RUNE, rim: bool = True) -> str:
    """Disco de couro com aro de ouro e a runa no meio, fundo transparente."""
    center = size / 2
    body = f'<circle cx="{center}" cy="{center}" r="{diameter / 2}" fill="url(#disc)"/>'
    if rim:
        width = max(1.0, diameter / 48)  # nos tamanhos pequenos do Windows, um pixel inteiro, senão o aro some
        body += (f'<circle cx="{center}" cy="{center}" r="{diameter / 2 - width / 2}" fill="none" '
                 f'stroke="{GOLD}" stroke-width="{width:.2f}"/>')
    body += rune.path(center, diameter * fraction, "url(#gold)")
    return document(size, DISC_GRADIENT + GOLD_GRADIENT, body)


def icons(rune: Rune) -> dict[str, tuple[int, str]]:
    center = LAYER / 2
    return {
        "icon_background_432": (LAYER, document(
            LAYER, LEATHER_GRADIENT, f'<rect width="{LAYER}" height="{LAYER}" fill="url(#leather)"/>')),
        "icon_foreground_432": (LAYER, document(LAYER, GOLD_GRADIENT, rune.path(center, RUNE, "url(#gold)"))),
        "icon_monochrome_432": (LAYER, document(LAYER, "", rune.path(center, RUNE, "#ffffff"))),
        # A splash corta num círculo de 288 px: o disco fica um pouco dentro, para o aro não ser cortado.
        "splash_icon_432": (LAYER, medallion(rune, LAYER, VISIBLE - 4)),
        # Android 7 mostra o PNG como está: círculo de 176 px, a forma redonda da grade de ícones antiga.
        "icon_192": (192, medallion(rune, 192, 176)),
    }


def windows_icon(rune: Rune, size: int) -> str:
    """O medalhão quase no quadro todo (o Windows não recorta o ícone). Nos tamanhos da barra de
    título e da barra de tarefas o detalhe fino some: a runa cresce e, abaixo de 32 px, o aro sai."""
    diameter = size - 2 * round(size / 32)
    if size < 32:
        return medallion(rune, size, diameter, fraction=0.92, rim=False)
    if size < 48:
        return medallion(rune, size, diameter, fraction=0.88)
    return medallion(rune, size, diameter)


def ico(images: list[tuple[int, bytes]]) -> bytes:
    """Arquivo .ico com cada tamanho em PNG, como o Godot gera os dele (o Windows aceita desde o Vista)."""
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = len(header) + 16 * len(images)
    entries = data = b""
    for size, png in images:
        entries += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(png), offset + len(data))
        data += png
    return header + entries + data


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--inkscape", default=DEFAULT_INKSCAPE)
    args = parser.parse_args()

    rune = Rune(SOURCE)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as temp:
        temp = Path(temp)
        jobs = [(name, size, svg, OUTPUT / f"{name}.png") for name, (size, svg) in icons(rune).items()]
        jobs += [(f"windows_{size}", size, windows_icon(rune, size), temp / f"windows_{size}.png")
                 for size in WINDOWS]
        actions = ["export-type:png", "export-area-page", "export-background-opacity:0"]
        for name, size, svg, target in jobs:
            source = temp / f"{name}.svg"
            source.write_text(svg, encoding="utf-8")
            target.unlink(missing_ok=True)
            actions += [f"file-open:{source.as_posix()}", f"export-width:{size}",
                        f"export-filename:{target.as_posix()}", "export-do", "file-close"]
        result = subprocess.run([args.inkscape, f"--actions={';'.join(actions)}"], capture_output=True, text=True)
        missing = [target.name for *_, target in jobs if not target.exists()]
        if result.returncode != 0 or missing:
            print(f"falhou: {missing} {result.stderr.strip()[:300]}")
            return 1
        (OUTPUT / "icon_windows.ico").write_bytes(
            ico([(size, (temp / f"windows_{size}.png").read_bytes()) for size in WINDOWS]))
    for name in [*icons(rune), "icon_windows"]:
        print(next(OUTPUT.glob(f"{name}.*")).relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main())
