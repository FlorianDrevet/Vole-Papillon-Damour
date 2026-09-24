"""Les méthodes de calcul de livres proches mises en concurrence.

Chaque méthode renvoie, pour chaque édition, ses TOP_N meilleurs voisins
(identifiant, score). Le livre lui-même est toujours exclu.
"""

import math
import re
from collections import Counter
from dataclasses import dataclass
from typing import Callable

import numpy as np

from .features import GENERIC_COLLECTIONS, Features, audience_compatible, next_tome, same_work
from .text import normalize

TOP_N = 10

BONUS = {
    "meme_serie": 0.20,
    "tome_suivant": 0.10,
    "meme_auteur": 0.08,
    "meme_forme": 0.03,
}


@dataclass
class Method:
    code: str
    label: str
    description: str
    needs_embeddings: bool
    run: Callable


Rankings = dict[str, list[tuple[str, float]]]


def _rank(features: list[Features], scores: np.ndarray, allowed: Callable[[Features, Features], bool] | None) -> Rankings:
    rankings = {}
    for i, query in enumerate(features):
        order = np.argsort(-scores[i])
        picked = []
        for j in order:
            if j == i or not np.isfinite(scores[i, j]):
                continue
            if allowed is not None and not allowed(query, features[j]):
                continue
            picked.append((features[j].id, float(scores[i, j])))
            if len(picked) == TOP_N:
                break
        rankings[query.id] = picked
    return rankings


def _filters(query: Features, candidate: Features) -> bool:
    return not same_work(query, candidate) and audience_compatible(query, candidate)


def _bonus_matrix(features: list[Features]) -> np.ndarray:
    n = len(features)
    bonus = np.zeros((n, n), dtype=np.float32)
    for i, q in enumerate(features):
        for j, c in enumerate(features):
            if i == j:
                continue
            value = 0.0
            if q.series_key and q.series_key == c.series_key:
                value += BONUS["meme_serie"]
                if next_tome(q, c):
                    value += BONUS["tome_suivant"]
            if q.surnames & c.surnames:
                value += BONUS["meme_auteur"]
            if q.form_family == c.form_family:
                value += BONUS["meme_forme"]
            bonus[i, j] = value
    return bonus


# --------------------------------------------------------------- sans embedding ---

RARE_SHARE = 0.10  # un terme ou un classement ne compte que s'il concerne au plus 10 % du catalogue


def rules(features: list[Features], _vectors) -> Rankings:
    """R0 : règles pures sur les métadonnées, aucun appel d'IA."""
    n = len(features)
    frequency = Counter(t for f in features for t in f.terms | {f"686:{c}" for c in f.class_686})
    rare = {t for t, count in frequency.items() if count <= max(2, RARE_SHARE * n)}
    scores = np.full((n, n), -np.inf, dtype=np.float32)
    for i, q in enumerate(features):
        q_rare = (q.terms | {f"686:{c}" for c in q.class_686}) & rare
        for j, c in enumerate(features):
            if i == j:
                continue
            score = 0.0
            if q.series_key and q.series_key == c.series_key:
                score += 4 + (1 if next_tome(q, c) else 0)
            if q.surnames & c.surnames:
                score += 2
            if q.collection and q.collection == c.collection and q.collection not in GENERIC_COLLECTIONS:
                score += 1
            score += min(2, len(q_rare & (c.terms | {f"686:{x}" for x in c.class_686})))
            if score > 0:
                scores[i, j] = score
    return _rank(features, scores, _filters)


STOPWORDS = set(normalize(
    "le la les un une des du de d l et en au aux a ou par pour sur dans avec sans ce ces cet cette son sa ses "
    "leur leurs qui que quoi dont il elle ils elles on nous vous se ne pas plus est sont etre avoir fait tome "
    "auteur collection serie genre sujets public roman romans"
).split())


