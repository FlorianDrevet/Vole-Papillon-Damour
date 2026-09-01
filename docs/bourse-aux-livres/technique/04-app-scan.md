# 04 — Application de scan

PWA Angular (`DT-08`), déployée en Container App dans l'environnement existant.
Réutilise `SharedUi` (`@vpd/ui`).

## 1. Ce qui contraint la conception

| Exigence | Conséquence technique |
|---|---|
| `ENF-01` — verdict en moins d'une seconde | Le verdict se calcule **localement**, sans aller-retour serveur |
| `ENF-05` — hors ligne | Copie complète du catalogue embarquée + file de sortie persistante |
| `ENF-03` — un scan toutes les 2 s pendant une heure | Pas de fuite mémoire, pas de re-rendu global à chaque scan |
| `RG-19` — zéro appui dans le cas nominal | Le scan suivant valide le précédent |
| `ENF-19` — bénévoles âgés, local mal éclairé | Contraste, gros caractères, verdict jamais porté par la seule couleur |

## 2. Stockage local (IndexedDB)

Trois magasins, aux durées de vie très différentes.

| Magasin | Contenu | Nature |
|---|---|---|
| `catalog` | Tout le catalogue, projection compacte | **Jetable** — se resynchronise |
| `outbox` | Gestes non encore transmis | **Précieux** — c'est du travail bénévole |
| `session` | Session en cours, mode, compteurs | Précieux |

**Ne jamais confondre `catalog` et `outbox`.** Le premier peut être effacé sans
conséquence ; perdre le second, c'est perdre des heures de tri. Une purge de cache
« pour repartir propre » qui viderait les deux serait un incident majeur.

### Projection `catalog`

```
{ isbn13, title, authors, workId,
  qtyAvailable, qtyAnnounced, salesCount,
  isWanted, isRare }
```

~200 octets par titre, **~3 Mo pour 15 000 titres**. Assez petit pour tout embarquer :
la question du volume ne se pose pas. Pas de couvertures — elles se chargent en ligne.

`isWanted` est un booléen dérivé côté serveur, **jamais l'identité des demandeurs**
(`RG-42`).

### Synchronisation delta

`GET /scan/catalog/delta?since={filigrane}` renvoie les entrées dont `UpdatedAt` est
postérieur. Quelques centaines de lignes par jour. Aucune résolution de conflit : les
mouvements sont cumulatifs et en ajout seul (`ENF-06`).

**Afficher la fraîcheur** (`ENF-05`) : un appareil non synchronisé depuis deux jours
affiche « premier exemplaire » sur un livre entré hier. Prévoir un bandeau « données du
12 mars » dès que l'écart dépasse un seuil.

### File de sortie

Chaque geste porte un identifiant produit par le client. La transmission se fait par
lots idempotents (`03` §4) : un rejeu après coupure ne duplique rien.

**Elle doit survivre à la fermeture de l'application et à une batterie à plat**
(`ENF-05`). Le nombre de gestes en attente reste visible en permanence (`ENF-07`) — un
bénévole ne doit jamais ranger un appareil en croyant son travail enregistré.

## 3. Le verdict, côté client

Calculé localement depuis `catalog`, en appliquant `RG-10` à `RG-15`. Le serveur
recalcule à la réception : **le client affiche, le serveur fait foi.**

Les seuils viennent du serveur (`ENF-25`) et sont mis en cache avec le catalogue, pour
rester applicables hors ligne.

ISBN absent de `catalog` : verdict « premier exemplaire » immédiat — inconnu localement
signifie zéro exemplaire et zéro vente — puis appel serveur en tâche de fond pour
obtenir le titre. Sans réseau, le geste part en file avec le seul ISBN.

## 4. Écrans

Suivre `../03-parcours-benevole-scan.md`. Trois points où l'implémentation dérape
facilement :

**Le bandeau de mode ne disparaît jamais.** C'est la seule protection contre une
session entière tenue dans le mauvais mode (`RG-20`). Ni au défilement, ni pendant un
chargement, ni sur l'écran de résultat.

**L'écran de fin de session est un écran métier, pas une confirmation.** Il annonce
combien de personnes seront prévenues et dans combien de temps (`RG-44`). C'est le
dernier moment utile pour repérer une erreur.

**La saisie manuelle d'ISBN ne doit jamais devenir le chemin nominal.** Elle apparaît
après quelques secondes d'échec de lecture, pas avant.

## 5. Lecture du code-barres

À valider au palier 0 (`QT-03`).

| Support | Mécanisme |
|---|---|
| Téléphone | Caméra via l'API navigateur. À éprouver sur couvertures abîmées, plastifiées, froissées |
| Scanette à gâchette | Se comporte comme un clavier : le code arrive suivi d'un retour chariot. Plus simple et plus fiable |

**Accepter les deux dès le départ** : une écoute clavier globale coûte peu et évite une
réécriture le jour de l'achat du matériel.

## 6. Ce que l'application ne fait pas

- Pas de modification des métadonnées — c'est l'administration.
- Pas de statistiques au-delà du compteur de session.
- Pas de prix, pas de total, pas d'encaissement (`RG-50`).
- Pas de gestion des comptes du public.
