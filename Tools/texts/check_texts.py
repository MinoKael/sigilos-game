"""Confere Data/texts/<idioma>.json contra o código.

Procura as chaves literais usadas em T("...") dentro de UI/ e GameEntry/ e diz quais faltam no
arquivo de textos e quais sobram nele. Chaves montadas a partir de enum (T($"atributo.{stat}"))
não aparecem aqui: essas o jogo confere ao abrir e avisa no console (Texts.MissingEnumKeys).

Uso:
    py Tools/texts/check_texts.py            # pt-BR
    py Tools/texts/check_texts.py en         # outro idioma
    py Tools/texts/check_texts.py --listar   # só lista as chaves do código
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CODE = [ROOT / "UI", ROOT / "GameEntry"]
LITERAL = re.compile(r'\bT\(\s*"([a-z0-9_.]+)"')
# Chaves passadas por variável ou em "? :" dentro de T(...): qualquer texto "grupo.chave" no código.
KEYLIKE = re.compile(r'"([a-z][a-z0-9_]*(?:\.[A-Za-z0-9_]+)+)"')
# Chaves montadas com prefixo fixo e o resto vindo de enum ou de outra chave.
DYNAMIC = re.compile(r'\bT\(\s*\$"([a-z0-9_.]+)\{')


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
            yield full


def load(language):
    path = ROOT / "Data" / "texts" / f"{language}.json"
    lines = [line for line in path.read_text(encoding="utf-8").splitlines() if not line.lstrip().startswith("//")]
    return set(flatten(json.loads("\n".join(lines))))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    literal, prefixes = code_keys()
    if "--listar" in sys.argv:
        print("\n".join(sorted(literal)))
        return 0

    language = args[0] if args else "pt-BR"
    known = load(language)
    # "chave_dica" acompanha "chave" quando o código monta a dica a partir do nome do botão.
    used = literal | {f"{k}_dica" for k in literal}
    missing = sorted(literal - known)
    unused = sorted(k for k in known - used if not any(k.startswith(p) for p in prefixes))
    for key in missing:
        print(f"FALTA   {key}")
    for key in unused:
        print(f"SOBRA   {key}")
    print(f"{len(literal)} chaves no código, {len(known)} no arquivo, {len(missing)} faltando, {len(unused)} sem uso.")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
