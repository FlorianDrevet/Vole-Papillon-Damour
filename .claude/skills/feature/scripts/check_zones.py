"""Vérifie la cohérence maquettes ↔ plan d'une feature avant le passage à Codex.

Usage :
    python check_zones.py docs/features/<slug>

Échoue (code 1) si :
- une zone data-zone des maquettes n'est citée nulle part dans 02-plan.md ;
- une ligne « Maquette : » du plan cite une zone ou une page preview/ qui n'existe pas.
Une zone volontairement non implémentée doit être citée dans une section
« Zones non implémentées » du plan, avec la raison.
"""
import re
import sys
from pathlib import Path

ZONE = re.compile(r"\b[A-Z]{1,3}\d+(?:-[\wÀ-ſ]+)+\b")


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    feature = Path(sys.argv[1])
    preview = feature / "maquettes" / "preview"
    plan_path = feature / "02-plan.md"
    if not plan_path.is_file():
        sys.exit(f"Plan introuvable : {plan_path}")
    plan = plan_path.read_text(encoding="utf-8")

    zones = {}
    for page in sorted(preview.glob("*.html")):
        if page.name == "index.html":
            continue
        for zone in re.findall(r'data-zone="([^"]+)"', page.read_text(encoding="utf-8")):
            zones.setdefault(zone, page.name)
    pages = {p.name for p in preview.glob("*.html")}

    problems = []
    for zone, page in sorted(zones.items()):
        if not re.search(rf"(?<![\w-]){re.escape(zone)}(?![\w-])", plan):
            problems.append(f"zone non couverte par le plan : {zone} ({page})")
    for number, line in enumerate(plan.splitlines(), 1):
        if "maquette" not in line.lower():
            continue
        for page in re.findall(r"preview/([\w-]+\.html)", line):
            if page not in pages:
                problems.append(f"ligne {number} : page inexistante preview/{page}")
        for zone in ZONE.findall(line):
            if zone not in zones:
                problems.append(f"ligne {number} : zone inexistante {zone}")

    print(f"{len(zones)} zones dans {len(pages) - ('index.html' in pages)} pages, plan : {plan_path}")
    if not zones:
        problems.append("aucune zone data-zone dans les maquettes (feature sans écran ? le noter dans README.md)")
    for problem in problems:
        print(f"  ✗ {problem}")
    if problems:
        sys.exit(1)
    print("  ✓ chaque zone est citée par le plan et chaque référence du plan existe")


if __name__ == "__main__":
    main()
