# Handoff Codex — ISBN non trouvés

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

Les administrateurs ont une rubrique « ISBN non trouvés » qui liste les livres pour lesquels aucune
source ne donne de notice. Ils exportent cette liste en CSV, la font compléter par un agent, puis
déposent le fichier : chaque ligne est vérifiée (l'ISBN doit correspondre à un « non trouvé ») avant
l'enregistrement. « Analyse des ISBNs » ne garde que les échecs, et « Livres hors ISBNdb » ne montre
plus les livres sans titre.

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\florian.drevet\RiderProjects\Vole-Papillon-Damour-isbn-not-found-tab`
- Branche : `feat/isbn-not-found-tab`, qui contient déjà la conception (spec, maquettes, plan). La PR
  livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/isbn-not-found-tab/01-fonctionnel.md` : **la spec, elle fait foi**
2. `docs/features/isbn-not-found-tab/02-plan.md` : **le plan à exécuter** (14 tâches)
3. `docs/features/isbn-not-found-tab/maquettes/README.md`, puis `maquettes/preview/index.html`
4. Skills du repo concernées : `.github/skills/cqrs-feature`, `.github/skills/dotnet-patterns`,
   `.github/skills/xunit-unit-testing` (backend) ; `.github/skills/angular-patterns`,
   `.github/skills/ui-ux-front-saas` (frontend).
5. Référence de format CSV : `src/Catalog/src/app/features/administration/isbn-application-import.ts`
   (séparateur `;`, BOM UTF-8, conversion ISBN-10 → ISBN-13). Ne pas réutiliser son flux d'écriture :
   l'import de cette feature passe par le backend (tâches 4 et 5).

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| 8 · navigation | `E4-Desktop.html`, `E4-Mobile.html` | `E4-*` (16 zones) |
| 9 · Analyse des ISBNs (E1) | `Main.html`, `E1-Mobile.html`, `E1-Etats.html` | `E1-*` (21 zones) |
| 10 · ISBN non trouvés (E2) | `E2-Desktop.html`, `E2-Mobile.html`, `E2-Etats.html` | `E2-*` (26 zones) |
| 11 · Import CSV (E3) | `E3-Desktop.html`, `E3-Mobile.html`, `E3-Etats.html` | `E3-*` (22 zones) |
| 12 · Livres hors ISBNdb (E5) | `E5-Desktop.html`, `E5-Mobile.html`, `E5-Etats.html` | `E5-*` (14 zones) |

`Main.html` est l'écran E1 desktop. Les zones des états (`*-Etats.html`, `*-vide-*`, `*-erreur`,
`*-chargement`, `E1-pastille-independante`, `E5-sans-titre-inconnu`, `E4-etat-zero`) sont couvertes
par les tests des tâches correspondantes.

Largeurs de vérification : **390 et 1440 px**.

## Spécifique à cette feature

- **Décisions validées par l'utilisateur** (appliquer telles quelles) :
  1. À l'import, un livre `NotFound` qui n'est pas marqué absent d'ISBNdb est marqué absent
     (`Book.MarkAbsentFromIsbndb(now)`) avant `MarkIsbndbCompleted`. Consigner le nombre de cas
     concernés dans la PR.
  2. Le compteur « métadonnées à vérifier » de la vue d'ensemble reste inchangé.
  3. La route est `/administration/isbn-not-found`, distincte de `not-found` (« Livres introuvables »).
- **Constantes non négociables** :
  - CSV : séparateur `;`, UTF-8 avec BOM, 13 colonnes dans cet ordre :
    `ISBN;Titre;Auteurs;Editeur;Annee_publication;Format;Langue;Genre_principal;Genres;Resume;Pages;Edition;Couverture_URL`.
  - Seul le titre est obligatoire. Sans auteur, la fiche est enregistrée mais reste « à compléter ».
  - Limites de fichier : 5 000 lignes et 2 Mo (RG-22). Limites par champ : RG-18, tableau de RG-08.
  - Export : **tous** les « non trouvés », pas seulement la page affichée (RG-07).
- **Interdits** :
  - ne pas modifier `01-fonctionnel.md`, `02-plan.md` ni `maquettes/` : ils appartiennent à la conception ;
  - ne pas toucher à la section `not-found` ni à ses compteurs (« Livres introuvables ») ;
  - ne pas toucher à `docs/bourse-aux-livres/`.
- **Flags / configuration** : aucun flag.
- **Azure / infra** : aucun déploiement, aucune commande `az` en écriture, aucune ressource.
- **Contrôle d'accès** : l'export et l'import sont réservés à l'administrateur (RG-23), côté backend.

## PR

- Titre : `feat(isbn-not-found): add ISBN not-found tab with CSV export and import`
- « Risks and follow-up » :
  - échecs déjà présents à la tâche 0, à citer tels quels ;
  - le commit `c3445ad6` corrige `.claude/skills/feature/scripts/export_maquettes.py` (`base.css` était
    perdu dans les aperçus) : il est inclus dans cette PR ;
  - les aperçus `maquettes/preview/` n'ont pas été ouverts dans un navigateur par la conception :
    la vérification visuelle de la tâche 13 est la première vraie comparaison ;
  - tout écart entre le plan et le code réel (signatures, lignes) à noter dans la PR ;
  - `MarkIsbndbCompleted` exige `IsAbsentFromIsbndb` : le cas des « non trouvés » non interrogés
    ISBNdb doit apparaître dans les tests de la tâche 5.
