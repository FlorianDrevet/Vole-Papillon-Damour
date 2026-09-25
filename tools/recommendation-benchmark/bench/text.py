"""Normalisation de texte et nettoyage des résumés."""

import re
import unicodedata


def strip_accents(text: str) -> str:
    return "".join(c for c in unicodedata.normalize("NFKD", text) if not unicodedata.combining(c))


def normalize(text: str | None) -> str:
    """Minuscules, sans accents, sans ponctuation, espaces réduits."""
    if not text:
        return ""
    text = strip_accents(text).lower().replace("œ", "oe").replace("æ", "ae")
    text = re.sub(r"[^a-z0-9]+", " ", text)
    return " ".join(text.split())


MIN_SUMMARY_LENGTH = 80
MAX_SUMMARY_LENGTH = 1500
EDITION_MARKERS = (
    "cette edition", "ce coffret", "cette nouvelle edition", "les points forts",
    "edition abregee", "edition collector", "a l occasion des",
)


def clean_summary(raw: str | None, title: str | None) -> str | None:
    """Rejette ce qui n'est pas un résumé d'œuvre (défauts observés en 03 §4)."""
    if not raw:
        return None
    text = " ".join(raw.split())
    if len(text) < MIN_SUMMARY_LENGTH:
        return None
    if text.startswith(("«", '"', "“", "—", "-")):
        return None  # extrait du texte plutôt que résumé
    if title and normalize(text) == normalize(title):
        return None
    if any(marker in normalize(text[:200]) for marker in EDITION_MARKERS):
        return None  # texte d'édition, pas d'œuvre
    return text[:MAX_SUMMARY_LENGTH]
