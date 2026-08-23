---
name: graphify-corpus
description: "Use when: corpus-level questions, documentation graph, architecture overview from docs+code, onboarding orientation, audit context, cross-file conceptual links, god nodes, community detection, surprising connections, diagram-to-code traceability."
---

# Skill : graphify-corpus - Graphe de connaissance corpus

> Charger ce skill pour toute tache necessitant une vue transversale entre code et documentation,
> une orientation architecturale rapide, une analyse de communautes, ou une exploration de liens
> conceptuels qui depassent le graphe de code pur.

---

## Regle cardinale : GitNexus pour le code, Graphify pour le corpus

Si les deux moteurs sont actives sur le projet :

| Dimension | GitNexus | Graphify |
|-----------|----------|----------|
| **Perimetre** | Code source uniquement | Corpus complet (code + docs + audits + diagrammes) |
| **Force principale** | Impact analysis, blast radius, rename-safe | Communautes conceptuelles, god nodes, connexions surprenantes |
| **Mutations** | `rename()`, `detect_changes()` | Aucune - lecture seule |
| **Docs / images / audits** | Non couvert | Couvert nativement |

**Aucun des deux ne remplace l'autre.**

---

## Pre-requis

1. Graphify installe : `pip install graphifyy` ou `uv tool install graphifyy`
2. Graphe initial construit :
   `python -c "from pathlib import Path; from graphify.watch import _rebuild_code; import sys; ok = _rebuild_code(Path('.')); sys.exit(0 if ok else 1)"`
3. Le fichier `graphify-out/graph.json` existe et est non vide
4. Le `.graphifyignore` a la racine exclut les sorties build et dependances
5. Le serveur MCP Graphify est declare dans `.vscode/mcp.json`
6. Pour `python -m graphify.serve`, le package `mcp` doit etre installe

---

## Commandes principales

| Commande | Usage |
|----------|-------|
| `python -m graphify query "concept"` | Trouver les nœuds lies a un concept |
| `python -m graphify path "A" "B"` | Chemin entre deux concepts |
| `python -m graphify explain "node"` | Explication contextuelle d'un nœud |
| `python -m graphify update .` | Mettre a jour le graphe |

---

## Sorties cles

| Fichier | Contenu |
|---------|---------|
| `graphify-out/graph.json` | Graphe complet (nœuds + aretes) |
| `graphify-out/GRAPH_REPORT.md` | God nodes, communautes, connexions surprenantes |

---

## Quand utiliser Graphify

- Comprendre comment la doc, les audits, les diagrammes et le code se relient
- Identifier les god nodes (concepts qui connectent beaucoup de fichiers)
- Decouvrir des communautes conceptuelles (groupes de fichiers fortement lies)
- Orienter un nouveau contributeur sur l'architecture globale
- Tracer un concept depuis la documentation jusqu'au code source
- Verifier la couverture documentaire d'une zone de code

---

## Quand NE PAS utiliser Graphify

- Pour l'impact analysis avant modification → utiliser GitNexus
- Pour le rename safe → utiliser GitNexus
- Pour le blast radius d'un symbole → utiliser GitNexus
- Pour la validation post-changement → utiliser GitNexus

---

## Integration VS Code controlee

Ne pas lancer `graphify vscode install` automatiquement sur un depot qui a deja une orchestration specifique.

Mode controle recommande :
1. Installer le skill utilisateur Copilot : `python -m graphify copilot install`
2. Garder `.github/copilot-instructions.md` du depot comme source de verite
3. Utiliser `/graphify` explicitement quand une tache justifie la couche corpus

---

## Bootstrap expectations

- Le bootstrap doit declarer un serveur `graphify` dans `.vscode/mcp.json`
- Le bootstrap doit creer un `.graphifyignore` adapte
- Le bootstrap doit executer le build initial du graphe
- Le bootstrap doit documenter la commande de mise a jour dans la memoire
