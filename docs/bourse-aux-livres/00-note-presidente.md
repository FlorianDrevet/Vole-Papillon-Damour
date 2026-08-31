# Un outil pour aider au tri de la bourse aux livres

*Note de présentation à l'attention du bureau de l'association — brouillon soumis à validation*

---

## Le problème que l'on cherche à résoudre

La bourse aux livres est la principale ressource financière de l'association. Elle
repose sur des dons, qui sont triés par des bénévoles avant d'être rangés dans le
local de vente.

Ce tri est aujourd'hui un exercice d'intuition. Devant un livre, le bénévole doit
décider de le garder ou de l'écarter, sans pouvoir répondre à trois questions qui
sont pourtant décisives :

- **En avons-nous déjà ?** Si le même titre est déjà présent en cinq exemplaires
  dans le local, en garder un sixième prend une place que l'on n'a pas.
- **Est-ce que ça se vend ?** Si ce titre s'est vendu douze fois l'an dernier, il
  faut le garder sans hésiter. S'il n'est jamais parti, la question se pose.
- **Est-ce que quelqu'un l'attend ?** Aujourd'hui, aucun moyen de le savoir.

Le local est plein. Chaque livre gardé à tort occupe la place d'un livre qui se
serait vendu. Chaque livre écarté à tort est une vente perdue.

## Ce que l'on propose

Une petite application dans laquelle le bénévole **scanne le code-barres au dos du
livre** — ce code est un identifiant unique appelé ISBN, présent sur pratiquement
tous les livres publiés depuis les années 1970.

En moins d'une seconde, l'écran affiche :

> **Le Petit Prince** — Antoine de Saint-Exupéry — Gallimard
>
> ⚠️ **Déjà 5 exemplaires en rayon** — inutile d'en garder un de plus
> ✅ **7 exemplaires vendus lors des dernières bourses** — ce titre part bien
> 🔔 **2 personnes recherchent ce livre**

Le bénévole garde ou écarte en connaissance de cause, en un coup d'œil.

Les livres gardés alimentent automatiquement **un catalogue en ligne**, où le public
peut voir ce qui est disponible à la prochaine bourse. Les personnes qui le souhaitent
peuvent y déclarer les livres qu'elles recherchent, et recevoir un e-mail quand l'un
d'eux arrive.

## Ce que cela change concrètement

### Pour le bénévole qui trie

Il travaille avec un appareil de la taille d'un téléphone. Devant chaque livre, il
appuie sur un bouton, vise le code-barres, lit la réponse, et pose le livre dans un
bac ou dans l'autre. **Un geste de plus par livre, deux secondes environ.**

Il n'a rien à taper, rien à chercher, aucune liste à tenir. C'est l'application qui
se souvient à sa place.

### Pour le bénévole qui range les rayons

Rien ne change dans son travail physique. Un geste s'ajoute au moment où un carton
arrive dans le local : il signale à l'application que ces livres sont désormais en
rayon. C'est ce geste qui les rend visibles en ligne et qui déclenche les e-mails
aux personnes qui les attendaient.

Ce point mérite d'être souligné : **rien n'est publié tant que les livres ne sont pas
réellement disponibles**. Un livre trié le mardi et rangé le samedi n'apparaît en
ligne que le samedi. On ne fera venir personne pour un livre encore dans un carton.

### Pour le bénévole à la caisse

C'est le point qui demande le plus d'attention. **Chaque livre vendu doit être scanné
à la caisse.** Sans cela, l'application continuerait d'annoncer en ligne des livres
déjà partis, et le catalogue perdrait toute crédibilité en une ou deux bourses.

C'est un geste rapide, mais il doit être systématique. Il faudra en tenir compte dans
l'organisation de la caisse les jours de forte affluence.

### Pour le public

Un site où l'on peut chercher un titre, parcourir le catalogue par genre, et voir ce
qui est disponible. On peut y créer un compte pour déclarer les livres que l'on
cherche et être prévenu par e-mail quand ils arrivent.

