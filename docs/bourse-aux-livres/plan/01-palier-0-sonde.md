# Lot 1 — Palier 0, la sonde de faisabilité

**Rien n'est déployé.** C'est ce qui permet d'abandonner ce palier sans coût si les
mesures sont mauvaises — et c'est le seul palier du projet dont c'est vrai.

**Ce qu'on construit.** Une application de scan en **consultation seule** : elle lit un
ISBN, affiche titre, auteur, éditeur, année, couverture. **Elle n'enregistre rien.** Pas de
session, pas de mouvement, pas de base.

**Ce qu'on cherche.** Deux choses qui peuvent tuer le projet et qu'aucun avis ne
tranchera : est-ce que la caméra lit les codes-barres de livres d'occasion réels, et est-ce
que les sources bibliographiques connaissent le fonds donné. Une troisième — est-ce qu'un
bénévole accepte le geste — ne se mesure pas seul et attendra la première utilisation
réelle ; la cadence, elle, se mesure dès maintenant, et c'est elle qui la commande.

**Critère de passage.** Lecture réussie et métadonnées présentes sur une nette majorité du
fonds testé. **Les chiffres cibles s'écrivent avant de lancer le test**, pas après — sinon
on les ajustera au résultat.

---

## `S0-1` — Les chiffres cibles

🔧 Avant d'écrire une ligne : écrire ce qui vaut « ça marche ». Taux de lecture au
premier essai, taux de métadonnées trouvées, cadence tenable.

📌 Les chiffres retenus, dans `NEXT.md`. C'est ce à quoi on comparera.

**Pourquoi c'est une étape et pas une formalité, même seul — surtout seul.** Une mesure
sans seuil écrit d'avance se relit toujours favorablement, et personne n'est là pour dire
le contraire. Les écrire prend cinq minutes ; les inventer après coup ne coûte rien sur le
moment et fausse tout le reste.

*À titre de repère, si aucun chiffre ne vient* : 90 % de lecture au premier essai, 85 % de
métadonnées trouvées, trois secondes par livre. Ce sont des ordres de grandeur à corriger,
pas des cibles héritées — mais un repère écrit vaut mieux qu'une case vide qu'on remplira
avec le résultat.

## `S0-2` — L'application de sonde

🔧 Application Angular minimale, réutilisant `SharedUi` (`@vpd/ui`) — donc branchée sur le
mécanisme de résolution rendu générique en `L0-4`. Lecture du code-barres par la caméra
**et** par écoute clavier — une scanette à gâchette se comporte comme un clavier, et
supporter les deux dès le départ coûte peu (`DT-08`).

Normalisation ISBN-10 → ISBN-13 et contrôle de clé (`RG-01`), refus explicite d'un
code-barres non-ISBN (`RG-02`).

🔧 **D'où viennent les métadonnées, puisque rien n'est déployé.** Le navigateur ne peut pas
interroger la BnF ni Google Books directement : ni l'un ni l'autre ne sert les en-têtes
`CORS` nécessaires, et les clés d'API n'ont rien à faire dans une page. La sonde appelle
donc **un unique point de terminaison de l'API existante, lancée en local par l'AppHost** —
`GET /books/{isbn13}/metadata`, qui exécute le pipeline de `T-07` §1 et renvoie titre,
auteur, éditeur, année, couverture. C'est le seul code backend de ce palier, il est jeté ou
conservé selon le verdict, et il fait que « rien n'est déployé » reste vrai : tout tourne
sur votre machine ou sur le réseau local.

*Conséquence pratique pour `S0-4` :* la campagne se fait avec un portable qui fait tourner
l'AppHost et un téléphone sur le même réseau. À vérifier avant de partir dans le local, pas
sur place.

✅ Tests unitaires sur la conversion et la clé de contrôle — c'est la priorité 3 de
`T-03` §6, et elle vaut d'être écrite ici puisque le code servira ensuite.

