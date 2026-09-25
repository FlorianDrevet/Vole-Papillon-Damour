"""Vérifie l'accès Azure OpenAI avant de lancer le benchmark.

    python -m bench.check
"""

from .embeddings import PRICE_PER_MILLION_TOKENS_USD, make_provider


def main() -> None:
    provider = make_provider("azure")
    print(f"Déploiement : {provider.deployment}")
    vectors, tokens = provider.embed(["Le Petit Prince. Antoine de Saint-Exupéry."])
    print(f"OK : un vecteur de {len(vectors[0])} dimensions, {sum(tokens)} tokens "
          f"({sum(tokens) / 1e6 * PRICE_PER_MILLION_TOKENS_USD:.8f} $).")


if __name__ == "__main__":
    main()
