---
name: graphify-corpus
description: "Use when: corpus-level questions, documentation graph, architecture overview from docs+code, onboarding orientation, audit context, cross-file conceptual links, god nodes, community detection, surprising connections, diagram-to-code traceability."
---

# Skill : graphify-corpus - Graphe de connaissance corpus

> Charger ce skill pour toute tache necessitant une vue transversale entre code et documentation,
> une orientation architecturale rapide, une analyse de communautes, ou une exploration de liens
> conceptuels qui depassent le graphe de code pur.

## Perimetre

Graphify fournit une vue corpus reliant le code, la documentation, les audits et les
diagrammes. Il est principalement utilise pour l'orientation architecturale, les
communautes conceptuelles et les connexions entre fichiers. Il complete les tests,
la revue de code et l'inspection directe des sources ; il ne les remplace pas.

## Pre-requis

1. Graphify installe : `python -m graphify` doit fonctionner (`pip install graphifyy` si besoin)
2. Graphe initial construit :
   `python -c "from pathlib import Path; from graphify.watch import _rebuild_code; import sys; ok = _rebuild_code(Path('.')); sys.exit(0 if ok else 1)"`
3. Les fichiers `graphify-out/graph.json` et `graphify-out/GRAPH_REPORT.md` existent
   et sont a jour pour les requetes structurelles
4. Le `.graphifyignore` a la racine exclut les sorties build et dependances
5. Pour `python -m graphify.serve`, le package `mcp` doit etre installe

## Commandes principales

| Commande | Usage |
|----------|-------|
| `python -m graphify query "concept"` | Trouver les noeuds lies a un concept |
| `python -m graphify path "A" "B"` | Chemin entre deux concepts |
| `python -m graphify explain "node"` | Explication contextuelle d'un noeud |
| `python -m graphify update .` | Mettre a jour le graphe |
| `python -m graphify cluster-only .` | Recalculer les communautes a partir du graphe existant |
| `python -m graphify watch .` | Rebuild local lors des changements de code |

## Sorties cles

| Fichier | Contenu |
|---------|---------|
| `graphify-out/graph.json` | Graphe complet (noeuds + aretes) |
| `graphify-out/GRAPH_REPORT.md` | God nodes, communautes, connexions surprenantes |
| `graphify-out/wiki/index.md` | Wiki corpus optionnel, navigable par les agents |

## Quand utiliser Graphify

- Comprendre comment la doc, les audits, les diagrammes et le code se relient
- Identifier les god nodes (concepts qui connectent beaucoup de fichiers)
- Decouvrir des communautes conceptuelles (groupes de fichiers fortement lies)
- Orienter un nouveau contributeur sur l'architecture globale
- Tracer un concept depuis la documentation jusqu'au code source
- Verifier la couverture documentaire d'une zone de code

## Quand ne pas utiliser Graphify

- Pour une modification locale qui ne demande aucune exploration transversale
- Pour remplacer les tests, la revue de code ou la validation de compilation
- Lorsque le graphe n'est pas construit ou que ses donnees sont obsoletes

## Workflow obligatoire

1. Lire `graphify-out/GRAPH_REPORT.md` avant toute question d'architecture ou de corpus.
2. Utiliser `query`, `path`, `explain` ou les outils MCP pour trouver les concepts et liens pertinents.
3. Inspecter ensuite les fichiers sources concernes avant toute modification.
4. Apres une modification significative du code, executer `python -m graphify update .` puis rejouer la requete utile.
5. Executer les tests/build de la surface touchee separement : le graphe ne prouve pas la correction.

Si le graphe est absent ou obsolete, le signaler, le construire ou le mettre a jour
quand c'est possible, puis revenir a l'exploration directe des sources.

Le serveur MCP stdio se lance avec :

```powershell
python -m graphify.serve graphify-out/graph.json
```

Il expose `query_graph`, `get_node`, `get_neighbors`, `get_community`, `god_nodes`,
`graph_stats` et `shortest_path`. La declaration workspace se trouve dans
`.vscode/mcp.json`.

## Integration multi-agents

Les instructions persistantes du depot sont dans `AGENTS.md`, `CLAUDE.md` et
`.github/copilot-instructions.md`. Les skills utilisateur peuvent etre synchronises
avec le package Graphify :

```powershell
graphify install --platform codex
graphify install --platform claude
graphify install --platform copilot
```

Pour un nouveau checkout, utiliser `graphify codex install`, `graphify claude install`
et `graphify vscode install`. Ces commandes doivent conserver les hooks et serveurs
MCP existants.

## Bootstrap expectations

- Le bootstrap doit declarer un serveur `graphify` dans `.vscode/mcp.json` si le projet l'utilise
- Le bootstrap doit creer un `.graphifyignore` adapte
- Le bootstrap doit executer le build initial du graphe avec le builder code-only
- Le bootstrap doit documenter la commande de mise a jour dans la memoire
