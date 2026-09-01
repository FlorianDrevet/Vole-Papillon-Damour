# 07 — Intégrations externes

Trois dépendances sortantes : BnF, Open Library, envoi d'e-mails. **Aucune n'est sur le
chemin critique d'une décision de tri** (`ENF-02`).

## 1. Le pipeline de résolution

```
ISBN normalisé (RG-01)
     │
     ▼
 fiche en base ?  ──oui──►  réponse immédiate, 0 appel externe
     │ non
     ▼
 création fiche (Pending) + contrainte d'unicité = déduplication
     │
     ▼
 BnF SRU   ── trouvée ──►  Resolved
     │ absente / délai 800 ms dépassé
     ▼
 Open Library ── trouvée ──►  Resolved (+ WorkId)
     │ absente
     ▼
 NotFound  →  fiche à l'ISBN seul (RG-03), file de rattrapage
```

Trois propriétés à préserver :

**La déduplication est portée par la base.** Cinq scanettes peuvent scanner le même
ISBN inconnu dans la même seconde. Une contrainte d'unicité sur `Isbn13` plus une
insertion idempotente suffisent : le premier gagne, les autres lisent son résultat.
Pas de sémaphore applicatif.

**Le délai est court et non bloquant.** 800 ms sur la BnF, puis on passe. Si rien ne
répond, la fiche reste `Pending` et `enrich` la reprendra. Le bénévole a déjà son
verdict.

**Open Library est appelée en tâche de fond même quand la BnF a répondu**, pour obtenir
le `WorkId` de `RG-46`. Jamais dans la seconde du scan.

## 2. BnF — SRU Catalogue général

| | |
|---|---|
| Authentification | Aucune |
| Quota | **Non publié.** Aucune limite de débit documentée |
| Licence | **Licence Ouverte Etalab** — conservation et rediffusion autorisées |
| Obligation | Mentionner la source et la date de récupération |
| Format | SRU, XML MARC (UNIMARC / InterMarc) |
| Recherche | Par ISBN (`bib.fuzzyISBN`), et par titre ou auteur pour `RG-47` |

La colonne `MetadataFetchedAt` n'est pas du confort : c'est la condition légale de
réutilisation.

**Absence de quota ne signifie pas absence de limite.** Les conditions générales
réservent un blocage « immédiatement et sans préavis ». Trois précautions :

- Le cache rend la question théorique : un ISBN n'est interrogé qu'une fois dans la vie
  du système. Après quelques mois, une session déclenche quelques dizaines d'appels.
- Un seul appel à la fois dans `enrich`, sans rafale.
- Un `User-Agent` identifiant l'association et un contact. Ce n'est demandé nulle part,
  mais c'est ce qui fait la différence entre se faire couper l'accès et recevoir un
  message demandant de lever le pied.

**Porte de sortie.** La BnF publie des jeux de données pré-constitués en téléchargement
(UNIMARC, INTERMARC, Dublin Core). Si le volume devenait un sujet, la réponse propre
serait un chargement de masse, pas un martèlement de l'API.

**Limite fonctionnelle.** Le dépôt légal ne couvre que la production française : les
dons en langue étrangère passeront à travers et dépendront d'Open Library seule.

## 3. Open Library

| | |
|---|---|
| Débit | **1 requête/seconde**, 3 en s'identifiant par `User-Agent` avec contact |
| Usage découragé | Moissonnage de masse, API en fond d'un service à fort trafic |
| Apport unique | Modèle **Œuvre / Édition**, seul équivalent gratuit de `RG-46` |
| Faiblesse | Couverture française irrégulière, surtout édition ancienne et jeunesse. Données communautaires : doublons, regroupements parfois faux |

Le débit d'une requête par seconde impose l'appel en tâche de fond. C'est cohérent : le
`WorkId` ne sert pas à afficher un verdict, il sert à rapprocher des demandes.

**Dumps mensuels gratuits** disponibles si l'on voulait un jour une base locale — non
retenu (`DT-02` : des dizaines de gigaoctets pour un catalogue qui n'en utilisera jamais
que quelques milliers de lignes).

## 4. Le regroupement en œuvres, et son repli

`RG-46` dépend du `WorkId`. S'il est absent, une demande de portée `ŒUVRE` ne se
déclenche pas — **l'alerte est manquée, silencieusement**. C'est le risque le plus
sournois de cette intégration.

Deux garde-fous :

- **`QT-01`** mesure au palier 0 la proportion de fiches sans `WorkId`.
- **Repli** si la couverture est insuffisante : rapprochement par titre + auteur
  normalisés. Il produit des faux positifs sur les séries, les homonymes et les
  adaptations. Retenu quand même — sans réservation ni engagement, **un membre prévenu à
  tort coûte moins cher qu'un membre jamais prévenu**.

## 5. Cache de la recherche publique

Chemin distinct du pipeline ci-dessus. `RG-47` : un visiteur cherche en texte libre des
livres jamais reçus.

- Clé : requête normalisée — minuscules, sans accents, termes triés.
- Durée courte, quelques heures. Contrairement aux notices, **un résultat de recherche
  n'est pas immuable** : le référentiel s'enrichit.
- C'est le seul endroit où un pic de trafic public frappe directement la BnF. Les
  visiteurs cherchent massivement les mêmes titres, donc le cache absorbe l'essentiel.

## 6. Couvertures

Récupérées en tâche de fond après création de la fiche, jamais pendant le scan.
Stockées dans le **blob existant**, servies par notre propre URL.

Ne pas pointer directement vers l'image chez la source : cela casse au premier
changement d'URL, expose les visiteurs à leur disponibilité, et leur envoie notre trafic
sans raison. La Licence Ouverte autorise explicitement la copie.

## 7. Envoi d'e-mails

Seul canal en v1 (décision fonctionnelle). Volume : quelques dizaines de messages par
semaine.

| Sujet | Traitement |
|---|---|
| Déclenchement | Depuis `sweep` uniquement, jamais depuis l'API |
| Contenu | Titre, **édition arrivée** (`RG-46`), date de disponibilité, dates de la bourse, mention de non-réservation (`RG-32`), lien de désinscription |
| Rebonds | Rappel entrant authentifié par secret partagé, incrémente `BounceCount`, suspend au-delà d'un seuil (`RG-31`) |
| Réputation | `RG-29` regroupe par membre : un e-mail par session, jamais un par livre |

L'architecture doit traiter « alerte » comme un **événement métier**, pas comme un envoi
d'e-mail : l'ajout des notifications push en v2 doit être un consommateur de plus, pas
une réécriture.

## 8. Résilience

| Panne | Comportement attendu |
|---|---|
| BnF indisponible | Fiches en `Pending`, reprises plus tard. **Le tri continue** |
| Open Library indisponible | Pas de `WorkId` ; les demandes par édition fonctionnent |
| Les deux indisponibles | `RG-03` : fiche à l'ISBN seul, complétée à la main ou plus tard |
| Envoi d'e-mail en échec | `Attempts` incrémenté, réessai ; au-delà d'un seuil, `Failed` et visible en administration |
| Réseau absent côté appareil | `ENF-05` : scan et file locale, rien ne bloque |

**Aucune de ces pannes ne doit empêcher de trier ou de vendre** (`ENF-21`). C'est le
critère qui prime sur tout le reste de ce document.
