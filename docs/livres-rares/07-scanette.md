# 07 — Scanette

Application : `src/Scan`. Maquette de référence : `CaisseRare` (trois écrans de caisse).
La **gestion** des fiches depuis la scanette n'a pas de maquette : voir
[`01 §3`](01-decisions-et-portee.md) et `Q5`.

C'est la partie la plus risquée du module, pour une raison : la scanette est
**hors-ligne d'abord**. Tout ce qui y est ajouté doit fonctionner sans réseau, dans une
salle des fêtes, sur le téléphone personnel d'un bénévole.

## 1. Tuile d'accueil

`src/Scan/src/app/scanner/scanner.component.html`, entre `home-mode-tri` (ligne 52) et
`home-mode-cash` (ligne 59) :

```html
<button *ngIf="canManageRareBooks" type="button" class="home-mode home-mode-rare"
        (click)="openRareBooks()">
  <svg …></svg>
  <span>Livres rares</span>
</button>
```

Styles dans `src/Scan/src/styles.scss`, à côté de `.home-mode-tri` (ligne 245) et
`.home-mode-cash` (ligne 261). Fond `var(--scan-purple)`, texte blanc — la variable
existe déjà et sert au verdict « Rare » (ligne 927) et à l'alerte caisse (ligne 1521).

Vérifier le comportement responsive : la média-query de la ligne 1891 traite
`.home-mode-actions` et `.home-mode-tri` spécifiquement. Une troisième tuile change la
hauteur disponible sur les petits écrans.

## 2. Routes

`src/Scan/src/app/scan-routing.ts` :

```ts
const rareGuard = scanRoleGuard('LivresRares');

{path: 'livres-rares', component: RareBookListComponent, canActivate: [rareGuard]},
{path: 'livres-rares/nouveau', component: RareBookFormComponent, canActivate: [rareGuard]},
{path: 'livres-rares/:id', component: RareBookFormComponent, canActivate: [rareGuard]},
```

Voir [`04 §5`](04-role-livres-rares.md) pour la refonte de `scanRoleGuard`, dont la
forme actuelle est un ternaire à deux branches.

## 3. Parcours de gestion

Cinq écrans, un geste par écran. Le bénévole est debout, à côté d'une table de tri.

1. **Liste** — recherche, fiches (vignette, titre, prix, statut), bouton flottant
   « Ajouter un livre ». Reprendre les cartes de `LivresRaresMobile`.
2. **Fourche** — deux choix pleine largeur, également valorisés :
   « Scanner l'ISBN » et « Ce livre n'a pas d'ISBN ». Aucun des deux n'est un repli.
   Une phrase courte explique que beaucoup de livres anciens n'en ont pas.
3. **Candidat bibliographique** (branche ISBN) — carte du résultat BnF/OpenLibrary,
   « Utiliser ces informations » / « Corriger », et la variante « Aucune référence
   trouvée — passer en saisie manuelle ». Réutiliser
   `GET /catalog/reference/search` ([`03 §5`](03-application-et-api.md)).
4. **Formulaire en trois temps** — Identifier → Décrire & prix → Photos. Les mêmes
   champs que la fiche admin ([`06 §4`](06-portail-administration.md)), répartis pour
   qu'aucun écran ne demande plus de six champs. Un rappel discret à l'étape 2 : « Ces
   informations seront visibles publiquement. »
5. **Photos** — grille de vignettes, gros bouton « Prendre une photo », désignation de
   la vignette, suppression. Le motif de capture existe déjà :
   `<input type="file" accept="image/*" capture="environment">`
   (`scanner.component.html:1-15`).

Une fiche créée depuis la scanette part en **`Brouillon`** ([`D8`](01-decisions-et-portee.md)).
C'est ce qui rend la saisie debout acceptable : rien ne part en vitrine sans relecture.

## 4. Hors ligne — le vrai sujet

`src/Scan/src/app/offline/scan-local-store.service.ts` tient une base IndexedDB
(`scanStoreNames` : `catalog`, `outbox`, `sales`, `session`) et une file de
transmission. Les photos y ajoutent une difficulté de nature différente : ce sont des
binaires de plusieurs mégaoctets.

### Ce qu'il faut ajouter

