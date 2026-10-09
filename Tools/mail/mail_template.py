"""Gera docs/mail_template.json: o template do correio (POST /admin/mail no sigilos-server).

Monta, a partir do jogo, o catálogo do que uma carta pode trazer: as moedas (MailItem), as raridades
e os conjuntos de runa (RuneRarity, RuneSet) com os nomes de Data/texts/pt-BR.json, os ids de todos
os monstros de Data/summons e os dos retratos especiais (um por desenho de Assets/Avatars). Antes de
gravar, confere o "template" e cada exemplo contra as regras do servidor (MailRewards em D:/dev/sigilos-server/Server/Program.cs) e do jogo (MailGift.Parse): um
exemplo inválido para tudo. Os limites abaixo copiam os do servidor; se ele mudar, mude aqui também.

Uso:
    py Tools/mail/mail_template.py
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "mail_template.json"
CONTENT = ROOT / "Core" / "Content"
PROGRESSION = ROOT / "Core" / "Progression"
TEXTS = ROOT / "Data" / "texts" / "pt-BR.json"
SUMMONS = ROOT / "Data" / "summons"
AVATARS = ROOT / "Assets" / "Avatars"
INFUSION_CORE = "infusion_core"

# MailRewards no servidor.
MAX_AMOUNT = 1_000_000
MAX_MONSTERS = 10
MAX_RUNES = 20
LIMITED_ITEMS = {"infusionCores": MAX_MONSTERS}
# Quantidades dos exemplos; moeda que não está aqui vai com 1.
SAMPLE_AMOUNTS = {"gold": 100, "mana": 120, "scrolls": 10, "essence": 5000, "fragments": 10, "reappraisalGems": 2, "infusionCores": 2}
MONSTER_KEY = re.compile(r"monster:[a-z0-9_]+")
RUNE_KEY = re.compile(r"rune:[1-6](:[A-Za-z]+(:[A-Za-z]+)?)?")
AVATAR_KEY = re.compile(r"avatar:[a-z0-9_]+(:awakened)?")


def enum_members(path):
    body = re.sub(r"//.*", "", path.read_text(encoding="utf-8-sig"))
    start = body.index("{", body.index("enum "))
    inner = body[start + 1:body.index("}", start)]
    return [member.split("=")[0].strip() for member in inner.split(",") if member.strip()]


def parse_texts(path):
    """Como em Tools/texts/check_texts.py: o arquivo de textos aceita linhas // e vírgula sobrando."""
    lines = [line for line in path.read_text(encoding="utf-8-sig").splitlines() if not line.lstrip().startswith("//")]
    return json.loads(re.sub(r",(\s*[}\]])", r"\1", "\n".join(lines)))


def camel(name):
    return name[0].lower() + name[1:]


def monsters():
    by_stars = {}
    for path in sorted(SUMMONS.glob("*.json")):
        family = json.loads(path.read_text(encoding="utf-8-sig"))
        for variation in family["variations"].values():
            by_stars.setdefault(family["star_grade"], {})[variation["variation_id"]] = \
                f'{variation["name"]} ({variation["element"]})'
    return {f"{stars}★": dict(sorted(ids.items())) for stars, ids in sorted(by_stars.items())}


def specials(texts):
    """Como SpecialAvatars.IdOf: o nome do desenho em minúsculas, com "_" no lugar de "-"."""
    names = texts["avatar"]["special"]
    ids = (svg.stem.lower().replace("-", "_") for svg in sorted(AVATARS.glob("*.svg")))
    return {i: names.get(i, "(sem nome em Data/texts)") for i in ids if i != "default"}


def item_limit(item):
    return LIMITED_ITEMS.get(item, MAX_AMOUNT)


def check(name, body, items, rarities, sets, monster_ids, special_ids):
    errors = []
    title, text, days = body.get("title", "").strip(), body.get("text", "").strip(), body.get("expiresInDays")
    if not 1 <= len(title) <= 80 or len(text) > 2000 or (days is not None and not 1 <= days <= 365):
        errors.append("invalid_mail")
    for key, amount in body.get("rewards", {}).items():
        item = next((i for i in items if i.lower() == key.strip().lower()), None)
        if item:
            limit = item_limit(item)
        elif MONSTER_KEY.fullmatch(key):
            limit = MAX_MONSTERS
            if key.split(":")[1] not in monster_ids:
                errors.append(f"{key}: o jogo não conhece o monstro")
        elif RUNE_KEY.fullmatch(key):
            limit = MAX_RUNES
            parts = key.split(":")
            if len(parts) >= 3 and parts[2] not in rarities or len(parts) == 4 and parts[3] not in sets:
                errors.append(f"{key}: o jogo não conhece a raridade ou o conjunto")
        elif AVATAR_KEY.fullmatch(key):
            limit = 1
            if key.split(":")[1] not in monster_ids | special_ids:
                errors.append(f"{key}: o jogo não conhece o retrato")
        else:
            errors.append(f"{key}: invalid_reward")
            continue
        if not 1 <= amount <= limit:
            errors.append(f"{key}: {amount} fora de 1 a {limit}")
    return [f"{name}: {e}" for e in errors]


