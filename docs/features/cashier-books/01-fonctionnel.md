# Livres dans l'application de caisse Android — spécification fonctionnelle

> Demande d'origine (2026-11-11) : « Je veux que l'app puisse faire les livres, re rajoute cette section »
>
> Décision (2026-11-11) : la tablette calcule le prix des livres comme pour la buvette ; les sorties de livres de la bourse se font dans la nouvelle application, pas sur la tablette.

## 1. Contexte

L'application Android de caisse (`src/MauiCashApp`, distribuée en APK par le BackOffice)
propose deux catégories : **Buvette** et **Loto**. Elle sert de calculette au comptoir :
le bénévole choisit les articles, la tablette affiche le total à encaisser, et le paiement
reste physique.

Une catégorie **Livres** existait dans la version initiale du projet. Elle a été retirée
lors de la restauration de l'application (PR #357, commit `4faeb3e2`) : le filtre des
produits n'accepte plus que les sections Buvette et Loto, et les tests vérifient qu'un
livre n'apparaît pas.

Le périmètre de la bourse aux livres (`docs/bourse-aux-livres/01-vision-et-perimetre.md`,
§5) excluait cette intégration : les livres ont leur propre mode vente, et « les deux
caisses restent séparées ». La règle `RG-50` pose qu'aucun prix de livre ordinaire n'est
stocké, affiché ni totalisé par l'application de bourse.

Cette demande rétablit la section Livres sur la tablette **comme calculette de prix**.
Les sorties de livres de la bourse ne passent pas par la tablette : elles se font dans la
nouvelle application. Cette répartition est une dérogation assumée à `RG-50` pour la
tablette (voir CL-06).

## 2. Objectifs

- Permettre au caissier de choisir des livres sur la tablette, au même titre que la
  buvette et le loto, pour obtenir le prix à encaisser.
- Ne proposer que les livres disponibles, comme pour les autres catégories.
- Calculer le total d'un panier qui mélange buvette, loto et livres.

## 3. Hors périmètre

| Exclusion | Motif |
|---|---|
| Sortie de stock des livres | Faite dans la nouvelle application, pas sur la tablette. |
| Enregistrement ou transmission des ventes | La tablette n'enregistre rien (CL-05). |
| Livres rares | Fiches autonomes gérées dans la Scanette ; hors de la tablette. |
| Modification de la Scanette et du site public | Non concernés. |
| Paiement et rendu de monnaie | Inchangé : l'encaissement reste physique. |

## 4. Acteurs

- **Caissier** : bénévole qui utilise la tablette au comptoir.
- **Administrateur** : fixe le prix des livres sur leur fiche produit, depuis le BackOffice.

## 5. Parcours

1. Le caissier ouvre la caisse ; l'onglet Buvette est sélectionné par défaut.
2. Il touche l'onglet **Livres** : la grille affiche les livres disponibles, avec leur prix.
3. Il touche un livre : il est ajouté au panier. Un nouvel appui augmente la quantité.
4. Le total du panier, buvette et livres confondues, est affiché.
5. `VALIDER` vide le panier, comme pour les autres produits. Aucune trace n'est conservée.

## 6. Écrans

- **E1 · Onglet Livres** — *nouveau*. Écran d'origine : écran de caisse, barre des
  catégories (Buvette, Loto). Même forme et même taille que les deux autres onglets, avec
  une icône de livre. Ordre : Buvette, Loto, Livres.
- **E2 · Grille des livres** — *nouveau*. Écran d'origine : grille de produits des
  catégories Buvette et Loto. Mêmes cartes, avec le prix du livre.
- **E3 · Panier** — *modifié*. Écran d'origine : colonne panier, total et bouton
  `VALIDER`. Un livre s'y ajoute comme un autre produit, et le total l'inclut.
- **E4 · États de la grille Livres** — *nouveau*. Trois états : grille vide (aucun livre
  disponible), chargement, hors ligne avec le catalogue en cache sur la tablette.

## 7. Règles métier

