# 05 — Site catalogue et administration

Application Angular distincte du `Website` et du `BackOffice` (décision fonctionnelle
`01` §6), avec rendu serveur. Déployée en Container App dans l'environnement existant.

Elle héberge **deux publics** : le catalogue public et l'espace d'administration.

## 1. Rendu serveur, et pourquoi il n'est pas négociable

`ENF-09` fait du référencement le principal canal d'acquisition gratuit de
l'association : quelqu'un cherche un titre sur un moteur et tombe sur la fiche du
catalogue. C'est ce qui fait venir du monde à la bourse, et ça vaut plus que n'importe
quelle communication.

Les fiches livres doivent donc être **rendues côté serveur et indexables**. Le
`Website` existant utilise déjà Angular SSR — même approche, avec une attention
particulière à l'appartenance des routes serveur, comme le note la mémoire projet à
propos de `app.routes.server.ts`.

L'espace d'administration, lui, n'a aucun besoin de référencement : il reste purement
client.

## 2. Structure

| Zone | Rendu | Authentification |
|---|---|---|
| Accueil, recherche, fiches, catalogue par genre | **SSR**, indexable | Aucune |
| Mon compte, liste de recherche | Client | Entra External ID (`ENF-16`) |
| Administration | Client | Rôle administrateur (`ENF-18`) |
| Mentions légales, confidentialité | SSR | Aucune |

## 3. Recherche : deux périmètres, jamais mélangés

`RG-47` impose deux blocs distincts — « à la bourse », puis « pas encore reçu ». Ce
n'est pas une préférence esthétique : mélanger les deux ferait croire à des
disponibilités inexistantes.

| Bloc | Source | Cache |
|---|---|---|
| À la bourse | Catalogue en base, plein texte SQL (`DT-07`) | Aucun |
| Pas encore reçu | Référentiel bibliographique externe | **Cache par requête normalisée**, durée courte |

Le cache du second bloc est le seul rempart entre un pic de trafic public et la BnF
(voir [`07-integrations-externes.md`](07-integrations-externes.md)). Clé : requête en
minuscules, sans accents, termes triés. Les visiteurs cherchent massivement les mêmes
titres, donc le taux de succès sera élevé.

## 4. Ajout à la liste de recherche

Le point le plus subtil de l'interface publique. `RG-46` : le membre choisit entre
suivre **l'œuvre** — comportement par défaut, pré-sélectionné — ou **une édition
précise**.

Deux erreurs à ne pas commettre :

- **Proposer l'édition en premier.** Dans une bourse à 1–2 €, la quasi-totalité des gens
  cherchent un texte, pas un tirage.
- **Masquer la portée choisie ensuite.** La liste doit afficher « toutes éditions » ou
  l'édition suivie, sans quoi personne ne comprendra pourquoi il reçoit une alerte pour
  une édition qu'il n'attendait pas.

Une entrée sans fiche correspondante s'affiche « pas encore reçu par l'association » —
formulation neutre, qui ne doit pas ressembler à une erreur.

## 5. Authentification

**Membres** : Entra External ID, successeur d'Azure AD B2C (`ENF-16`). L'association ne
détient aucun mot de passe. L'inscription n'est proposée **qu'au clic sur « me
prévenir »**, jamais à l'entrée du site.

**Administrateurs** : rôle explicite, distinct de l'identité des membres. Conséquence
assumée de `Q-06` — cette authentification est à mettre en place dans la nouvelle
application, elle n'est pas héritée du `BackOffice`.

## 6. Données personnelles

| Exigence | Implémentation |
|---|---|
| `ENF-10` | Seuls e-mail et liste de recherche. Aucun nom, aucune adresse |
| `ENF-11` | Finalité annoncée à l'inscription, en clair. Aucune case pré-cochée |
| `ENF-12` | Suppression en deux clics, cascade effective sur liste et historique d'alertes |
| `ENF-14` | Aucun traceur publicitaire. Une mesure d'audience, si elle existe, doit fonctionner **sans consentement** — donc sans bandeau de cookies |

Ce dernier point est un choix d'architecture autant que de conformité : une mesure sans
cookie évite d'imposer un bandeau à chaque visiteur d'un site associatif.

## 7. Administration

Écrans décrits en `../05-administration.md`. Deux points de vigilance technique :

**L'écran des sessions** (`05` §4 bis) affiche un **compte à rebours** avant envoi et
permet l'annulation ou l'envoi immédiat. Il lit directement la table d'outbox — c'est ce
que `DT-03` rend possible et qu'un broker de messages aurait interdit.

**L'écran de désengorgement** (`05` §5) sert l'objectif `O2`. Sa requête croise fiches
et mouvements avec agrégat : c'est la plus lourde du système, et la seule à surveiller
côté performance. Consultée quelques fois par mois, donc aucune optimisation prématurée
— mais un index adapté dès l'écriture.

## 8. Réutilisation

`SharedUi` (`@vpd/ui`) est partagé. Attention à un piège déjà documenté dans la mémoire
projet : les images Docker des applications Angular se construisent **depuis le contexte
`src/`**, et non depuis le dossier de l'application, parce que les chemins TypeScript
résolvent `@vpd/ui` via `../SharedUi`. La nouvelle application suit la même contrainte.
