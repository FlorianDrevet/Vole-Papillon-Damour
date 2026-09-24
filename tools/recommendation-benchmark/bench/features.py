"""Ce que les méthodes ont le droit de savoir d'un livre.

Tout est déduit des données BnF et Open Library, comme le ferait l'application en
production. Les étiquettes du corpus (corpus_spec) ne sont jamais lues ici.
"""

import re
from dataclasses import dataclass

from .text import normalize

JUNIOR_MARKERS = re.compile(
    r"junior|jeunesse|cadet|bibliotheque (rose|verte)|j aime lire|castor|witty|kids|"
    r"premiers romans|mon premier|heure des histoires|albums"
)
MANGA_PUBLISHERS = ("kana", "pika", "kurokawa", "ki oon", "kaze", "tonkam", "glenat manga")
MANGA_MARKERS = re.compile(r"manga|shonen|shonen|seinen|shojo")
BD_PUBLISHERS = (
    "dargaud", "dupuis", "casterman", "lombard", "delcourt", "soleil", "bamboo", "futuropolis",
    "l association", "rue de sevres", "blake et mortimer", "moulinsart", "fluide glacial", "vents d ouest",
)
GENERIC_COLLECTIONS = {
    "folio", "folio plus", "folio classique", "pocket", "le livre de poche", "livre de poche",
    "j ai lu", "points", "10 18", "babel", "gf", "garnier flammarion", "classiques", "le livre de poche classiques",
}


@dataclass
class Features:
    id: str
    title: str
    authors: list[str]
    surnames: set[str]
    collection: str
    series_key: str | None
    tome: int | None
    audience: str          # déduit : jeunesse | ado | adulte
    form_family: str       # déduit : texte | illustre | essai
    class_686: set[str]
    terms: set[str]        # formes 608 et sujets 606 normalisés
    bnf_work_id: str | None
    ol_work_key: str | None
    title_key: str
    texts: dict[str, str | None]


def _audience(bnf: dict) -> str:
    age = None
    if bnf.get("audience_333"):
        match = re.search(r"(\d+)\s*ans", bnf["audience_333"])
        age = int(match.group(1)) if match else None
    if age is not None:
        return "jeunesse" if age <= 12 else "ado"
    haystack = normalize(" ".join(filter(None, [bnf.get("collection"), bnf.get("publisher")])))
    if bnf.get("cnlj") or JUNIOR_MARKERS.search(haystack):
        return "jeunesse"
    return "adulte"


def _form_family(bnf: dict) -> str:
    forms = normalize(" ".join(bnf.get("forms_608") or []))
    publisher = normalize(bnf.get("publisher"))
    collection = normalize(bnf.get("collection"))
    if MANGA_MARKERS.search(collection) or any(p in publisher for p in MANGA_PUBLISHERS):
        return "illustre"
    if "bandes dessinees" in forms or "romans graphiques" in forms or "manga" in forms:
        return "illustre"
    if any(p in publisher for p in BD_PUBLISHERS):
        return "illustre"
    dewey = (bnf.get("dewey") or "").strip()
    if dewey and not dewey.startswith("8"):
        return "essai"
    if bnf.get("subjects_606") and "roman" not in forms:
        return "essai"
    return "texte"


def _series(bnf: dict) -> tuple[str | None, int | None]:
    if bnf.get("series_title"):
        return normalize(bnf["series_title"]), bnf.get("series_number") or bnf.get("part_number")
    if bnf.get("part_number") and bnf.get("title"):
        return normalize(bnf["title"]), bnf["part_number"]
    return None, None


def _isbndb_subjects(book: dict | None) -> list[str]:
    subjects = (book or {}).get("subjects") or []
    return [s for s in dict.fromkeys(str(s).strip() for s in subjects) if s][:8]


def _isbndb_texts(entry: dict, bnf: dict, book: dict | None) -> dict[str, str]:
    """Variantes de texte avec ISBNdb (source payante), pour le second benchmark."""
    from .isbndb import isbndb_summary

    title = entry["label"]["title"]
    theirs = isbndb_summary(book, title)
    subjects = _isbndb_subjects(book)
    extra = ("Sujets : " + ", ".join(subjects)) if subjects else None

    def with_extra(text: str) -> str:
        return text + (". " + extra if extra else "")

    head = _title_line(bnf) + (". Auteur : " + ", ".join(bnf["authors"]) if bnf.get("authors") else "")
    return {
        # Cascade : BnF, puis Open Library, puis ISBNdb en dernier recours ; sujets ISBNdb ajoutés
        "compose_isbndb": with_extra(_compose(bnf, entry.get("summary_work") or theirs)),
        # ISBNdb d'abord : quelle source a les meilleurs résumés ?
        "compose_isbndb_prefere": with_extra(_compose(bnf, theirs or entry.get("summary_work"))),
        # ISBNdb seul : titre, auteurs, sujets et résumé ISBNdb, sans BnF ni Open Library pour le texte
        "isbndb_seul": ". ".join(filter(None, [head, extra, theirs])),
    }


