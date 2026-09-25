---
name: feature
description: Use when the user starts or resumes a feature, change or fix request in Vole-Papillon-Damour ("/feature …", "nouvelle feature", "je veux ajouter…", or when run inside a feat/* worktree that has docs/features/<slug>/). Runs the design pipeline — worktree, functional spec, Claude Design mockups, implementation plan, Codex handoff. Claude designs; Codex implements.
---

# /feature — de l'idée au prompt Codex

Claude **conçoit**, Codex (modèle Luna, effort max) **implémente**. Ce skill enchaîne
quatre phases avec une validation de l'utilisateur entre chacune. L'utilisateur ne doit
jamais avoir à répéter ces consignes : applique-les d'office, en français.

Tout ce qui concerne une feature vit dans **un seul worktree, une seule branche, un seul
dossier** :

```
C:\Users\florian.drevet\RiderProjects\Vole-Papillon-Damour-<slug>   (branche feat/<slug>)
└── docs/features/<slug>/
    ├── README.md          statut des phases + liens (canvas, fichiers)
    ├── 01-fonctionnel.md  phase 1
    ├── maquettes/         phase 2 : preview/*.html (+ data-zone), sources/, README.md
    ├── 02-plan.md         phase 3
    └── HANDOFF.md         phase 4 : ce que Codex exécute
```

`<slug>` : kebab-case anglais court, déduit de la demande (`book-reservation`). Préfixe
de branche `feat/`, `fix/` ou `refactor/` selon la nature ; ne pas demander.

## Reprise

Si le répertoire courant (ou un worktree `…-<slug>` que l'utilisateur désigne) contient
déjà `docs/features/<slug>/README.md`, **lis sa section « Statut »** et reprends à la
première phase non validée. Si l'utilisateur écrit « validé » / « ok » / « go » pendant
une pause, coche la phase dans le README, committe, et passe à la suivante sans
autre question.

## Phase 0 — Espace de travail

1. `rtk git fetch origin main`
2. `rtk git worktree add -b feat/<slug> ../Vole-Papillon-Damour-<slug> origin/main`
   (depuis le checkout principal). Ne jamais travailler dans le checkout principal ni
   dans le worktree d'une autre tâche.
3. Toutes les lectures et écritures suivantes se font dans ce worktree, en chemins
   absolus.
4. Crée `docs/features/<slug>/README.md` à partir de [`templates/README.md`](templates/README.md).

## Phase 1 — Spécification fonctionnelle

Utilise **superpowers:brainstorming**, avec ces surcharges (elles priment sur le skill) :

- Le document va dans `docs/features/<slug>/01-fonctionnel.md`, **en français**, et reste
  **fonctionnel** : pas de choix de code, de classes ou d'endpoints. Les contraintes
  techniques connues vont dans une section « Contraintes ».
- Plan du document : Contexte · Objectifs · Hors périmètre · Acteurs · Parcours ·
  **Écrans** (liste numérotée `E1…En`, chacun marqué *nouveau* ou *modifié* avec l'écran
  existant d'origine) · Règles métier numérotées · Critères d'acceptation · Questions
  ouvertes. Si la feature étend un module de `docs/` (ex. `docs/bourse-aux-livres/`),
  lis ses specs et reprends sa numérotation de règles (`RG-xx`).
- Lis `NEXT.md` et les specs voisines avant de poser les questions.
- **Ne pas enchaîner sur writing-plans** à la fin du brainstorming : la phase suivante est
  la maquette.
- Commit : `docs(<slug>): spécification fonctionnelle`.

**⏸ Pause.** Donne le chemin du fichier et demande une relecture. Applique les
corrections jusqu'à « validé ».

## Phase 2 — Maquettes Claude Design

Si la spec ne liste aucun écran, note « Pas d'écran » dans le README et passe en phase 3.

1. `Artifact` → `action: "quickstart"`, `intent: "design"`, puis suis les instructions du
   type Design. Le canvas part de **l'existant** :
   - convention visuelle : `docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md` ;
   - écrans existants : `docs/bourse-aux-livres/maquettes/README.md` (liens des canvas
     publiés et sources `.dc.html`) et `docs/features/*/maquettes/sources/` ;
   - un écran *modifié* reprend l'écran d'origine à l'identique et ne change que ce que la
     spec demande ; un écran *nouveau* suit la même grammaire.
2. Pour chaque écran de la spec : un artboard desktop 1440 px **et** un mobile 390 px,
   plus un artboard « états » (vide, chargement, erreur, cas limites). Titre d'artboard
   préfixé par l'identifiant (`E3 · Confirmation — mobile`).
3. **Chaque élément à implémenter porte `data-zone="E<n>-<élément>"`** (ex.
   `data-zone="E1-bouton-signaler"`). Le plan et Codex s'appuient sur ces identifiants.
4. Ajoute sur le canvas des notes qui renvoient aux règles de la spec (`RG-xx`).
5. Publie le canvas. Publier depuis le scratchpad demande le chemin long
   (`C:/Users/florian.drevet/AppData/...`), pas le chemin court `FLORIA~1.DRE`.
   Reporte le lien dans le README.

**⏸ Pause.** Donne le lien du canvas. L'utilisateur peut le retoucher dans Claude
Design. Itère jusqu'à « validé ».

Export, **après** validation, depuis la version publiée et non depuis tes fichiers
locaux, puisque l'utilisateur a pu modifier le canvas :

1. `Artifact` `action: "list"`, `scope: "files"`, `url: <canvas>`, puis `action: "read"`
   avec `paths` = tous les `*.dc.html` + `canvas.json`, `out_dir` =
   `<scratchpad>/<slug>-canvas`.
   Si la lecture échoue, demande à l'utilisateur d'exporter le zip depuis Claude Design
   et passe son chemin au script à la place du dossier.
2. `python .claude/skills/feature/scripts/export_maquettes.py <scratchpad>/<slug>-canvas docs/features/<slug>/maquettes --url <canvas> --title "<titre>"`
3. Écris `maquettes/README.md` : lien du canvas, comment ouvrir `preview/index.html`,
   rappel de `V2-CONVENTION.md` (réutiliser les variables CSS existantes, pas les
   hexadécimaux en dur), et le tableau **Fichier `preview/` · Écran · Zones · Spec (§, RG)**.
   Le script affiche les zones de chaque page.
4. Commit : `docs(<slug>): maquettes`.

## Phase 3 — Plan d'implémentation

Utilise **superpowers:writing-plans**, avec ces surcharges :

- Fichier : `docs/features/<slug>/02-plan.md`, en français.
- L'exécutant est **Codex**, pas des sous-agents Claude. Remplace l'en-tête « REQUIRED
  SUB-SKILL » par : `> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.`
- Lis le code réel (graphify d'abord, cf. CLAUDE.md) et les skills du repo
  (`.github/skills/`) : chaque tâche donne des chemins de fichiers **existants**.
- **Tâche 0** : état initial (build et tests avant modification ; noter les échecs déjà
  présents sans les corriger). **Dernière tâche** : validation complète et PR.
- Chaque tâche qui touche une interface contient une ligne
  `**Maquette :** preview/<Page>.html — zones E1-…, E1-…` et reprend les libellés mot pour
  mot. Chaque tâche indique le test à écrire d'abord et son message de commit.
- Une zone volontairement non implémentée va dans une section « Zones non implémentées »,
  avec la raison.
- Si le plan contient du code non trivial, compile-le et teste-le dans le scratchpad
  avant de le figer.
- **Garde-fou obligatoire** :
  `python .claude/skills/feature/scripts/check_zones.py docs/features/<slug>` doit passer.
- Commit : `docs(<slug>): plan d'implémentation`.

**⏸ Pause.** Donne le chemin du plan, le nombre de tâches et les points qui méritent un
arbitrage. Itère jusqu'à « validé ».

## Phase 4 — Passage à Codex

1. Remplis [`templates/HANDOFF.md`](templates/HANDOFF.md) dans
   `docs/features/<slug>/HANDOFF.md`. N'y mets **que** ce qui est propre à la feature ;
   les règles communes (TDD, commits, livraison, PR) sont dans la skill Codex
   `.agents/skills/implement-handoff/SKILL.md` et ne doivent pas être recopiées.
2. Coche la phase 4 dans le README, commit `docs(<slug>): handoff Codex`, puis
   `rtk git push -u origin feat/<slug>`.
3. **Termine par exactement ce bloc**, prêt à coller dans Codex :

   ````
   ```text
   Worktree : C:\Users\florian.drevet\RiderProjects\Vole-Papillon-Damour-<slug>
   Branche  : feat/<slug>
   Utilise la skill implement-handoff et exécute docs/features/<slug>/HANDOFF.md.
   ```
   ````

   suivi d'une ligne : *Modèle Codex : Luna, effort max. Ouvre Codex dans le worktree
   ci-dessus.*

Claude **n'implémente pas** et **n'ouvre pas la PR** : c'est Codex qui l'ouvre en fin
d'implémentation, sur la même branche. C'est la seule dérogation au
`delivery-workflow` : la phase de conception s'arrête à la branche poussée.

## Après Codex

Si l'utilisateur revient avec des écarts ou des questions de Codex, travaille dans le
même worktree : corrige la spec, les maquettes (ré-export) ou le plan, relance
`check_zones.py`, committe, et donne un message court à transmettre à Codex.
