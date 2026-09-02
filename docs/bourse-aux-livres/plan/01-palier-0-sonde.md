# Lot 1 — Palier 0, la sonde de faisabilité

**Rien n'est déployé.** C'est ce qui permet d'abandonner ce palier sans coût si les
mesures sont mauvaises — et c'est le seul palier du projet dont c'est vrai.

**Ce qu'on construit.** Une application de scan en **consultation seule** : elle lit un
ISBN, affiche titre, auteur, éditeur, année, couverture. **Elle n'enregistre rien.** Pas de
session, pas de mouvement, pas de base.

**Ce qu'on cherche.** Trois choses qui peuvent tuer le projet, et qu'aucun avis ne
tranchera : est-ce que la caméra lit les codes-barres de livres d'occasion réels, est-ce
que les sources bibliographiques connaissent le fonds donné, et est-ce qu'un bénévole
accepte le geste.

**Critère de passage.** Lecture réussie et métadonnées présentes sur une nette majorité du
fonds testé, et acceptation du geste par le bénévole. **Les chiffres cibles se fixent avec
l'association avant de lancer le test**, pas après — sinon on les ajustera au résultat.

---

## `S0-1` — Les chiffres cibles

🔧 Avant d'écrire une ligne : convenir avec l'association de ce qui vaut « ça marche ».
Taux de lecture au premier essai, taux de métadonnées trouvées, cadence tenable.

📌 Les chiffres retenus, dans `NEXT.md`. C'est ce à quoi on comparera.

**Pourquoi c'est une étape et pas une formalité.** Une mesure sans seuil convenu d'avance
se relit toujours favorablement.

## `S0-2` — L'application de sonde

🔧 Application Angular minimale, réutilisant `SharedUi` (`@vpd/ui`). Lecture du
code-barres par la caméra **et** par écoute clavier — une scanette à gâchette se comporte
comme un clavier, et supporter les deux dès le départ coûte peu (`DT-08`).

Normalisation ISBN-10 → ISBN-13 et contrôle de clé (`RG-01`), refus explicite d'un
code-barres non-ISBN (`RG-02`).

✅ Tests unitaires sur la conversion et la clé de contrôle — c'est la priorité 3 de
`03` §6, et elle vaut d'être écrite ici puisque le code servira ensuite.

**Ne pas construire** : session, file de sortie, IndexedDB, verdict. Rien de tout cela
n'est mesuré à ce palier, et tout sera réécrit au palier 1 avec les vraies contraintes.

## `S0-3` — L'instrument de mesure des sources

🔧 Un utilitaire — pas l'application — qui interroge **BnF, Open Library et Google Books en
parallèle** sur une même liste d'ISBN et consigne, pour chacun : réponse ou non, présence
d'un `WorkId`, présence d'une couverture.

L'essai gratuit de sept jours d'ISBNdb peut être ajouté **comme instrument de mesure**,
sans engagement : c'est ce qui dira si l'écart justifierait de rouvrir `DT-01`.

📌 Rien pendant l'exécution ; les résultats à la fin.

## `S0-4` — La campagne, sur 300 livres réels

🧪 **C'est le cœur du palier, et il se fait avec un bénévole, sur des dons réels.** Trois
cents livres d'affilée, pas trente.

Relever, pour `QT-03` :

| Mesure | Pourquoi |
|---|---|
| Taux de lecture au premier essai | La faisabilité même |
| Délai moyen jusqu'à lecture | `ENF-01` |
| Taux de recours à la saisie manuelle | Si elle devient le chemin nominal, l'outil ne tient pas |
| Cadence tenue et **ressenti du bénévole** | `ENF-03`, `ENF-19` — et c'est le critère qui compte le plus |

Et pour `QT-01`, sur les mêmes livres :

| Mesure | Ce qu'elle décide |
|---|---|
| Taux de réponse par source | Valide ou invalide `DT-01` |
| Présence d'un `WorkId` | Sans lui, `RG-46` tombe et le repli titre + auteur s'impose (`07` §4) |
| Présence d'une couverture | Confort |
| **Livres sans ISBN du tout** | Répond à `Q-03` : un angle mort assumé dont on ignore la taille |
| Code-barres illisible mais ISBN imprimé | Récupérable à la main |

**Tester sur des couvertures abîmées, plastifiées, froissées, et dans le local, à son
éclairage réel.** Une mesure faite sur des livres neufs sous une lampe de bureau ne mesure
rien.

📌 **Tous les chiffres, dans `NEXT.md`, avec la date et le nombre de livres.** Ce sont les
données les plus précieuses produites par le projet à ce stade, et elles ne se
reconstituent pas.

## `S0-5` — Le verdict

🔧 Comparer aux chiffres de `S0-1` et trancher, avec l'association.

Ce que les résultats décident :

| Résultat | Conséquence |
|---|---|
| Lecture caméra insuffisante | Achat d'une scanette à gâchette **avant** le palier 1 (`Q-08`), ou report |
| Couverture bibliographique faible | Réordonner le pipeline de `DT-01`, ou rouvrir ISBNdb si l'écart dépasse ~20 % en sa faveur |
| Peu de `WorkId` | Le repli titre + auteur de `07` §4 devient obligatoire, pas optionnel — et il faut le concevoir avant le palier 3 |
| Beaucoup de livres sans ISBN | Rouvrir le périmètre avec l'association : c'est une exclusion assumée, pas une fatalité |
| Bénévole réticent au geste | **Le plus grave, et le moins technique.** Rien de ce qui suit n'a de valeur si l'outil n'est pas utilisé |

📌 La décision prise, et sur quels chiffres.

**Le palier 0 a le droit de dire non.** C'est sa fonction. Un critère non atteint arrête le
projet ou le redéfinit — il ne se contourne pas en passant au palier suivant.
