# Code Graph Intelligence

Ce socle prepare les projets a utiliser un moteur d'intelligence code comme couche standard de comprehension structurelle.

Le choix du moteur est fait au bootstrap :
- **GitNexus** pour les projets open-source, code-first
- **Graphify** pour les projets entreprise avec un corpus riche (docs+code+audits)
- **Les deux** pour les projets tres riches

## GitNexus — Workflow recommande

1. `gitnexus_query()` pour trouver les processus et symboles pertinents
2. `gitnexus_context()` pour comprendre un symbole cible
3. `gitnexus_impact()` avant refactorisation ou modification transverse
4. `gitnexus_detect_changes()` apres implementation
5. `gitnexus_rename()` pour les renommages safe

## Graphify — Workflow recommande

1. `graphify query "concept"` pour trouver les nœuds pertinents
2. `graphify path "A" "B"` pour tracer les chemins entre concepts
3. `graphify explain "node"` pour comprendre un nœud en contexte
4. Lire `graphify-out/GRAPH_REPORT.md` pour les god nodes et communautes

## Always Do

- Utiliser le code graph en premier pour l'exploration structurelle
- Executer l'impact analysis avant modification transverse
- Valider le scope des changements apres implementation
- Mettre a jour la memoire thematique apres chaque tache non triviale

## Never Do

- Ne jamais editer un symbole partage sans impact analysis
- Ne jamais ignorer les avertissements HIGH ou CRITICAL
- Ne jamais renommer avec find-and-replace (utiliser gitnexus_rename ou le rename IDE)
- Ne jamais committer sans verifier le scope des changements

## A integrer par bootstrap

- memoire thematique dans `.github/memory/`
- skill adapte (`.github/skills/gitnexus-workflow/SKILL.md` ou `.github/skills/graphify-corpus/SKILL.md`)
- serveur MCP dans `.vscode/mcp.json`
