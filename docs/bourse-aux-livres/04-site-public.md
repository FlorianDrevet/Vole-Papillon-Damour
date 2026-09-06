# 04 — Le site public du catalogue

Application web distincte du site de l'association et du back-office (décision arrêtée,
`01` §6). Le site de l'association y renvoie par un lien ; les deux restent séparés.

## 1. Ce que le site doit produire comme effet

Un visiteur doit pouvoir, en moins d'une minute et sans compte, répondre à :
« est-ce que ce livre est à la bourse ? » et « qu'est-ce qu'il y a en ce moment ? ».

Tout le reste — compte, liste de recherche, alertes — est secondaire et ne doit jamais
s'interposer entre le visiteur et cette réponse.

## 2. Arborescence

| Page | Accès | Rôle |
|---|---|---|
| Accueil | public | Recherche, prochaines dates de bourse, nouveautés, livres rares |
| Résultats de recherche | public | Liste filtrable |
| Catalogue par genre | public | Navigation sans intention précise |
| Fiche livre | public | Détail d'un titre et disponibilité |
| Mon compte | connecté | Liste de recherche, préférences, suppression du compte |
| Administration | administrateur | Voir `05-administration.md` |
| Mentions légales, confidentialité | public | Obligations RGPD (`ENF-10`) |

## 3. Accueil

Trois blocs, dans cet ordre :

1. **La barre de recherche**, dominante, avec un exemple concret en aide de saisie
   (« un titre, un auteur, ou le code-barres »).
2. **La prochaine bourse** : dates, horaires et adresse, repris de l'événement
   `AssoEvents` de type `Books` existant (`RG-36`). Aucune saisie en double.
3. **Trois sélections** : arrivés récemment, livres rares, et une sélection par genre.

Le catalogue complet est parcourable (décision arrêtée) mais l'accueil ne déverse pas
15 000 titres : il donne des points d'entrée.

## 4. Recherche et navigation

**Recherche** par titre, auteur ou ISBN, dans un seul champ. Elle doit tolérer
l'absence d'accents, les fautes de frappe légères et l'inversion titre/auteur — sans
quoi elle sera jugée inutilisable dès le premier essai.

Elle porte sur **deux périmètres distincts, jamais mélangés** (`RG-47`) :

```
   Recherche : « le petit prince »

   ── Dans la bourse aux livres ───────────────────
   ✅ Le Petit Prince · Gallimard 1999 · 3 dispo
   📅 Le Petit Prince · Folio 2015 · dès le 14 mars

   ── Pas encore dans la bourse aux livres ────────
   Le Petit Prince · Gallimard Jeunesse, grand format
   Le Petit Prince · édition illustrée, 2021
                         [ Ajouter à ma liste de recherche 🔔 ]
```

La section « Dans la bourse aux livres » arrive toujours en premier : la question la
plus fréquente reste « qu'est-ce qu'il y a à la bourse ? ». Le second bloc existe pour
une seule raison — permettre d'ajouter à sa liste de recherche un livre que
l'association n'a jamais reçu — et ne doit jamais donner l'impression que ces livres
sont disponibles.

**Filtres** : genre, disponibilité, livres rares.
**Tris** : pertinence (défaut), arrivée récente.

Le **genre vient des sources bibliographiques**, normalisé, et de nulle part ailleurs
(`Q-07`). Il est donc hétérogène et souvent absent : le filtre est un confort de
navigation, pas un classement exhaustif. Un livre sans genre reste trouvable par la
recherche — qui est le chemin principal —, il ne sort simplement pas des filtres.

Le filtre de disponibilité propose trois valeurs, qui correspondent aux états du cycle
de vie :

| Valeur | Ce qu'elle montre |
|---|---|
| `Disponible maintenant` | Exemplaires vendables dès la prochaine ouverture |
| `À la prochaine bourse` | Exemplaires annoncés, pas encore vendables (`RG-22`) |
| `Tout` | Les deux, plus les titres épuisés |

Un résultat affiche couverture, titre, auteur, et **l'état de disponibilité**, distinct
selon le cas : « 3 disponibles », « 2 à partir du 14 mars », ou « épuisé ». C'est la
donnée que les gens viennent chercher ; elle ne doit jamais être masquée derrière un
clic, et **un exemplaire annoncé ne doit jamais être présenté comme disponible**.

**Les livres à quantité nulle restent affichés**, marqués « épuisé », plutôt que
disparaître : cela permet de les ajouter à sa liste de recherche, ce qui est
précisément le cas d'usage des alertes (`RG-26`).

## 5. Fiche livre

```
┌──────────────────────────────────────────────┐
│  ┌────────┐   Le Petit Prince                │
│  │        │   Antoine de Saint-Exupéry       │
│  │  couv  │   Gallimard · 1999               │
│  │        │   Jeunesse                       │
│  └────────┘                                  │
│                                              │
│   ✅  3 exemplaires disponibles              │
│       depuis le 3 mars 2026                  │
│                                              │
│   📅  2 autres à partir du 14 mars           │
│                                              │
│   ISBN 9782070408504                         │
└──────────────────────────────────────────────┘
```