def tfidf(features: list[Features], _vectors) -> Rankings:
    """L1 : TF-IDF lexical sur le texte composé, aucun appel d'IA."""
    documents = [
        [w for w in normalize(f.texts["compose"]).split() if len(w) > 2 and w not in STOPWORDS]
        for f in features
    ]
    document_frequency = Counter(word for doc in documents for word in set(doc))
    n = len(documents)
    vocabulary = {word: index for index, word in enumerate(document_frequency)}
    matrix = np.zeros((n, len(vocabulary)), dtype=np.float32)
    for i, doc in enumerate(documents):
        for word, count in Counter(doc).items():
            matrix[i, vocabulary[word]] = (1 + math.log(count)) * math.log((1 + n) / (1 + document_frequency[word]))
    matrix /= np.linalg.norm(matrix, axis=1, keepdims=True).clip(min=1e-12)
    return _rank(features, matrix @ matrix.T, None)


# ---------------------------------------------------------------- avec embedding ---

def _cosine(vectors: dict[str, np.ndarray], variant: str) -> np.ndarray:
    matrix = vectors[variant]
    return matrix @ matrix.T


def embedding_raw(variant: str):
    def run(features, vectors):
        return _rank(features, _cosine(vectors, variant), None)
    return run


def embedding_filtered(variant: str):
    def run(features, vectors):
        return _rank(features, _cosine(vectors, variant), _filters)
    return run


def hybrid(variant: str):
    def run(features, vectors):
        return _rank(features, _cosine(vectors, variant) + _bonus_matrix(features), _filters)
    return run


METHODS: list[Method] = [
    Method("R0", "Règles seules (sans IA)",
           "Même série (+4, tome suivant +1), même auteur (+2), même collection typée (+1), genres, sujets et "
           "classement BnF rares en commun (+1 chacun, 2 au plus). Filtres : même œuvre et public incompatible "
           "exclus. Un livre sans aucun point commun n'est pas proposé.", False, rules),
    Method("L1", "TF-IDF lexical (sans IA)",
           "Mots partagés entre textes composés, pondérés par leur rareté. Aucun filtre.", False, tfidf),
    Method("E1", "Embedding titre + auteur",
           "Vecteur du seul titre et des auteurs. Aucun filtre.", True, embedding_raw("titre_auteur")),
    Method("E2", "Embedding résumé de l'édition",
           "Vecteur du résumé propre à l'édition, sinon titre + auteur. Aucun filtre.", True,
           embedding_raw("resume_edition")),
    Method("E3", "Embedding résumé de l'œuvre",
           "Vecteur du meilleur résumé parmi toutes les éditions de l'œuvre (BnF, sinon Open Library), "
           "sinon titre + auteur. Aucun filtre.", True, embedding_raw("resume_oeuvre")),
    Method("E4", "Embedding texte composé",
           "Vecteur de : titre, auteurs, collection, série, genres, sujets, public, résumé de l'œuvre. "
           "Aucun filtre.", True, embedding_raw("compose")),
    Method("F4", "E4 + filtres",
           "E4, puis exclusion de la même œuvre et des publics incompatibles (jeunesse / adulte).", True,
           embedding_filtered("compose")),
    Method("H3", "E3 + filtres + bonus",
           "Résumé de l'œuvre, filtres, puis bonus : même série +0,20, tome suivant +0,10, même auteur +0,08, "
           "même forme +0,03.", True, hybrid("resume_oeuvre")),
    Method("H4", "E4 + filtres + bonus (candidat retenu)",
           "Texte composé, filtres et bonus. C'est la méthode proposée pour la production.", True,
           hybrid("compose")),
    Method("H4-512", "H4 en 512 dimensions",
           "H4 avec des vecteurs réduits à 512 dimensions (stockage divisé par 3).", True, hybrid("compose@512")),
    Method("H4-256", "H4 en 256 dimensions",
           "H4 avec des vecteurs réduits à 256 dimensions (stockage divisé par 6).", True, hybrid("compose@256")),
]
