"""Évaluation : la note de pertinence d'un voisin, et les indicateurs par méthode.

La note ne lit QUE les étiquettes du corpus annoté (corpus_spec), jamais les données
utilisées par les méthodes.

Grille de pertinence d'un candidat C proposé pour un livre Q :
  3  même série (autre tome)
  2  même famille thématique, public compatible et même type de forme
  1  lien plus lâche : même famille mais public ou forme différents, adaptation
     (BD tirée du roman), ou même auteur hors série
  0  sans rapport
Une autre édition de la même œuvre n'est pas notée : c'est une FUITE, comptée à part.
"""

import math
from dataclasses import dataclass, field

K = 5

FORM_FAMILY = {"roman": "texte", "conte": "texte", "bd": "illustre", "manga": "illustre", "essai": "essai", "pratique": "pratique"}


def audience_relation(a: str, b: str) -> str:
    """full : compatible ; partial : voisin (ado) ; none : jeunesse face à adulte."""
    if a == b or "tout-public" in (a, b):
        return "full"
    if {a, b} == {"jeunesse", "adulte"}:
        return "none"
    return "partial"


def is_leak(q: dict, c: dict) -> bool:
    return q["key"] == c["key"]


def grade(q: dict, c: dict) -> int:
    if is_leak(q, c):
        return 0
    if q["series"] and c["series"] and q["series"][0] == c["series"][0]:
        return 3
    adaptation = c["adaptation_of"] == q["key"] or q["adaptation_of"] == c["key"] or (
        q["adaptation_of"] and q["adaptation_of"] == c["adaptation_of"])
    shared = set(q["clusters"]) & set(c["clusters"])
    audience = audience_relation(q["audience"], c["audience"])
    same_form = FORM_FAMILY[q["form"]] == FORM_FAMILY[c["form"]]
    if shared and audience == "full" and same_form and not adaptation:
        return 2
    if shared or adaptation:
        return 1
    if q["author"] == c["author"] and audience != "none":
        return 1
    return 0


def dcg(gains: list[int]) -> float:
    return sum(g / math.log2(i + 2) for i, g in enumerate(gains))


@dataclass
class MethodScore:
    code: str
    ndcg: float = 0.0
    precision: float = 0.0          # part des K voisins notés ≥ 2
    series_hit: float | None = None  # un autre tome de la série dans le top K
    next_tome_hit: float | None = None
    leak_rate: float | None = None  # requêtes dont le top K contient une autre édition de la même œuvre
    audience_mismatch: float = 0.0  # part des voisins jeunesse face à adulte
    form_mismatch: float = 0.0      # part des voisins d'un autre type de forme
    coverage: float = 0.0           # requêtes avec K voisins
    diversity: float = 0.0          # auteurs distincts dans le top K
    per_cluster: dict[str, float] = field(default_factory=dict)
    per_query: dict[str, float] = field(default_factory=dict)


def evaluate(code: str, rankings: dict, labels: dict[str, dict]) -> MethodScore:
    """labels : id d'édition -> étiquettes de son œuvre (asdict(Work))."""
    ids = list(labels)
    ndcgs, precisions, audience_bad, form_bad, covered, diversity = [], [], [], [], [], []
    series_hits, next_hits, leaks = [], [], []
    per_cluster: dict[str, list[float]] = {}
    per_query = {}
    for qid in ids:
        q = labels[qid]
        top = [cid for cid, _ in rankings.get(qid, [])][:K]
        gains = [grade(q, labels[c]) for c in top]
        ideal = sorted((grade(q, labels[c]) for c in ids if c != qid and not is_leak(q, labels[c])), reverse=True)[:K]
        score = dcg(gains) / dcg(ideal) if dcg(ideal) > 0 else 0.0
        ndcgs.append(score)
        per_query[qid] = score
        for cluster in q["clusters"]:
            per_cluster.setdefault(cluster, []).append(score)
        precisions.append(sum(g >= 2 for g in gains) / K)
        audience_bad.extend(audience_relation(q["audience"], labels[c]["audience"]) == "none" for c in top)
        form_bad.extend(FORM_FAMILY[q["form"]] != FORM_FAMILY[labels[c]["form"]] for c in top)
        covered.append(len(top) == K)
        diversity.append(len({labels[c]["author"] for c in top}))
        others = [c for c in ids if c != qid]
        if q["series"]:
            series_id, tome = q["series"][0], q["series"][1]
            in_series = lambda c: bool(labels[c]["series"]) and labels[c]["series"][0] == series_id  # noqa: E731
            is_next = lambda c: bool(labels[c]["series"]) and tuple(labels[c]["series"]) == (series_id, tome + 1)  # noqa: E731
            if any(in_series(c) and not is_leak(q, labels[c]) for c in others):
                series_hits.append(any(in_series(c) and not is_leak(q, labels[c]) for c in top))
            if any(is_next(c) for c in others):
                next_hits.append(any(is_next(c) for c in top))
        if any(is_leak(q, labels[c]) for c in others):
            leaks.append(any(is_leak(q, labels[c]) for c in top))

    def mean(values):
        return sum(values) / len(values) if values else 0.0

    return MethodScore(
        code=code,
        ndcg=mean(ndcgs),
        precision=mean(precisions),
        series_hit=mean(series_hits) if series_hits else None,
        next_tome_hit=mean(next_hits) if next_hits else None,
        leak_rate=mean(leaks) if leaks else None,
        audience_mismatch=mean(audience_bad),
        form_mismatch=mean(form_bad),
        coverage=mean(covered),
        diversity=mean(diversity),
        per_cluster={k: mean(v) for k, v in per_cluster.items()},
        per_query=per_query,
    )
