---
description: "Explore le projet en profondeur, initialise la memoire thematique, cree ou adapte les agents et skills selon la stack reelle, prepare le code graph Graphify et la configuration MCP workspace."
---

# Agent : memory-bootstrap - Initialisation intelligente du projet

> Invoquer cet agent sur tout nouveau projet, ou quand le socle agentique doit etre regenere apres un changement majeur.

## Objectif

1. Produire un `MEMORY.md` leger a la racine comme index.
2. Produire `.github/memory/` avec les fichiers thematiques, `dream-state.md` et `changelog.md`.
3. Generer les agents specialises adaptes a la stack reelle du projet.
4. Generer les skills de base puis les skills conditionnels pertinents.
5. Mettre a jour `.github/copilot-instructions.md`, `AGENTS.md`, `CLAUDE.md` et `.vscode/mcp.json`.
6. Initialiser Graphify si le projet le requiert.

## Ce que cet agent ne fait jamais

- modifier le code source metier du projet
- refactorer l'application cible
- ajouter des affirmations non verifiees dans la memoire

---

## Workflow obligatoire

### Phase 0 - Clarification : utiliser Graphify ou aucun moteur

**Avant toute exploration, demander a l'utilisateur :**

> Souhaitez-vous activer Graphify pour l'exploration transversale du projet ?
> - **Graphify** (corpus riche docs+code+audits, communautes conceptuelles)
> - **Aucun moteur** (exploration directe des fichiers)

Stocker le choix dans `.github/memory/dream-state.md` sous une cle `codeGraphEngine: graphify | none`.

### Phase 1 - Discovery

