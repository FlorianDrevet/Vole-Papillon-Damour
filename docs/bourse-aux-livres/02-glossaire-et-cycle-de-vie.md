# 02 — Glossaire et cycle de vie

## 1. Glossaire

Ce vocabulaire est contraignant : il doit être employé tel quel dans les écrans, les
documents et le code.

| Terme | Définition |
|---|---|
| **ISBN** | Identifiant international unique d'une **édition**, encodé dans le code-barres au dos du livre. Deux éditions d'un même texte ont deux ISBN différents. Existe en 10 et 13 chiffres ; on stocke toujours la forme à 13 chiffres. |
| **Œuvre** | Le texte, indépendamment de ses éditions. *Le Petit Prince* est une œuvre ; ses dizaines de tirages chez différents éditeurs en sont les éditions. Sert exclusivement à rapprocher une demande d'un livre scanné (`RG-46`) — **le stock n'est jamais géré au niveau de l'œuvre**. |
| **Édition** | Un tirage précis d'une œuvre : éditeur, format, année. Identifiée par son ISBN. **C'est l'unité de gestion du stock.** |
| **Fiche livre** | L'unité de gestion du système. Une fiche par ISBN, donc par édition : métadonnées, quantités, historique de vente, statut, et l'œuvre dont elle relève. **Il n'existe pas d'entité « exemplaire ».** |
| **Référentiel bibliographique** | Source externe décrivant les livres publiés, interrogeable par titre, auteur ou ISBN. Alimente les métadonnées au scan et permet de suivre un livre que l'association n'a jamais reçu (`RG-47`). Choix de la source : `Q-10`. |
| **Quantité disponible** | Nombre d'exemplaires vendables dès à présent. |
| **Quantité annoncée** | Nombre d'exemplaires promis pour une bourse à venir, pas encore vendables. |
| **Métadonnées** | Titre, auteur, éditeur, année, genre, image de couverture. Obtenues automatiquement à partir de l'ISBN. |
| **Tri** | Opération par laquelle un bénévole décide, don en main, de garder ou d'écarter un livre. |
| **Écarté** | Livre refusé au tri. Il quitte définitivement le circuit. Compté, jamais mis à disposition. |
| **Mode de mise à disposition** | Choix fait **avant de commencer à scanner**, valable pour toute la session : `DISPONIBLE MAINTENANT` ou `PROCHAINE BOURSE`. C'est ce choix qui détermine l'effet public de chaque scan. |
| **Session de scan** | Ensemble des scans réalisés par un bénévole sous un même mode. Elle s'ouvre au choix du mode et se clôt sur demande, après 2 h d'inactivité, à la déconnexion ou à l'expiration du jeton (`RG-43`). **C'est l'unité de correction** — une erreur de mode se répare en rebasculant la session entière — **et le fait générateur des alertes**, mises en file à sa clôture puis envoyées 2 h plus tard (`RG-44`). |
| **Annoncé** | État d'un livre scanné en mode `PROCHAINE BOURSE`. Visible en ligne avec sa date, mais pas encore vendable. |
| **Bascule** | Passage automatique d'annoncé à disponible, à la date d'ouverture de la bourse de rattachement. Aucun geste humain. |
| **Session de bourse** | Période de vente ouverte au public. Correspond à un `AssoEvents` de type `Books` existant. |
| **Vente** | Sortie d'un exemplaire, enregistrée par un scan à la caisse. Décrémente la quantité disponible. |
| **Livre rare** | Fiche dont la valeur estimée dépasse le seuil de l'association. Vendue hors du tarif 1–2 €, dans une section dédiée. |
| **Liste de recherche** | Ensemble des ISBN qu'un membre inscrit déclare rechercher. |
| **Alerte** | E-mail envoyé à un membre quand un livre de sa liste devient disponible ou est annoncé pour une bourse datée. |
| **Remise à plat** | Correction périodique des quantités pour absorber les ventes non scannées. Voir `RG-31`. |

### Termes à ne pas employer

| À éviter | Pourquoi | Employer |
|---|---|---|
| « Produit » | Réservé à la buvette (agrégat `Product` existant). Les livres ne sont pas des produits. | Fiche livre |
| « Stock » seul | Ambigu entre le local et les quantités d'une fiche | Quantité disponible / annoncée |
| « Commande » | Réservé à l'agrégat `Order` de la buvette | Vente |
| « Lot », « carton » | Ces notions ont été écartées (`Q-01`). Il n'existe aucun regroupement physique suivi par le système. | Session de scan |
| « Mise en rayon » | Suggère un geste humain de publication, qui n'existe pas | Mise à disposition |
| « Réservé » | Aucune réservation n'existe en v1 | — |