C'est aussi, pour l'association, un outil de visibilité : un catalogue en ligne
consultable donne une raison de venir à la bourse plutôt que d'y passer par hasard.

## Ce que cela demande à l'association

| | |
|---|---|
| **Matériel** | Un ou deux appareils de scan (type scanette de livreur), à acheter **seulement après une phase de test sur un téléphone personnel**. On ne dépense rien tant que l'on n'a pas vérifié que la méthode fonctionne sur de vrais dons. |
| **Habitudes** | Un geste de scan au tri, un geste au rangement des cartons, un scan à la caisse. |
| **Rigueur** | Le scan en caisse est la condition de fiabilité de l'ensemble. C'est le principal engagement demandé. |
| **Données personnelles** | Le site collectera des adresses e-mail, uniquement de personnes qui s'inscrivent volontairement. Cela impose des obligations légales simples mais réelles : information des personnes, possibilité de se désinscrire et de supprimer son compte. Elles sont prévues dans la conception. |
| **Développement** | Réalisé en interne, bénévolement. Pas de coût de prestataire. Restent les frais d'hébergement, dans la continuité de ce qui existe déjà. |

## Ce que l'on ne fait pas

Il est aussi important de dire ce qui est volontairement laissé de côté :

- **Pas de vente en ligne.** Le site est un catalogue, pas une boutique. On vient
  acheter sur place.
- **Pas de réservation.** Voir un livre en ligne ne le met pas de côté. Premier arrivé,
  premier servi. Une réservation supposerait un espace dédié et un suivi des mises de
  côté que l'on ne souhaite pas imposer aux bénévoles pour l'instant.
- **Pas de prise en charge des livres sans code-barres.** Les livres anciens, publiés
  avant l'usage de l'ISBN, continueront d'être triés comme aujourd'hui. Ils ne seront
  ni comptés ni visibles en ligne. *Il faudra mesurer quelle proportion des dons cela
  représente : c'est l'un des points à observer pendant la phase de test.*

## Comment on procède

L'ensemble est découpé en étapes, chacune apportant quelque chose d'utile et
permettant de s'arrêter si l'expérience n'est pas concluante.

| Étape | Ce qui est livré | Ce que l'on vérifie |
|---|---|---|
| **0 — Test** | Une application de scan qui *affiche* les informations d'un livre sans rien enregistrer, utilisée sur un téléphone personnel | Les codes-barres se lisent-ils bien sur des livres d'occasion abîmés ? Les informations remontent-elles pour les livres français ? Un bénévole tient-il ce rythme sur 300 livres ? |
| **1 — Le socle** | Tri, mise en rayon et scan de vente. Usage interne, rien de public | Les bénévoles adoptent-ils l'outil ? Les chiffres de stock sont-ils justes après une bourse complète ? |
| **2 — La vitrine** | Le catalogue en ligne, consultable par tous | Le public s'en saisit-il ? |
| **3 — Les alertes** | Comptes, listes de recherche, e-mails d'arrivée | |

**L'étape 0 est la plus importante à valider aujourd'hui**, car elle ne coûte rien
d'autre que du temps et elle conditionne tout le reste.

## Ce sur quoi une décision est attendue

1. **L'accord de principe** sur la démarche et sur l'engagement de scanner les ventes
   en caisse.
2. **Le lancement de l'étape 0**, sans achat de matériel.
3. **La manière de faire passer les livres du tri au rayon.** Trois organisations sont
   possibles et elles n'ont pas les mêmes conséquences sur le travail des bénévoles.
   Elles sont décrites dans [`08-questions-ouvertes.md`](08-questions-ouvertes.md),
   question `Q-01`. Un avis sur ce qui est réalisable dans le local serait précieux
   avant de trancher.

---

*Ce document est un brouillon de travail. Toute remarque sur ce qui ne correspondrait
pas à la réalité du terrain est utile, en particulier sur les gestes demandés aux
bénévoles.*
