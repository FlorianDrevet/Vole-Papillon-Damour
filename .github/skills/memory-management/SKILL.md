---
description: "Skill for updating MEMORY.md and .github/memory thematic files. Defines routing, formatting, validation, and Dream-aware memory maintenance."
applyTo: "MEMORY.md,.github/memory/**/*.md"
---

# Skill : memory-management

Charger ce skill pour toute mise a jour de `MEMORY.md` ou de `.github/memory/`.

## Regles de base

- `MEMORY.md` reste un index leger (~80 lignes max).
- Les details vont dans les fichiers thematiques sous `.github/memory/`.
- `changelog.md` recoit une ligne pour toute tache non triviale.
- `dream-state.md` ne sert qu'a gerer les gates Dream et stocker le choix de code graph engine.
- Ne jamais ecrire de secrets ou de speculation.
- La memoire generee au bootstrap doit etre une **base de connaissance exploitable**, pas un simple inventaire de stack.
- Pour un depot moyen ou grand, preferer **plusieurs fichiers thematiques precis** plutot qu'un petit nombre de fichiers vagues.

## Routage

| Information | Fichier |
|-------------|---------|
| Vision rapide, table des thematiques | `MEMORY.md` |
| Structure du projet | `01-solution-overview.md`, `02-project-structure.md` |
| Runtime, API, domain model | `03-*` selon le projet |
| Architecture applicative (CQRS, slices, couches, MVC, services) | `04-*` |
| API / transport HTTP / contrats | `05-*` |
| Donnees, persistance | `06-*` |
| Moteur metier, generation, integrations majeures | `07-*` |
| Frontend / clients | `08-*` |
| Runtime / orchestration | `09-*` |
| Auth / build / run commands | `10-*` |
| Agents et skills | `11-agents-skills.md` |
| Endpoints API / surfaces d'entree | `12-*` |
| Code graph (Graphify, si active) | `07-code-graph.md` |
| Design system / conventions UI | `14-*` |
| Historique des mises a jour memoire | `changelog.md` |
| Gates Dream + choix code graph | `dream-state.md` |

## Standard de richesse au bootstrap

- Un petit depot simple peut rester compact.
- Un depot multi-surfaces, multi-clients, ou a architecture en couches doit viser une memoire **detaillee**, generalement **9 a 14 fichiers thematiques**.
- Ne pas compresser dans un seul fichier des sujets distincts comme domaine, CQRS, API, persistance, runtime, auth/build, agents, endpoints, et code graph si le depot est suffisamment riche.
- Preferer des fichiers courts et denses plutot que de longs fichiers melangeant tout.

## Contenu minimal attendu par type de fichier

### `MEMORY.md`

- table des fichiers thematiques existants
- quick reference avec pieges critiques ou regles de routage concretes
- resume de fonctionnement de la memoire et de `@dream`
- pour les depots riches, commandes de base ou note d'orientation rapide

### Fichiers thematiques

Chaque fichier thematique doit contenir des **faits verifies** tels que :
- noms exacts de projets, packages, apps ou assemblies
- fichiers d'entree et surfaces runtime
- dossiers et frontieres de responsabilite
- slices ou modules verifies
- conventions de code ou patterns reels
- zones a risque, pieges, ou validations necessaires
- commandes de build/test/run quand elles sont pertinentes au sujet

Aucun fichier thematique ne doit se limiter a :
- une phrase de stack generique
- des suppositions non verifiees
- une liste de frameworks sans lien avec des dossiers, fichiers, commandes, ou flux reels

## Matrice de generation recommandee

### Depot simple

- `01-solution-overview.md`
- `02-project-structure.md`
- `03-runtime-and-domain.md` ou equivalent
- `05-data-and-storage.md` si pertinent
- `06-agents-skills.md` ou `11-agents-skills.md`
- `07-code-graph.md`

### Depot moyen/riche en couches ou multi-surfaces

- `01-solution-overview.md`
- `02-project-structure.md`
- `03-domain-model.md` ou `03-runtime-and-domain.md`
- `04-cqrs-pattern.md`, `04-application-flow.md`, ou equivalent
- `05-api-layer.md` ou equivalent
- `06-persistence.md` ou equivalent
- `07-integrations.md`, `07-generation-engine.md`, ou equivalent si un moteur ou des integrations majeures existent
- `08-frontend.md` ou equivalent pour chaque surface client si necessaire
- `09-runtime-and-orchestration.md` si Aspire, Docker, workers, jobs, ou plusieurs runtimes coexistent
- `10-auth-and-build.md` si auth/build/run merite un fichier dedie
- `11-agents-skills.md`
- `12-api-endpoints.md` si l'API est non triviale
- `07-code-graph.md`
- `14-frontend-design-system.md` si une UI structurante existe

## Verification avant de considerer la memoire suffisante

- Chaque surface majeure du depot a son propre fichier ou une section clairement delimitee.
- Les fichiers contiennent plusieurs faits concrets verifies, pas seulement des generalites.
- Les zones a risque et les conventions importantes sont documentees.
- Les commandes de build/test/run majeures sont capturees quelque part dans la memoire.
- Si le depot expose une API non triviale, un fichier endpoints ou un equivalent existe.
- Si le depot a plusieurs clients/runtimes, ils ne sont pas tous resumes en deux bullets vagues.

## Format de dream-state.md

```markdown
# Dream State

## Gates

| Gate | Value |
|------|-------|
| `lastDreamDate` | YYYY-MM-DD |
| `sessionsSinceLastDream` | N |

## Config

| Key | Value |
|-----|-------|
| `codeGraphEngine` | graphify / none |

## Rules

- Time gate: at least 24h since `lastDreamDate`
- Session gate: `sessionsSinceLastDream` >= 5
- Both gates must pass to trigger a dream
```

## Dream-aware maintenance

- `@dev` incremente `sessionsSinceLastDream` a chaque session.
- `@dream` consolide, deduplique et prune.
- L'index doit rester court et scannable.
- Les fichiers thematiques ne doivent pas depasser ~150 lignes.
- Le changelog ne garde que les 60 derniers jours.
