# 05 — Administration

Espace réservé aux administrateurs, situé **dans le site public du catalogue** et non
dans le `BackOffice` existant (décision arrêtée, `01` §6). Conséquence assumée : les
administrateurs auront deux outils, et l'authentification administrateur est à mettre
en place dans la nouvelle application.

## 1. Tableau de bord

Vue d'ouverture, en chiffres, sur la période en cours et la précédente pour comparaison.

| Indicateur | Pourquoi il est utile |
|---|---|
| Livres disponibles et annoncés (total et titres distincts) | Mesure de la saturation du local |
| Livres triés sur la période, gardés / écartés | Mesure de l'activité de tri et du taux de rejet |
| Ventes sur la dernière bourse : nombre et montant | Résultat de l'événement |
| Titres disponibles jamais vendus depuis leur première mise à disposition | **Le principal levier de désengorgement.** Ce sont les candidats au retrait. |
| Livres marqués rares en attente d'expertise | File de travail |
| Sessions ouvertes en ce moment | Sessions non clôturées, dont les alertes ne sont pas encore parties (`RG-44`) |
| Fiches sans métadonnées | File de travail |
| Écart d'inventaire estimé | Signal de dérive du compteur (`RG-34`) |

Ce tableau existe dès le palier 1, même sous forme sommaire : sans lui, on n'a aucun
moyen de vérifier que le palier 1 a atteint son critère de passage.

## 2. Statistiques par bourse

Chaque session de bourse est un `AssoEvents` de type `Books` existant (`RG-36`). Aucune
saisie de dates en double.

Pour une bourse donnée : nombre de livres vendus, recette, répartition par genre,
meilleures ventes, comparaison avec les bourses précédentes, et courbe des ventes par
journée d'ouverture.

C'est ce qui permet de répondre à « quel jour ouvrir », « quels genres marchent »,
« la bourse de mars a-t-elle mieux marché que celle de février ».

## 3. Statistiques par livre

Depuis une fiche : historique complet des mouvements (entrées, ventes, corrections,
retraits), par bourse. Répond à « ce titre revient tout le temps en don, mais est-ce
qu'il se vend ? ».

## 4. Gestion du catalogue

| Action | Détail |
|---|---|
| Corriger les métadonnées | Titre, auteur, éditeur, année, genre, couverture. Une correction manuelle **ne doit jamais être écrasée** par une actualisation automatique ultérieure (`RG-05`) |
| Ajouter une fiche à la main | Pour les cas où le scan est impossible mais l'ISBN connu |
| Ajuster les quantités disponible et annoncée | Génère un mouvement de type `CORRECTION`, tracé et attribué (`RG-35`) |
| Retirer des livres | Mouvement `RETRAIT` : désherbage, don à une autre structure, mise au rebut |
| Masquer une fiche du catalogue public | Sans la supprimer ni perdre son historique |
| Supprimer une fiche | Réservé aux fiches créées par erreur. Refusé si des ventes y sont rattachées (`RG-06`) |
| Marquer ou démarquer « rare », fixer un prix | Complète ou corrige la détection automatique |
| Fusionner deux fiches | Cas des ISBN-10 et ISBN-13 d'une même édition mal normalisés (`RG-07`) |

### Files de travail

Des listes de travail concrètes, plutôt que des écrans de recherche :

- **Fiches sans métadonnées** — à compléter à la main.
- **Livres marqués rares** — à expertiser et à tarifer. Alimentée à la main en v1 ; elle
  recevra les résultats de l'estimation asynchrone si celle-ci est un jour implémentée
  (`RG-14`).
- **Annonces sans date** — exemplaires annoncés alors qu'aucune bourse n'était
  programmée (`RG-24`). Ils se rattachent automatiquement dès qu'une bourse est créée,
  mais **leurs alertes restent en attente d'ici là**. Une file qui s'allonge est le
  signe que l'agenda n'est pas tenu.

## 4 bis. Sessions de scan

Écran indispensable depuis l'abandon du geste de mise en rayon. Le mode d'une session
détermine l'effet public de deux cents scans, et sa clôture déclenche les e-mails
(`RG-44`) : c'est le seul endroit d'où l'on voit ce qui s'est réellement passé.

### Liste des sessions

| Colonne | Contenu |
|---|---|
| Bénévole | Qui a tenu la session |
| Début, fin, durée | Horodatages et temps de tri effectif |
| Cause de clôture | `TERMINER`, inactivité, déconnexion, jeton expiré (`RG-43`) |
| **Mode** | `DISPONIBLE MAINTENANT` ou `PROCHAINE BOURSE` |
| Bourse de rattachement | Pour les sessions annoncées |
| Livres scannés | Total |
| Gardés / écartés | Répartition de la décision de tri |
| Alertes envoyées | Nombre d'e-mails partis à la clôture |
| Statut | `EN_COURS`, `TERMINEE`, `REPRISE` |

