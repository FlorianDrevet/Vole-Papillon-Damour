# Copilot Instructions

## Getting Started

> Premiere utilisation ? Lancez `@memory-bootstrap` pour explorer le projet, initialiser la memoire thematique, generer les agents et skills adaptes, et preparer la configuration MCP workspace.

Ce fichier reste leger. La connaissance detaillee vit dans `MEMORY.md`, dans `.github/memory/`, et dans les agents/skills generes automatiquement par `memory-bootstrap`.

## Environnement de developpement

> L'utilisateur travaille sur **Windows**. Toutes les commandes terminal doivent utiliser la syntaxe **PowerShell** (`pwsh`). Utiliser `.\ ` pour les chemins relatifs, `;` comme separateur de commandes, `$env:` pour les variables d'environnement. Ne jamais suggerer de commandes bash/sh.

## Agents de base

| Agent | Role | File |
|-------|------|------|
| `dev` | Orchestrateur principal — lit la memoire, route vers les specialistes, charge les skills | `.github/agents/dev.agent.md` |
| `memory-bootstrap` | Explore le projet, initialise la memoire thematique, cree/adapte les agents et skills, met a jour la config MCP | `.github/agents/memory-bootstrap.agent.md` |
| `architect` | Analyse d'architecture et plan d'implementation | `.github/agents/architect.agent.md` |
| `dotnet-dev` | Expert backend .NET, genere seulement si un backend .NET est detecte | `.github/agents/dotnet-dev.agent.md` |
| `python-dev` | Expert backend Python, genere seulement si un backend Python est detecte | `.github/agents/python-dev.agent.md` |
| `documentation-professor` | Documentation technique, onboarding, pedagogie | `.github/agents/documentation-professor.agent.md` |
| `review-expert` | Revue pre-merge, gate qualite | `.github/agents/review-expert.agent.md` |
| `vibe-coding-refractaire` | Revue anti-vibe coding, chasse aux code smells | `.github/agents/vibe-coding-refractaire.agent.md` |
| `review-remediator` | Applique un backlog de correction de review | `.github/agents/review-remediator.agent.md` |
| `audit-expert` | Audits techniques avec reconciliation GitHub | `.github/agents/audit-expert.agent.md` |
| `dream` | Consolidation periodique de la memoire | `.github/agents/dream.agent.md` |
| `merge-main` | Merge de `main` avec resolution semantique | `.github/agents/merge-main.agent.md` |
| `pr-manager` | Conventions de Pull Request | `.github/agents/pr-manager.agent.md` |
| `memory` | Agent deprecie, redirige vers `dev` | `.github/agents/memory.agent.md` |

## Skills de base

| Skill | Role | File |
|-------|------|------|
| `memory-management` | Regles de mise a jour de `MEMORY.md` et de `.github/memory/` | `.github/skills/memory-management/SKILL.md` |
| `gitnexus-workflow` | Exploration structurelle, analyse d'impact, validation post-changement (open-source) | `.github/skills/gitnexus-workflow/SKILL.md` |
| `graphify-corpus` | Graphe de connaissance corpus, exploration docs+code (entreprise) | `.github/skills/graphify-corpus/SKILL.md` |
| `tdd-workflow` | Cycle TDD obligatoire Red-Green-Refactor | `.github/skills/tdd-workflow/SKILL.md` |
| `audit-workflow` | Audit technique, rapport, reconciliation GitHub | `.github/skills/audit-workflow/SKILL.md` |

## Bootstrap Outputs

Apres execution de `@memory-bootstrap`, des agents et skills supplementaires peuvent apparaitre selon la stack detectee : `dotnet-dev` ou `python-dev` pour le backend (de facon exclusive sauf vrai projet multi-backend), `front-dev`, `angular-front`, `aspire-debug`, `cqrs-feature`, `ui-ux-front-saas`, `testing`, `ci-cd`, `dotnet-patterns`, `xunit-unit-testing`, `python-patterns`.

Le bootstrap met aussi a jour `MEMORY.md`, `.github/memory/`, `AGENTS.md`, `CLAUDE.md` et `.vscode/mcp.json`.

## Regle backend exclusive

- Si le projet cible a un backend `.NET`, le bootstrap documente et genere `dotnet-dev` sans `python-dev`.
- Si le projet cible a un backend `Python`, le bootstrap documente et genere `python-dev` sans `dotnet-dev`.
- Les deux agents backend ne coexistent dans le projet cible que si le depot possede reellement plusieurs backends distincts.

## Code Graph Intelligence — GitNexus vs Graphify

Le socle supporte **deux moteurs d'intelligence code** au choix :

| Critere | GitNexus | Graphify |
|---------|----------|----------|
| **Contexte ideal** | Open-source, code-first | Entreprise, corpus riche (docs+code+audits) |
| **Force** | Impact analysis, blast radius, rename-safe, execution flows | Communautes conceptuelles, god nodes, docs-to-code traceability |
| **Transport MCP** | `npx gitnexus mcp` (stdio) | `python -m graphify.serve graph.json` (stdio) |
| **Mutations** | `rename()`, `detect_changes()` | Lecture seule |
| **Docs/images/audits** | Non couvert | Couvert nativement |

Le choix est fait au bootstrap via `@memory-bootstrap` et configure le bon serveur MCP, skill, et section AGENTS.md/CLAUDE.md.

**Regle de priorite :** Si les deux moteurs sont actives sur un meme projet :
- GitNexus pour le code (impact, rename, blast radius)
- Graphify pour le corpus (docs, audits, diagrammes, architecture transversale)
- Ne jamais utiliser Graphify pour l'impact analysis ou le rename
- Ne jamais utiliser GitNexus pour la traceabilite doc-to-code

## Regle Skills

Quand un skill s'applique a la demande, l'agent doit le lire avec `read_file` avant toute generation de code.

## Code Quality Guardrails

Regles universelles applicables a tout code genere :
- pas de magic strings : enums, constantes, `nameof()`
- un seul type public top-level par fichier
- typage fort plutot que `object`, `Dictionary<,>`, `JsonDocument`, `any`
- pas d'abstraction decorative sans levier reel
- organiser en sous-dossiers thematiques quand un dossier depasse ~6 fichiers

## Pull Requests

Toute PR faite par un agent Copilot doit suivre `.github/agents/pr-manager.agent.md`.

## Conventions projet

Voir `MEMORY.md` et `.github/memory/` pour toutes les conventions detectees par `memory-bootstrap`.

## Critical pitfalls (quick reference)

1. **TDD obligatoire :** Ne jamais ecrire de code de production sans tests d'abord
2. **Magic strings :** Centraliser dans des constantes ou enums
3. **Une classe par fichier :** Pas de fichiers poubelles groupant des dizaines de DTOs
4. **Typage fort :** Eviter `object`, `Dictionary<,>`, `any` si le schema est connu
5. **Patterns avec levier :** Comparer les options avant d'introduire un pattern structural
6. **Organisation fichiers :** Creer des sous-dossiers thematiques quand un dossier grossit
