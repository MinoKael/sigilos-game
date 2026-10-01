"""Confere Data/texts/pt-BR.json (a base) contra o código e as traduções contra a base.

Procura as chaves literais usadas em T("...") dentro de UI/ e GameEntry/ e diz quais faltam no
arquivo de textos e quais sobram nele. Chaves montadas a partir de enum (T($"stat.{stat}"))
não aparecem aqui: essas o jogo confere ao abrir e avisa no console (Texts.MissingEnumKeys).
Depois confere cada tradução (en.json...): mesmas chaves e mesmos marcadores {0}, {1}... da base,
e o grupo "names": a tradução de cada nome dos dados (invocações, habilidades, fases, Masmorras,
chefes, Loja), pelo nome em português que está em Data/.

Uso:
    py Tools/texts/check_texts.py            # pt-BR e todas as traduções
    py Tools/texts/check_texts.py en         # a base e só esta tradução
    py Tools/texts/check_texts.py --listar   # só lista as chaves do código
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CODE = [ROOT / "UI", ROOT / "GameEntry"]
DATA = ROOT / "Data"
TEXTS = DATA / "texts"
BASE = "pt-BR"
NAMES = "names"
LITERAL = re.compile(r'\bT\(\s*"([a-z0-9_.]+)"')
# Chaves passadas por variável ou em "? :" dentro de T(...): qualquer texto "grupo.chave" no código.
KEYLIKE = re.compile(r'"([a-z][a-z0-9_]*(?:\.[A-Za-z0-9_]+)+)"')
# Chaves montadas com prefixo fixo e o resto vindo de enum ou de outra chave.
DYNAMIC = re.compile(r'\bT\(\s*\$"([a-z0-9_.]+)\{')
HOLE = re.compile(r"\{\d+\}")


def code_keys():
    literal, prefixes = set(), set()
    for folder in CODE:
        for path in folder.rglob("*.cs"):
            text = path.read_text(encoding="utf-8")
            literal.update(LITERAL.findall(text))
            literal.update(k for k in KEYLIKE.findall(text) if not k.endswith((".svg", ".json", ".cs")))
            prefixes.update(DYNAMIC.findall(text))
    return literal, prefixes


def flatten(node, prefix=""):
    for key, value in node.items():
        full = f"{prefix}.{key}" if prefix else key
        if isinstance(value, dict):
            yield from flatten(value, full)
        else:
            yield full, value


def parse(path):
    lines = [line for line in path.read_text(encoding="utf-8").splitlines() if not line.lstrip().startswith("//")]
    text = re.sub(r",(\s*[}\]])", r"\1", "\n".join(lines))
    return json.loads(text)


def load(language):
    """Os textos por chave (sem o grupo "names") e os nomes traduzidos, à parte."""
    tree = parse(TEXTS / f"{language}.json")
    names = tree.pop(NAMES, {})
    return dict(flatten(tree)), names


def data_names():
    """Todo "name" e "base_name" dos arquivos de Data/ (fora os textos): o que o grupo "names" traduz."""
    found = set()

    def walk(node):
        if isinstance(node, dict):
            for key, value in node.items():
                if key in ("name", "base_name") and isinstance(value, str):
                    found.add(value)
                else:
                    walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    for path in sorted(DATA.rglob("*.json")):
        if TEXTS in path.parents or path.name == "stat_model.json":
            continue
        walk(parse(path))
    return found


def check_translation(language, base, names):
    other, translated = load(language)
    problems = 0
    for key in sorted(base.keys() - other.keys()):
        print(f"{language}: FALTA   {key}")
        problems += 1
    for key in sorted(other.keys() - base.keys()):
        print(f"{language}: SOBRA   {key}")
        problems += 1
    for key in sorted(base.keys() & other.keys()):
        if set(HOLE.findall(base[key])) != set(HOLE.findall(other[key])):
            print(f"{language}: MARCADORES  {key}")
            problems += 1
    for name in sorted(names - translated.keys()):
        print(f"{language}: FALTA NOME   {name}")
        problems += 1
    for name in sorted(translated.keys() - names):
        print(f"{language}: SOBRA NOME   {name}")
        problems += 1
    print(f"{language}: {len(other)} chaves e {len(translated)} nomes, {problems} problemas.")
    return problems


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    literal, prefixes = code_keys()
    if "--listar" in sys.argv:
        print("\n".join(sorted(literal)))
        return 0

    base, base_names = load(BASE)
    missing = sorted(literal - base.keys())
    unused = sorted(k for k in base.keys() - literal if not any(k.startswith(p) for p in prefixes))
    for key in missing:
        print(f"FALTA   {key}")
    for key in unused:
        print(f"SOBRA   {key}")
    if base_names:
        print(f'{BASE}.json não precisa do grupo "{NAMES}": os nomes da base são os de Data/.')
    print(f"{len(literal)} chaves no código, {len(base)} em {BASE}.json, {len(missing)} faltando, {len(unused)} sem uso.")

    names = data_names()
    translations = args or sorted(p.stem for p in TEXTS.glob("*.json") if p.stem != BASE)
    problems = sum(check_translation(language, base, names) for language in translations)
    return 1 if missing or problems or base_names else 0


if __name__ == "__main__":
    sys.exit(main())