Une fiche peut porter les deux états à la fois : des exemplaires disponibles
maintenant, et d'autres annoncés pour une bourse à venir. **Les deux lignes sont
distinctes et ne s'additionnent jamais dans un total unique** — un visiteur qui se
déplace aujourd'hui ne doit compter que sur la première.

L'action « Me prévenir quand il y en aura » n'apparaît que lorsqu'aucun exemplaire
n'est disponible. Un titre disponible se consulte directement, sans proposer une
alerte inutile.

Cas particulier : un titre uniquement annoncé sans date connue (`RG-24`) affiche
« prochainement disponible, date à préciser », sans promesse de calendrier.

**La date est affichée systématiquement.** C'est la manière honnête de dire au visiteur
à quel point l'information est fraîche, sachant que le compteur est susceptible de
dériver (`RG-34`).

Une mention permanente accompagne la disponibilité : *« Disponibilité indicative,
mise à jour à chaque vente. Les livres partent vite. »* Mieux vaut cette réserve
qu'une promesse démentie sur place.

## 6. Compte et liste de recherche

### Création de compte

Via **Microsoft Entra External ID** (décision arrêtée, `DT-10`). L'association ne détient
et ne stocke aucun mot de passe — c'est le même fournisseur d'identité que celui des
bénévoles et des administrateurs, le site public étant la **seule** application où
l'inscription en libre-service est ouverte.

L'inscription n'est proposée qu'au moment où elle sert : au clic sur
« Me prévenir quand il y en aura ». Aucun mur d'inscription à l'entrée du site.

### Liste de recherche

- Ajout depuis une fiche du catalogue lorsque le titre n'a plus d'exemplaire disponible,
  **ou depuis un résultat de la section « Pas encore dans la bourse aux livres »** —
  c'est-à-dire y compris un livre que l'association n'a jamais reçu (`RG-47`). C'est le
  cas d'usage principal.
- Un livre actuellement disponible se consulte directement : l'action « Me prévenir »
  n'est pas proposée tant qu'un exemplaire est disponible.
- Retrait à tout moment, en un clic.
- Limite raisonnable par compte (`RG-27`).

### Choisir la portée : l'œuvre ou une édition précise

Un ISBN désigne une édition, pas un texte. *Le Petit Prince* existe en dizaines
d'éditions, donc en dizaines d'ISBN. À l'ajout, le membre choisit ce qu'il suit
(`RG-46`) :

```
┌──────────────────────────────────────────────┐
│  Le Petit Prince — Antoine de Saint-Exupéry  │
│                                              │
│  Que souhaitez-vous suivre ?                 │
│                                              │
│  ◉  N'importe quelle édition                 │
│     Vous serez prévenu dès qu'un exemplaire  │
│     arrive, quel que soit l'éditeur          │
│                                              │
│  ○  Une édition précise                      │
│     ┌──────────────────────────────────┐     │
│     │ Gallimard, Folio, 1999         ▾ │     │
│     └──────────────────────────────────┘     │
│                                              │
│              [ Ajouter à ma liste ]          │
└──────────────────────────────────────────────┘
```

« N'importe quelle édition » est **pré-sélectionné** : dans une bourse à 1–2 €, la
quasi-totalité des gens cherchent un texte, pas un tirage. L'édition précise sert au
collectionneur, à l'édition illustrée, au grand format.

### Ce que le membre voit sur sa liste

