# 01 — Décisions techniques

Chaque décision porte un identifiant stable. **Une décision ne se réécrit pas** : si
elle est remplacée, elle reste ici marquée `Remplacée par DT-nn`. On doit pouvoir
comprendre plus tard pourquoi un choix a été fait avec l'information de l'époque.

| # | Décision | Statut |
|---|---|---|
| `DT-01` | BnF en source principale, Open Library en complément | Prise |
| `DT-02` | Tout dans SQL Server, aucune base supplémentaire | Prise |
| `DT-03` | Outbox en table, pas de broker de messages | Prise |
| `DT-04` | Worker différé en Container App `kind=functionapp` dédié | Prise — réexaminée, maintenue ; sous réserve de `QT-02` |
| `DT-05` | La fiche livre est le cache ; pas de couche de cache serveur | Prise |
| `DT-06` | Unité de travail explicite pour les écritures multi-agrégats | Prise |
| `DT-07` | Recherche par le plein texte SQL Server d'abord | Prise |
| `DT-08` | App de scan en PWA Angular | Prise |
| `DT-09` | Traitements différés hébergés dans l'API | ⛔ **Écartée** au profit de `DT-04` |

---

## `DT-01` — BnF en source principale, Open Library en complément

**Contexte.** Il faut résoudre un ISBN en métadonnées (`RG-03`), permettre une
recherche par titre ou auteur sur des livres jamais reçus (`RG-47`), et rattacher une
édition à son œuvre (`RG-46`).

**Décision.** Pipeline : cache local → **BnF SRU Catalogue général** → **Open Library**
→ échec assumé (`RG-03`). Google Books et ISBNdb sont écartés.

**Motivation.**