1. Lire les fichiers racine : `*.sln`, `*.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `NuGet.config`, `pyproject.toml`, `requirements*.txt`, `poetry.lock`, `Pipfile`, `uv.lock`, `package.json`, `angular.json`, `tsconfig.json`, `README*`, `.gitignore`
2. Cartographier `src/`, `apps/`, `services/`, `tests/`, `docs/`, `infrastructure/`, `pipelines/`
3. Lire les points d'entree backend pertinents selon la stack detectee : `Program.cs`, `Startup.cs`, `DependencyInjection.cs`, `main.py`, `app.py`, `wsgi.py`, `asgi.py`, `manage.py`, ainsi que les points d'entree frontend pertinents
4. Verifier si `graphify-out/graph.json` existe deja
5. Lire les fichiers de build/run/test evidents (`package.json`, scripts, README, docs/getting-started, solution files) pour capturer des commandes verifiees
6. Lire au moins un fichier representatif par surface majeure detectee : backend, frontend, client natif, worker, integration majeure, tests, docs

### Phase 2 - Detect stack and architecture

Detecter explicitement :
- Backend : .NET, Python, Node.js, Go, Java...
- Framework backend Python si present : FastAPI, Django, Flask, autre service Python leger
- Frontend : Angular, React, Vue, Blazor...
- Architecture : CQRS, Clean Architecture, N-Tier, Vertical Slices, MVC, Microservices...
- Persistance : EF Core, SQLAlchemy, Django ORM, Dapper, MongoDB, CosmosDB...
- Auth : Identity, OIDC, JWT, Azure AD...
- Orchestration : Aspire, Docker Compose, Kubernetes...
- CI/CD : GitHub Actions, Azure DevOps Pipelines, GitLab CI...

### Phase 3 - Detect patterns and high-risk zones

1. Lire des exemples representatifs d'entites, handlers, services, endpoints, modules Python, composants frontend
2. Identifier les conventions de nommage et d'organisation
3. Identifier les zones a haut risque et les flux critiques
4. Si le code graph est deja indexe, lire ses stats et clusters

**Regles de detection Python a appliquer :**
- **FastAPI** si l'on trouve `fastapi`, `FastAPI(`, `APIRouter`, `Depends`, `pydantic`, ou `uvicorn`
- **Django** si l'on trouve `manage.py`, `settings.py`, `INSTALLED_APPS`, `django.` ou la structure classique `project/app`
- **Flask** si l'on trouve `flask`, `Flask(`, `Blueprint`, `current_app` ou une app factory explicite
- Si plusieurs signaux coexistent, documenter le cadre dominant et les exceptions dans la memoire

### Phase 3bis - Build the evidence map for memory

Avant d'ecrire la memoire, reunir un socle minimum de faits verifies :

- **Structure** : noms exacts des projets/apps/solutions et frontieres des dossiers
- **Runtime** : points d'entree, surfaces deployables, jobs, clients, workers
- **Architecture** : CQRS, couches, slices, MVC, services, repository pattern, etc.
- **Data** : ORM, DbContext, repositories, persistence adapters, stockage cloud
- **Transport** : API HTTP, controllers, minimal APIs, contracts, clients Refit/SDK
- **Frontend/clients** : frameworks, apps, separations par surface, dependances structurantes
- **Auth/build/run** : commandes verifiees, auth, policies, pipelines, outillage principal
- **Critical zones** : flows sensibles, symboles partages, integrations externes, risques de drift entre surfaces

Pour un depot multi-surfaces, ne pas s'arreter apres une lecture superficielle de la stack. Lire suffisamment pour pouvoir nommer les vraies slices, les vrais modules, et les vraies zones de risque.

### Phase 4 - Generate the memory system

Generer ou mettre a jour :

- `MEMORY.md` comme index leger (~80 lignes max)
- `.github/memory/01-solution-overview.md`
- `.github/memory/02-project-structure.md`
- `.github/memory/03-domain-model.md` ou `.github/memory/03-runtime-and-domain.md` selon la structure detectee
- `.github/memory/04-cqrs-pattern.md`, `.github/memory/04-application-flow.md`, ou equivalent si l'architecture le justifie
- `.github/memory/05-api-layer.md` ou `.github/memory/05-data-and-storage.md` si une surface HTTP ou transport existe
- `.github/memory/06-persistence.md` ou equivalent si persistence significative
- `.github/memory/07-integrations.md`, `.github/memory/07-generation-engine.md`, ou equivalent si un moteur ou des integrations majeures existent
- `.github/memory/08-frontend.md` si frontend ou clients detectes
- `.github/memory/09-runtime-and-orchestration.md` si plusieurs runtimes / orchestration / workers existent
- `.github/memory/10-auth-and-build.md` si auth et commandes merite un fichier dedie
- `.github/memory/11-agents-skills.md`
- `.github/memory/12-api-endpoints.md` si l'API est non triviale
- `.github/memory/13-code-graph.md`
- `.github/memory/14-frontend-design-system.md` si un design system ou des conventions UI structurantes existent
- `.github/memory/changelog.md`
- `.github/memory/dream-state.md`

Regles :
- `MEMORY.md` reste un index leger
- `dream-state.md` initialise `sessionsSinceLastDream` a 0, `lastDreamDate` a la date du jour
- `dream-state.md` contient aussi `codeGraphEngine: <choix>`
- `changelog.md` note la date et la nature du bootstrap
- la profondeur de la memoire doit **suivre la complexite reelle du depot** ; un depot a backend + plusieurs clients + architecture en couches ne doit pas sortir avec 6 ou 7 fichiers vagues
- preferer des fichiers thematiques dedies pour domaine, architecture, API, persistance, auth/build, endpoints, runtime, code graph, plutot qu'un fichier unique trop large
- chaque fichier doit contenir des faits verifies, des noms exacts, et des zones a risque concretes

### Phase 4bis - Mandatory memory deepening pass

Avant de considerer le bootstrap termine, relire la memoire produite et verifier :

1. que chaque surface majeure detectee a un fichier ou une section dediee
2. que `MEMORY.md` contient une vraie quick reference, pas seulement une table de fichiers
3. que les fichiers thematiques mentionnent des modules/slices/aggregates/projets exacts
4. que les commandes build/test/run importantes sont capturees quelque part
5. que les flux critiques, risques de drift, et pieges techniques sont documentes

Si la memoire ressemble encore a un simple inventaire de stack, faire une seconde passe de lecture puis enrichir les fichiers avant de continuer.

### Phase 5 - Generate the base agents

**Toujours verifier ou creer :**
- `dev.agent.md`
- `architect.agent.md`
- `documentation-professor.agent.md`
- `review-expert.agent.md`
- `vibe-coding-refractaire.agent.md`
- `review-remediator.agent.md`
- `audit-expert.agent.md`
- `dream.agent.md`
- `merge-main.agent.md`
- `pr-manager.agent.md`
- `memory.agent.md` (deprecie, redirige vers dev)

**Generer conditionnellement :**
- `dotnet-dev.agent.md` si backend .NET detecte
- `python-dev.agent.md` si backend Python detecte
- `front-dev.agent.md` ou l'agent frontend adapte au framework detecte si frontend detecte
- `aspire-debug.agent.md` si Aspire/AppHost detecte

**Regle d'exclusivite backend :**
- si le backend detecte est `.NET`, generer `dotnet-dev` et ne pas generer `python-dev`
- si le backend detecte est `Python`, generer `python-dev` et ne pas generer `dotnet-dev`
- si plusieurs backends coexistent reellement dans le projet, documenter explicitement les zones et generer un agent par backend present

### Phase 6 - Generate the base skills

**Toujours verifier ou creer :**
- `.github/skills/memory-management/SKILL.md`
- `.github/skills/tdd-workflow/SKILL.md`
- `.github/skills/audit-workflow/SKILL.md`

**Selon le choix de code graph :**
- Si Graphify : `.github/skills/graphify-corpus/SKILL.md`

**Generer conditionnellement :**
- `cqrs-feature` si architecture CQRS detectee
- `dotnet-patterns` si backend .NET
- `xunit-unit-testing` si .NET avec xUnit
- `python-patterns` si backend Python
- `angular-patterns` si Angular
- `ui-ux-front-saas` si frontend avec UI
- `ci-cd` si pipelines detectees

**Si backend Python :**
- ajouter des consignes de test alignees sur la stack detectee (`pytest`, `unittest`, etc.) en plus du skill `tdd-workflow`
- documenter explicitement le framework detecte (FastAPI, Django, Flask, autre) dans la memoire et dans l'agent backend genere

### Phase 7 - Update workspace instructions and docs

Mettre a jour : `.github/copilot-instructions.md`, `AGENTS.md`, `CLAUDE.md`.

**Regle de documentation backend :**
- ne documenter que l'agent backend reellement genere pour le projet cible
- retirer les references backend de la stack non detectee des tableaux, quick references et exemples de delegation
- conserver les deux uniquement si le projet cible possede effectivement deux backends distincts

**AGENTS.md** doit contenir :
- La section Code Graph Intelligence correspondant au choix (Graphify ou aucun moteur)
- Les regles "Always Do" et "Never Do" adaptees au moteur choisi
- Les ressources et outils MCP disponibles

**CLAUDE.md** doit contenir :
- La meme section Code Graph Intelligence (pour compatibilite Claude Code) si Graphify est active

### Phase 8 - Prepare MCP workspace config

Creer ou mettre a jour `.vscode/mcp.json` avec :

**Selon le choix de code graph :**

Si **Graphify** :
```json
{
  "servers": {
    "graphify": {
      "type": "stdio",
      "command": "python",
      "args": ["-m", "graphify.serve", "${workspaceFolder}/graphify-out/graph.json"]
    }
  }
}
```

**Toujours proposer :**
- `github` (GitHub MCP server) avec `inputs` pour le token
- `azure-devops` si Azure DevOps detecte
- `aspire` si le projet utilise Aspire

### Phase 9 - Initialize the code graph

**Si Graphify :**
1. Verifier que `python -m graphify` est disponible (installer si besoin : `pip install graphifyy`)
2. Creer un `.graphifyignore` a la racine excluant `node_modules/`, `bin/`, `obj/`, `.git/`, `dist/`
3. Executer le build code-only du graphe : `python -c "from pathlib import Path; from graphify.watch import _rebuild_code; import sys; ok = _rebuild_code(Path('.')); sys.exit(0 if ok else 1)"`
4. Verifier que `graphify-out/graph.json` et `graphify-out/GRAPH_REPORT.md` existent
5. Documenter la commande de mise a jour dans le skill et la memoire

---

## Code Graph - Resume des regles par moteur

### Graphify - Regle standard

Le bootstrap doit preparer les projets a utiliser Graphify comme primitive corpus :
- `graphify query "concept"` pour trouver les nœuds pertinents
- `graphify path "A" "B"` pour tracer les chemins entre concepts
- `graphify explain "node"` pour comprendre un nœud en contexte
- Lire `graphify-out/GRAPH_REPORT.md` pour les god nodes et communautes
- Utiliser le serveur MCP pour les requetes en session

---

## Output attendu

```text
[ ] MEMORY.md est un index leger
[ ] .github/memory/ existe avec dream-state et changelog
[ ] La profondeur de la memoire est adaptee a la complexite du depot (compacte pour petit depot, detaillee pour depot riche)
[ ] Code graph engine choisi et documente dans dream-state.md
[ ] Les surfaces majeures du depot ont des fichiers thematiques dedies (domain, architecture, API, persistance, frontend/clients, runtime, auth/build, endpoints, code graph quand pertinents)
[ ] Agents de base generes ou verifies (incluant review-expert, vibe-coding-refractaire, audit-expert)
[ ] Agent backend genere exclusivement selon la stack detectee (`dotnet-dev` ou `python-dev`, ou plusieurs seulement si le projet est reellement multi-backend)
[ ] Skills de base generes ou verifies (incluant tdd-workflow, audit-workflow)
[ ] Code graph skill genere selon le choix (`graphify-corpus` si necessaire)
[ ] Code graph initialise (indexation executee avec succes)
[ ] .vscode/mcp.json cree ou mis a jour avec le bon serveur code graph
[ ] AGENTS.md, CLAUDE.md et copilot-instructions.md alignes
[ ] .github/test-debt.md cree (fichier vide pour tracking dette de tests)
```
