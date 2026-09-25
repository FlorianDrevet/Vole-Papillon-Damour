"""Étape 1 : constituer le corpus réel à partir de la BnF et d'Open Library.

    python -m bench.build_corpus

Produit data/corpus.json (les éditions retenues et tout ce que les sources en disent)
et data/journal-constitution.md (ce qui a été trouvé, choisi ou manqué, œuvre par œuvre).
"""

import json
from dataclasses import asdict
from pathlib import Path

from .corpus_spec import WORKS, Work
from .sources import openlibrary_lookup, sru_search
from .text import clean_summary, normalize

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "data"

LARGE_PRINT_MARKERS = ("gros caracteres", "grands caracteres", "large vision", "voir de pres", "a vue d oeil")
TEXT_FORMS = ("roman", "conte", "essai", "pratique")


def _is_candidate(work: Work, record: dict) -> bool:
    if not record["isbns"]:
        return False
    wanted = normalize(work.title)
    titles = {normalize(record["title"]), normalize(record["part_title"])}
    if work.tome is not None:
        titles.add(normalize(record["series_title"]))  # tome d'une série : titre propre différent
    if wanted not in titles:
        return False
    if work.tome is not None and work.tome not in (record["part_number"], record["series_number"]):
        return False
    if record["languages"] and "fre" not in record["languages"]:
        return False
    extra = normalize(" ".join(filter(None, [record["collection"], record["subtitle"]])))
    if any(marker in extra for marker in LARGE_PRINT_MARKERS):
        return False
    if work.form in TEXT_FORMS and any("bande" in normalize(f) for f in record["forms_608"]):
        return False  # une adaptation en BD n'est pas l'œuvre textuelle
    return True


def _pick(candidates: list[dict], count: int) -> list[dict]:
    """La plus récente, puis une édition plus ancienne distincte si demandé."""
    ordered = sorted(candidates, key=lambda r: r["year"] or 0, reverse=True)
    picked, seen = [], set()
    for record in ordered:
        isbn = record["isbns"][0]
        if isbn not in seen:
            picked.append(record)
            seen.add(isbn)
            break
    if count > 1:
        rest = [r for r in ordered if r["isbns"][0] not in seen]
        if rest:
            picked.append(rest[len(rest) // 2])
    return picked


def _work_summary(work: Work, candidates: list[dict], picked: list[dict]) -> tuple[str | None, str | None]:
    """Le résumé le plus long parmi les éditions de l'œuvre (BnF), sinon Open Library."""
    pool = [
        cleaned
        for record in candidates
        for raw in record["summaries"]
        if (cleaned := clean_summary(raw, work.title))
    ]
    if pool:
        return max(pool, key=len), "bnf-oeuvre"
    isbns = [r["isbns"][0] for r in picked] + [r["isbns"][0] for r in candidates if r not in picked][:3]
    for isbn in isbns:
        description = clean_summary(openlibrary_lookup(isbn)["description"], work.title)
        if description:
            return description, "openlibrary"
    return None, None


def build() -> None:
    DATA.mkdir(parents=True, exist_ok=True)
    corpus, log = [], []
    for work in WORKS:
        cql = f'bib.title all "{work.title}" and bib.author all "{work.author}" and bib.doctype any "a"'
        records = sru_search(cql, maximum=50)
        candidates = [r for r in records if _is_candidate(work, r)]
        # Priorité aux notices qui citent l'auteur : écarte les adaptations (BD d'un roman…)
        by_author = [r for r in candidates if normalize(work.author) in normalize(" ".join(r["authors"]))]
        picked = _pick(by_author or candidates, work.editions)
        work_summary, work_summary_source = _work_summary(work, candidates, picked) if picked else (None, None)
        log.append((work, len(records), len(candidates), picked, work_summary_source))
        for index, record in enumerate(picked, start=1):
            isbn = record["isbns"][0]
            corpus.append({
                "id": f"{work.key}#{index}",
                "work_key": work.key,
                "isbn13": isbn,
                "label": asdict(work),
                "bnf": record,
                "openlibrary": openlibrary_lookup(isbn),
                "summary_edition": next(
                    (s for raw in record["summaries"] if (s := clean_summary(raw, work.title))), None),
                "summary_work": work_summary,
                "summary_work_source": work_summary_source,
            })
        status = "OK" if len(picked) == work.editions else ("MANQUANT" if not picked else "PARTIEL")
        if picked and not any(normalize(work.author) in normalize(" ".join(r["authors"])) for r in picked):
            status = "AUTEUR?"  # la notice retenue ne cite pas l'auteur annoté : à vérifier
        print(f"{status:8} {work.key:18} notices={len(records):3} retenues={len(candidates):3} "
              f"éditions={len(picked)} résumé={work_summary_source or '-'}", flush=True)

    (DATA / "corpus.json").write_text(json.dumps(corpus, ensure_ascii=False, indent=1), encoding="utf-8")
    _write_log(log, corpus)
    print(f"\n{len(corpus)} éditions pour {len({c['work_key'] for c in corpus})} œuvres -> data/corpus.json")


def _write_log(log, corpus) -> None:
    lines = [
        "# Journal de constitution du corpus",
        "",
        "Généré par `python -m bench.build_corpus`. Une ligne par œuvre annotée.",
        "",
        f"**{len(corpus)} éditions** retenues pour **{len({c['work_key'] for c in corpus})} œuvres** "
        f"sur {len(log)} annotées.",
        "",
        "| Œuvre | Notices BnF | Candidates | Éditions retenues (ISBN, année) | Résumé d'œuvre |",
        "|---|---|---|---|---|",
    ]
    for work, found, candidates, picked, source in log:
        editions = ", ".join(f"{r['isbns'][0]} ({r['year'] or '?'})" for r in picked) or "**aucune**"
        lines.append(f"| `{work.key}` {work.title} — {work.author} | {found} | {candidates} | {editions} | {source or '—'} |")
    (DATA / "journal-constitution.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    build()
