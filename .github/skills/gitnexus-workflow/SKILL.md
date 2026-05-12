# Skill : gitnexus-workflow

Charger ce skill pour toute tache necessitant exploration structurelle, analyse d'impact, validation post-changement, ou refactoring.

---

## Quand utiliser GitNexus

- Tu dois savoir **qui appelle** un handler, un service, ou une interface
- Tu dois connaitre le **blast radius** d'un changement avant de le faire
- Tu veux **renommer un symbole** de facon safe (graphe + texte)
- Tu veux **valider** qu'un changement n'a pas touche plus de flux que prevu
- Tu dois **tracer un bug** en suivant un execution flow

---

## Workflow recommande

### Exploration

1. `gitnexus_query("concept")` — trouver les processus et symboles pertinents
2. `gitnexus_context("SymbolName")` — vue 360° d'un symbole (appelants, appeles, processus)
3. Lecture ciblee des fichiers identifies

### Impact avant modification

1. `gitnexus_impact(target: "SymbolName", direction: "upstream")` — blast radius
2. Mettre a jour les dependants d=1 (WILL BREAK)
3. Signaler les risques HIGH ou CRITICAL avant edition

### Validation apres modification

1. `gitnexus_detect_changes()` — verifier que seuls les flux attendus sont touches
2. Si des flux inattendus apparaissent, investiguer avant de continuer

### Refactoring safe

1. `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` — preview
2. Revoir les graph edits (safe) et text_search edits (review manual)
3. `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: false})` — appliquer

---

## Niveaux de risque

| Depth | Signification | Action |
|-------|---------------|--------|
| d=1 | WILL BREAK — appelants/importeurs directs | DOIT mettre a jour |
| d=2 | LIKELY AFFECTED — dependances indirectes | Devrait tester |
| d=3 | MAY NEED TESTING — transitif | Tester si chemin critique |

---

## Outils disponibles

| Outil | Usage |
|-------|-------|
| `gitnexus_query({query: "..."})` | Recherche par concept |
| `gitnexus_context({name: "..."})` | Vue complete d'un symbole |
| `gitnexus_impact({target: "...", direction: "upstream"})` | Blast radius |
| `gitnexus_detect_changes({scope: "staged"})` | Validation pre-commit |
| `gitnexus_rename({symbol_name: "...", new_name: "...", dry_run: true})` | Rename safe |
| `gitnexus_cypher({query: "MATCH ..."})` | Requetes custom |

---

## Ressources MCP

| Resource | Usage |
|----------|-------|
| `gitnexus://repo/<name>/context` | Overview du codebase |
| `gitnexus://repo/<name>/clusters` | Zones fonctionnelles |
| `gitnexus://repo/<name>/processes` | Tous les flux d'execution |
| `gitnexus://repo/<name>/process/{name}` | Trace pas a pas |

---

## Bootstrap expectations

- Le bootstrap doit documenter GitNexus dans `AGENTS.md`, `CLAUDE.md`, la doc MCP, et la memoire.
- Le bootstrap doit proposer un serveur `gitnexus` dans `.vscode/mcp.json`.
- L'indexation initiale se fait avec `npx gitnexus analyze`.
- La reindexation apres commit se fait avec `npx gitnexus analyze` (ajouter `--embeddings` si necessaire).

---

## Integration avec les agents

- `@dev` verifie la fraicheur de l'index (> 7 jours = reindexer)
- `@architect` utilise query + context pour comprendre avant de planifier
- `@dotnet-dev` et `@python-dev` utilisent impact avant modification transverse
- `@review-expert` utilise detect_changes pour verifier le scope du diff
- `@dream` utilise detect_changes pour identifier les signaux recents