def _compose(bnf: dict, work_summary: str | None, with_collection: bool = True) -> str:
    parts = [_title_line(bnf)]
    if bnf.get("authors"):
        parts.append("Auteur : " + ", ".join(bnf["authors"]))
    if with_collection and bnf.get("collection"):
        parts.append("Collection : " + bnf["collection"])
    if bnf.get("series_title"):
        tome = f", tome {bnf['series_number']}" if bnf.get("series_number") else ""
        parts.append(f"Série : {bnf['series_title']}{tome}")
    if bnf.get("forms_608"):
        parts.append("Genre : " + ", ".join(dict.fromkeys(bnf["forms_608"])))
    if bnf.get("subjects_606"):
        parts.append("Sujets : " + ", ".join(dict.fromkeys(bnf["subjects_606"])))
    if bnf.get("audience_333"):
        parts.append("Public : " + bnf["audience_333"])
    if work_summary:
        parts.append(work_summary)
    return ". ".join(parts)


def _title_line(bnf: dict) -> str:
    title = bnf.get("title") or ""
    if bnf.get("part_title") and normalize(bnf["part_title"]) != normalize(title):
        title += f", {bnf['part_title']}"
    if bnf.get("subtitle"):
        title += f" : {bnf['subtitle']}"
    return title


def build_features(entry: dict, isbndb_book: dict | None = None, with_isbndb: bool = False) -> Features:
    bnf = entry["bnf"]
    title_author = _title_line(bnf) + (". " + ", ".join(bnf["authors"]) if bnf.get("authors") else "")
    series_key, tome = _series(bnf)
    surnames = {normalize(s) for s in bnf.get("author_surnames") or []}
    first_surname = sorted(surnames)[0] if surnames else ""
    return Features(
        id=entry["id"],
        title=_title_line(bnf),
        authors=bnf.get("authors") or [],
        surnames=surnames,
        collection=normalize(bnf.get("collection")),
        series_key=series_key,
        tome=tome,
        audience=_audience(bnf),
        form_family=_form_family(bnf),
        class_686={c.strip() for c in bnf.get("class_686") or []},
        terms={normalize(t) for t in (bnf.get("forms_608") or []) + (bnf.get("subjects_606") or []) if t},
        bnf_work_id=bnf.get("bnf_work_id"),
        ol_work_key=(entry.get("openlibrary") or {}).get("work_key"),
        title_key=normalize(bnf.get("title")) + "|" + first_surname,
        texts={
            "titre_auteur": title_author,
            "resume_edition": entry.get("summary_edition"),
            "resume_oeuvre": entry.get("summary_work"),
            "compose": _compose(bnf, entry.get("summary_work")),
            "compose_sans_collection": _compose(bnf, entry.get("summary_work"), with_collection=False),
            **(_isbndb_texts(entry, bnf, isbndb_book) if with_isbndb else {}),
        },
    )


def same_work(a: Features, b: Features) -> bool:
    """Deux éditions de la même œuvre, d'après les seules données des sources."""
    if a.tome is not None and b.tome is not None and a.tome != b.tome:
        return False  # deux tomes d'une série partagent parfois l'identifiant de la série
    if a.bnf_work_id and a.bnf_work_id == b.bnf_work_id:
        return True
    if a.ol_work_key and a.ol_work_key == b.ol_work_key:
        return True
    same_title = a.title_key.split("|")[0] == b.title_key.split("|")[0]
    # Recoupement d'auteurs plutôt que « premier auteur » : un traducteur peut s'intercaler.
    return same_title and (not a.surnames or not b.surnames or bool(a.surnames & b.surnames))


def audience_compatible(a: Features, b: Features) -> bool:
    return {a.audience, b.audience} != {"jeunesse", "adulte"}


def next_tome(query: Features, candidate: Features) -> bool:
    return (
        query.series_key is not None
        and query.series_key == candidate.series_key
        and query.tome is not None
        and candidate.tome == query.tome + 1
    )