def main():
    texts = parse_texts(TEXTS)
    item_names = texts["mail"]["item"]
    items = {camel(i): item_names[i] for i in enum_members(PROGRESSION / "MailItem.cs")}
    items["mana"] += " (pode passar do máximo)"
    items["infusionCores"] += f", 1 a {MAX_MONSTERS} (cada um chega como monstro; igual a monster:{INFUSION_CORE})"
    rarities = {r: f"{texts['rarity'][r]}, {i} subatributo{'s' if i != 1 else ''}"
                for i, r in enumerate(enum_members(CONTENT / "RuneRarity.cs"))}
    sets = {s: texts["set"][s] for s in enum_members(CONTENT / "RuneSet.cs")}
    catalog_monsters = monsters()
    monster_ids = {i for group in catalog_monsters.values() for i in group} | {INFUSION_CORE}
    catalog_specials = specials(texts)

    template = {
        "to": "<nome da conta, e-mail ou *>",
        "title": "Presente completo",
        "text": "Uma carta com todos os tipos de recompensa.",
        "rewards": {**{item: SAMPLE_AMOUNTS.get(item, 1) for item in items},
                    "monster:knight_fire": 1,
                    "rune:5": 2,
                    "rune:5:Hero": 1,
                    "rune:6:Legendary:Vigor": 1,
                    "avatar:imp_fire:awakened": 1},
        "expiresInDays": 30,
    }
    examples = {
        "pergaminhos_especiais": {
            "to": "*",
            "title": "Evento de aniversário",
            "text": "Pergaminhos especiais para todos.",
            "rewards": {"lightDarkScrolls": 1, "legendaryScrolls": 1},
            "expiresInDays": 30,
        },
        "aviso_sem_recompensa": {
            "to": "*",
            "title": "Manutenção amanhã",
            "text": "O servidor fica fora do ar das 3h às 4h.",
        },
        "moedas_para_todos": {
            "to": "*",
            "title": "Manutenção concluída",
            "text": "Obrigado pela paciência.",
            "rewards": {"mana": 120, "gold": 50},
            "expiresInDays": 7,
        },
        "todas_as_moedas": {
            "to": "Mestre",
            "title": "Compensação",
            "text": "",
            "rewards": {item: SAMPLE_AMOUNTS.get(item, 1) for item in items},
        },
        "monstro_para_uma_conta": {
            "to": "player@example.com",
            "title": "Um novo aliado",
            "text": "Cópia nova, nível 1, como a da invocação.",
            "rewards": {"monster:knight_fire": 2},
        },
        "nucleo_de_infusao": {
            "to": "Mestre",
            "title": "Núcleos de Infusão",
            "text": f"Até {MAX_MONSTERS} por carta; monster:{INFUSION_CORE} dá o mesmo resultado.",
            "rewards": {"infusionCores": 3},
        },
        "runas": {
            "to": "*",
            "title": "Runas de presente",
            "text": "Sorteadas como as da vitória.",
            "rewards": {"rune:4": 5, "rune:5:Rare": 2, "rune:6:Legendary:Frenzy": 1},
            "expiresInDays": 14,
        },
        "retrato": {
            "to": "Mestre",
            "title": "Retrato exclusivo",
            "text": "Libera o retrato da conta, mesmo sem ter o monstro.",
            "rewards": {"avatar:imp_fire": 1, "avatar:dragon_dark:awakened": 1},
        },
    }
    if catalog_specials:
        examples["retrato_especial"] = {
            "to": "*",
            "title": "Retrato especial",
            "text": "Um retrato que não é monstro, para quem coletar.",
            "rewards": {f"avatar:{next(iter(catalog_specials))}": 1},
        }

    errors = [e for name, body in {"template": template, **examples}.items()
              for e in check(name, body, items, rarities, sets, monster_ids, set(catalog_specials))]
    if errors:
        print("\n".join(errors))
        sys.exit(1)

    document = {
        "_leia": [
            "Template do correio (POST /admin/mail no sigilos-server). Copie um dos corpos de 'exemplos' (ou o 'template') e mande; o resto do arquivo é referência.",
            "Fontes: D:\\dev\\sigilos-server\\Server\\Program.cs (MailRequest, MailRewards) e, no jogo, Core/Progression/MailItem.cs, MailGift.cs, Mailbox.cs e GameEntry/Account/CloudMail.cs.",
            "O servidor confere só o formato das chaves; o jogo descarta em silêncio monstro/retrato que não conhece e runa com raridade ou conjunto inválidos.",
            "Gerado por py Tools/mail/mail_template.py: rode de novo quando entrar monstro, moeda, raridade ou conjunto.",
        ],
        "envio": {
            "rota": "POST https://sigilos.minopavel.duckdns.org/admin/mail",
            "cabecalhos": {
                "X-Admin-Key": "<SIGILOS_ADMIN_KEY, 24+ caracteres>",
                "Content-Type": "application/json; charset=utf-8",
            },
            "limite": "30 pedidos por minuto por IP (todas as rotas /admin)",
            "campos": {
                "to": "opcional. Nome da conta (sem diferença de maiúsculas e acentos), e-mail, ou \"*\" / vazio / ausente = todas as contas, inclusive as criadas depois do envio",
                "title": "obrigatório, 1 a 80 caracteres (depois do Trim)",
                "text": "opcional, até 2000 caracteres; pode ficar vazio",
                "rewards": "opcional. Objeto chave → quantidade (inteiro ≥ 1). Sem rewards, a carta é só um aviso. A mesma moeda duas vezes com maiúsculas diferentes (gold e Gold) = 400 invalid_reward",
                "expiresInDays": "opcional, 1 a 365; ausente = a carta não vence",
            },
            "respostas": {
                "201": {"id": "GUID", "to": "nome (ou e-mail) da conta, ou \"*\"", "expiresAt": "data ou null"},
                "400 invalid_mail": "title vazio/maior que 80, text maior que 2000, ou expiresInDays fora de 1–365",
                "400 invalid_reward": "chave desconhecida, quantidade < 1 ou acima do máximo, ou moeda repetida (a resposta traz 'allowed' e 'max')",
                "404 user_not_found": "'to' não bate com nome nem e-mail",
                "401": "X-Admin-Key ausente ou errada (ou SIGILOS_ADMIN_KEY não configurada)",
            },
            "outras_rotas": {
                "GET /admin/mail": "lista as cartas enviadas (mais novas primeiro) com 'claims' = quantas contas coletaram",
                "DELETE /admin/mail/{id}": "retira a carta e quem a coletou: 204 ou 404 mail_not_found",
            },
        },
        "template": template,
        "exemplos": examples,
        "catalogo": {
            "moedas": {
                "_regra": f"chave = nome da moeda (sem diferença de maiúsculas); 1 a {MAX_AMOUNT:,} cada".replace(",", ".")
                          + ", menos " + ", ".join(f"{i} (1 a {m})" for i, m in LIMITED_ITEMS.items()),
                **items,
            },
            "runas": {
                "_regra": f"chave rune:<estrelas>[:<raridade>[:<conjunto>]]; 1 a {MAX_RUNES} por chave. Sem raridade/conjunto, sorteados. Conjunto exige raridade (rune:5::Vigor não vale). Raridade ou conjunto com erro de grafia: o servidor aceita e o jogo descarta a runa",
                "estrelas": [1, 2, 3, 4, 5, 6],
                "raridades": rarities,
                "conjuntos": sets,
            },
            "monstros": {
                "_regra": f"chave monster:<id> (minúsculas, o id da variante); 1 a {MAX_MONSTERS} por chave; cópias novas no nível 1 (Baú se a coleção estiver cheia). Também vale monster:{INFUSION_CORE}",
                "ids": catalog_monsters,
            },
            "retratos": {
                "_regra": "chave avatar:<id> ou avatar:<id>:awakened; quantidade sempre 1. De monstro: os ids de catalogo.monstros.ids; especiais (sem :awakened): os de especiais, um por desenho de Assets/Avatars",
                "especiais": catalog_specials,
            },
        },
    }

    OUT.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{OUT.relative_to(ROOT)}: {len(monster_ids) - 1} monstros, {len(items)} moedas, {len(examples)} exemplos")


if __name__ == "__main__":
    main()
