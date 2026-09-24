"""Étape 1 bis : enrichir le corpus avec ISBNdb (source payante), puis mesurer ce qu'elle apporte.

    python -m bench.isbndb

Lit la clé ISBNDB_API_KEY dans .env.local. Les notices restent dans cache/ (licence
ISBNdb : à supprimer si l'abonnement s'arrête). Seules des statistiques agrégées sont
écrites dans out/isbndb-couverture.md.
"""

import html
import json
import os
import re
from pathlib import Path

from .embeddings import load_local_env
from .sources import IsbndbError, isbndb_lookup_many
from .text import clean_summary, normalize

ROOT = Path(__file__).resolve().parent.parent
CORPUS_CACHE = ROOT / "cache" / "isbndb-corpus.json"

FRENCH = {"le", "la", "les", "des", "une", "est", "dans", "pour", "qui", "avec", "sur", "son"}
ENGLISH = {"the", "and", "of", "his", "her", "with", "is", "in", "to", "who", "for", "that"}


def strip_html(text: str | None) -> str | None:
    if not text:
        return None
    text = re.sub(r"<br\s*/?>|</p>", " ", text, flags=re.I)
    text = re.sub(r"<[^>]+>", "", text)
    return " ".join(html.unescape(text).split()) or None


def language_of(text: str | None) -> str | None:
    if not text:
        return None
    words = normalize(text).split()
    fr, en = sum(w in FRENCH for w in words), sum(w in ENGLISH for w in words)
    return "fr" if fr > en else "en" if en > fr else None


def isbndb_summary(book: dict | None, title: str) -> str | None:
    """Synopsis, sinon présentation (overview), nettoyés comme les autres résumés."""
    if not book:
        return None
    for field in ("synopsis", "overview"):
        cleaned = clean_summary(strip_html(book.get(field)), title)
        if cleaned:
            return cleaned
    return None


def load_isbndb() -> dict[str, dict | None] | None:
    if not CORPUS_CACHE.exists():
        return None
    return json.loads(CORPUS_CACHE.read_text(encoding="utf-8"))


def main() -> None:
    load_local_env()
    key = os.environ.get("ISBNDB_API_KEY", "").strip()
    if not key:
        raise SystemExit("ISBNDB_API_KEY manquant dans .env.local")
    corpus = json.loads((ROOT / "data" / "corpus.json").read_text(encoding="utf-8"))
    try:
        books = isbndb_lookup_many([e["isbn13"] for e in corpus], key)
    except IsbndbError as error:
        raise SystemExit(str(error))
    CORPUS_CACHE.write_text(json.dumps(books, ensure_ascii=False), encoding="utf-8")

    n = len(corpus)
    def pct(count):
        return f"{count}/{n} ({100 * count // n} %)"
    found = [e for e in corpus if books.get(e["isbn13"])]
    summaries = {e["id"]: isbndb_summary(books.get(e["isbn13"]), e["label"]["title"]) for e in corpus}
    gained = [e for e in corpus if not e["summary_work"] and summaries[e["id"]]]
    languages = [language_of(s) for s in summaries.values() if s]
    fields = ["synopsis", "overview", "subjects", "dewey_decimal", "pages", "language", "excerpt", "reviews",
              "edition", "binding", "dimensions_structured", "date_published", "image"]
    lines = [
        "# ISBNdb : ce que la source apporte au corpus",
        "",
        "Généré par `python -m bench.isbndb`. Statistiques agrégées seulement : les notices ISBNdb "
        "restent dans le cache local (licence).",
        "",
        "| Mesure | Éditions |",
        "|---|---|",
        f"| Trouvées dans ISBNdb | {pct(len(found))} |",
        f"| Avec un résumé exploitable (synopsis ou overview, nettoyé) | {pct(sum(1 for s in summaries.values() if s))} |",
        f"| … en français | {sum(1 for l in languages if l == 'fr')} |",
        f"| … en anglais | {sum(1 for l in languages if l == 'en')} |",
        f"| **Résumé gagné là où BnF et Open Library n'en avaient pas** | **{pct(len(gained))}** |",
        f"| Résumé d'œuvre disponible, BnF + Open Library seuls | {pct(sum(1 for e in corpus if e['summary_work']))} |",
        f"| Résumé d'œuvre disponible, avec ISBNdb en dernier recours | "
        f"{pct(sum(1 for e in corpus if e['summary_work'] or summaries[e['id']]))} |",
        "",
        "Remplissage des champs ISBNdb, parmi les éditions trouvées :",
        "",
        "| Champ | Rempli |",
        "|---|---|",
    ]
    for field in fields:
        filled = sum(1 for e in found if books[e["isbn13"]].get(field))
        lines.append(f"| `{field}` | {filled}/{len(found)} |")
    lines += ["", "Éditions qui gagnent un résumé grâce à ISBNdb : " +
              (", ".join(f"{e['bnf']['title']} ({e['label']['author']})" for e in gained) or "aucune"), ""]
    out = ROOT / "out"
    out.mkdir(exist_ok=True)
    (out / "isbndb-couverture.md").write_text("\n".join(lines), encoding="utf-8")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
