# 09 — Questions techniques ouvertes

**À lire avant d'écrire du code de production.** Deux de ces points se règlent par une
mesure, pas par un avis, et l'un d'eux peut invalider une décision déjà prise.

| # | Sujet | Statut | Quand |
|---|---|---|---|
| `QT-01` | Couverture réelle des sources bibliographiques | 🔴 **Bloquant** | Palier 0 |
| `QT-02` | Déclencheur planifié et mise à l'échelle à zéro | 🔴 **Bloquant** | Avant le palier 1 |
| `QT-03` | Lecture du code-barres au navigateur | 🟠 À mesurer | Palier 0 |
| `QT-04` | Dimensionnement Entra External ID | 🟡 À vérifier | Avant le palier 3 |
| `QT-05` | Unité de travail et `BaseRepository` | 🟢 Tranchée, à cadrer | Palier 1 |
| `QT-06` | Tolérance aux fautes de la recherche | 🟢 Différée | Après le palier 2 |

---

## `QT-01` — Couverture réelle des sources

> 🔴 **Bloquant.** `DT-01` est un pari documenté, pas un fait établi.

**Le test.** Pendant le palier 0, interroger **BnF, Open Library et Google Books en
parallèle** sur les mêmes 300 livres réellement donnés, et relever pour chacun :

| Mesure | Pourquoi elle compte |
|---|---|
| Taux de réponse par source | Valide ou invalide `DT-01` |
| Présence d'un `WorkId` | Conditionne `RG-46`. Sans lui, le repli titre + auteur s'impose |
| Présence d'une couverture | Confort d'affichage |
| Livres sans ISBN du tout | Répond à `Q-03` fonctionnel — angle mort assumé dont on ignore la taille |
| Code-barres illisible mais ISBN imprimé | Récupérable par saisie manuelle |

L'essai gratuit de 7 jours d'ISBNdb peut être ajouté à la comparaison **comme
instrument de mesure**, sans engagement. Si un écart supérieur à ~20 % apparaît en sa
faveur, la question de l'abonnement se rouvre — en sachant précisément quel trou il
comble.

**Ce que le résultat décide.** L'ordre du pipeline, la nécessité du repli de `RG-46`,
et éventuellement la réouverture du périmètre « livres sans ISBN ».

---

## `QT-02` — Déclencheur planifié et mise à l'échelle à zéro

> 🔴 **Bloquant.** Un échec ici est silencieux : les alertes ne partent jamais.

**Le conflit.** La documentation liste le déclencheur planifié parmi ceux qui montent
depuis zéro via KEDA. Des retours indiquent qu'une application descendue à zéro n'est
pas réveillée par son minuteur et attend un autre événement.

**Le test, trente minutes.** Déployer une fonction planifiée avec `minReplicas: 0`, ne
pas y toucher pendant deux heures, vérifier dans les journaux qu'elle s'est exécutée aux
échéances attendues.

**Les trois issues** sont décrites en
[`06-traitements-differes.md`](06-traitements-differes.md) §6 : `minReplicas: 0` si le
réveil fonctionne ; `minReplicas: 1` sinon, au prix d'un conteneur permanent ; ou
temporisation par file Azure Queue Storage, dont le déclencheur réveille bien depuis
zéro.

**Ce que le résultat décide.** Le coût mensuel du worker et, éventuellement, la forme du
délai de `RG-44`.

---

## `QT-03` — Lecture du code-barres au navigateur

> 🟠 À mesurer au palier 0.

Le lecteur caméra doit fonctionner sur des livres d'occasion : couvertures abîmées,
plastifiées, froissées, mal éclairées. C'est la faisabilité même du palier 0, donc de
tout le reste.

**À relever** : taux de lecture au premier essai, délai moyen jusqu'à lecture, taux de
recours à la saisie manuelle, et ressenti d'un bénévole sur au moins 300 livres
d'affilée.

Une scanette à gâchette se comporte comme un clavier et ne pose pas ce problème — mais
l'achat vient **après** la mesure (`Q-08` fonctionnel), pas avant. D'où l'intérêt de
supporter les deux entrées dès le départ.

---

## `QT-04` — Dimensionnement Entra External ID

> 🟡 À vérifier avant le palier 3.

`ENF-16` retient Entra External ID. À confirmer avant de s'engager :

- Le palier gratuit couvre-t-il quelques centaines de comptes, et à quel coût au-delà ?
- Le parcours d'inscription peut-il rester aussi léger que le veut `04` §6 fonctionnel —
  proposé seulement au clic sur « me prévenir » ?
- La suppression exigée par `ENF-12` s'applique-t-elle bien des deux côtés, chez le
  fournisseur d'identité **et** dans notre base ?

Le troisième point est le plus facile à rater : effacer nos données en laissant
l'identité vivante n'est pas une suppression.

---

## `QT-05` — Unité de travail et `BaseRepository`

> 🟢 Tranchée par `DT-06`, reste à cadrer.

Le `BaseRepository` existant appelle `SaveChangesAsync()` à chaque opération. Trois
traitements exigent l'atomicité entre agrégats
([`02-modele-de-donnees.md`](02-modele-de-donnees.md) §5).

**Décidé** : le module livres n'utilise pas le `BaseRepository` pour ces cas et passe
par une transaction explicite. **À cadrer** : la forme exacte — unité de travail
introduite proprement, ou transaction ouverte dans le handler.

**Contrainte** : le changement reste **additif**. Généraliser le comportement aux
tranches existantes serait un chantier de migration à part entière, hors périmètre.

---

## `QT-06` — Tolérance aux fautes de la recherche

> 🟢 Différée par `DT-07`.

Le plein texte SQL Server gère les accents par la collation, mal les fautes de frappe.
Azure AI Search comblerait l'écart, avec une réserve : son palier gratuit **peut être
supprimé après des périodes d'inactivité**, ce qui correspond exactement au profil en
dents de scie de l'association — une semaine par mois.

**À décider après le palier 2**, sur retour d'usage réel plutôt que par anticipation.
Un index de recherche n'est pas une source de vérité : l'ajouter plus tard ne migre
aucune donnée.

---

## Ce qui n'est pas une question ouverte

Pour éviter de rouvrir ce qui est tranché :

| Sujet | Décision |
|---|---|
| Base de données | `DT-02` — tout dans SQL Server. Les seuils qui rouvriraient le sujet y sont chiffrés |
| Broker de messages | `DT-03` — table d'outbox. Rouvrable seulement à l'arrivée de plusieurs consommateurs (push v2) |
| Cache | `DT-05` — la fiche est le cache, pas d'expiration |
| ISBNdb | Écarté par `DT-01`. Rouvrable si `QT-01` montre un écart significatif |
| Prix dans le système | `RG-50` — aucun. Décision fonctionnelle, pas technique |