Le critère décisif n'a pas été la richesse des données mais **le droit de les
conserver**. `ENF-01` (moins d'une seconde) et `ENF-05` (hors ligne) imposent de
garder les notices indéfiniment. La BnF publie ses métadonnées descriptives sous
**Licence Ouverte Etalab** : conservation, stockage et rediffusion autorisés contre
mention de la source et de la date. C'est net, et c'est ce qui rend `DT-05` légalement
propre.

S'y ajoute la couverture : le dépôt légal couvre toute la production française, y
compris l'édition ancienne et la jeunesse — précisément la matière première de dons
associatifs, et l'angle mort de `Q-03`.

Open Library complète deux manques de la BnF : le **modèle Œuvre/Édition**, seul
équivalent gratuit de ce qu'exige `RG-46`, et les livres étrangers, hors dépôt légal.

**Alternatives écartées.**

| Écartée | Motif |
|---|---|
| **Google Books** | Aucun regroupement en œuvres, donc `RG-46` tombe. Surtout : position ambiguë sur la conservation — les conditions propres à Books n'en disent rien, les règles de 30 jours documentées visent Maps et Places. Cette ambiguïté est disqualifiante quand toute l'architecture repose sur le cache. Quota journalier chiffré en prime. |
| **ISBNdb** | ~305 $/an au palier Premium, sans les prix de revente qui sont réservés au palier à 75 $/mois. Couverture du fonds français ancien inférieure au dépôt légal, pas de niveau œuvre. `ENF-23` s'y oppose : ~200 à 280 livres vendus par an pour payer l'API. |
| **Dilicom (FEL), Electre** | Réservés aux professionnels du livre sur abonnement, et centrés sur le livre **disponible à la vente** — alors que les dons sont souvent épuisés depuis vingt ans. |
| **WorldCat / OCLC** | Conditions restrictives, orienté institutions. |

**Conséquences.**

- Format SRU en XML MARC à parser, nettement plus lourd que du JSON. C'est le prix.
- Obligation de porter la mention de source et la date de récupération (`02` §3).
- Les livres étrangers dépendent d'Open Library seule, dont la couverture française est
  irrégulière — l'inverse est vrai aussi.
- **À valider par mesure** : `QT-01` fait interroger les sources en parallèle sur des
  dons réels au palier 0. Cette décision est un pari documenté, pas un fait établi.

---

## `DT-02` — Tout dans SQL Server, aucune base supplémentaire

**Contexte.** La question a été posée explicitement : faut-il ajouter du NoSQL pour le
catalogue et les notices ?

**Décision.** Données maîtres, notices et outbox dans le **SQL Server existant**.
Notices brutes dans une colonne JSON. Couvertures dans le **blob** existant. Copie
embarquée sur l'appareil en **IndexedDB**. Aucune ressource de données nouvelle.

**Motivation.**

*Les chiffres ferment l'axe performance.* 15 000 fiches ≈ 5 Mo, ou 25 Mo avec les
notices brutes ; ~30 000 mouvements par an ≈ 4,5 Mo. **Moins de 100 Mo après cinq
ans**, hors images. Charge de pointe : cinq scanettes à un scan toutes les deux
secondes, soit **2,5 requêtes/seconde**. Tous les candidats sont surdimensionnés ; le
choix se fait donc sur le coût total, pas sur la performance.

*Les requêtes exigées sont relationnelles.* L'écran de désengorgement (`05` §5) croise
fiches et mouvements avec agrégat et filtre ; les statistiques par bourse (`05` §2)
joignent sur `AssoEvents` ; `RG-10` somme disponible et annoncé. Séparer les magasins,
c'est écrire ces jointures à la main dans le code applicatif.

*Les mouvements sont l'historique comptable* (`ENF-22`), et les quantités en découlent.
Cela demande des transactions et de l'intégrité référentielle, pas de la cohérence
éventuelle.

*Le coût marginal de SQL Server est zéro* — déjà provisionné, sauvegardé, câblé dans
l'AppHost Aspire et dans EF Core. Aucun palier gratuit ne bat zéro, parce que le coût
d'une seconde base n'est pas sur la facture : il est dans le deuxième système à
sauvegarder, à monter en local et à déboguer.

**Sur la souplesse de schéma.** Les notices externes ont des formes hétérogènes — un
UNIMARC BnF et un JSON Open Library n'ont ni les mêmes champs ni la même structure. La
réponse est une **colonne JSON dans SQL Server** (`RawPayload`), pas une base
documentaire : on garde la souplesse et l'auditabilité, dans une seule transaction et
une seule sauvegarde.

**Seuils qui rendraient une base dédiée légitime.** Aucun n'est approché :

- plus de quelques millions de mouvements — soit un facteur 100 ;
- plus de quelques dizaines de requêtes/seconde soutenues — on est à 2,5 ;
- besoin de distribution géographique — sans objet ;
- données maîtres réellement sans schéma — ce n'est pas le cas.

**Note.** Le budget n'est pas un critère ici. Même financé, un magasin séparé resterait
le mauvais choix pour ces données : trois des quatre arguments ci-dessus ne dépendent
pas du coût.

---

## `DT-03` — Outbox en table, pas de broker de messages

**Contexte.** `RG-44` impose d'envoyer les alertes 2 h après la clôture d'une session.
La question s'est posée d'un Service Bus avec messages programmés.

**Décision.** Une table `OutboxMessage` avec une échéance `DueAt`. Aucun broker.

**Motivation.**

*L'administration doit voir la file.* `05` §4 bis exige de lister les alertes en
attente avec un compte à rebours, de les annuler et de forcer l'envoi. Un message
programmé dans un broker est invisible : on peut l'annuler si l'on a gardé son numéro
de séquence, mais on ne peut ni le lister, ni le joindre à une session, ni l'afficher.
Il faudrait donc une table **en plus** du broker, et deux sources de vérité qui peuvent
diverger.

*L'atomicité l'impose.* Clôturer une session et mettre ses alertes en file doivent être
un seul acte : clôturer sans mettre en file, ou l'inverse, est une incohérence
silencieuse. En base, c'est une transaction. Avec un broker externe, c'est le problème
de la double écriture — dont la solution canonique est justement un outbox. **Le broker
ajouterait ce qu'il était censé remplacer.**

*Le volume ne le justifie pas.* Quelques dizaines de messages par semaine.

**Quand rouvrir.** Diffusion à plusieurs consommateurs — le scénario le plus plausible
est l'arrivée des notifications push de la v2, où « livre disponible » aurait deux
abonnés. À ce moment, des topics se discuteront.

---

## `DT-04` — Worker différé en Container App `kind=functionapp` dédié

> ♻️ **Réexaminée, et maintenue.** L'API passe à `minReplicas: 1` — elle ne peut pas
> être indisponible pour le site web — ce qui retire à cette décision **sa prémisse
> d'origine**, sans emporter la décision elle-même. Elle tient pour des raisons qui
> n'étaient pas les siennes au départ : voir le [réexamen](#réexamen--pourquoi-le-worker-survit-à-sa-prémisse)
> en fin de section. Le texte ci-dessous n'est pas retouché. L'alternative — dissoudre
> le worker dans l'API — est instruite et écartée en [`DT-09`](#dt-09--traitements-différés-hébergés-dans-lapi).

**Contexte.** Les Container Apps sont configurées avec `minReplicas: 0`. Un
`BackgroundService` hébergé dans l'API ne s'exécuterait donc qu'au hasard du trafic
HTTP : les alertes de `RG-44` et la bascule de `RG-23` ne partiraient pas.

**Décision.** Une application dédiée dans l'environnement Container Apps **existant**,
créée via `Microsoft.App/containerApps` avec `kind=functionapp`.

**Motivation.**

Isoler les traitements de fond évite qu'un rattrapage lourd dégrade le temps de réponse
d'un scan. Le modèle de programmation Functions apporte les déclencheurs planifiés
déclaratifs et les réessais sans plomberie. La facturation reste celle de Container
Apps, **sans supplément pour le modèle Functions**. Et .NET Aspire a une intégration
Azure Functions, ce qui préserve le montage local existant.

**À ne pas confondre.** Deux voies existent ; celle par `Microsoft.Web/sites` avec
`managedEnvironmentId` est marquée *legacy* dans la documentation. C'est
`Microsoft.App` avec `kind=functionapp` qu'il faut, et il ne s'agit **pas** d'un
environnement séparé : l'application vit dans le `managedEnvironment` déjà déclaré.

**Réserve — `QT-02`.** La documentation liste le déclencheur planifié parmi ceux qui
montent depuis zéro via KEDA, mais des retours indiquent qu'une application descendue à
zéro n'est pas réveillée par son minuteur. Pour `RG-44`, ce serait un échec silencieux.
**À mesurer avant de construire dessus.** Trois issues selon le résultat : `minReplicas: 0`
si le réveil fonctionne ; `minReplicas: 1` sinon, au prix d'un conteneur allumé en
permanence ; ou temporisation par file Azure Queue Storage — un message peut rester
invisible jusqu'à sept jours et son déclencheur réveille bien une application à zéro,
la table restant la source de vérité.

**Alternative conservée en repli.** Un ACA Job en cron : planification garantie par la
plateforme, facturation à l'exécution seule (~21 600 vCPU-secondes/mois contre 180 000
gratuits). Moins confortable à l'exploitation, mais sans zone d'ombre.

