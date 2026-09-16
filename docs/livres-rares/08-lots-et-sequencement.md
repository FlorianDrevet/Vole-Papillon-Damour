# 08 — Lots et séquencement

Onze lots, ordonnés par dépendance. Chacun est une branche, une PR, une validation.
Aucun lot ne doit laisser `main` dans un état où le catalogue public est cassé.

## Vue d'ensemble

```
L0 ─ conteneur blob                  (préalable technique, indépendant)
L1 ─ domaine RareBook                (aucune dépendance)
     ├── L2 ─ persistance + migration
     │        └── L3 ─ application + API
     │                 ├── L4 ─ rôle LivresRares
     │                 ├── L5 ─ portail admin        ← dépend de L4
     │                 ├── L8 ─ catalogue public
     │                 └── L9 ─ alertes « il est parti »
     └── L6 ─ retrait de Book.IsRare  ← dépend de L3, bloque L8
L7 ─ scanette : gestion              ← dépend de L4 et L5
L10 ─ scanette : caisse + recette    ← dépend de L6 et de la réponse à Q1
```

Chemin critique : **L1 → L2 → L3 → L6 → L8**. C'est lui qui livre la valeur publique.
L7 et L10 peuvent suivre.

---

## L0 — Rendre `IBlobService` conscient du conteneur

**Pourquoi d'abord** : `DeleteFileAsync` résout toujours le conteneur des actualités
([`01 §6.1`](01-decisions-et-portee.md)). Ajouter le conteneur rare avant de corriger ce
bug, c'est livrer une suppression de photo qui échoue en silence.

- Changer la signature pour prendre le conteneur cible.
- Adapter les appelants existants : actualités, produits, événements, lots.
- Ajouter le conteneur `livres-rares` dans `BlobSettings`, `appsettings*.json`,
  `AppHost/Program.cs` et le Bicep d'`infra/`.

**Sortie** : un test prouve qu'une suppression dans un conteneur non-actualités
supprime réellement le blob. Aucun changement fonctionnel visible.

## L1 — Domaine `RareBook`

Agrégat, entité photo, value objects, invariants. Voir [`02 §1-3`](02-modele-de-donnees.md).

**Sortie** : `Domain.tests` vert, y compris les invariants de publication, l'ordre des
photos et l'idempotence de `MarkSold`. Rien d'autre du dépôt n'a changé.

## L2 — Persistance et migration

Configurations EF, `IProjectDbContext`, index, migration `AddRareBooks` **sans encore
supprimer `Books.IsRare`**.

> Découpage volontaire : la création des tables et la suppression de la colonne sont
> deux migrations. La première est sûre et déployable seule ; la seconde est irréversible
> pour les données et appartient à L6.

**Sortie** : `Infrastructure.tests` vert, modèle EF sans changement en attente,
migration appliquée et annulée sur une base locale.

## L3 — Application et API

Commandes, requêtes, contrats, endpoints, téléversement de photos. Voir
[`03`](03-application-et-api.md).

**Sortie** : `Application.tests` et `Api.tests` verts ; les endpoints répondent en
local ; `EndpointAuthorizationTests` couvre chaque route. La politique `RareBooks`
n'existe pas encore — utiliser `Administration` en attendant L4 et le changer dans L4.

## L4 — Rôle `LivresRares`

Voir [`04`](04-role-livres-rares.md). Entra, `AccountRoles`, politiques, les trois
frontends.

**Sortie** : le test d'acceptation de [`04 §7`](04-role-livres-rares.md) passe de bout en
bout sur l'environnement de développement, avec un compte ne portant que ce rôle.
Attention au piège de `publishAccount` dans la scanette.

## L5 — Portail d'administration

Voir [`06`](06-portail-administration.md). Section, liste, formulaire, galerie photos,
vue restreinte.

**Sortie** : un bénévole `LivresRares` seul crée, modifie, publie et supprime une fiche
avec photos, sans voir aucune autre section. Vérification responsive à 1280 et 390 px.

## L6 — Retrait de `Book.IsRare` ⚠️

