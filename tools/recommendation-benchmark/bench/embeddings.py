"""Calcul des embeddings, avec cache disque.

Fournisseurs :
- azure : Azure OpenAI (Foundry), modèle text-embedding-3-small, endpoint /openai/v1/.
          Authentification Microsoft Entra ID (az login) par défaut, clé d'API en option.
- fake  : vecteurs par hachage de mots, sans réseau. Sert à vérifier la chaîne de
          traitement ; ses résultats ne mesurent RIEN de sémantique.

Le cache (cache/embeddings-<fournisseur>.npz) évite de repayer un texte déjà vectorisé.
"""

import hashlib
import os
import re
import time
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np

from .text import normalize

ROOT = Path(__file__).resolve().parent.parent
FULL_DIMENSIONS = 1536
BATCH_SIZE = 100
PRICE_PER_MILLION_TOKENS_USD = 0.02  # text-embedding-3-small, Azure, septembre 2026


def load_local_env() -> None:
    """Charge .env.local (clé=valeur) sans écraser les variables déjà définies."""
    path = ROOT / ".env.local"
    if not path.exists():
        return
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        os.environ.setdefault(key.strip(), value.strip().strip('"'))


@dataclass
class Usage:
    requests: int = 0
    tokens: int = 0
    tokens_by_variant: dict[str, int] = field(default_factory=dict)
    seconds: float = 0.0
    estimated: bool = False

    @property
    def cost_usd(self) -> float:
        return self.tokens / 1_000_000 * PRICE_PER_MILLION_TOKENS_USD


class EmbeddingStore:
    def __init__(self, provider: str):
        self.provider = provider
        self.path = ROOT / "cache" / f"embeddings-{provider}.npz"
        self.vectors: dict[str, np.ndarray] = {}
        self.tokens: dict[str, int] = {}
        if self.path.exists():
            data = np.load(self.path)
            self.vectors = {k: data[k] for k in data.files if not k.startswith("tok_")}
            self.tokens = {k[4:]: int(data[k]) for k in data.files if k.startswith("tok_")}

    def save(self) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        arrays = dict(self.vectors)
        arrays.update({f"tok_{k}": np.array(v) for k, v in self.tokens.items()})
        np.savez_compressed(self.path, **arrays)


def text_key(text: str) -> str:
    return hashlib.sha1(text.encode("utf-8")).hexdigest()


class FakeProvider:
    name = "fake"

    def embed(self, texts: list[str]) -> tuple[list[np.ndarray], list[int]]:
        vectors, tokens = [], []
        for text in texts:
            words = normalize(text).split()
            vector = np.zeros(FULL_DIMENSIONS, dtype=np.float32)
            for word in words + [f"{a}_{b}" for a, b in zip(words, words[1:])]:
                digest = hashlib.sha1(word.encode("utf-8")).digest()
                index = int.from_bytes(digest[:4], "little") % FULL_DIMENSIONS
                vector[index] += 1.0 if digest[4] % 2 else -1.0
            vectors.append(vector)
            tokens.append(max(1, len(text) // 4))
        return vectors, tokens


class AzureProvider:
    name = "azure"

    def __init__(self):
        from openai import OpenAI

        endpoint = os.environ.get("AZURE_OPENAI_ENDPOINT", "").strip()
        self.deployment = os.environ.get("AZURE_OPENAI_EMBEDDING_DEPLOYMENT", "text-embedding-3-small").strip()
        if not endpoint:
            raise SystemExit(
                "AZURE_OPENAI_ENDPOINT manquant. Renseigner .env.local (voir .env.example "
                "et docs/recommandations-livres/06-tutoriel-foundry-embeddings.md).")
        base_url = re.sub(r"/+$", "", endpoint)
        if not base_url.endswith("/openai/v1"):
            base_url += "/openai/v1"
        api_key = os.environ.get("AZURE_OPENAI_API_KEY", "").strip()
        if not api_key:
            from azure.identity import AzureCliCredential, get_bearer_token_provider

            scope = os.environ.get("AZURE_OPENAI_TOKEN_SCOPE", "https://cognitiveservices.azure.com/.default")
            api_key = get_bearer_token_provider(AzureCliCredential(), scope)
        self.client = OpenAI(base_url=base_url + "/", api_key=api_key, max_retries=6)

    def embed(self, texts: list[str]) -> tuple[list[np.ndarray], list[int]]:
        response = self.client.embeddings.create(model=self.deployment, input=texts)
        ordered = sorted(response.data, key=lambda item: item.index)
        vectors = [np.asarray(item.embedding, dtype=np.float32) for item in ordered]
        # L'API ne détaille pas les tokens par texte : on répartit au prorata des longueurs.
        total = response.usage.prompt_tokens
        lengths = [max(1, len(t)) for t in texts]
        tokens = [round(total * length / sum(lengths)) for length in lengths]
        return vectors, tokens


def make_provider(name: str):
    if name == "azure":
        load_local_env()
        return AzureProvider()
    if name == "fake":
        return FakeProvider()
    raise ValueError(f"fournisseur inconnu : {name}")


def embed_all(provider, texts_by_variant: dict[str, list[str]]) -> tuple[dict[str, np.ndarray], Usage]:
    """Vectorise chaque variante de texte ; renvoie des matrices (n, 1536) normalisées."""
    store = EmbeddingStore(provider.name)
    usage = Usage(estimated=provider.name != "azure")
    pending = sorted({t for texts in texts_by_variant.values() for t in texts if text_key(t) not in store.vectors})
    started = time.perf_counter()
    for start in range(0, len(pending), BATCH_SIZE):
        batch = pending[start:start + BATCH_SIZE]
        vectors, tokens = provider.embed(batch)
        usage.requests += 1
        for text, vector, count in zip(batch, vectors, tokens):
            store.vectors[text_key(text)] = vector
            store.tokens[text_key(text)] = count
        print(f"  embeddings : {min(start + BATCH_SIZE, len(pending))}/{len(pending)}", flush=True)
    usage.seconds = time.perf_counter() - started
    if pending:
        store.save()

    matrices = {}
    for variant, texts in texts_by_variant.items():
        matrix = np.stack([store.vectors[text_key(t)] for t in texts])
        matrices[variant] = matrix / np.linalg.norm(matrix, axis=1, keepdims=True).clip(min=1e-12)
        variant_tokens = sum(store.tokens.get(text_key(t), 0) for t in set(texts))
        usage.tokens_by_variant[variant] = variant_tokens
    usage.tokens = sum(store.tokens.get(text_key(t), 0) for t in {t for ts in texts_by_variant.values() for t in ts})
    return matrices, usage


def truncate(matrix: np.ndarray, dimensions: int) -> np.ndarray:
    """Réduction de dimension de text-embedding-3 : tronquer puis renormaliser."""
    reduced = matrix[:, :dimensions]
    return reduced / np.linalg.norm(reduced, axis=1, keepdims=True).clip(min=1e-12)
