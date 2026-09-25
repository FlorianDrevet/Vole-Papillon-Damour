---
name: implement-handoff
description: "Use when asked to execute a docs/features/<slug>/HANDOFF.md prepared by Claude: implement a validated spec + plan + Claude Design mockups end to end (TDD, one commit per task, visual check against maquettes/preview, PR). The design is settled; do not re-discuss it."
---

# Implémenter un handoff de conception

La conception (spec, maquettes, plan) est **terminée et validée** par l'utilisateur. Tu
exécutes un plan, tu ne rediscutes pas l'architecture. Le fichier `HANDOFF.md` donne ce
qui est propre à la feature ; ce skill donne les règles communes.

## 1. Démarrage

1. Travaille **uniquement** dans le worktree et la branche indiqués par le handoff
   (`feat/<slug>`), déjà créés. N'en crée pas d'autre. Vérifie avec `rtk git status` et
   `rtk git branch --show-current`.
2. Lis `AGENTS.md`, puis `.github/skills/delivery-workflow/SKILL.md` et
   `.github/skills/tdd-workflow/SKILL.md`, puis tout ce que la section « À lire » du
   handoff liste, **dans l'ordre**.
3. Ouvre `docs/features/<slug>/maquettes/preview/index.html` : il donne la liste des
   écrans et de leurs zones `data-zone`.

## 2. Exécution du plan

- Exécute les tâches de `02-plan.md` **dans l'ordre**, étape par étape.
- **Tâche 0 (état initial)** : build et tests avant toute modification. Ne corrige pas
  les échecs déjà présents ; note-les pour la PR.
- **Tests d'abord** : écris le test, vérifie qu'il échoue pour la bonne raison, puis
  implémente jusqu'au vert.
- **Un commit par tâche**, avec le message indiqué par le plan (`rtk git …`).
- Le code donné tel quel dans le plan a été compilé et testé : recopie-le exactement,
  sauf s'il contredit le code réel (voir §4).
- Respecte à la lettre les constantes et les interdits de la section « Spécifique » du
  handoff.
- Aucun déploiement Azure, aucune commande `az` en écriture, aucune nouvelle ressource,
  sauf si le handoff l'autorise explicitement.

## 3. Interfaces : fidélité aux maquettes

Pour chaque tâche qui contient une ligne `**Maquette :** preview/<Page>.html — zones …` :

1. Ouvre la page de maquette et repère chaque élément `data-zone` cité.
2. Reprends **mot pour mot** les libellés, et la hiérarchie, les espacements, la
   typographie et les couleurs, en t'appuyant sur les composants et les variables CSS
   existants de l'application (ex. `--catalog-*`), jamais sur des hexadécimaux en dur
   (`docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md`).
3. Vérification visuelle avec la skill `playwright` ou `screenshot` : capture la page
   implémentée **et** la page de maquette aux largeurs du handoff (390 et 1440 px par
   défaut), compare zone par zone, et corrige les écarts. Ni la page ni ses zones ne
   doivent provoquer de défilement horizontal. Garde les captures pour la PR.
4. Les zones listées dans « Zones non implémentées » du plan ne sont pas à faire.

## 4. Écarts et blocages

- Si le plan contredit le code réel (nom de fichier, signature, ligne), **suis le code
  réel** en restant fidèle à l'intention du plan, et note l'écart pour la PR.
- Si une contradiction touche **la spec** (règle métier, RGPD, sécurité, données
  personnelles), **arrête-toi** et signale-la au lieu de trancher.
- Ne modifie pas `01-fonctionnel.md`, les maquettes ni le plan : ils appartiennent à la
  conception. Tes écarts vont dans la PR.

## 5. Livraison

1. Suite complète, selon les surfaces touchées :
   - backend : `dotnet build src/Backend/Vole_Papillon_Damour.slnx` puis `dotnet test` sur
     les projets `*.tests` concernés ;
   - front : `npm test` et `npm run build` dans chaque `src/<App>/` touché ;
   - infra : `az bicep build --file infra/main.bicep` si `infra/` est touché ;
   - enfin `graphify update .`.
2. `rtk git fetch origin main`, `rtk git rebase origin/main`, puis relance toute la suite.
3. `rtk git push -u origin feat/<slug>`.
4. Ouvre la PR vers `main` au format du `delivery-workflow`, avec le titre du handoff :
   - « Validation » : les sorties exactes des commandes, et pour chaque écran
     (identifiant `E<n>`) les largeurs vérifiées et le résultat ;
   - « Risks and follow-up » : les points du handoff, les échecs de la tâche 0 et tous
     les écarts au plan.
5. Coche « 5. PR ouverte par Codex » dans `docs/features/<slug>/README.md` avec le lien
   de la PR, committe (`docs(<slug>): PR ouverte`) et pousse.
6. **Ne merge pas.** Rends : le lien de la PR, le worktree, la branche, les résultats des
   tests, la liste des écarts au plan et des questions restées ouvertes.
