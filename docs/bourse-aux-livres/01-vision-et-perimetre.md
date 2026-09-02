# 01 — Vision et périmètre

## 1. Problème

La bourse aux livres finance l'association. Elle repose sur trois activités
successives, aujourd'hui déconnectées les unes des autres :

1. **Le tri.** Des bénévoles écartent les livres abîmés, les exemplaires en trop
   grand nombre et les titres jugés invendables.
2. **Le rangement.** Les livres retenus rejoignent un local de vente déjà saturé.
3. **La vente.** Les livres partent à 1–2 €, hors section « livres rares » vendue plus cher.

Le tri est le point de décision central, et c'est celui où l'information manque. Le
bénévole n'a accès ni à l'état du stock, ni à l'historique des ventes, ni à la demande
exprimée par le public. Il arbitre à l'intuition une ressource rare : la place en rayon.

## 2. Objectifs

| # | Objectif | Comment on saura que c'est atteint |
|---|---|---|
| O1 | Donner au bénévole, au moment du tri, l'information qui manque pour décider | Le nombre d'exemplaires disponibles et annoncés, le nombre de ventes passées et la demande sont affichés en moins d'une seconde après le scan |
| O2 | Réduire la saturation du local | Baisse du nombre de doublons excédentaires conservés d'une bourse à l'autre |
| O3 | Ne plus écarter de livres demandés ou qui se vendent | Les titres à historique de vente positif ne sont plus écartés pour cause de doublon |
| O4 | Identifier les livres de valeur avant qu'ils partent à 1 € | **Non couvert par la v1** (`Q-02`). Seul le marquage manuel « rare » existe ; l'estimation automatique est reportée et fonctionnera en asynchrone |
| O5 | Faire venir du monde à la bourse | Consultations du catalogue en ligne, et alertes suivies d'une visite |

## 3. Acteurs

| Acteur | Rôle | Contexte d'usage |
|---|---|---|
| **Bénévole trieur** | Scanne les dons, décide de garder ou d'écarter | Debout, cadence soutenue, souvent en bavardant. Peut être âgé, peu à l'aise avec le numérique |
| **Bénévole rangeur** | Range physiquement les livres retenus dans le local | **Aucune saisie à faire** : la disponibilité découle du mode choisi au tri et de la date de la bourse |
| **Bénévole caissier** | Scanne les livres vendus | Jour de bourse, avec de la file d'attente |
| **Administrateur** | Pilote le catalogue, consulte les statistiques, gère les comptes | Sur ordinateur, hors événement |
| **Visiteur du site** | Cherche des livres, consulte le catalogue | Non connecté, sur mobile ou ordinateur |
| **Membre inscrit** | Tient une liste de livres recherchés, reçoit des alertes | Compte créé via Microsoft Entra External ID |

## 4. Ce qui est dans le périmètre

### Application de scan (usage bénévole)

- Scan d'un code-barres ISBN et affichage immédiat des informations du livre
- Aide à la décision : exemplaires disponibles et annoncés, historique de vente,
  demande exprimée
- Enregistrement de la décision : gardé ou écarté
- Choix, avant chaque session de tri, du mode de mise à disposition :
  `DISPONIBLE MAINTENANT` ou `PROCHAINE BOURSE` (`RG-20`)
- Bascule automatique des exemplaires annoncés en exemplaires disponibles à la date
  d'ouverture de la bourse, sans intervention humaine (`RG-23`)
- Mode vente : scan de sortie à la caisse
- Fonctionnement sur téléphone d'abord, sur terminal de scan dédié ensuite

### Site public (application web distincte)

- Catalogue consultable et parcourable sans compte
- Recherche par titre, auteur, ISBN ; navigation par genre
- **Recherche élargie à un référentiel bibliographique externe**, pour suivre un livre
  que l'association n'a jamais reçu (`RG-47`)
- Fiche livre avec le nombre d'exemplaires disponibles
- Création de compte et liste de livres recherchés, **au niveau de l'œuvre ou d'une
  édition précise** (`RG-46`)