- **CL-01** — La section Livres n'affiche que les livres disponibles. Un livre désactivé
  n'apparaît pas, y compris depuis le cache hors ligne.
- **CL-02** — Un livre suit la même règle d'ajout au panier qu'un autre produit : une
  ligne par livre, quantité augmentée à chaque appui.
- **CL-03** — La section Livres ne remplace ni la Buvette ni le Loto. Aucun livre n'apparaît
  dans ces deux onglets, et l'ordre des onglets est Buvette, Loto, Livres.
- **CL-04** — Le prix affiché d'un livre est celui de sa fiche produit. Le total du panier
  additionne les livres aux autres produits.
- **CL-05** — La tablette n'enregistre aucune sortie de livre, aucune vente et ne transmet
  rien au système de la bourse. Le total est une aide au calcul, pas une écriture.
- **CL-06** — **Dérogation à `RG-50`, pour la tablette seulement.** La calculette affiche
  et totalise le prix des livres ordinaires. `RG-50` reste vrai pour le système de la
  bourse : aucun prix de livre ordinaire n'y est calculé, et les sorties sont enregistrées
  dans la nouvelle application. Cette dérogation doit être écrite dans les docs de la bourse
  (voir §9).
- **CL-07** — Hors ligne, la calculette reste utilisable avec le catalogue en cache. Les
  totaux ne dépendent d'aucun appel réseau.

## 8. Critères d'acceptation

- Quand le caissier touche l'onglet Livres, seuls les livres disponibles apparaissent,
  chacun avec son prix.
- Un livre désactivé n'apparaît pas, même hors ligne avec le cache.
- Aucun livre n'apparaît dans les onglets Buvette ou Loto.
- Ajouter un livre au panier met à jour le total ; `VALIDER` vide le panier.
- Aucune requête d'enregistrement de vente n'est émise par la tablette.
- Sans livre disponible, la grille affiche l'état vide.
- Hors ligne avec le cache, les livres disponibles restent affichés.
- Les tests `CashierProductFilterTests` et `Categories_OnlyExposeBuvetteAndLoto` sont
  mis à jour : les livres disponibles sont inclus, les livres désactivés restent exclus.

## 9. Contraintes et mises à jour de docs

- Les produits de la tablette viennent de `GET /product`, endpoint public sans connexion
  (`src/MauiCashApp/README.md`). Aucune connexion n'est ajoutée : la tablette ne fait que
  lire le catalogue.
- L'icône de la catégorie est `Resources/Images/book.svg`. La référence `book.png` est correcte : le plugin MAUI génère le PNG à la compilation (`MauiImage Include="Resources\Images\*"`), comme `drink.png` et `loto.png` aujourd'hui.
- Application Android uniquement (`net10.0-android`).
- Docs à mettre à jour pendant l'implémentation, pour acter la calculette :
  - `docs/bourse-aux-livres/01-vision-et-perimetre.md` §5 : la ligne « Intégration à
    l'application MAUI de caisse buvette » devient une exclusion limitée aux sorties et
    aux ventes ; l'affichage des prix sur la tablette est autorisé.
  - `docs/bourse-aux-livres/06-regles-metier.md`, `RG-50` : ajouter la précision « la
    calculette de la tablette affiche les prix, sans rien enregistrer ».
  - `.github/memory/04-frontend.md` : la phrase « books are hidden » ne sera plus vraie.

## 10. Questions ouvertes

**Q-01 — Tranchée le 2026-11-11** : option A, restaurer la section comme calculette de
prix. Les sorties de livres se font dans la nouvelle application.

**Q-02 — À confirmer avant la livraison :** des produits de section Livres avec un prix
existent-ils en base ? La page Caisse du BackOffice propose toujours un onglet « Livres »
(`src/BackOffice/src/app/feature/caisse/caisse.component.ts`). Si la réponse est non, la
grille sera vide après la mise en production.

**Q-03 — À confirmer :** la « nouvelle application » pour les sorties de livres est-elle la
Scanette (`src/Scan`) ? La spec le suppose, car c'est là que la bourse enregistre les
sorties dans les docs existants.