### Réexamen — pourquoi le worker survit à sa prémisse

*Écrit après coup, le jour où l'API est passée à `minReplicas: 1`.*

La décision ci-dessus reposait sur un argument de fiabilité : un `BackgroundService`
logé dans un conteneur susceptible d'être éteint ne s'exécute qu'au hasard du trafic.
Avec un réplica d'API permanent, **cet argument tombe**. Trois raisons le remplacent, et
elles figuraient déjà, en second rang, dans la motivation d'origine.

*L'isolation devient l'argument principal.* Le réplica permanent est là pour servir le
site web, pas pour héberger du travail de fond. Un rattrapage d'enrichissement ou un
balayage d'outbox partagerait alors le processeur avec le rendu SSR et le scan des
bénévoles, au détriment de `ENF-01`. Le découplage coûte une application ; il achète la
garantie qu'aucun traitement différé ne dégrade un temps de réponse.

*Les déclencheurs planifiés et les réessais restent déclaratifs.* Un `BackgroundService`
demande d'écrire soi-même la boucle, le calendrier, la temporisation exponentielle et
l'exclusion entre répliques. Le modèle Functions les fournit.

*Les cycles de vie restent séparés.* L'API se déploie au rythme du site et du scan ; le
worker au rythme du métier différé. Chacun se met à l'échelle, se redémarre et se
diagnostique sans toucher à l'autre.

