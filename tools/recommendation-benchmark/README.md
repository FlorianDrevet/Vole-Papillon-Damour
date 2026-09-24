# Benchmark des méthodes de livres proches

Outil jetable d'aide à la décision pour les recommandations de livres
(`docs/recommandations-livres/`). Il compare plusieurs façons de calculer « les livres les
plus proches » sur un corpus réel, noté par une vérité de référence annotée à la main.

Protocole, lecture des résultats et analyse : `docs/recommandations-livres/05-benchmark.md`.
Accès Azure OpenAI : `docs/recommandations-livres/06-tutoriel-foundry-embeddings.md`.

## Installation

```powershell
cd tools/recommendation-benchmark
python -m venv .venv
.\.venv\Scripts\python -m pip install -r requirements.txt
```

## Exécution

| Commande | Effet | Coût |
|---|---|---|
| `.\.venv\Scripts\python -m bench.build_corpus` | Interroge la BnF et Open Library, écrit `data/corpus.json` et `data/journal-constitution.md`. Déjà fait : le corpus est versionné | Gratuit, ~5 min la première fois, puis cache |
| `.\.venv\Scripts\python -m bench.check` | Vérifie l'accès Azure OpenAI | < 0,0001 $ |
| `.\.venv\Scripts\python -m bench.run --provider azure` | **Le benchmark** : toutes les méthodes, rapport `out/rapport-azure.md` | ≈ 0,002 $ |
| `.\.venv\Scripts\python -m bench.run --provider none` | Seulement les méthodes sans IA | Gratuit |
| `.\.venv\Scripts\python -m bench.run --provider fake` | Contrôle de la chaîne sans réseau (chiffres non significatifs) | Gratuit |
| `.\.venv\Scripts\python -m unittest discover -s tests` | Tests de la grille de notation | Gratuit |

Les embeddings sont mis en cache (`cache/`) : relancer ne repaie rien.

## Organisation

| Fichier | Rôle |
|---|---|
| `bench/corpus_spec.py` | **La vérité de référence** : 158 œuvres annotées (public, forme, familles, séries). Modifiable |
| `bench/sources.py` | Accès BnF (SRU, UNIMARC) et Open Library, avec cache |
| `bench/build_corpus.py` | Choix des éditions, résumé d'œuvre, journal de constitution |
| `bench/features.py` | Ce que les méthodes ont le droit de savoir : tout est **déduit des notices** |
| `bench/embeddings.py` | Azure OpenAI (Entra ID) ou vecteurs factices, cache, comptage des tokens |
| `bench/methods.py` | Les 11 méthodes en concurrence |
| `bench/metrics.py` | Grille de pertinence et indicateurs |
| `bench/report.py` | Rapport Markdown |

## Ajouter une méthode

Écrire une fonction `(features, vectors) -> {id: [(id_voisin, score), …]}` dans
`bench/methods.py` et l'ajouter à `METHODS`. Elle apparaît dans le rapport.