| Élément | Décision |
|---|---|
| Magasin `rareBooks` | Les fiches rares embarquées, pour la consultation et la caisse |
| Magasin `rarePhotoQueue` | Les photos en attente, stockées en `Blob` — **jamais en base64**, qui gonfle de 33 % |
| Version de base | Incrémenter `scanDatabaseVersion` et écrire la montée de version dans `onupgradeneeded` (ligne 934) et dans `scan-local-store-migration.ts` |
| Compression | Redimensionner à 2000px de côté maximum et réencoder en JPEG qualité 0,82 **avant** la mise en file, via `createImageBitmap` + `OffscreenCanvas`. Une photo de téléphone brute fait 4 à 8 Mo ; réduite, 300 à 600 Ko |
| Quota | `navigator.storage.estimate()` avant chaque mise en file. Au-delà de 80 % d'occupation, refuser la photo avec un message explicite plutôt que d'échouer à l'écriture. Il existe déjà un précédent d'alerte de stockage : branche `fix/scan-storage-persistence-alert` |
| Transmission | File séquentielle, une photo à la fois, reprise après échec, **plafonnée en nombre de tentatives**. Suivre le patron de `scan-sync.service.ts` |
| Affichage | Badge « 3 photos en attente d'envoi » dans `scan-status-bar.component` |

⚠️ **Une fiche créée hors ligne n'a pas d'identifiant serveur.** Comme les gestes de
scan, elle a besoin d'un identifiant client (`ClientGestureId` existe déjà sur
`BookAnnouncement`). Les photos mises en file référencent cet identifiant client, et la
synchronisation doit résoudre la correspondance après création côté serveur. C'est le
point le plus délicat du lot : le traiter en premier, avec ses tests.

## 5. Caisse

Maquettes : `CaisseAjoutRare` (S3, page « Scanette ») pour le parcours retenu, et
`CaisseRare` (A12) pour le traitement visuel du bandeau violet — **dont le panier et le
total sont caducs** (`D4`).

Le geste attendu, dit par l'association : *« rajouter un bouton quelque part pour
ajouter un livre rare à la session de caisse, le chercher et l'ajouter, et quand je
valide la sortie de la session cela le sort des livres rares dispo. »*

Trois moments.

### Moment 1 — le scan tombe sur un rare

L'écran change de couleur **avant** que le bénévole puisse saisir quoi que ce soit. Le
bandeau violet plein `#5f3a86` porte :

- un libellé mono « Livre rare » précédé d'un trait orange ;
- le titre en `Newsreader` 34px blanc ;
- la mention d'auteur et l'éditeur ;
- sous un filet, « Prix ferme » et le montant en **`Newsreader` 66px** blanc, avec
  « non négociable » à droite.

Sous le bandeau : la vignette, un rappel « Vérifiez l'exemplaire » avec la description
des défauts, et « Voir les 5 photos → ». Puis un encart violet pâle : « Exemplaire
unique. Après validation, il disparaît du catalogue public immédiatement. »

Le bouton principal devient « **Ajouter à la session** ». Aucun montant n'y figure : le
prix est au-dessus, pour être lu.

Le bloc existant `.cash-rare-item` (`styles.scss:1513`) en est l'ancêtre : il a déjà le
fond violet et la bonne présence. Il porte aujourd'hui `min-height: 148px` et aucun
prix ; c'est lui qu'il faut faire grandir, pas un nouveau composant.

### Moment 2 — ajouter un rare à la main

C'est le chemin principal, puisqu'un livre rare n'a généralement pas de code-barres.

Un bouton **« Ajouter un livre rare »** dans le pied de l'écran de caisse, au-dessus de
« Valider la sortie » : contour violet `1.5px #5f3a86`, 54px, icône de marque-page. Il
n'apparaît que si le catalogue embarqué contient au moins une fiche rare disponible.

Il ouvre une recherche par titre : champ bordé violet, arrondi, 54px, compteur de
résultats à droite. « La recherche porte sur les fiches rares embarquées dans la
scanette. Elle marche sans réseau. » Les résultats sont les cartes rares compactes,
photo comprise — c'est elle qui confirme qu'on tient le bon exemplaire ; la sélectionnée
est bordée `1.5px #5f3a86`.

Sous les résultats, la phrase qui cadre le rôle de l'outil :

