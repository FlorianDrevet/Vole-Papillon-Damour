"""Rédaction du rapport Markdown à partir des résultats."""

from datetime import datetime

from .embeddings import PRICE_PER_MILLION_TOKENS_USD, Usage
from .features import Features, same_work
from .metrics import FORM_FAMILY, K, MethodScore, grade, is_leak

SHOWCASE = [
    "hp1#1", "percy1#1", "1984#1", "1984bd#1", "millenium1#1", "germinal#1", "etranger#1", "petitprince#1",
    "dune#1", "asterix1#1", "onepiece1#1", "clans1#1", "sapiens#1", "deuxiemesexe#1", "jesaiscuisiner#1",
    "herisson#1",
]
SHOWCASE_METHODS = ["E3", "E4", "H4", "H6"]
MARKS = {3: "★★★", 2: "★★", 1: "★", 0: "✗"}
PROJECTED_BOOKS = 20_000


def _pct(value) -> str:
    return "—" if value is None else f"{100 * value:.0f} %"


def _num(value) -> str:
    return f"{value:.3f}"


def render(provider: str, deployment: str | None, corpus: list[dict], features: list[Features],
           labels: dict[str, dict], methods, scores: dict[str, MethodScore], rankings: dict,
           usage: Usage | None, timings: dict[str, float]) -> str:
    by_id = {f.id: f for f in features}
    titles = {e["id"]: f"{e['bnf']['title']} — {e['label']['author']}" for e in corpus}
    lines: list[str] = []
    add = lines.append

    add("# Benchmark des méthodes de livres proches — résultats")
    add("")
    add(f"Généré le {datetime.now():%Y-%m-%d %H:%M} par `python -m bench.run --provider {provider}`.")
    if provider == "azure":
        add(f"Embeddings : Azure OpenAI, déploiement `{deployment}` (text-embedding-3-small).")
    elif provider == "fake":
        add("")
        add("> **⚠ Exécution de contrôle.** Les embeddings sont des vecteurs de hachage de mots, pas un modèle "
            "sémantique. Ce rapport vérifie la chaîne de traitement ; **ses chiffres E\\*, F\\*, H\\* ne mesurent "
            "rien**. Relancer avec `--provider azure`.")
    else:
        add("")
        add("> **Exécution sans embeddings.** Seules les méthodes sans IA (R0, L1) sont évaluées. "
            "Relancer avec `--provider azure` pour les autres.")
    add("")

    # 1. Corpus ------------------------------------------------------------------
    n = len(corpus)
    works = len({e["work_key"] for e in corpus})
    def count(predicate):
        c = sum(1 for e in corpus if predicate(e))
        return f"{c} ({100 * c // n} %)"
    add("## 1. Le corpus")
    add("")
    add(f"**{n} éditions réelles de {works} œuvres**, notices BnF et Open Library récupérées le "
        f"{datetime.now():%d/%m/%Y}. Chaque édition sert à son tour de livre de départ : "
        f"{n} requêtes, {K} voisins demandés.")
    add("")
    add("| Donnée disponible | Éditions |")
    add("|---|---|")
    add(f"| Résumé propre à l'édition (BnF 330) | {count(lambda e: e['summary_edition'])} |")
    add(f"| Résumé de l'œuvre, toutes éditions confondues | {count(lambda e: e['summary_work'])} |")
    add(f"| … dont venu de la BnF | {count(lambda e: e['summary_work_source'] == 'bnf-oeuvre')} |")
    add(f"| … dont venu d'Open Library | {count(lambda e: e['summary_work_source'] == 'openlibrary')} |")
    add(f"| Collection (225) | {count(lambda e: e['bnf']['collection'])} |")
    add(f"| Série et tome (461) | {count(lambda e: e['bnf']['series_title'])} |")
    add(f"| Identifiant d'œuvre BnF (500) | {count(lambda e: e['bnf']['bnf_work_id'])} |")
    add(f"| Identifiant d'œuvre Open Library | {count(lambda e: (e['openlibrary'] or {}).get('work_key'))} |")
    add(f"| Mention CNLJ (livre jeunesse évalué) | {count(lambda e: e['bnf']['cnlj'])} |")
    add(f"| Public explicite (333) | {count(lambda e: e['bnf']['audience_333'])} |")
    add("")

    # 2. Déductions ----------------------------------------------------------------
    add("## 2. Ce que les méthodes devinent seules")
    add("")
    add("Les filtres et bonus reposent sur des informations **déduites des notices**. Leur justesse, "
        "comparée aux étiquettes :")
    add("")
    audience_ok = sum(
        1 for f in features
        if labels[f.id]["audience"] == "tout-public"
        or f.audience == labels[f.id]["audience"]
        or (f.audience == "jeunesse" and labels[f.id]["audience"] == "ado"))
    form_ok = sum(1 for f in features if f.form_family == FORM_FAMILY[labels[f.id]["form"]]
                  or (f.form_family == "essai" and labels[f.id]["form"] == "pratique"))
    series_labeled = [f for f in features if labels[f.id]["series"]]
    series_found = sum(1 for f in series_labeled if f.series_key)
    pairs = [(a, b) for i, a in enumerate(features) for b in features[i + 1:]]
    true_same = [(a, b) for a, b in pairs if labels[a.id]["key"] == labels[b.id]["key"]]
    detected = sum(1 for a, b in true_same if same_work(a, b))
    false_merge = [(a, b) for a, b in pairs if labels[a.id]["key"] != labels[b.id]["key"] and same_work(a, b)]
    add("| Déduction | Justesse |")
    add("|---|---|")
    add(f"| Public (jeunesse / adulte) | {audience_ok}/{n} ({100 * audience_ok // n} %) |")
    add(f"| Forme (texte / illustré / essai) | {form_ok}/{n} ({100 * form_ok // n} %) |")
    add(f"| Série reconnue, parmi les livres de série | {series_found}/{len(series_labeled)} |")
    add(f"| Éditions d'une même œuvre reconnues comme telles | {detected}/{len(true_same)} paires |")
    add(f"| Œuvres différentes confondues à tort | {len(false_merge)} paires |")
    if false_merge:
        add("")
        add("Confusions : " + " ; ".join(f"{titles[a.id]} / {titles[b.id]}" for a, b in false_merge[:6]))
    add("")

    # 3. Résultats ---------------------------------------------------------------------
    add("## 3. Résultats par méthode")
    add("")
    add("Classement par nDCG@5. Lecture : plus c'est haut, mieux c'est, sauf pour les trois colonnes "
        "d'erreurs (fuite, public, forme).")
    add("")
    add("| Méthode | nDCG@5 | Précision@5 | Série@5 | Tome suivant@5 | Fuite même œuvre ↓ | Jeunesse↔adulte ↓ "
        "| Autre forme ↓ | Couverture | Auteurs distincts | Calcul |")
    add("|---|---|---|---|---|---|---|---|---|---|---|")
    ordered = sorted(scores.values(), key=lambda s: s.ndcg, reverse=True)
    labels_by_code = {m.code: m.label for m in methods}
    for s in ordered:
        add(f"| **{s.code}** {labels_by_code[s.code]} | {_num(s.ndcg)} | {_pct(s.precision)} | {_pct(s.series_hit)} "
            f"| {_pct(s.next_tome_hit)} | {_pct(s.leak_rate)} | {_pct(s.audience_mismatch)} | {_pct(s.form_mismatch)} "
            f"| {_pct(s.coverage)} | {s.diversity:.1f} | {timings.get(s.code, 0):.2f} s |")
    add("")
    add("<details><summary>Définition des indicateurs</summary>")
    add("")
    add("- **nDCG@5** : qualité du classement des 5 voisins (1 = parfait), avec la grille : même série 3, "
        "même famille + public + forme 2, lien lâche 1, sans rapport 0.")
    add("- **Précision@5** : part des 5 voisins notés au moins 2.")
    add("- **Série@5** : pour un livre dont un autre tome est au corpus, part des cas où un tome de la série "
        "est proposé. **Tome suivant@5** : idem pour le tome n+1 exactement.")
    add("- **Fuite même œuvre** : pour une œuvre présente en deux éditions, part des cas où l'autre édition "
        "est proposée. C'est une erreur : ce n'est pas une recommandation.")
    add("- **Jeunesse↔adulte** : part des voisins d'un public opposé. **Autre forme** : BD proposée sous un "
        "roman, ou l'inverse (indicatif : ce n'est pas toujours faux).")
    add("- **Couverture** : part des livres qui obtiennent 5 voisins. **Auteurs distincts** : diversité du top 5.")
    add("")
    add("</details>")
    add("")
    for method in methods:
        if method.code in scores:
            add(f"- **{method.code}** — {method.description}")
    add("")

    # 4. Par famille -----------------------------------------------------------------
    focus = [c for c in ("R0", "L1", "E3", "E4", "H4", "H6") if c in scores]
    add("## 4. Par famille thématique (nDCG@5)")
    add("")
    add("| Famille | " + " | ".join(focus) + " |")
    add("|---|" + "---|" * len(focus))
    clusters = sorted({c for s in scores.values() for c in s.per_cluster})
    for cluster in clusters:
        add(f"| {cluster} | " + " | ".join(_num(scores[c].per_cluster.get(cluster, 0)) for c in focus) + " |")
    add("")

    # 5. Exemples ------------------------------------------------------------------
    shown = [c for c in SHOWCASE_METHODS if c in scores]
    add("## 5. Exemples côte à côte")
    add("")
    add("Notes : ★★★ même série · ★★ proche · ★ lien lâche · ✗ sans rapport · ⚠ autre édition du même livre.")
    add("")
    for qid in SHOWCASE:
        if qid not in by_id:
            continue
        add(f"### {titles[qid]}")
        add("")
        add("| # | " + " | ".join(shown) + " |")
        add("|---|" + "---|" * len(shown))
        columns = []
        for code in shown:
            cells = []
            for cid, _ in rankings[code].get(qid, [])[:K]:
                mark = "⚠" if is_leak(labels[qid], labels[cid]) else MARKS[grade(labels[qid], labels[cid])]
                cells.append(f"{mark} {titles[cid]}")
            columns.append(cells + [""] * (K - len(cells)))
        for rank in range(K):
            add(f"| {rank + 1} | " + " | ".join(col[rank] for col in columns) + " |")
        add("")

    # 6. Échecs du candidat ------------------------------------------------------------
    candidate = next((c for c in ("H6", "H4") if c in scores), ordered[0].code)
    add(f"## 6. Les dix livres où {candidate} se trompe le plus")
    add("")
    add("| Livre | nDCG@5 | Trois premiers voisins |")
    add("|---|---|---|")
    worst = sorted(scores[candidate].per_query.items(), key=lambda item: item[1])[:10]
    for qid, value in worst:
        neighbours = " ; ".join(
            f"{MARKS[grade(labels[qid], labels[cid])]} {titles[cid]}" for cid, _ in rankings[candidate].get(qid, [])[:3])
        add(f"| {titles[qid]} | {_num(value)} | {neighbours} |")
    add("")

    # 7. Coût ----------------------------------------------------------------------------
    add("## 7. Coût")
    add("")
    if usage is None:
        add("Aucun appel d'IA dans cette exécution.")
    else:
        note = " (estimation : 1 token ≈ 4 caractères)" if usage.estimated else " (compté par l'API)"
        add(f"Tokens envoyés pour ce corpus{note} : **{usage.tokens:,}**, soit **{usage.cost_usd:.5f} $** "
            f"à {PRICE_PER_MILLION_TOKENS_USD} $ par million. {usage.requests} requête(s), "
            f"{usage.seconds:.1f} s.".replace(",", " "))
        add("")
        add("| Texte vectorisé | Tokens (textes uniques) | Tokens moyens par livre | Projection 20 000 livres |")
        add("|---|---|---|---|")
        for variant, tokens in usage.tokens_by_variant.items():
            per_book = tokens / n
            projected = per_book * PROJECTED_BOOKS / 1_000_000 * PRICE_PER_MILLION_TOKENS_USD
            add(f"| {variant} | {tokens:,} | {per_book:.0f} | {projected:.3f} $ |".replace(",", " "))
    add("")
    add("Stockage des vecteurs pour 20 000 livres (float32) : 1536 dimensions ≈ "
        f"{PROJECTED_BOOKS * 1536 * 4 / 1e6:.0f} Mo · 512 ≈ {PROJECTED_BOOKS * 512 * 4 / 1e6:.0f} Mo · "
        f"256 ≈ {PROJECTED_BOOKS * 256 * 4 / 1e6:.0f} Mo.")
    add("")

    # 8. Limites -------------------------------------------------------------------
    add("## 8. Limites du protocole")
    add("")
    add("- **La vérité de référence est un jugement éditorial** (`bench/corpus_spec.py`) : familles, publics "
        "et séries sont posés à la main. Elle est lisible et modifiable ; les méthodes n'y ont pas accès.")
    add("- **Le corpus est fait de livres connus.** Les métadonnées y sont meilleures qu'en moyenne ; un "
        "catalogue réel de bourse aura moins de résumés.")
    add("- **Corpus de 165 éditions.** Dans un catalogue de 20 000 livres, il y aura beaucoup plus de "
        "voisins plausibles, et les écarts entre méthodes se creuseront plutôt qu'ils ne se réduiront.")
    add("- **Pas de signal de comportement** (co-achats, co-sélection) : il n'existe pas encore.")
    add("")
    return "\n".join(lines) + "\n"
