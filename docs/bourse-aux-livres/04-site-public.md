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
   `AssoEvents` de type `Books` existant (`RG-33`). Aucune saisie en double.
3. **Trois sélections** : arrivés récemment, livres rares, et une sélection par genre.

Le catalogue complet est parcourable (décision arrêtée) mais l'accueil ne déverse pas
15 000 titres : il donne des points d'entrée.

## 4. Recherche et navigation

**Recherche** par titre, auteur ou ISBN, dans un seul champ. Elle doit tolérer
l'absence d'accents, les fautes de frappe légères et l'inversion titre/auteur — sans
quoi elle sera jugée inutilisable dès le premier essai.

**Filtres** : genre, disponibilité (`en rayon` / `tout`), livres rares.
**Tris** : pertinence (défaut), arrivée récente.

Un résultat affiche couverture, titre, auteur, et **le nombre d'exemplaires
disponibles**. Ce nombre est la donnée que les gens viennent chercher ; il ne doit
jamais être masqué derrière un clic.

**Les livres à quantité nulle restent affichés**, marqués « épuisé », plutôt que
disparaître : cela permet de les ajouter à sa liste de recherche, ce qui est
précisément le cas d'usage des alertes (`RG-22`).

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
│       mis en rayon le 3 mars 2026            │
│                                              │
│   Prix : 1 à 2 € sur place                   │
│                                              │
│   ┌────────────────────────────────────────┐ │
│   │  🔔  Me prévenir quand il y en aura     │ │
│   └────────────────────────────────────────┘ │
│                                              │
│   ISBN 9782070408504                         │
└──────────────────────────────────────────────┘
```

**La date de mise en rayon est affichée systématiquement.** C'est la manière honnête
de dire au visiteur à quel point l'information est fraîche, sachant que le stock est
un compteur susceptible de dériver (`RG-31`).

Une mention permanente accompagne la disponibilité : *« Disponibilité indicative,
mise à jour à chaque vente. Les livres partent vite. »* Mieux vaut cette réserve
qu'une promesse démentie sur place.

## 6. Compte et liste de recherche

### Création de compte

Via **Microsoft Entra External ID** (décision arrêtée). L'association ne détient et ne
stocke aucun mot de passe.

L'inscription n'est proposée qu'au moment où elle sert : au clic sur
« Me prévenir quand il y en aura ». Aucun mur d'inscription à l'entrée du site.

### Liste de recherche

- Ajout depuis une fiche livre, ou par recherche depuis « Mon compte ».
- On peut y mettre un livre actuellement disponible : cela signifie « préviens-moi au
  prochain réapprovisionnement ».
- Retrait à tout moment, en un clic.
- Une limite raisonnable par compte évite les listes de plusieurs milliers d'entrées
  (`RG-23`).

### Ce que le membre voit sur sa liste

Pour chaque entrée : couverture, titre, disponibilité actuelle, date d'ajout, et la
date de la dernière alerte reçue s'il y en a eu une.

## 7. Alertes

### Déclenchement

Une alerte part **uniquement à la mise en rayon** d'un livre présent dans une liste de
recherche (`RG-20`, `RG-24`). Jamais au tri, jamais à la constitution d'un carton.

C'est la garantie centrale du système : personne n'est invité à se déplacer pour un
livre encore dans un carton à l'atelier de tri.

### Contenu de l'e-mail

- Le titre, l'auteur, la couverture
- Les dates et l'adresse de la prochaine bourse
- **Une mention explicite de non-réservation** : « Ce livre n'est pas mis de côté.
  Premier arrivé, premier servi. »
- Un lien de désinscription et un lien vers la liste de recherche

### Garde-fous

| Situation | Comportement |
|---|---|
| Un carton de 90 livres arrive, dont 12 dans la liste d'un même membre | **Un seul e-mail regroupant les 12 titres** (`RG-25`). Douze e-mails simultanés feraient fuir le destinataire et abîmeraient la réputation d'envoi du domaine. |
| Le même livre est remis en rayon quatre fois en deux mois | Une alerte au plus par livre et par membre sur une période donnée (`RG-26`) |
| Le livre est vendu avant que le membre ne vienne | Aucune action. C'est le fonctionnement annoncé. |
| Adresse e-mail en échec de remise répété | Alertes suspendues, membre informé à sa prochaine connexion (`RG-27`) |

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
- Pas d'affichage des livres écartés au tri, ni des livres encore en carton.

## 10. Évolution identifiée : la réservation

Volontairement hors v1, mais anticipée pour ne pas se fermer la porte.

Si elle était retenue plus tard, il faudrait : un espace physique de mise de côté dans
le local, une durée de validité et une expiration automatique, un état
« réservé » distinct de « en rayon » dans le cycle de vie, et un écran de retrait à la
caisse. Le modèle par ISBN sans exemplaire individuel ne s'y oppose pas, à condition
de gérer une quantité réservée distincte de la quantité disponible.