- Alerte e-mail dès qu'un livre recherché devient disponible ou est annoncé
- Espace d'administration : statistiques, gestion du catalogue et des comptes

### Rattachement à l'existant

- Les bourses sont déjà modélisées : `AssoEvents` de type `EventsTypeEnum.Books`.
  Les ventes et les statistiques s'y rattachent, ce qui permet un bilan par bourse et
  un lien avec le calendrier affiché sur le site de l'association.

## 5. Ce qui est hors périmètre

| Exclusion | Motif |
|---|---|
| Vente en ligne, paiement, panier | La bourse est un événement physique. Aucune demande. |
| Réservation ou mise de côté | Charge logistique pour les bénévoles jugée disproportionnée en v1. Décrit comme évolution en `04-site-public.md` §7. |
| Livres sans ISBN | Décision explicite. Angle mort assumé, à mesurer pendant le palier 0 — voir `Q-03`. |
| Suivi de l'exemplaire physique individuel | Le suivi se fait par ISBN avec une quantité. Voir §6 et `Q-04`. |
| Gestion des dons en amont (donateurs, reçus fiscaux) | Sujet distinct. |
| Intégration à l'application MAUI de caisse buvette | Les livres ont leur propre mode vente dans l'application de scan. Les deux caisses restent séparées. |
| Notifications push | Reportées en v2. L'e-mail seul en v1. |
| **Gestion des prix et encaissement** | Les prix sont décidés au comptoir ; le système n'en connaît aucun et ne calcule aucun total (`RG-50`). La caisse reste physique. Seule la recette globale d'une bourse peut être saisie à la main (`RG-51`). |
| **Estimation automatique de la valeur marchande** | Reportée : non prioritaire, et aucune source fiable identifiée. Quand elle existera, elle sera **asynchrone** — jamais pendant le scan — et ses résultats apparaîtront en fin de session et en administration. Voir `Q-02` et `RG-14`. Le marquage manuel « rare », lui, existe dès la v1. |

## 6. Décisions structurantes déjà prises

Ces choix sont arbitrés. Ils ne sont pas à rediscuter, mais leurs conséquences sont
documentées car elles pèsent sur le reste.

| Décision | Conséquence à assumer |
|---|---|
| **Suivi par ISBN avec quantité**, pas par exemplaire | On ne peut pas savoir *quel* exemplaire est parti, ni depuis quand un livre dort en rayon. Un livre vendu sans être scanné reste « disponible » indéfiniment : d'où l'obligation d'une remise à plat périodique (`RG-34`). |
| **Scan systématique en caisse** | La fiabilité du catalogue public dépend entièrement de la discipline des caissiers. C'est le principal risque humain du projet. |
| **Livres sans ISBN hors périmètre** | Une partie du stock restera invisible du système comme du site. |
| **Site public en application distincte** | Une charte graphique, une authentification et un déploiement de plus à maintenir, en marge du `Website` et du `BackOffice` existants. |
| **Administration dans le site public**, pas dans le `BackOffice` existant | L'authentification administrateur doit être refaite dans la nouvelle application ; les administrateurs auront deux outils. |
| **Comptes publics via Microsoft Entra External ID** | Le service qui remplace Azure AD B2C, cohérent avec l'hébergement Azure existant. L'association ne stocke aucun mot de passe. |
| **Écart au tri enregistré sans motif** | On connaîtra le volume écarté, pas les raisons. Impossible d'affiner les seuils de tri à partir des données. |
| **Deux modes de mise à disposition choisis avant le scan**, plutôt qu'un geste de mise en rayon (`Q-01`) | Aucun geste de publication à faire dans le local, et rien qui puisse être oublié. En contrepartie : le risque se déplace sur une **erreur de mode en début de session**, silencieuse, qui n'est rattrapable qu'en bloc (`RG-25`). Et l'agenda des bourses cesse d'être un simple affichage : sa date **pilote la disponibilité réelle** du catalogue (`RG-36`). |

## 7. Paliers de livraison