**Ce que le réexamen ne lave pas.** `QT-02` reste entièrement ouverte et **bloquante** :
c'est désormais le worker, et lui seul, qui vit à zéro réplica. Si son minuteur ne le
réveille pas, `RG-44` échoue en silence. La mesure décrite en
[`09-questions-techniques.md`](09-questions-techniques.md) reste à faire avant le
palier 1, et les trois issues décrites plus haut restent les trois issues.

---

## `DT-05` — La fiche livre est le cache

**Décision.** Aucune couche de cache serveur. La table des fiches **est** le cache, et
les notices n'expirent jamais.

**Motivation.** `RG-03` impose déjà de créer une fiche pour tout ISBN scanné. Un cache
séparé dupliquerait cette table. Et une notice bibliographique est **immuable en
pratique** : un ISBN désigne une édition figée, dont le titre et l'éditeur ne changeront
pas. Donc pas de durée de vie, pas d'invalidation, pas de logique de péremption.

**Ce qui en découle.**

- **Cache négatif obligatoire.** Un ISBN inconnu des deux sources sera redonné — c'est
  une bourse aux livres, les mêmes titres reviennent. Sans état `NotFound` mémorisé,
  chaque don relance deux appels externes indéfiniment. Réessais à J+7 puis J+30, puis
  bascule en file manuelle.
- **Verrou d'unicité sur l'ISBN.** Cinq scanettes peuvent scanner le même ISBN inconnu
  dans la même seconde ; sans déduplication, c'est cinq fois le pipeline.
- **Les corrections manuelles sont protégées** (`RG-05`) : le rattrapage ne doit jamais
  écraser un champ marqué comme corrigé.
- **Les couvertures sont copiées** chez nous, pas pointées chez la source — la Licence
  Ouverte l'autorise, et cela évite de dépendre de leur disponibilité.

---

## `DT-06` — Unité de travail explicite pour les écritures multi-agrégats

**Contexte.** Le `BaseRepository` existant appelle `SaveChangesAsync()` **à chaque
opération** (`AddAsync`, `UpdateAsync`, `DeleteAsync`). Il n'existe pas d'unité de
travail.

**Le problème.** `RG-44` exige que la clôture d'une session et l'insertion de ses
lignes d'outbox soient atomiques. Avec un enregistrement par appel, une panne entre les
deux laisse une session close dont les alertes ne partiront jamais — ou l'inverse. Le
même problème vaut pour `RG-45` (reprise en bloc) et pour toute écriture touchant
plusieurs agrégats.

**Décision.** Les tranches `Books` n'utilisent pas le `BaseRepository` tel quel pour
les écritures composites. Elles passent par une transaction explicite ou une unité de
travail introduite pour ces cas, avec `SaveChanges` unique en fin de traitement.

**Portée.** On ne refait pas les tranches existantes : le changement est **additif** et
limité au nouveau domaine. Rendre le comportement général serait un chantier de
migration à part entière, hors du périmètre de ce projet.

---

## `DT-07` — Recherche par le plein texte SQL Server d'abord

**Contexte.** `ENF-08` demande des résultats en moins d'une seconde sur 15 000 titres,
en tolérant l'absence d'accents, les fautes de frappe légères et l'inversion
titre/auteur.

**Décision.** Recherche plein texte SQL Server en v1. Azure AI Search **différé**.

**Motivation.** C'est le seul besoin que le relationnel ne couvre qu'à moitié : les
accents se règlent par la collation, la tolérance aux fautes est faible. Mais un index
de recherche n'est **pas une source de vérité** : l'ajouter plus tard ne migre aucune
donnée et ne coûte presque rien. C'est exactement le type de décision qu'il faut
retarder jusqu'à disposer d'un retour d'usage.

**Réserve sur le palier gratuit d'Azure AI Search.** Il offre 50 Mo et 3 index, ce qui
suffit — mais le service **peut être supprimé après des périodes d'inactivité**. Le
trafic de l'association est en dents de scie, une semaine par mois : c'est précisément
le profil concerné. À ne pas placer sur un chemin critique sans supervision.

