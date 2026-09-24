"""Accès aux sources bibliographiques : BnF (SRU, UNIMARC) et Open Library.

Toutes les réponses sont mises en cache sur disque (cache/http/) : relancer la
constitution du corpus ne réinterroge pas les services.
"""

import hashlib
import json
import re
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
HTTP_CACHE = ROOT / "cache" / "http"
USER_AGENT = "vpd-recommendation-benchmark/1.0 (association Vole Papillon d'Amour)"
SRU_ENDPOINT = "https://catalogue.bnf.fr/api/SRU"


def _cached_get(url: str, pause: float) -> bytes | None:
    HTTP_CACHE.mkdir(parents=True, exist_ok=True)
    path = HTTP_CACHE / (hashlib.sha1(url.encode("utf-8")).hexdigest() + ".bin")
    if path.exists():
        data = path.read_bytes()
        return None if data == b"__404__" else data
    for attempt in range(4):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
            with urllib.request.urlopen(request, timeout=40) as response:
                data = response.read()
            path.write_bytes(data)
            time.sleep(pause)
            return data
        except urllib.error.HTTPError as error:
            if error.code == 404:
                path.write_bytes(b"__404__")
                return None
            if error.code in (429, 500, 502, 503, 504) and attempt < 3:
                time.sleep(3 * (attempt + 1))
                continue
            return None
        except (urllib.error.URLError, TimeoutError):
            if attempt < 3:
                time.sleep(3 * (attempt + 1))
                continue
            return None
    return None


# --------------------------------------------------------------------------- BnF ---

def sru_search(cql: str, maximum: int = 50) -> list[dict]:
    query = urllib.parse.urlencode({
        "version": "1.2", "operation": "searchRetrieve", "query": cql,
        "recordSchema": "unimarcXchange", "maximumRecords": str(maximum), "startRecord": "1",
    })
    data = _cached_get(f"{SRU_ENDPOINT}?{query}", pause=0.5)
    if not data:
        return []
    root = ET.fromstring(data)
    records = [
        element for element in root.iter()
        if element.tag.startswith("{info:lc/xmlns/marcxchange") and element.tag.endswith("}record")
    ]
    return [parse_unimarc(record) for record in records]


def _datafields(record, tag):
    return [f for f in record if f.tag.endswith("datafield") and f.get("tag") == tag]


def _subfields(field, code):
    return [(s.text or "").strip() for s in field if s.tag.endswith("subfield") and s.get("code") == code and (s.text or "").strip()]


def _first(record, tag, code):
    for field in _datafields(record, tag):
        values = _subfields(field, code)
        if values:
            return values[0]
    return None


def _all(record, tag, code):
    return [value for field in _datafields(record, tag) for value in _subfields(field, code)]


def isbn10_to_13(isbn10: str) -> str:
    core = "978" + isbn10[:9]
    total = sum(int(d) * (1 if i % 2 == 0 else 3) for i, d in enumerate(core))
    return core + str((10 - total % 10) % 10)


def normalize_isbn(raw: str) -> str | None:
    digits = re.sub(r"[^0-9Xx]", "", raw or "")
    if len(digits) == 13 and digits.startswith(("978", "979")):
        return digits
    if len(digits) == 10:
        return isbn10_to_13(digits)
    return None


def _number(text: str | None) -> int | None:
    if not text:
        return None
    match = re.search(r"\d+", text)
    return int(match.group()) if match else None


def parse_unimarc(record) -> dict:
    """Extrait d'une notice UNIMARC tout ce qui peut servir à rapprocher des livres."""
    isbns = [isbn for isbn in (normalize_isbn(v) for v in _all(record, "010", "a")) if isbn]
    year = None
    for tag in ("210", "214"):
        for value in _all(record, tag, "d"):
            match = re.search(r"(1[5-9]\d\d|20\d\d)", value)
            if match:
                year = int(match.group(1))
                break
        if year:
            break
    authors, surnames = [], []
    for tag in ("700", "701", "702"):
        for field in _datafields(record, tag):
            roles = _subfields(field, "4")
            if tag == "702" and roles and "070" not in roles:
                continue  # 702 : rôle « auteur » (070) ou non précisé ; pas traducteur ni préfacier
            surname = (_subfields(field, "a") or [""])[0]
            given = (_subfields(field, "b") or [""])[0]
            if surname:
                authors.append(f"{given} {surname}".strip())
                surnames.append(surname)
    series_title = _first(record, "461", "t")
    series_number = _number(_first(record, "461", "v"))
    part_number = _number(_first(record, "200", "h"))
    cnlj = any(
        (s.text or "").strip() == "CNLJ"
        for f in record if f.tag.endswith("datafield")
        for s in f if s.tag.endswith("subfield") and s.get("code") == "2"
    )
    return {
        "isbns": isbns,
        "title": _first(record, "200", "a"),
        "subtitle": _first(record, "200", "e"),
        "part_number": part_number,
        "part_title": _first(record, "200", "i"),
        "authors": authors,
        "author_surnames": surnames,
        "publisher": _first(record, "210", "c") or _first(record, "214", "c"),
        "year": year,
        "collection": _first(record, "225", "a"),
        "series_title": series_title,
        "series_number": series_number,
        "bnf_work_id": _first(record, "500", "3"),
        "bnf_work_title": _first(record, "500", "a"),
        "class_686": _all(record, "686", "a"),
        "dewey": _first(record, "676", "a"),
        "forms_608": _all(record, "608", "a"),
        "subjects_606": _all(record, "606", "a") + _all(record, "606", "x"),
        "audience_333": _first(record, "333", "a"),
        "cnlj": cnlj,
        "languages": _all(record, "101", "a"),
        "original_languages": _all(record, "101", "c"),
        "summaries": _all(record, "330", "a"),
    }


# ------------------------------------------------------------------- Open Library ---

def _json(url: str):
    data = _cached_get(url, pause=0.4)
    if not data:
        return None
    try:
        return json.loads(data)
    except json.JSONDecodeError:
        return None


def _description(document) -> str | None:
    value = (document or {}).get("description")
    if isinstance(value, dict):
        value = value.get("value")
    return value.strip() if isinstance(value, str) and value.strip() else None


def openlibrary_lookup(isbn13: str) -> dict:
    """Description (édition, sinon œuvre) et identifiant d'œuvre Open Library."""
    edition = _json(f"https://openlibrary.org/isbn/{isbn13}.json")
    if not edition:
        return {"found": False, "work_key": None, "description": None}
    description = _description(edition)
    work_key = None
    works = edition.get("works") or []
    if works:
        work_key = works[0].get("key")
        if not description and work_key:
            description = _description(_json(f"https://openlibrary.org{work_key}.json"))
    return {"found": True, "work_key": work_key, "description": description}
