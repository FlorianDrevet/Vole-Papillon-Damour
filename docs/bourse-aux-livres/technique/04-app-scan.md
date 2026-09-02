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

**La réponse porte aussi les paramètres**, systématiquement : les neuf entiers de
`AssociationSettings` ([`02`](02-modele-de-donnees.md) §2) et leur `UpdatedAt`. Sans eux,
le verdict calculé hors ligne au §5 n'applique pas les seuils réels de `RG-10` et
`RG-12` — il applique ceux qui étaient codés en dur le jour de la construction, ce qui
vide `ENF-25` de son sens dès qu'un administrateur ajuste une valeur. Neuf entiers ne
coûtent rien, et les joindre au delta évite un second appel susceptible d'échouer seul.

**Afficher la fraîcheur** (`ENF-05`) : un appareil non synchronisé depuis deux jours
affiche « premier exemplaire » sur un livre entré hier. Prévoir un bandeau « données du
12 mars » dès que l'écart dépasse un seuil.

### File de sortie

Chaque geste porte un identifiant produit par le client. La transmission se fait par
lots idempotents (`03` §4) : un rejeu après coupure ne duplique rien.

**Elle doit survivre à la fermeture de l'application et à une batterie à plat**
(`ENF-05`). Le nombre de gestes en attente reste visible en permanence (`ENF-07`) — un
bénévole ne doit jamais ranger un appareil en croyant son travail enregistré.

## 3. Le déroulé d'un scan

Un scan produit **deux choses indépendantes**. Tout le reste en découle.

| | Origine | Délai | Peut échouer ? |
|---|---|---|---|
| **Le verdict** — doublons, ventes, demande | Copie locale | Instantané | Non |
| **Les métadonnées** — titre, auteur, couverture | Serveur, puis BnF | jusqu'à ~1 s | **Oui, sans conséquence** |

```
  lecture du code-barres
          │
          ▼
  normalisation ISBN-13 + clé (RG-01)    ← purement local, aucun réseau
          │
          ▼
  lecture dans IndexedDB `catalog`
          │
          ├───── trouvé ──────► verdict + titre affichés
          │                     ~0 ms, aucun réseau
          │
          └───── absent ──────► verdict « premier exemplaire » affiché
                                immédiatement
                                     │
                    ┌────────────────┴────────────────┐
                    ▼                                 ▼
          écriture dans `outbox`            requête métadonnées
          (durable, obligatoire)            (best-effort, jetable)
                    │                                 │
                    ▼                                 ▼
          vidage dès que le réseau           le titre remplit sa zone
          le permet                          s'il arrive à temps
```

### « En parallèle » : ce que cela impose

**L'interface n'attend jamais les métadonnées.** Elle rend l'écran avec ce qu'elle
sait. C'est `ENF-02` appliqué au client.

**La zone du verdict ne bouge pas quand le titre arrive.** Réserver la place par un
gabarit de hauteur fixe. Si l'apparition du titre pousse le verdict vers le bas, le
bénévole qui était en train de le lire perd sa ligne — sur des centaines de livres,
c'est une gêne permanente.

**Une réponse tardive doit être jetée.** À deux secondes par livre et 800 ms de réseau,
la réponse arrive fréquemment **après** que le livre suivant a été scanné. Chaque
réponse porte l'ISBN demandé et n'est appliquée que si l'écran affiche encore ce
livre. Sans ce garde-fou, on obtient le titre du livre précédent sur le livre
courant — un défaut qui rend l'outil incompréhensible sur le terrain.

**Le geste passe toujours par `outbox`, connecté ou non.** Il n'existe pas de branche
« si en ligne, envoyer directement ». C'est ce qui garantit que le mode hors ligne
n'est pas un chemin exotique testé une fois : **c'est le seul chemin**, et être
connecté signifie seulement que la file se vide immédiatement.

**Les métadonnées ne transitent pas par la réponse du POST de scan.** Deux raisons : le
POST doit répondre vite, et hors ligne il n'y a pas de réponse du tout. C'est une
requête distincte, best-effort, dont l'échec est sans effet.

## 4. Le mode hors ligne

### Fonctionnellement

| | |
|---|---|
| **Identique** | Scanner, obtenir un verdict, garder ou écarter, enchaîner, scanner en caisse, consulter. Le bénévole ne change aucun geste |
| **Dégradé** | Un livre inconnu de la copie locale n'affiche que son ISBN. Les compteurs reflètent la dernière synchronisation, pas la réalité |
| **Impossible** | Ouvrir une session en mode `PROCHAINE BOURSE` si aucune date n'a jamais été synchronisée |

**La péremption est le point à comprendre.** Deux bénévoles trient en parallèle sur deux
appareils. A scanne un titre, voit 5 exemplaires, en garde un. B fait de même au même
moment, voit 5 aussi. Résultat : 7 en rayon pour un seuil à 5.