**Piste complémentaire.** L'app de scan détient déjà les 15 000 titres en local
(`DT-08`) : sa recherche y est instantanée et hors ligne, sans serveur. Seul le site
public a besoin d'une recherche serveur, pour le rendu indexable exigé par `ENF-09`.

---

## `DT-08` — App de scan en PWA Angular

**Décision.** Application web progressive en Angular, réutilisant `SharedUi`
(`@vpd/ui`), avec IndexedDB pour la copie locale du catalogue et la file de sortie.

**Motivation.** La décision fonctionnelle est de commencer par le web, sur téléphone
personnel, avant tout achat de matériel (palier 0, `Q-08`). Angular aligne la pile sur
les deux applications existantes et permet de partager les composants — `ENF-24` de
nouveau : une seule pile front à maintenir.

La copie locale du catalogue tient dans **~3 Mo** pour 15 000 titres, ce qui rend le
sujet du volume sans objet : on synchronise tout, par delta.

**Réserve.** Le lecteur de code-barres via la caméra du navigateur est le point à
valider au palier 0 sur des couvertures abîmées et plastifiées (`QT-03`). Une scanette
à gâchette se comporte comme un clavier, ce qui est plus simple — mais l'achat vient
après la mesure.

---

## `DT-09` — Traitements différés hébergés dans l'API

> ⛔ **Écartée.** Instruite le jour où l'API est passée à `minReplicas: 1`, puis écartée
> au profit de [`DT-04`](#dt-04--worker-différé-en-container-app-kindfunctionapp-dédié),
> maintenue. Conservée ici parce qu'elle reste l'alternative sérieuse : si le worker
> devait un jour coûter plus qu'il ne rapporte, c'est ce document qu'on rouvrirait.

**Ce qu'elle proposait.** Supprimer l'application worker et héberger les traitements
différés en services hébergés (`BackgroundService`) **dans le projet API**.

**L'argument, et il est bon.** L'API passe à `minReplicas: 1` pour une raison étrangère
au module livres : elle ne peut pas être indisponible pour le site web. Le conteneur
tourne donc en permanence. La prémisse de `DT-04` — un processus susceptible d'être
éteint — disparaît, un service hébergé s'exécuterait de façon fiable, et le coût
marginal serait **nul**. S'y ajoutent un projet au lieu de deux, une image, un
déploiement, aucune image de base Functions, aucune contrainte d'ingress ni de compte de
stockage, et la fermeture immédiate de `QT-02` — l'une des deux mesures bloquantes du
palier 1 — sans avoir à la mesurer.

**Pourquoi elle est écartée.**

*Le réplica permanent est allumé pour le site web, pas pour du travail de fond.* Y loger
les balayages ferait partager le processeur entre le rendu SSR, les requêtes de scan et
un rattrapage d'enrichissement potentiellement lourd. `ENF-01` porte sur le délai
d'affichage du verdict au scan : c'est précisément ce qu'on ne veut pas voir dépendre de
la charge différée.

*`maxReplicas: 2` obligerait à traiter l'exclusion.* Deux répliques d'API exécuteraient
les mêmes balayages simultanément. Les opérations étant conçues en réclamation
conditionnelle (`06` §5), rien ne se doublonnerait — mais il faudrait une ligne de bail
en base pour que les journaux ne racontent qu'une histoire à la fois. Le modèle
Functions règle la question sans code.

*Les cycles de vie se retrouveraient liés.* Chaque déploiement du site ou du scan
redémarrerait les traitements de fond, et l'échelle de l'un imposerait celle des autres.

**Ce que le refus coûte.** `QT-02` reste ouverte et bloquante, et il faut la mesurer.
C'est le prix assumé de l'isolation — voir le réexamen de `DT-04`.

**Quand rouvrir.** Si `QT-02` se révèle mal-comportée au point d'imposer un worker à
`minReplicas: 1`, on paierait alors **deux** conteneurs permanents pour un travail qui
tient dans quelques requêtes SQL toutes les cinq minutes. Ce jour-là, `DT-09` redevient
le bon choix. Le passage d'un hôte à l'autre est peu coûteux : la logique vit dans les
bibliothèques `Application` et `Infrastructure` (`06` §2), pas dans l'hôte.

**Ce que cela ne change pas.** `DT-03` (outbox en table) reste valide dans les deux cas.