Le lot le plus risqué. Les neuf points d'appel de
[`02 §5`](02-modele-de-donnees.md), la seconde migration, la reprise des données.

**À faire dans cet ordre :**

1. Écrire les tests de régression sur les neuf points **avant** de toucher au code, en
   les faisant passer par la nouvelle jointure.
2. Basculer les neuf lectures.
3. Supprimer `MarkBookRareCommandHandler`, l'endpoint `/rare`, l'interrupteur du
   gabarit admin.
4. Migration : reprise des données puis suppression de la colonne.

**Sortie** : suite complète verte. Les livres marqués rares apparaissent dans la file de
migration du portail admin.

> **Avertir l'association avant le déploiement de ce lot** : la section publique
> « Livres rares » sera vide jusqu'à ce que les fiches reprises soient complétées et
> publiées ([`02 §6`](02-modele-de-donnees.md)).

## L7 — Scanette : gestion

Voir [`07 §1-4`](07-scanette.md). Tuile, routes, parcours en cinq écrans, file photo
hors ligne.

**Traiter la résolution d'identifiant client en premier** : une fiche créée hors ligne
n'a pas d'identifiant serveur, et les photos la référencent.

**Sortie** : création complète d'une fiche avec trois photos, en mode avion, puis
synchronisation au retour du réseau, sur un vrai téléphone. Le test en simulateur ne
suffit pas pour ce lot.

## L8 — Catalogue public

Voir [`05`](05-catalogue-public.md). Liste, fiche, carte, intégration accueil et
recherche, pastille d'en-tête, sitemap et indexation.

**Sortie** : les deux pages rendues côté serveur, indexables, correctes à 1280 et 390 px.
Un livre rare publié depuis l'admin apparaît en vitrine.

## L9 — Alertes « il est parti »

Voir [`03 §7`](03-application-et-api.md). Extension de la liste de suivi, motif d'alerte,
gabarit d'e-mail, expédition par le worker.

**Sortie** : marquer une fiche vendue envoie un e-mail aux suiveurs, une seule fois,
dans le respect du délai de refroidissement.

## L10 — Caisse, panier et recette ⚠️

**Bloqué par `Q1`.** Voir [`07 §5-7`](07-scanette.md) et
[`01 §4`](01-decisions-et-portee.md).

Écrans de caisse, recherche hors ligne par titre, total du panier, `RegisterSale`
enrichi, recette calculée en regard de la saisie manuelle de `RG-51`, delta de
synchronisation élargi.

**Sortie** : vente d'un rare hors ligne, rejouée à la synchronisation sans double
comptage ; annulation dans les 30 secondes qui remet la fiche disponible et annule
l'alerte.

---

## Mise à jour de la documentation métier

À faire **dans le lot qui rend la règle fausse**, pas dans un lot de rattrapage :

| Document | Lot | Modification |
|---|---|---|
| `06-regles-metier.md` `RG-50` | L3 | Amender : le prix existe pour les livres rares |
| `06-regles-metier.md` `RG-51` | L10 | Amender : la recette est calculée, la saisie manuelle prime |
| `08-questions-ouvertes.md` `Q-05` | L3 | Réouvrir et retrancher |
| `05-administration.md` §4 | L5 | Le marquage devient une fiche |
| `03-parcours-benevole-scan.md` §5 | L10 | L'écran de caisse retrouve un total |
| `02-glossaire-et-cycle-de-vie.md` | L6 | « Livre rare » n'est plus un indicateur |
| `04-site-public.md` | L8 | La section rare a ses propres pages |
| `NEXT.md` | chaque lot | Une ligne par lot livré, comme le reste du dépôt |

## Règles valables pour tous les lots

- Worktree dédié depuis `origin/main` fraîchement récupéré, branche dédiée, PR vers
  `main`. Voir `.github/skills/delivery-workflow/SKILL.md`.
- Test rouge avant code de production, sans exception.
- `graphify update .` après toute modification de code.
- Validation de la surface touchée en local : aucun pipeline CI n'existe dans ce dépôt.
- Contrôle responsive à 1280 et 390×844 pour toute modification visible.
