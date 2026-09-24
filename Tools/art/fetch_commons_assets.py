"""Baixa a arte do Wikimedia Commons listada em commons_assets.csv para Assets/.

Adaptado de Rabiscos&Runas/Tools/fetch_commons_assets.py. Só usa a biblioteca padrão do Python 3.

Três passos, todos repetíveis:
1. Baixa o que falta (pula arquivos que já estão válidos). O Wikimedia limita rajadas (HTTP 429):
   o script espera entre downloads e respeita o cabeçalho Retry-After.
2. Ajusta o tamanho de renderização dos SVG: o Godot rasteriza o SVG no width/height do arquivo, e
   os símbolos do Commons têm 16 px (alquímicos) ou 60 px (runas). O script escreve width/height
   para o lado maior ter RENDER_SIZE px, sem tocar no viewBox nem no desenho.
3. Reescreve Assets/CREDITOS.md a partir do CSV.

Uso (da raiz do repositório):
    py Tools/art/fetch_commons_assets.py            # baixa o que falta, ajusta e gera créditos
    py Tools/art/fetch_commons_assets.py --check    # só lista o que falta
    py Tools/art/fetch_commons_assets.py --chrome C:/Chromium/Application/chrome.exe
        (baixa pelo Chromium sem janela: quando o Wikimedia responde 429 a todo pedido do Python)

O Python do Inkscape não tem certificados SSL: use o Python do python.org (`py`).
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import re
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
ROOT = TOOLS.parent.parent
MANIFEST = TOOLS / "commons_assets.csv"
ASSETS = ROOT / "Assets"
CREDITS = ASSETS / "CREDITOS.md"
FILE_PATH_URL = "https://commons.wikimedia.org/wiki/Special:FilePath/"
PAGE_URL = "https://commons.wikimedia.org/wiki/File:"
USER_AGENT = "SigilosMVP/0.1 (hobby game prototype; asset fetch)"
RENDER_SIZE = 256

LICENSE_URLS = {
    "CC BY 3.0": "https://creativecommons.org/licenses/by/3.0/",
    "Public domain": "https://en.wikipedia.org/wiki/Public_domain",
}


def is_valid(file: Path) -> bool:
    """Um download bloqueado salva uma página HTML de erro no lugar da imagem."""
    if not file.exists() or file.stat().st_size == 0:
        return False
    return b"<svg" in file.read_bytes()[:2000]


def download(commons_file: str, target: Path, attempts: int, delay: float) -> bool:
    url = FILE_PATH_URL + urllib.parse.quote(commons_file.replace(" ", "_"))
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    temporary = target.with_suffix(target.suffix + ".part")
    for attempt in range(1, attempts + 1):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                temporary.write_bytes(response.read())
            if is_valid(temporary):
                temporary.replace(target)
                return True
            print(f"    resposta inválida (tentativa {attempt})")
        except urllib.error.HTTPError as error:
            header = error.headers.get("Retry-After") if error.headers else None
            wait = float(header) if header and header.isdigit() else delay * 6 * attempt
            print(f"    HTTP {error.code} (tentativa {attempt}); esperando {wait:.0f} s")
            time.sleep(wait)
        except urllib.error.URLError as error:
            print(f"    erro de rede: {error.reason}")
            time.sleep(delay * attempt)
        finally:
            temporary.unlink(missing_ok=True)
    return False


def upload_url(commons_file: str) -> str:
    """O endereço direto do arquivo: o Commons guarda cada um sob os dígitos do MD5 do nome."""
    name = commons_file.replace(" ", "_")
    digest = hashlib.md5(name.encode("utf-8")).hexdigest()
    return f"https://upload.wikimedia.org/wikipedia/commons/{digest[0]}/{digest[:2]}/{urllib.parse.quote(name)}"


def download_with_chrome(chrome: str, profile: str, commons_file: str, target: Path) -> bool:
    """O Chromium sem janela abre o SVG e imprime o documento (--dump-dom), que é o próprio XML."""
    command = [chrome, "--headless=new", "--disable-gpu", "--no-first-run", f"--user-data-dir={profile}",
               "--dump-dom", upload_url(commons_file)]
    try:
        result = subprocess.run(command, capture_output=True, timeout=90)
    except subprocess.TimeoutExpired:
        print("    o Chromium não respondeu")
        return False
    target.write_bytes(result.stdout)
    if is_valid(target):
        return True
    target.unlink(missing_ok=True)
    print("    resposta inválida")
    return False


def set_render_size(file: Path) -> None:
    """Escreve width/height no <svg> raiz para o lado maior ter RENDER_SIZE px."""
    text = file.read_text(encoding="utf-8")
    match = re.search(r"<svg\b[^>]*>", text)
    if not match:
        return
    tag = match.group(0)

    view_box = re.search(r'viewBox="([^"]+)"', tag)
    if view_box:
        _, _, width, height = (float(v) for v in re.split(r"[ ,]+", view_box.group(1).strip()))
    else:
        width = float(re.search(r'\bwidth="([\d.]+)', tag).group(1))
        height = float(re.search(r'\bheight="([\d.]+)', tag).group(1))
        tag_with_box = tag.replace("<svg", f'<svg viewBox="0 0 {width:g} {height:g}"', 1)
        text = text.replace(tag, tag_with_box, 1)
        tag = tag_with_box

    scale = RENDER_SIZE / max(width, height)
    new_tag = re.sub(r'\s(width|height)="[^"]*"', "", tag)
    new_tag = new_tag.replace("<svg", f'<svg width="{width * scale:g}" height="{height * scale:g}"', 1)
    file.write_text(text.replace(tag, new_tag, 1), encoding="utf-8")


def write_credits(rows: list[dict[str, str]]) -> None:
    lines = [
        "# Créditos de arte",
        "",
        "Toda a arte vem do [Wikimedia Commons](https://commons.wikimedia.org/). Os ícones de criatura e de",
        "interface são do projeto [game-icons.net](https://game-icons.net), republicados no Commons; as runas e",
        "os símbolos de elemento são de domínio público. Lista-fonte: `Tools/art/commons_assets.csv`.",
        "",
        "Parte dos arquivos veio de Rabiscos&Runas (`Game/Assets`), que usa as mesmas fontes.",
        "",
        "Alteração feita: só o `width`/`height` do `<svg>` raiz, para o Godot rasterizar em "
        f"{RENDER_SIZE} px (`Tools/art/fetch_commons_assets.py`). O desenho não muda. No jogo, as silhuetas",
        "pretas são recoloridas pelo shader `Shaders/doodle.gdshader`.",
        "",
        "| Arquivo no projeto | Arquivo no Commons | Autor | Licença |",
        "| --- | --- | --- | --- |",
    ]
    for row in rows:
        page = PAGE_URL + urllib.parse.quote(row["commons_file"].replace(" ", "_"))
        license_url = LICENSE_URLS.get(row["license"], "")
        license_cell = f"[{row['license']}]({license_url})" if license_url else row["license"]
        lines.append(f"| `{row['path']}` | [{row['commons_file']}]({page}) | {row['author']} | {license_cell} |")
    CREDITS.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="só lista os arquivos faltando")
    parser.add_argument("--delay", type=float, default=5.0, help="segundos entre downloads (padrão: 5)")
    parser.add_argument("--attempts", type=int, default=4, help="tentativas por arquivo (padrão: 4)")
    parser.add_argument("--chrome", default="", help="caminho do chrome.exe/Chromium para baixar sem o urllib")
    args = parser.parse_args()
    profile = tempfile.mkdtemp(prefix="sigilos-chrome-") if args.chrome else ""

    with MANIFEST.open(encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))

    missing = [row for row in rows if not is_valid(ASSETS / row["path"])]
    print(f"{len(rows) - len(missing)}/{len(rows)} arquivos já estão em {ASSETS}.")
    if args.check:
        for row in missing:
            print(f"  falta: {row['path']}  ({row['commons_file']})")
        return 1 if missing else 0

    failed = []
    for index, row in enumerate(missing, start=1):
        target = ASSETS / row["path"]
        target.parent.mkdir(parents=True, exist_ok=True)
        print(f"[{index}/{len(missing)}] {row['path']}")
        ok = (download_with_chrome(args.chrome, profile, row["commons_file"], target) if args.chrome
              else download(row["commons_file"], target, args.attempts, args.delay))
        if ok:
            print("    ok")
        else:
            failed.append(row["path"])
            print("    FALHOU")
        time.sleep(args.delay)

    for row in rows:
        file = ASSETS / row["path"]
        if is_valid(file):
            set_render_size(file)

    write_credits(rows)
    print(f"Créditos em {CREDITS.relative_to(ROOT)}.")

    if failed:
        print(f"\n{len(failed)} arquivo(s) ainda faltando. Rode de novo mais tarde (ou com --delay maior).")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
