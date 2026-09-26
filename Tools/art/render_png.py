"""Renderiza os SVG de Assets/ em PNG de vários tamanhos, com o Inkscape.

O Godot rasteriza o SVG uma vez, no tamanho do arquivo, e depois só escala a imagem: o que aparece
pequeno na tela fica serrilhado. Com PNG já renderizado em vários tamanhos (o lado maior com 32, 64,
128, 256 e 512 px), o jogo escolhe o mais próximo do tamanho na tela (UI/Style/Art.cs) e o Godot
só reduz um pouco, com mipmaps.

Saída: Assets/Rendered/<Pasta>/<nome>_<tamanho>.png. Só refaz o que é mais velho que o SVG.

Uso (da raiz do repositório):
    py Tools/art/render_png.py                 # renderiza o que falta ou mudou
    py Tools/art/render_png.py --force         # renderiza tudo de novo
    py Tools/art/render_png.py --inkscape "C:/Program Files/Inkscape/bin/inkscape.exe"
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
ASSETS = ROOT / "Assets"
OUTPUT = ASSETS / "Rendered"
FOLDERS = ("Icons", "Glyphs", "Creatures", "Elements", "Effects")
SIZES = (32, 64, 128, 256, 512)
DEFAULT_INKSCAPE = "C:/Program Files/Inkscape/bin/inkscape.exe"


def number(value: str | None) -> float:
    match = re.match(r"\s*([0-9.]+)", value or "")
    return float(match.group(1)) if match else 0.0


def proportions(svg: Path) -> tuple[float, float]:
    """Largura e altura do desenho: do width/height do arquivo, senão do viewBox."""
    head = svg.read_text(encoding="utf-8", errors="replace")[:4000]
    tag = re.search(r"<svg\b[^>]*>", head, re.S)
    text = tag.group(0) if tag else head
    width = number((re.search(r'\bwidth="([^"]+)"', text) or [None, None])[1])
    height = number((re.search(r'\bheight="([^"]+)"', text) or [None, None])[1])
    if width > 0 and height > 0:
        return width, height
    box = re.search(r'viewBox="([^"]+)"', text)
    if box:
        parts = [float(p) for p in re.split(r"[\s,]+", box.group(1).strip())]
        return parts[2], parts[3]
    return 1.0, 1.0


def render(inkscape: str, svg: Path, target: Path) -> bool:
    width, height = proportions(svg)
    side = "export-width" if width >= height else "export-height"
    actions = ["export-type:png", "export-area-page"]
    for size in SIZES:
        out = target / f"{svg.stem}_{size}.png"
        actions += [f"{side}:{size}", f"export-filename:{out.as_posix()}", "export-do"]
    result = subprocess.run([inkscape, str(svg), f"--actions={';'.join(actions)}"], capture_output=True, text=True)
    missing = [size for size in SIZES if not (target / f"{svg.stem}_{size}.png").exists()]
    if result.returncode != 0 or missing:
        print(f"    falhou: {svg.name} {result.stderr.strip()[:200]}")
        return False
    return True


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--inkscape", default=DEFAULT_INKSCAPE)
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()

    done = failed = skipped = 0
    for folder in FOLDERS:
        target = OUTPUT / folder
        target.mkdir(parents=True, exist_ok=True)
        for svg in sorted((ASSETS / folder).glob("*.svg")):
            outputs = [target / f"{svg.stem}_{size}.png" for size in SIZES]
            fresh = all(o.exists() and o.stat().st_mtime >= svg.stat().st_mtime for o in outputs)
            if fresh and not args.force:
                skipped += 1
                continue
            print(f"{folder}/{svg.name}")
            if render(args.inkscape, svg, target):
                done += 1
            else:
                failed += 1

    print(f"{done} renderizados, {skipped} já em dia, {failed} com falha.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