C'est inhérent à une décision prise localement, et **ce n'est pas grave** : `RG-10` est
un seuil indicatif, pas une contrainte comptable. Mais cela impose de synchroniser
souvent dès qu'il y a du réseau, et d'afficher la fraîcheur (`ENF-05`) — un bénévole
hors ligne depuis deux jours doit comprendre que « premier exemplaire » signifie
« inconnu de mon appareil », pas « inconnu de l'association ».

### Techniquement

**1. Demander le stockage persistant.** Le point le plus important, et le plus oublié :
un navigateur **peut supprimer IndexedDB** sous pression de stockage. Perdre `catalog`
est indolore, il se resynchronise. Perdre `outbox`, c'est perdre des heures de tri
bénévole.

Demander explicitement la persistance au premier lancement, **vérifier que la demande a
été accordée**, et le signaler si elle ne l'est pas. À éprouver sur iPhone, où des
données de site peu visité peuvent être évincées (`QT-03`).

**2. Identifiants générés par le client.** Chaque geste porte un identifiant produit sur
l'appareil. La transmission se fait par lots contre un endpoint idempotent (`03` §4) :
un lot rejoué après une coupure en pleine transmission ne crée aucun doublon. C'est ce
qui permet de retransmettre sans raisonner.

**3. Deux horodatages, pas un.** Un geste hors ligne est daté par le client — nécessaire,
puisque le serveur ne le voit parfois que des heures plus tard. Mais **l'heure d'un
appareil n'est pas fiable** : une horloge fausse produirait des mouvements aberrants
polluant les statistiques par bourse.

Conserver donc l'heure client, pour l'ordre réel des gestes, **et** l'heure de réception
serveur, pour l'audit. Si l'heure client est absurde — dans le futur, ou antérieure au
début de la session — se rabattre sur l'heure serveur et marquer le mouvement.

**4. Vidage de la file.** Déclenché au retour au premier plan de l'application, à la
détection du retour réseau, et par une minuterie tant que l'application est ouverte. Ne
pas dépendre uniquement d'une API de synchronisation en arrière-plan : son support est
inégal selon les navigateurs.

Le vidage est séquentiel et reprend là où il s'est arrêté. Le nombre de gestes en
attente reste visible en permanence (`ENF-07`).

**5. La clôture automatique face à un appareil hors ligne.** Le piège le plus subtil.

`RG-43` clôt une session après 2 h sans scan. Or le serveur juge l'inactivité sur ce
qu'il a **reçu**. Un bénévole qui trie hors ligne pendant trois heures paraît inactif :
sa session est close, ses alertes partent, puis ses scans arrivent — dans une session
déjà fermée.

Deux règles pour traiter ce cas :

- Le balayage ne clôt une session que si l'appareil **n'a pas non plus donné signe de
  vie** : conserver un `LastSyncAt` distinct de `LastScanAt`.
- Des gestes arrivant après une clôture sont **acceptés dans leur session d'origine**.
  Les alertes qu'ils déclenchent forment une nouvelle mise en file ; l'anti-répétition
  de `RG-30` empêche qu'un membre soit prévenu deux fois. La session est marquée comme
  ayant reçu des arrivées tardives, visible en administration.

**6. Service worker.** Mise en cache de la coquille applicative, pour que l'application
démarre sans réseau. Séparer strictement le cache de la coquille des magasins de
données : une purge de l'un ne doit jamais toucher les autres.

## 5. Le verdict, côté client

Calculé localement depuis `catalog`, en appliquant `RG-10` à `RG-15`. Le serveur
recalcule à la réception : **le client affiche, le serveur fait foi.**

Les seuils viennent du serveur (`ENF-25`) et sont mis en cache avec le catalogue, pour
rester applicables hors ligne.

ISBN absent de `catalog` : verdict « premier exemplaire » immédiat — inconnu localement
signifie zéro exemplaire et zéro vente — puis appel serveur en tâche de fond pour
obtenir le titre. Sans réseau, le geste part en file avec le seul ISBN.

## 6. Écrans

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

## 7. Lecture du code-barres

À valider au palier 0 (`QT-03`).

| Support | Mécanisme |
|---|---|
| Téléphone | Caméra via l'API navigateur. À éprouver sur couvertures abîmées, plastifiées, froissées |
| Scanette à gâchette | Se comporte comme un clavier : le code arrive suivi d'un retour chariot. Plus simple et plus fiable |

**Accepter les deux dès le départ** : une écoute clavier globale coûte peu et évite une
réécriture le jour de l'achat du matériel.

## 8. Ce que l'application ne fait pas

- Pas de modification des métadonnées — c'est l'administration.
- Pas de statistiques au-delà du compteur de session.
- Pas de prix, pas de total, pas d'encaissement (`RG-50`).
- Pas de gestion des comptes du public.
