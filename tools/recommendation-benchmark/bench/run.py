"""Étape 2 : calculer les voisins avec chaque méthode, les évaluer, écrire le rapport.

    python -m bench.run --provider azure   # le vrai benchmark (Azure OpenAI)
    python -m bench.run --provider none    # méthodes sans IA seulement
    python -m bench.run --provider fake    # contrôle de la chaîne, sans réseau
"""

import argparse
import json
import os
import time
from pathlib import Path

from .embeddings import embed_all, make_provider, truncate
from .features import build_features
from .methods import METHODS
from .metrics import evaluate
from .report import render

ROOT = Path(__file__).resolve().parent.parent


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--provider", choices=["azure", "none", "fake"], default="azure")
    args = parser.parse_args()

    corpus = json.loads((ROOT / "data" / "corpus.json").read_text(encoding="utf-8"))
    features = [build_features(entry) for entry in corpus]
    labels = {}
    for entry in corpus:
        label = dict(entry["label"])
        label["series"] = tuple(label["series"]) if label["series"] else None
        labels[entry["id"]] = label

    vectors, usage, deployment = {}, None, None
    if args.provider != "none":
        provider = make_provider(args.provider)
        deployment = getattr(provider, "deployment", None)
        texts = {
            "titre_auteur": [f.texts["titre_auteur"] for f in features],
            "resume_edition": [f.texts["resume_edition"] or f.texts["titre_auteur"] for f in features],
            "resume_oeuvre": [f.texts["resume_oeuvre"] or f.texts["titre_auteur"] for f in features],
            "compose": [f.texts["compose"] for f in features],
        }
        print(f"Embeddings ({args.provider})…", flush=True)
        vectors, usage = embed_all(provider, texts)
        vectors["compose@512"] = truncate(vectors["compose"], 512)
        vectors["compose@256"] = truncate(vectors["compose"], 256)

    scores, rankings, timings = {}, {}, {}
    for method in METHODS:
        if method.needs_embeddings and not vectors:
            continue
        started = time.perf_counter()
        rankings[method.code] = method.run(features, vectors)
        timings[method.code] = time.perf_counter() - started
        scores[method.code] = evaluate(method.code, rankings[method.code], labels)
        print(f"{method.code:7} nDCG@5={scores[method.code].ndcg:.3f}", flush=True)

    out = ROOT / "out"
    out.mkdir(exist_ok=True)
    report = render(args.provider, deployment, corpus, features, labels, METHODS, scores, rankings, usage, timings)
    report_path = out / f"rapport-{args.provider}.md"
    report_path.write_text(report, encoding="utf-8")
    (out / f"voisins-{args.provider}.json").write_text(
        json.dumps(rankings, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\nRapport : {report_path.relative_to(ROOT)}")


if __name__ == "__main__":
    os.environ.setdefault("PYTHONIOENCODING", "utf-8")
    main()