## 2. Cycle de vie d'un livre

```
                              don reçu
                                 │
                                 ▼
                      ┌────────────────────┐
                      │    scan au tri     │
                      │  (dans une session │
                      │   ayant un mode)   │
                      └─────────┬──────────┘
                                │
                 ┌──────────────┴───────────────┐
                 ▼                              ▼
          ┌────────────┐                    gardé
          │  ÉCARTÉ    │                       │
          │ (terminal) │          ┌────────────┴────────────┐
          └────────────┘          │  selon le mode de la    │
                                  │        session          │
                                  └────────────┬────────────┘
                mode PROCHAINE BOURSE          │       mode DISPONIBLE MAINTENANT
                       ┌───────────────────────┴───────────────────────┐
                       ▼                                               │
                ┌─────────────┐                                        │
                │   ANNONCÉ   │   visible en ligne avec sa date,       │
                │  (bourse X) │   non vendable                         │
                └──────┬──────┘                                        │
                       │  bascule automatique                          │
                       │  à la date d'ouverture de X                   │
                       ▼                                               ▼
                ┌────────────────────────────────────────────────────────┐
       ┌───────►│                      DISPONIBLE                        │◄──────┐
       │        └───────────────────────┬────────────────────────────────┘       │
       │                                │                                        │
 remise à plat                   scan de vente                            autre don du
 (correction)                           │                                 même titre
       │                                ▼                                        │
       │                         ┌─────────────┐                                 │
       └─────────────────────────┤    VENDU    ├─────────────────────────────────┘
                                 │ (compteur)  │
                                 └─────────────┘
```

**Points d'attention sur ce schéma.**

- Les états ne portent pas sur un exemplaire mais sur **des quantités attachées à une
  fiche**. Une même fiche peut simultanément avoir 3 exemplaires disponibles,
  2 annoncés pour la bourse de mars et 47 ventes cumulées.
- **La seule décision humaine de publication est le choix du mode, fait une fois avant
  de scanner.** Il n'existe aucun geste de validation ultérieur.
- **La bascule est automatique et pilotée par la date de l'événement.** Personne ne la
  déclenche ; personne ne peut l'oublier.
- L'état `ÉCARTÉ` est terminal et sans quantité récupérable : on n'incrémente qu'un
  compteur de refus, pour la statistique.

## 3. Les deux modes de mise à disposition

C'est le cœur du dispositif. Le mode est choisi avant le premier scan et s'applique à
toute la session (`RG-20`).

| | `DISPONIBLE MAINTENANT` | `PROCHAINE BOURSE` |
|---|---|---|
| **Quand l'utiliser** | Les livres partent directement en rayon, ou y sont déjà | Les livres sont triés en avance et seront rangés d'ici la prochaine bourse |
| **Effet immédiat** | Quantité disponible +1 | Quantité annoncée +1, rattachée à une bourse |
| **Sur le site public** | « Disponible » | « Disponible à partir du 14 mars » |
| **Effet différé** | aucun | Bascule automatique en disponible à la date d'ouverture |
| **Alerte e-mail** | « disponible » | « sera disponible le 14 mars » |

Ce dispositif répond au besoin d'origine — ne pas annoncer comme disponible un livre
encore dans un carton — **sans imposer un second scan ni un geste de validation**. Le
livre annoncé est visible en ligne dès le tri, mais avec une date : la promesse faite
au public est datée, donc tenable.

## 4. Modèle conceptuel

Description fonctionnelle des informations à conserver. Ce n'est pas un schéma de base
de données : la modélisation technique est un travail distinct.

### Fiche livre

Identifiée par son ISBN-13.