Les sessions encore ouvertes sont mises en évidence : ce sont les seules qu'on peut
corriger sans conséquence, puisque aucun e-mail n'en est parti.

### Corrections possibles (`RG-45`)

| Action | Effet |
|---|---|
| Changer le mode de la session | Rebascule tous ses livres entre disponible et annoncé (`RG-25`) |
| Changer la bourse de rattachement | Pour une session annoncée sur la mauvaise date |
| Retirer un livre de la session | Annule ses mouvements et corrige les quantités |
| Annuler la session entière | Annule tous ses mouvements |
| Consulter le détail des mouvements | Diagnostic d'un écart de stock |

Toute correction produit des mouvements tracés et attribués (`RG-35`) ; rien n'est
effacé, la session est marquée `REPRISE`.

**Le moment de la correction change tout :**

| Session | Alertes |
|---|---|
| Encore ouverte | Aucun e-mail parti. Correction intégrale. |
| Déjà close | E-mails partis. Les quantités sont rétablies, mais l'administrateur est explicitement informé de ce qui n'est plus rattrapable. |

**C'est la fonction de rattrapage la plus importante de l'administration.** Sans elle,
une session scannée dans le mauvais mode ne se corrige que fiche par fiche.

### Suivi de l'activité

La même liste sert de suivi du bénévolat : qui trie, combien de temps, à quel rythme,
avec quel taux de rejet. Sous réserve de `ENF-15` — ces chiffres servent à corriger des
erreurs et à mesurer la charge de travail, **jamais à comparer les bénévoles entre
eux**.

## 5. Désengorgement du local

L'objectif O2 est de réduire la saturation. Un écran dédié le sert directement :

> Titres disponibles depuis plus de *N* mois, jamais vendus, en plus de *M* exemplaires,
> triés par nombre d'exemplaires décroissant.

Il en découle une liste de retrait, exportable pour être traitée physiquement dans le
local. Sans cet écran, le système ne fait qu'observer la saturation sans jamais aider
à la résorber.

## 6. Remise à plat de l'inventaire

Conséquence directe du suivi par ISBN sans exemplaire individuel : le compteur dérive
à cause des ventes non scannées. Le mécanisme est décrit en `RG-34`.

L'administration doit permettre :

- de saisir un comptage physique pour un ensemble de fiches et d'ajuster les quantités,
- de visualiser l'ampleur de l'écart constaté à chaque remise à plat,
- de suivre cet écart dans le temps : **c'est l'indicateur de la discipline de scan en
  caisse**, donc le principal indicateur de santé du projet.

## 7. Gestion des membres du site

| Action | Détail |
|---|---|
| Lister et rechercher les membres | |
| Consulter une liste de recherche | Pour du support, jamais pour de l'exploitation commerciale |
| Bloquer un compte | Suspend les alertes, conserve les données |
| Supprimer un compte | Suppression effective, y compris liste de recherche et historique d'alertes (`ENF-12`) |
| Consulter les alertes envoyées | Diagnostic « je n'ai rien reçu » |

Les comptes sont créés en autonomie via Entra External ID ; il n'y a pas de création
manuelle de compte membre par un administrateur.

## 8. Gestion des bénévoles

| Action | Détail |
|---|---|
| Créer, désactiver un compte bénévole | |
| Attribuer les droits | Tri, caisse, administration (`RG-40`) |
| Voir l'activité d'un bénévole | Nombre de scans, sessions de tri et leur mode |
| Corriger une série de scans erronés | Voir §4 bis. Deux erreurs à rattraper en bloc : une session tenue dans le mauvais mode de mise à disposition, et des livres scannés en caisse alors qu'il s'agissait d'un tri |

Ce dernier point n'est pas théorique : c'est l'erreur la plus probable du système, et
elle est silencieuse.

## 9. Paramètres de l'association

Valeurs qui pilotent les règles métier, modifiables sans intervention technique :

| Paramètre | Utilisé par |
|---|---|
| Seuil de doublon déclenchant « inutile d'en garder » | `RG-10` |
| Nombre de ventes à partir duquel un titre est « demandé » | `RG-12` |
| Seuil de valeur d'un livre rare | `RG-14` |
| Ancienneté et quantité déclenchant une proposition de retrait | §5 |
| Limite d'entrées d'une liste de recherche | `RG-27` |
| Délai minimum entre deux alertes pour un même livre et un même membre | `RG-30` |

Ces valeurs seront fausses au départ. Les rendre modifiables sans redéploiement est ce
qui permettra de les ajuster au vu du terrain.

## 10. Ce que l'administration ne fait pas

- Elle ne remplace pas l'application de scan : on n'y saisit pas des ventes à la main
  en masse.
- Elle n'envoie pas d'e-mails de masse aux membres. Le seul e-mail prévu est l'alerte
  de disponibilité. Toute communication générale relève des outils existants de
  l'association.
