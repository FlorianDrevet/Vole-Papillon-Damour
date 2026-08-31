# 02 — Glossaire et cycle de vie

## 1. Glossaire

Ce vocabulaire est contraignant : il doit être employé tel quel dans les écrans, les
documents et le code.

| Terme | Définition |
|---|---|
| **ISBN** | Identifiant international unique d'une édition, encodé dans le code-barres au dos du livre. Deux éditions différentes d'un même texte ont deux ISBN différents. Existe en 10 et 13 chiffres ; on stocke toujours la forme à 13 chiffres. |
| **Fiche livre** | L'unité de gestion du système. Une fiche par ISBN : métadonnées, quantité en rayon, historique de vente, statut. **Il n'existe pas d'entité « exemplaire ».** |
| **Quantité en rayon** | Nombre d'exemplaires de cette fiche considérés comme disponibles à la vente. C'est un compteur, pas une liste. |
| **Métadonnées** | Titre, auteur, éditeur, année, genre, image de couverture. Obtenues automatiquement à partir de l'ISBN. |
| **Tri** | Opération par laquelle un bénévole décide, don en main, de garder ou d'écarter un livre. |
| **Retenu** | Livre gardé au tri. Il n'est pas encore en rayon. |
| **Écarté** | Livre refusé au tri. Il quitte définitivement le circuit. Compté, jamais mis en rayon. |
| **Mise en rayon** | Geste par lequel des livres retenus deviennent effectivement disponibles. **C'est le seul moment où un livre devient visible en ligne et où les alertes partent.** |
| **Lot** | Regroupement de livres retenus entre le tri et la mise en rayon. Sa forme concrète — carton étiqueté, lot nommé, ou absence de lot — dépend de l'arbitrage `Q-01`. |
| **Session de bourse** | Période de vente ouverte au public. Correspond à un `AssoEvents` de type `Books` existant. |
| **Vente** | Sortie d'un exemplaire, enregistrée par un scan à la caisse. Décrémente la quantité en rayon. |
| **Livre rare** | Fiche dont la valeur estimée dépasse le seuil de l'association. Vendue hors du tarif 1–2 €, dans une section dédiée. |
| **Liste de recherche** | Ensemble des ISBN qu'un membre inscrit déclare rechercher. |
| **Alerte** | E-mail envoyé à un membre quand un livre de sa liste de recherche est mis en rayon. |
| **Remise à plat** | Correction périodique de la quantité en rayon pour absorber les ventes non scannées. Voir `RG-31`. |

### Termes à ne pas employer

| À éviter | Pourquoi | Employer |
|---|---|---|
| « Produit » | Réservé à la buvette (agrégat `Product` existant). Les livres ne sont pas des produits. | Fiche livre |
| « Stock » seul | Ambigu entre le local et la quantité d'une fiche | Quantité en rayon |
| « Commande » | Réservé à l'agrégat `Order` de la buvette | Vente |
| « Réservé » | Aucune réservation n'existe en v1 | — |

## 2. Cycle de vie d'un livre

```
                         don reçu
                            │
                            ▼
                    ┌───────────────┐
                    │  scan au tri  │
                    └───────┬───────┘
                            │
              ┌─────────────┴──────────────┐
              ▼                            ▼
       ┌────────────┐               ┌─────────────┐
       │  ÉCARTÉ    │               │   RETENU    │
       │ (terminal) │               │ (dans un    │
       └────────────┘               │    lot)     │
                                    └──────┬──────┘
                                           │  mise en rayon
                                           ▼
                                    ┌─────────────┐
                        ┌──────────►│  EN RAYON   │◄─────────┐
                        │           └──────┬──────┘          │
                        │                  │                 │
             remise à plat            scan de vente     autre don du
             (correction)                  │            même titre
                        │                  ▼                 │
                        │           ┌─────────────┐          │
                        └───────────┤    VENDU    │          │
                                    │ (compteur)  ├──────────┘
                                    └─────────────┘
```

**Points d'attention sur ce schéma.**