**Ne pas construire** : session, file de sortie, IndexedDB, verdict, authentification.
Rien de tout cela n'est mesuré à ce palier, et tout sera réécrit au palier 1 avec les
vraies contraintes. En particulier, la sonde **ne se connecte pas** : `QT-07` et `QT-08` se
sont mesurées en `L0-12`, sur une page jetable, précisément pour que ce palier reste sans
identité.

## `S0-3` — L'instrument de mesure des sources

🔧 Un utilitaire — pas l'application — qui interroge **BnF, Open Library et Google Books en
parallèle** sur une même liste d'ISBN et consigne, pour chacun : réponse ou non, présence
d'un `WorkId`, présence d'une couverture.

L'essai gratuit de sept jours d'ISBNdb peut être ajouté **comme instrument de mesure**,
sans engagement : c'est ce qui dira si l'écart justifierait de rouvrir `DT-01`.

📌 Rien pendant l'exécution ; les résultats à la fin.

## `S0-4` — La campagne, sur 300 livres réels

🧪 **C'est le cœur du palier, et il se fait sur des dons réels.** Trois cents livres
d'affilée, pas trente — c'est la répétition qui révèle la cadence et la fatigue, pas
l'échantillon. Seul, c'est une soirée ; à deux, une heure.

Relever, pour `QT-03` :

| Mesure | Pourquoi |
|---|---|
| Taux de lecture au premier essai | La faisabilité même |
| Délai moyen jusqu'à lecture | `ENF-01` |
| Taux de recours à la saisie manuelle | Si elle devient le chemin nominal, l'outil ne tient pas |
| Cadence tenue au bout de deux cents livres, pas au bout de dix | `ENF-03`, `ENF-19`. C'est le critère qui compte le plus, et le seul substitut au ressenti d'un bénévole tant qu'aucun n'a l'outil en main |

Et pour `QT-01`, sur les mêmes livres :

| Mesure | Ce qu'elle décide |
|---|---|
| Taux de réponse par source | Valide ou invalide `DT-01` |
| Présence d'un `WorkId` | Sans lui, `RG-46` tombe et le repli titre + auteur s'impose (`T-07` §4) |
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

🔧 Comparer aux chiffres de `S0-1` et trancher.

Ce que les résultats décident :

| Résultat | Conséquence |
|---|---|
| Lecture caméra insuffisante | Achat d'une scanette à gâchette **avant** le palier 1 (`Q-08`), ou report |
| Couverture bibliographique faible | Réordonner le pipeline de `DT-01`, ou rouvrir ISBNdb si l'écart dépasse ~20 % en sa faveur |
| Peu de `WorkId` | Le repli titre + auteur de `T-07` §4 devient obligatoire, pas optionnel — et il faut le concevoir avant le palier 3 |
| Beaucoup de livres sans ISBN | Rouvrir le périmètre : c'est une exclusion assumée (`Q-03`), pas une fatalité |
| Cadence intenable au bout de deux cents livres | **Le plus grave, et le moins technique.** Rien de ce qui suit n'a de valeur si l'outil n'est pas utilisé — et un geste qui vous fatigue en fatiguera d'autres |

📌 La décision prise, et sur quels chiffres.

**Le palier 0 a le droit de dire non.** C'est sa fonction, et elle ne disparaît pas parce
qu'on construit tout d'une traite pour ne montrer que le résultat fini. Un critère non
atteint ne bloque pas forcément la suite — mais il change ce qu'on construit :

- une lecture caméra insuffisante veut dire une scanette à gâchette, donc une entrée
  clavier traitée comme le chemin nominal et non comme un secours ;
- peu de `WorkId` veut dire concevoir le repli titre + auteur **avant** d'écrire les
  alertes, pas après.

C'est pour cela que ces mesures se font maintenant, alors qu'elles n'ont l'air de rien :
elles ne décident pas d'aller plus loin, elles décident de ce qu'il y aura à écrire.