Pour chaque entrée : couverture, titre, auteur, **portée suivie** (« toutes éditions »
ou l'édition choisie), disponibilité actuelle au catalogue, date d'ajout, et la date de
la dernière alerte reçue s'il y en a eu une.

Une entrée qui ne correspond à aucune fiche du catalogue s'affiche comme
« pas encore reçu par l'association » — sans que cela ait l'air d'une erreur.

## 7. Alertes

### Déclenchement

Une alerte est **constituée au scan**, dès qu'un livre d'une liste de recherche est
rendu disponible ou annoncé pour une bourse datée (`RG-28`). Elle est mise en file
**à la clôture de la session de tri**, et **envoyée 2 heures plus tard** (`RG-44`).

Aucun e-mail ne part pendant qu'un bénévole scanne, ni au moment où il termine. Cela
garantit un seul message par membre et par session, et laisse une fenêtre de rattrapage
qui survit à la fin du tri : une erreur repérée dans les deux heures se corrige encore
sans que personne n'ait été prévenu à tort.

Le message diffère selon le mode de la session de tri :

| Mode | Ce que reçoit le membre |
|---|---|
| `DISPONIBLE MAINTENANT` | « *Le Petit Prince* est disponible. Prochaine ouverture : 14 mars. » |
| `PROCHAINE BOURSE`, date connue | « *Le Petit Prince* sera disponible à la bourse du 14 mars. » |
| `PROCHAINE BOURSE`, date inconnue | Rien pour l'instant : l'alerte est différée jusqu'à ce qu'une date existe (`RG-24`) |

**Prévenir tôt est un choix assumé.** L'inquiétude initiale — faire venir quelqu'un
pour un livre encore dans un carton — est levée par la date : on ne dit jamais
« venez maintenant » pour un livre qui ne sera là que le 14. Et prévenir dès le tri
laisse au membre le temps de s'organiser, ce qu'une alerte envoyée le matin de
l'ouverture ne permettrait pas.

C'est aussi la raison pour laquelle une alerte n'est **jamais** envoyée sans date : un
e-mail annonçant un livre « prochainement » n'aide personne à se déplacer.

### Contenu de l'e-mail

- Le titre, l'auteur, la couverture
- **Quelle édition est arrivée** — éditeur, année, format. Indispensable pour une
  demande de portée « toutes éditions » (`RG-46`) : le membre doit savoir ce qu'il va
  trouver, et ne pas croire qu'on lui promet l'édition qu'il avait en tête
- **La date à laquelle le livre sera effectivement disponible**, en évidence
- Les dates, horaires et adresse de la bourse concernée
- **Une mention explicite de non-réservation** : « Ce livre n'est pas mis de côté.
  Premier arrivé, premier servi. »
- Un lien de désinscription et un lien vers la liste de recherche

### Garde-fous

| Situation | Comportement |
|---|---|
| Une session de tri de 200 livres contient 12 titres de la liste d'un même membre | **Un seul e-mail regroupant les 12 titres**, envoyé à la clôture de la session (`RG-29`). Douze e-mails simultanés feraient fuir le destinataire et abîmeraient la réputation d'envoi du domaine. |
| Le bénévole range son appareil sans clôturer | La session se clôt d'elle-même après 2 h sans scan (`RG-43`), puis les e-mails partent 2 h plus tard (`RG-44`). Retard maximal : quatre heures — sans conséquence, puisque les livres ne sont de toute façon disponibles qu'à l'ouverture du local. |
| Le même livre est réapprovisionné quatre fois en deux mois | Une alerte au plus par livre et par membre sur une période donnée (`RG-30`) |
| Le livre passe d'annoncé à disponible | **Aucun second e-mail.** L'alerte a déjà été envoyée à l'annonce, avec la date (`RG-23`) |
| Une session est corrigée avant sa clôture, ou dans les 2 h qui suivent | Aucun e-mail n'est parti : les alertes en attente sont annulées ou recalculées. Correction intégrale et invisible du public (`RG-45`) |
| Une session est corrigée après l'envoi | Les e-mails sont partis. Les quantités sont rétablies, l'administrateur est informé de ce qui n'est plus rattrapable (`RG-25`) |
| La bourse annoncée est déplacée ou annulée | Aucun e-mail de correction en v1. Le site, lui, affiche la nouvelle date (`RG-38`). C'est une limite assumée : un membre peut se déplacer sur la foi d'une date périmée |
| Le livre est vendu avant que le membre ne vienne | Aucune action. C'est le fonctionnement annoncé. |
| Adresse e-mail en échec de remise répété | Alertes suspendues, membre informé à sa prochaine connexion (`RG-31`) |

### Ce qui est reporté en v2

Notifications push web. Le canal e-mail seul en v1 (décision arrêtée). L'architecture
doit néanmoins traiter « alerte » comme un événement métier et non comme un envoi de
mail, pour que l'ajout d'un canal ne soit pas une réécriture.

## 8. Données personnelles

Le détail des obligations est en `07-exigences-non-fonctionnelles.md` (`ENF-10` à
`ENF-13`). Côté parcours visible :

- La finalité est annoncée au moment de l'inscription, pas seulement dans les mentions
  légales.
- La suppression du compte est accessible depuis « Mon compte », en deux clics, et
  supprime effectivement la liste de recherche et l'historique d'alertes.
- Aucune donnée n'est nécessaire pour consulter le catalogue.

## 9. Ce que le site public ne fait pas

- Pas de vente, pas de panier, pas de paiement.
- Pas de réservation ni de mise de côté.
- Pas de compte pour les bénévoles : ils utilisent l'application de scan.
- Pas d'affichage des livres écartés au tri.
- **Pas d'indication d'emplacement** : le site ne dit jamais dans quel rayon ni dans quel
  bac se trouve un livre (`Q-07`). Le suivi se fait par ISBN et par quantité, sans
  exemplaire individuel : le système ne le sait pas, et l'annoncer serait promettre une
  précision qu'il n'a pas. Le visiteur vient avec un titre ; le bénévole sait où sont ses
  rayons.

## 10. Évolution identifiée : la réservation

Volontairement hors v1, mais anticipée pour ne pas se fermer la porte.

Si elle était retenue plus tard, il faudrait : un espace physique de mise de côté dans
le local, une durée de validité et une expiration automatique, un état
« réservé » distinct de « disponible » dans le cycle de vie, et un écran de retrait à la
caisse. Le modèle par ISBN sans exemplaire individuel ne s'y oppose pas, à condition
de gérer une quantité réservée distincte de la quantité disponible.
