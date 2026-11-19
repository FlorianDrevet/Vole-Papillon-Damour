# Plan d'implémentation — Étiquettes des livres sans ISBN

> Exécutant : Claude. L'utilisateur a demandé l'implémentation directe (au lieu de Codex, prévu par
> `/feature`) : tout est sur `feat/book-labels`, sans PR. Spec : [`01-fonctionnel.md`](01-fonctionnel.md).
> Maquettes : [`maquettes/preview/`](maquettes/README.md).

## État initial (tâche 0)

- Backend : Domain, Application, Infrastructure et Api compilent ; 260 + 833 + 259 tests verts après
  la généralisation `Book` → `BookCode` (commits `e53d446d` → `527c1136`).
- Scanette : `ng build` et `ng test` verts (374 tests) après `npm run link:shared-ui` de Scan.
- Catalog : `ng build` vert ; `ng test` : 3 échecs préexistants dans le compte (carte de compte,
  favoris), fichiers non touchés par cette feature.

## Tâches réalisées

| # | Tâche | Règles | Commit |
|---|-------|--------|--------|
| T1 | Code interne EAN-13 préfixe 2, compteur monotone `InternalBookCodeSequence` | RG-170, RG-171 | `3104ffa6` |
| T2 | Migrations `InternalBookCodeSequence`, `BookPhotos` (cascade) | RG-170, Q11 | `3104ffa6`, `0f076d38` |
| T3 | Création ordinaire sans ISBN (`POST /books/without-isbn`, `POST /books/admin/books/without-isbn`) | RG-180, RG-173 | `3104ffa6` |
| T4 | Photos d'un livre sans ISBN (≤ 10, JPEG/PNG/WebP, ≤ 8 Mo) | Q11 | `0f076d38` |
| T5 | Lectures : liste admin, fiche publique (couverture = photo, drapeau `IsInternalCode`) | RG-181 | `0f076d38` |
| T6 | Scan, caisse, Ma sélection, alertes : codes internes reconnus, codes inconnus refusés | RG-172, RG-182, Q9 | `0c2158c5` |
| T7 | Livre rare sans ISBN : code par exemplaire, quantité 1 | RG-173 | `0f076d38` |
| T8 | Traitements ISBNdb et enrichissement : filtre `IsInternalCode` côté SQL | RG-170 | `b8daf268` |
| T9 | Scanette : entrée, page de création E2/E3, impression E5, réimpression E4 | RG-175, RG-179, RG-180 | `17502121` |
| T10 | Site Catalog : liste admin E9, formulaire E10, fiche publique E11 | RG-181, RG-183, Q6 | `1c8f784b` |

Tests : domaine 266, application 839, infrastructure 259 ; scanette 374 ; catalog 692 (+ 3 préexistants).

## Écarts avec la spec

- **Livre rare sans ISBN, création** : l'ancien parcours hors ligne (`startNoIsbn`) ne crée plus de
  brouillon, il ouvre la création en ligne (RG-180).
- **E9, filtre ordinaire / rare** : la liste admin ne montre que les livres ordinaires. Les livres
  rares sans ISBN apparaissent dans « Livres rares », où ils portent leur code (un par exemplaire).
- **Réponse de l'endpoint admin de création** : `200` (et non `201`), car le `Location` pointerait
  vers une lecture qui n'accepte pas encore un code interne.
- **RG-177 (date de dernière impression)** : non implémentée. Tant que l'impression est simulée
  (RG-178), rien n'est enregistré ; la liste E5 « jamais imprimées » attend le pilote réel.
- **Caisse E8 (affichage dédié)** : le code interne est résolu vers sa fiche (RG-172) ; la ligne
  « code interne reconnu » du canevas n'a pas de rendu dédié (voir ci-dessous).

## Maquettes couvertes

**Maquette :** preview/Main.html — zones E1-entree-ajouter-sans-isbn, E1-entree-imprimante
**Maquette :** preview/E2-FormulaireSansIsbn.html — zones E2-bascule-type, E2-champ-titre, E2-champ-auteur, E2-champ-editeur, E2-champ-annee, E2-champ-etat, E2-champ-quantite, E2-photos, E2-message-connexion, E2-bouton-creer
**Maquette :** preview/E3-FicheCreee.html — zones E3-code-chiffres, E3-bouton-imprimer, E3-bouton-plus-tard
**Maquette :** preview/E4-ResultatIsbn.html — zones E4-bouton-reimprimer-isbn, E4-isbn-lisible
**Maquette :** preview/E5-FicheRare.html — zones E5-bouton-imprimer, E5-bouton-plus-tard, E5-prix-ferme
**Maquette :** preview/E6-Imprimante.html — zones E6-etat, E6-appareil, E6-bandeau-simulation, E6-bouton-connecter, E6-bouton-tester
**Maquette :** preview/E7-ApercuImpression.html — zones E7-apercu-etiquette, E7-nombre-etiquettes, E7-bouton-imprimer, E7-statut-simulation, E7-bouton-annuler
**Maquette :** preview/E8-CaisseCodeInterne.html — zones E8-ligne-code-interne
**Maquette :** preview/E9-ListeAdmin.html — zones E9-bouton-ajouter
**Maquette :** preview/E10-FormulaireAdmin.html — formulaire admin, mêmes champs que E2 (pas de zone dans le canevas)
**Maquette :** preview/E11-FichePublique.html — fiche publique, sans ISBN (pas de zone dans le canevas)
**Maquette :** preview/E11-FichePubliqueMobile.html — fiche publique mobile (pas de zone dans le canevas)

## Zones non implémentées

- `E4-isbn-lisible` : le verdict affiche déjà l'ISBN ; aucune zone dédiée n'a été ajoutée.
- `E5-bouton-plus-tard` : la fiche rare n'a pas d'écran de choix après impression ; le bouton retour existant suffit.
- `E5-prix-ferme` : le prix est imprimé sur l'étiquette (RG-179) ; son affichage sur la fiche rare n'a pas été ajouté.
- `E7-bouton-annuler` : l'aperçu d'étiquette n'a pas de bouton d'annulation distinct du retour à l'accueil.
- `E8-ligne-code-interne` : la caisse résout le code interne vers sa fiche (RG-172) ; la ligne dédiée
  « code interne reconnu » du canevas n'est pas rendue séparément.
