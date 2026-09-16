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

Maquette `CaisseRare`, trois moments.

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

Le bouton principal devient « **Ajouter au panier · 60 €** », et le secondaire
« Ce n'est pas ce livre ».

Le bloc existant `.cash-rare-item` (`styles.scss:1513`) en est l'ancêtre : il a déjà le
fond violet et la bonne présence. Il porte aujourd'hui `min-height: 148px` et aucun
prix ; c'est lui qu'il faut faire grandir, pas un nouveau composant.

### Moment 2 — un rare sans ISBN

Rien à scanner. Un champ de recherche bordé violet, arrondi, 54px, avec le compteur de
résultats à droite. « La recherche porte sur les fiches rares embarquées dans la
scanette, prix compris. Elle marche sans réseau. »

Les résultats sont les cartes rares compactes ; la sélectionnée est bordée
`1.5px #5f3a86`.

Repli : « Aucune fiche ne correspond ? Encaissez en “livre rare hors catalogue” et
prévenez l'administration après la bourse. » Voir `Q3`.

### Moment 3 — vente validée

Écran sombre `#062a44` avec un halo violet, coche verte, et le montant en `Newsreader`
44px. Puis un encart « Ce que ça a changé », qui énumère en clair :

- le livre est retiré de la page Livres rares ;
- les personnes qui le suivaient reçoivent un e-mail « il est parti » ;
- le montant est compté dans les recettes de la bourse.

C'est de la pédagogie, pas de la décoration : c'est ce qui rend le geste
compréhensible.

## 6. Le panier et le total

⚠️ **Ce paragraphe dépend de `Q1`** ([`01 §4`](01-decisions-et-portee.md)) et ne doit
pas être implémenté avant qu'elle soit tranchée.

La maquette montre « Panier · 3 livres — 3,00 € » puis « Total à encaisser 63,00 € ».
Sous l'hypothèse par défaut (`D4`) :

- les livres ordinaires sont comptés et multipliés par le prix unitaire de la bourse,
  lu dans `AssociationSettings` ;
- les livres rares ajoutent leur prix ferme individuel ;
- le total est **affiché**, jamais encaissé par l'application.

L'écran de caisse actuel a été explicitement réécrit sans colonne prix ni total par la
décision `Q-05`, et son bouton dit `VALIDER` et non `ENCAISSER`. Le rétablissement d'un
total revient en arrière sur ce point précis : le signaler dans la PR et amender
`docs/bourse-aux-livres/03-parcours-benevole-scan.md` §5.

`RegisterSaleCommandHandler` doit désormais, pour un livre rare :

1. appeler `MarkRareBookSoldCommand` ;
2. enregistrer le montant sur la vente, pour la recette (`D5`) ;
3. rester **idempotent** — la caisse fonctionne hors ligne et rejoue ses gestes. Le
   mécanisme existe déjà (`ClientGestureId`, inversion de vente) : s'y raccrocher, ne
   pas en inventer un second.

L'annulation de vente dans les 30 secondes (`RG-49`, visible dans la maquette) doit
**remettre la fiche en disponible** et annuler l'alerte si elle n'est pas encore partie.
Sans cela, une erreur de caisse envoie un e-mail « il est parti » à des gens pour un
livre toujours sur la table.

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
  date de dernière synchronisation sur l'écran de caisse rare.
