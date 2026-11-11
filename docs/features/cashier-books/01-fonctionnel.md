# Livres dans l'application de caisse Android — spécification fonctionnelle

> Demande d'origine (2026-11-11) : « Je veux que l'app puisse faire les livres, re rajoute cette section »

## 1. Contexte

L'application Android de caisse (`src/MauiCashApp`, distribuée en APK par le BackOffice)
propose deux catégories : **Buvette** et **Loto**.

Une catégorie **Livres** existait dans la version initiale du projet. Elle a été retirée
lors de la restauration de l'application (PR #357, commit `4faeb3e2`) : le filtre des
produits n'accepte plus que les sections Buvette et Loto, et les tests vérifient qu'un
livre n'apparaît pas.

Le périmètre de la bourse aux livres pose l'inverse. Selon
`docs/bourse-aux-livres/01-vision-et-perimetre.md` (§5), l'intégration à l'application de
caisse buvette est hors périmètre : les livres ont leur propre mode vente dans la
Scanette, et « les deux caisses restent séparées ». La règle `RG-50` ajoute qu'un livre
ordinaire n'a aucun prix stocké, affiché ni totalisé par l'application.

Cette demande rétablit la section dans l'application Android. Son mode de
fonctionnement dépend de Q-01 : il doit être tranché avant toute implémentation.

## 2. Objectifs

- Permettre au caissier de sélectionner des livres depuis la tablette, au même titre que
  la buvette et le loto.
- Ne proposer que les livres disponibles, comme pour les autres catégories.
- Selon Q-01, soit vendre des livres avec un prix et un total (option A), soit enregistrer
  la sortie des livres sans prix ni total (option B).

## 3. Hors périmètre

| Exclusion | Motif |
|---|---|
| Modification de la Scanette et du site public | Non concernés par cette demande. |
| Enregistrement durable des ventes sur la tablette | Inchangé : l'application ne persiste pas les ventes terminées. |
| Paiement et rendu de monnaie | Inchangé : l'encaissement reste physique. |
| Catégorie Livres sur l'application de scan | Déjà couverte par la Scanette. |

## 4. Acteurs

- **Caissier** : bénévole qui utilise la tablette au comptoir.
- **Administrateur** : gère les produits depuis le BackOffice (option A uniquement).

## 5. Parcours

Parcours décrit pour l'**option A** (version restaurée) :

1. Le caissier ouvre la caisse ; l'onglet Buvette est sélectionné par défaut.
2. Il touche l'onglet **Livres** : la grille affiche les livres disponibles.
3. Il touche un livre : il est ajouté au panier. Un nouvel appui augmente la quantité.
4. Le total du panier inclut les livres.
5. `VALIDER` vide le panier, comme pour les autres produits.

Pour l'**option B**, les étapes 3 et 4 changent : toucher un livre enregistre une sortie
de livre pendant une bourse ouverte, sans prix ni total (voir CL-05).

## 6. Écrans

- **E1 · Onglet Livres** — *nouveau*. Écran d'origine : écran de caisse, barre des
  catégories (Buvette, Loto). Même forme et même taille que les deux autres onglets, avec
  une icône de livre. Ordre : Buvette, Loto, Livres.
- **E2 · Grille des livres** — *nouveau*. Écran d'origine : grille de produits des
  catégories Buvette et Loto. Mêmes cartes ; l'affichage du prix dépend de Q-01.
- **E3 · Panier** — *modifié*. Écran d'origine : colonne panier, total et bouton
  `VALIDER`. Un livre s'y ajoute comme un autre produit ; la présence du total pour un
  livre dépend de Q-01.
- **E4 · États de la grille Livres** — *nouveau*. Trois états : grille vide (aucun livre
  disponible), chargement, hors ligne avec le catalogue en cache sur la tablette.

## 7. Règles métier

- **CL-01** — La section Livres n'affiche que les livres disponibles. Un livre désactivé
  n'apparaît pas, y compris depuis le cache hors ligne.