- Les états ne portent pas sur un exemplaire mais sur **des quantités attachées à une
  fiche**. Une même fiche peut simultanément avoir 3 exemplaires en rayon, 2 dans un
  lot non encore rangé et 47 ventes cumulées.
- **Aucune transition ne rend un livre public en dehors de la mise en rayon.** C'est la
  garantie centrale du système : un livre trié mais pas encore rangé n'existe pas pour
  le public et ne déclenche aucune alerte.
- L'état `ÉCARTÉ` est terminal et sans quantité récupérable : on n'incrémente qu'un
  compteur de refus, pour la statistique.

## 3. Modèle conceptuel

Description fonctionnelle des informations à conserver. Ce n'est pas un schéma de base
de données : la modélisation technique est un travail distinct.

### Fiche livre

Identifiée par son ISBN-13.

| Information | Nature | Origine |
|---|---|---|
| ISBN-13 | identifiant | scan |
| Titre, auteur(s), éditeur, année, genre | métadonnées | source externe, corrigeable par un administrateur |
| Image de couverture | métadonnée | source externe |
| Quantité en rayon | compteur | calculé par les mouvements |
| Ventes cumulées | compteur | scans de vente |
| Refus cumulés | compteur | scans de tri écartés |
| Valeur estimée + date d'estimation | métadonnée | source de prix, voir `Q-02` |
| Marquée « rare » | indicateur | automatique par seuil, ou manuel |
| Date de première entrée, date de dernière mise en rayon | horodatage | mouvements |
| Masquée du catalogue public | indicateur | administrateur |

### Mouvement

Toute variation de quantité est tracée. C'est la source de vérité ; les compteurs de la
fiche en découlent.

| Information | Exemple |
|---|---|
| ISBN concerné | `9782070408504` |
| Type | `MISE_EN_RAYON`, `VENTE`, `REFUS`, `CORRECTION`, `RETRAIT` |
| Quantité | `+1`, `-1`, `-12` |
| Date et heure | `2026-03-14T10:32:17+01:00` |
| Bénévole auteur du geste | identifiant du compte bénévole |
| Session de bourse rattachée, si applicable | identifiant `AssoEvents` |
| Lot d'origine, si applicable | identifiant de lot |

Conserver les mouvements plutôt que le seul compteur permet les statistiques par
bourse, l'annulation d'une erreur de scan, et l'audit d'un écart d'inventaire.

### Lot

Existe sous une forme ou une autre selon l'arbitrage `Q-01`.

| Information |
|---|
| Identifiant, et code-barres imprimable si l'option carton est retenue |
| Statut : `OUVERT`, `FERMÉ`, `MIS_EN_RAYON` |
| Contenu : liste des ISBN et quantités |
| Bénévole créateur, dates de création, de fermeture et de mise en rayon |

### Membre du site

| Information | Remarque |
|---|---|
| Identifiant Entra External ID | l'association ne stocke aucun mot de passe |
| Adresse e-mail | canal des alertes |
| Liste de recherche : ISBN + date d'ajout | |
| Préférences d'alerte, statut du compte (actif / bloqué) | |
| Historique des alertes envoyées | évite les doublons d'envoi — `RG-24` |

### Bénévole

| Information |
|---|
| Identifiant, nom, statut actif ou non |
| Droits : tri, mise en rayon, caisse, administration |
| Appareils associés |

## 4. Relation avec le domaine existant

| Élément existant | Relation |
|---|---|
| `AssoEvents` (type `Books`) | Une session de bourse **est** un `AssoEvents` existant. Les ventes s'y rattachent. Aucune duplication de la notion d'événement. |
| `Product`, `Order` (buvette) | **Aucune relation.** Les livres ne sont pas des produits et les ventes de livres ne sont pas des commandes. Toute tentative de réutiliser ces agrégats introduirait une confusion durable. |
| `User` | Les bénévoles peuvent s'appuyer sur le mécanisme d'authentification existant. Les membres du public relèvent d'Entra External ID, qui est un mécanisme distinct. |
| `Website`, `BackOffice` | Le site public du catalogue est une application distincte. Seul un lien depuis le site de l'association vers le catalogue est prévu. |