Chaque palier a une valeur propre et un critère d'arrêt : si le critère n'est pas
atteint, on ne passe pas au suivant.

### Palier 0 — Sonde de faisabilité

**Contenu.** Application de scan en consultation seule : elle lit un ISBN, affiche
titre, auteur, éditeur, année, couverture. Elle n'enregistre rien.

**Ce qu'on mesure.**
- Taux de lecture réussie du code-barres sur des livres d'occasion réels, y compris
  couvertures abîmées, plastifiées ou froissées
- Taux de couverture des métadonnées sur du fonds français, en particulier édition
  ancienne et livre jeunesse
- Proportion de dons sans ISBN exploitable
- Cadence tenable et ressenti d'un bénévole sur au moins 300 livres d'affilée

**Critère de passage.** Lecture réussie et métadonnées présentes sur une nette majorité
du fonds testé, et acceptation du geste par le bénévole. Chiffres cibles à fixer avec
l'association avant de lancer le test.

**Coût.** Du temps. Aucun achat de matériel.

### Palier 1 — Le socle interne

**Contenu.** Fiches livres, quantités disponible et annoncée, tri avec ses deux modes,
bascule automatique, scan de vente, écran de statistiques minimal. Aucune exposition
publique.

**Critère de passage.** Après une bourse complète, l'écart entre le stock théorique et
un comptage physique par échantillon reste dans une marge acceptable, à définir avec
l'association. C'est le test réel de la discipline de scan en caisse.

**Montée en charge.** Le catalogue démarre vide alors que le local contient déjà
plusieurs milliers de livres. Il n'y a **pas de reprise préalable de l'existant** : le
catalogue se remplit au fil des tris (`RG-48`). Pendant cette période, l'aide à la
décision est partielle — « inutile d'en garder » ne se déclenche presque jamais et
« premier exemplaire » se déclenche à tort sur des titres déjà en rayon.

C'est un choix assumé : la reprise de l'existant représenterait des milliers de scans
sans contrepartie immédiate. Elle peut se faire progressivement, par des sessions de
scan des rayons en mode `DISPONIBLE MAINTENANT`, en commençant par les rayons les plus
denses. Ce n'est pas un prérequis.

**Ce point doit être annoncé aux bénévoles.** Un outil qui répond « premier exemplaire »
sur un livre qu'ils savent présent en cinq exemplaires perd leur confiance si personne
ne les a prévenus que le système apprend en marchant.

### Palier 2 — La vitrine

**Contenu.** Catalogue public consultable, recherche, navigation, fiche livre,
administration du catalogue.

**Dépend de** : palier 1 validé. Publier un catalogue sur un stock non fiable serait
contre-productif.

### Palier 3 — Les alertes

**Contenu.** Comptes membres, listes de recherche, alertes e-mail, signal « recherché »
remonté au bénévole trieur.

**Dépend de** : palier 2 en production et alimenté.

### Évolutions identifiées, non planifiées

- **Estimation asynchrone de la valeur marchande** (`Q-02`), avec restitution en fin de
  session et dans une file d'administration
- **Écran dédié de remise à plat de l'inventaire** (`05` §6) : comptage en masse et
  suivi de l'écart dans le temps. En attendant, la remise à plat se fait fiche par
  fiche via la correction manuelle des quantités (`05` §4)
- Notifications push web
- Application mobile native pour le public
- Prise en charge des livres sans ISBN

## 8. Hypothèses de dimensionnement

À confirmer, mais retenues comme base de conception.

| Grandeur | Ordre de grandeur retenu |
|---|---|
| Livres disponibles | 3 000 à 15 000 |
| Livres triés par session | environ 1 000 |
| Appareils de scan simultanés | 2 à 5 |
| Fréquence des bourses | environ une semaine par mois |
| Membres inscrits au site | quelques centaines |

Ces valeurs excluent toute solution qui ne tiendrait pas la recherche sur plusieurs
milliers de titres, mais n'imposent aucune contrainte de très grande échelle.