- **CL-02** — Un livre suit la même règle d'ajout au panier qu'un autre produit : une
  ligne par livre, quantité augmentée à chaque appui.
- **CL-03** — La section Livres ne remplace ni la Buvette ni le Loto. Aucun livre n'apparaît
  dans ces deux onglets.
- **CL-04** *(option A)* — Le prix d'un livre est celui de sa fiche produit. Le total du
  panier additionne les livres aux autres produits.
- **CL-05** *(option B)* — Aucun prix de livre ordinaire n'est affiché ni totalisé
  (`RG-50`). La tablette ne sert qu'à enregistrer les sorties, et seulement pendant une
  bourse ouverte (`RG-33`).
- **CL-06** — La tablette n'enregistre aucune vente terminée au-delà de la session
  courante, comme aujourd'hui (option A). L'option B doit dire où les sorties sont
  enregistrées (voir Q-03).

## 8. Critères d'acceptation

- Quand le caissier touche l'onglet Livres, seuls les livres disponibles apparaissent.
- Un livre désactivé n'apparaît pas, même hors ligne avec le cache.
- Aucun livre n'apparaît dans les onglets Buvette ou Loto.
- Option A : ajouter un livre au panier met à jour le total ; `VALIDER` vide le panier.
- Option B : un livre affiché dans le panier n'a ni prix ni total.
- Sans livre disponible, la grille affiche l'état vide.
- Hors ligne avec le cache, les livres disponibles restent affichés.
- Les tests `CashierProductFilterTests` et `Categories_OnlyExposeBuvetteAndLoto` reflètent
  le nouveau comportement.

## 9. Contraintes connues

- Les produits de la tablette viennent de `GET /product`, endpoint public sans connexion
  (`src/MauiCashApp/README.md`). L'option B enregistre des sorties via les routes
  `/scan/*`, réservées aux rôles `Tri` et `Caisse` (`.github/memory/10-api-endpoints.md`) :
  la tablette devrait alors se connecter, ce qu'elle ne fait plus depuis la PR #357.
- L'icône existe sous `Resources/Images/book.svg`. L'ancienne référence `book.png` n'existe plus.
- Application Android uniquement (`net10.0-android`).

## 10. Questions ouvertes

**Q-01 — Bloquant : quel mode de vente des livres sur la tablette ?**

- **A — Restaurer la section telle qu'elle était.** Les livres se vendent comme la buvette,
  avec prix et total. Le code existait déjà, mais cela contredit `RG-50` et la décision
  « deux caisses séparées » pour les livres ordinaires.
- **B — Sorties de livres pendant une bourse.** La tablette enregistre quels livres sortent,
  sans prix ni total, comme la Scanette (`docs/bourse-aux-livres/03-parcours-benevole-scan.md`
  §5). Cela respecte `RG-50`, mais duplique le mode Caisse de la Scanette et oblige la
  tablette à se connecter.
- **C — Ne pas réintroduire les livres sur la tablette.** Les livres restent dans la
  Scanette.

*Recommandation :* ne pas choisir A tant que `RG-50` n'est pas révisé. Cette règle est
posée comme structurante dans le périmètre. Si l'objectif est de vendre des livres à prix
fixe au comptoir buvette, c'est une évolution de `RG-50` à acter, pas un simple retour de
code.

**Q-02 — Si A : les produits Livres existent-ils encore ?** La page Caisse du BackOffice
propose toujours un onglet « Livres » (`src/BackOffice/src/app/feature/caisse/caisse.component.ts`).
Y a-t-il aujourd'hui des produits de section Livres en base, avec ou sans prix ?

**Q-03 — Si B : quels livres la tablette affiche-t-elle ?** Les livres de la bourse
viennent du catalogue ISBN, pas de la table des produits. La tablette doit-elle afficher
tout le catalogue de la bourse, ou seulement les livres mis à disposition en mode Caisse ?