> Le prix est ferme et non négociable. Il s'affiche pour être lu : vous encaissez comme
> d'habitude, la scanette ne compte pas.

Un rare ajouté apparaît dans la liste de session avec une bordure violette, la mention
`Livre rare · 60 €` et une croix pour le retirer.

Repli quand rien ne correspond : le livre sort comme un livre ordinaire et
l'administration est prévenue après la bourse. Aucun écran spécifique.

### Moment 3 — sortie de session validée

C'est **ici**, et pas à l'ajout, que le livre quitte la vitrine (`D5 bis`).

Écran sombre `#062a44` avec un halo violet, coche verte, et le compte de livres sortis
en `Newsreader` 42px — **un compte de livres, jamais un montant**. Puis un encart « Ce
que ça a changé » :

- le livre quitte la page Livres rares ;
- sa fiche est conservée, marquée vendue, avec sa date et sa bourse ;
- les personnes qui le suivaient reçoivent un e-mail.

Et, en retrait, la phrase qui évite un malentendu durable :

> Aucun montant n'est enregistré. La recette reste saisie à la main à la clôture de la
> bourse.

C'est de la pédagogie, pas de la décoration : c'est ce qui rend le geste
compréhensible.

## 6. Ce que la caisse ne fait pas

Aucun panier, aucun total, aucune recette (`D4`, `D5`). L'écran de caisse garde son
bouton `VALIDER` et sa nature actuelle : il enregistre quels livres sortent.

Concrètement, pour l'implémentation :

- **Ne pas** ajouter de champ prix sur `BookMovement`, sur la vente, ni ailleurs dans le
  circuit de sortie. Le prix vit sur la fiche rare et nulle part ailleurs.
- **Ne pas** toucher à `RG-51` ni à la saisie manuelle de recette existante
  (`saveFairRevenue` dans le portail admin).
- `docs/bourse-aux-livres/03-parcours-benevole-scan.md` §5 n'a **pas** à être amendé :
  l'écran de caisse ne change pas de nature.

`RegisterSaleCommandHandler` n'est pas modifié pour les livres ordinaires. Pour un livre
rare ajouté à une session, la validation de sortie doit :

1. appeler `MarkRareBookSoldCommand` avec l'identifiant de session ;
2. rester **idempotent** — la caisse fonctionne hors ligne et rejoue ses gestes. Le
   mécanisme existe déjà (`ClientGestureId`, inversion de vente) : s'y raccrocher, ne
   pas en inventer un second.

L'annulation de sortie dans les 30 secondes (`RG-49`) appelle
`RestoreRareBookAvailabilityCommand` : elle **remet la fiche en disponible** et annule
l'alerte si elle n'est pas encore partie. Sans cela, une erreur de caisse envoie un
e-mail « il est parti » à des gens pour un livre toujours sur la table.

## 7. Delta de synchronisation

`GetCatalogDeltaQueryHandler` alimente le catalogue embarqué. Il expose aujourd'hui
`IsRare` en booléen (lignes 88, 221 et le record ligne 401).

Il doit désormais transporter, pour chaque fiche rare publiée : identifiant, titre,
mention d'auteur, **prix**, état, description courte, **URL de la vignette**, et
l'ISBN quand il existe.

Deux conséquences à mesurer avant de coder :

- **Le volume.** Le delta grossit d'une vignette par fiche rare. Avec quelques dizaines
  de fiches c'est négligeable ; prévoir tout de même une vignette dédiée de petite
  taille plutôt que la photo pleine résolution.
- **La fraîcheur du prix.** Une scanette hors ligne depuis deux jours affiche un prix
  périmé si l'administration l'a changé entre-temps. Horodater le delta et afficher la
  date de dernière synchronisation sur l'écran de caisse rare. Le risque est réel mais
  borné : le prix est lu par un humain qui a le livre en main, pas additionné par la
  machine.
- **La disponibilité.** Le delta ne transporte que les fiches **publiées et
  disponibles**. Un rare vendu sur une autre caisse disparaît des résultats de recherche
  à la synchronisation suivante — entre-temps, deux caisses peuvent le proposer. Le
  serveur tranche à la validation : le second `MarkRareBookSold` est idempotent et
  n'émet pas de seconde alerte.