| Information | Nature | Origine |
|---|---|---|
| ISBN-13 | identifiant | scan |
| Identifiant de l'œuvre | rattachement | référentiel bibliographique. Peut être absent : la fiche reste exploitable, mais aucune demande de portée `ŒUVRE` ne s'y rapprochera (`RG-46`) |
| Titre, auteur(s), éditeur, année, genre | métadonnées | source externe, corrigeable par un administrateur |
| Image de couverture | métadonnée | source externe |
| Quantité disponible | compteur | calculé par les mouvements |
| Quantité annoncée, par bourse de rattachement | compteur | calculé par les mouvements |
| Ventes cumulées | compteur | scans de vente |
| Refus cumulés | compteur | scans de tri écartés |
| Valeur estimée + date d'estimation | métadonnée | source de prix, voir `Q-02` |
| Marquée « rare » | indicateur | automatique par seuil, ou manuel |
| Date de première entrée, date de dernière mise à disposition | horodatage | mouvements |
| Masquée du catalogue public | indicateur | administrateur |

### Mouvement

Toute variation de quantité est tracée. C'est la source de vérité ; les compteurs de la
fiche en découlent.

| Information | Exemple |
|---|---|
| ISBN concerné | `9782070408504` |
| Type | `ENTREE_ANNONCE`, `ENTREE_DIRECTE`, `BASCULE`, `VENTE`, `REFUS`, `CORRECTION`, `RETRAIT` |
| Quantité | `+1`, `-1`, `-12` |
| Date et heure | `2026-03-14T10:32:17+01:00` |
| Session de scan d'origine | identifiant de session |
| Bénévole auteur du geste | identifiant du compte bénévole |
| Bourse de rattachement, si applicable | identifiant `AssoEvents` |

Conserver les mouvements plutôt que le seul compteur permet les statistiques par
bourse, l'annulation d'une erreur de scan, la reprise en bloc d'une session au mauvais
mode (`RG-22`), et l'audit d'un écart d'inventaire.

### Session de scan

Remplace la notion de lot, écartée en `Q-01`. Elle n'a **aucune existence physique** :
c'est une unité de travail et de correction, pas un carton.

| Information |
|---|
| Identifiant |
| Mode : `DISPONIBLE_MAINTENANT` ou `PROCHAINE_BOURSE` |
| Bourse de rattachement, si le mode l'exige et si elle est connue |
| Bénévole, date de début, date de fin, durée |
| Cause de clôture : `MANUELLE`, `INACTIVITE`, `DECONNEXION`, `JETON_EXPIRE` |
| Mouvements produits, compteurs de livres scannés / gardés / écartés |
| Alertes : état (`EN_ATTENTE` / `ENVOYEES` / `ANNULEES`), heure d'envoi prévue, nombre |
| Statut : `EN_COURS`, `TERMINEE`, `REPRISE` (si corrigée par un administrateur) |

### Membre du site

| Information | Remarque |
|---|---|
| Identifiant Entra External ID | l'association ne stocke aucun mot de passe |
| Adresse e-mail | canal des alertes |
| Liste de recherche : une entrée par livre suivi | Chaque entrée porte une **portée** (`OEUVRE` ou `EDITION`), la cible correspondante (identifiant d'œuvre ou ISBN-13), et sa date d'ajout (`RG-46`). Une entrée peut ne correspondre à aucune fiche : le livre n'a jamais été reçu (`RG-47`) |
| Préférences d'alerte, statut du compte (actif / bloqué) | |
| Historique des alertes envoyées | évite les doublons d'envoi — `RG-26` |

### Bénévole

| Information |
|---|
| Identifiant, nom, statut actif ou non |
| Droits : tri, caisse, administration |
| Appareils associés |

## 5. Relation avec le domaine existant

| Élément existant | Relation |
|---|---|
| `AssoEvents` (type `Books`) | Une session de bourse **est** un `AssoEvents` existant. Sa date d'ouverture **pilote la bascule automatique** (`RG-21`) et les ventes s'y rattachent. La dépendance est désormais forte : la qualité de l'agenda conditionne le bon fonctionnement du dispositif. |
| `Product`, `Order` (buvette) | **Aucune relation.** Les livres ne sont pas des produits et les ventes de livres ne sont pas des commandes. Toute tentative de réutiliser ces agrégats introduirait une confusion durable. |
| `User` | **L'authentification maison est supprimée** (`DT-10`). Bénévoles, administrateurs et membres du public relèvent tous d'Entra External ID. La table `User` subsiste sans mot de passe ni rôle : elle sert de point d'ancrage local pour l'attribution des gestes (`RG-41`), rattachée au compte par son identifiant d'objet. |
| `Website`, `BackOffice` | Le site public du catalogue est une application distincte. Seul un lien depuis le site de l'association vers le catalogue est prévu. |
