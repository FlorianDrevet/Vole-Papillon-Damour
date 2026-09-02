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
| `DT-10` | Entra External ID comme fournisseur d'identité unique | Prise |
| `DT-11` | Base SQL en palier fixe `S1`, sortie du serverless à pause automatique | Prise — sous réserve de `QT-09` |
| `DT-12` | Azure Communication Services Email, sur un sous-domaine d'envoi dédié | Prise |
| `DT-13` | Catalogue sur `livres.volepapillondamour.fr`, URL en slug + ISBN | Prise |
| `DT-14` | Une seule table de personnes, rapprochée par `oid` | Prise |
| `DT-15` | Socle unifié : .NET 10 partout, Aspire à jour, versions centralisées | Prise |
| `DT-16` | L'observabilité s'écrit avec la fonctionnalité, jamais après | Prise |

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

> ♻️ **Réexaminée, et maintenue.** L'API est passée à `minReplicas: 1` (`36b0e50`) —
> elle ne peut pas être indisponible pour le site web — ce qui retire à cette décision
> **sa prémisse d'origine**, sans emporter la décision elle-même. Elle tient pour des raisons qui
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

**L'argument, et il est bon.** L'API est passée à `minReplicas: 1` pour une raison
étrangère au module livres : elle ne peut pas être indisponible pour le site web. Le
conteneur tourne donc en permanence, et il n'est pas le seul — `36b0e50` a fait de même
pour `website` et `backOffice`. La prémisse de `DT-04` — un processus susceptible d'être
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
`minReplicas: 1`, ce serait un **quatrième** conteneur allumé en continu pour un travail
qui tient dans quelques requêtes SQL toutes les cinq minutes. Ce jour-là, `DT-09` redevient
le bon choix. Le passage d'un hôte à l'autre est peu coûteux : la logique vit dans les
bibliothèques `Application` et `Infrastructure` (`06` §2), pas dans l'hôte.

**Ce que cela ne change pas.** `DT-03` (outbox en table) reste valide dans les deux cas.

---

## `DT-10` — Entra External ID comme fournisseur d'identité unique

**Décision.** **Un seul fournisseur d'identité pour tous les publics** — membres du site,
bénévoles, administrateurs — dans un locataire Microsoft Entra External ID. Suppression
de l'authentification maison du backend, avec ses mots de passe en base et sa clé de
signature symétrique.

**Contexte.** Le plan initial coupait en deux : Entra External ID pour le public
(`ENF-16`), l'authentification JWT existante du backend pour les bénévoles (`ENF-17`), et
un rôle explicite quelque part pour les administrateurs (`ENF-18`). Trois mécanismes pour
trois publics, dans une association où la même personne est souvent les trois.

**Motivation.**

*Un seul endroit où un compte existe.* La coupure imposait deux annuaires qui divergent
dès le premier compte désactivé d'un côté sans l'être de l'autre. Un bénévole qui part
doit disparaître en une opération, pas en deux.

*L'authentification maison est le seul endroit où l'association détient des mots de
passe.* Empreintes, sel, clé de signature symétrique en Key Vault, point de terminaison
d'inscription ouvert : c'est la surface la plus sensible du système, et elle n'apporte
rien qu'Entra ne fasse mieux. La supprimer retire un risque au lieu d'en gérer un.

*Le coût marginal est faible.* Le locataire externe est de toute façon créé pour le
public. Y ajouter les bénévoles ne change ni sa nature ni son ordre de grandeur de
facturation.

*Une seule bibliothèque cliente.* MSAL partout, au lieu de MSAL pour le public et un
gestionnaire de jeton maison — cookie, décodage, expiration — pour le back-office.

**Ce que cela coûte.**

*Les comptes à privilèges vivent dans un locataire externe*, dont les protections ne sont
pas celles d'un locataire de travail. Pour trois ou quatre administrateurs d'une
association, c'est acceptable. Ce ne le serait pas ailleurs.

*Une migration à faire, et à faire entièrement.* L'inventaire est en
[`10-identite-et-droits.md`](10-identite-et-droits.md) §6. Une migration à moitié faite
laisse un chemin d'authentification parallèle ouvert : pire que de ne rien changer.

*La durée de vie des jetons entre en tension avec `ENF-17` et le mode hors ligne.* C'est
le vrai point dur, traité en `10` §9 et mesuré par `QT-08`.

**Les droits.** Des **rôles applicatifs** portés par l'enregistrement de l'API — `Tri`,
`Caisse`, `Administration` — attribués directement aux comptes, et lus dans la
revendication `roles` du jeton. Pas de groupes de sécurité : ils transportent des GUID
qu'il faudrait faire correspondre à des droits, et leur attribution à un rôle applicatif
n'est pas gratuite. Pas de rôle en base : ce serait le second annuaire qu'on cherche
justement à supprimer. Le raisonnement complet, et la condition de réouverture en faveur
des groupes, sont en `10` §4.

**Un membre du public n'a aucun rôle.** L'absence de rôle *est* le statut de membre. Sans
cela, chaque inscription en libre-service devrait écrire dans l'annuaire, donc brancher
une extension d'authentification sur le flux d'inscription — de la mécanique à surveiller
pour un droit qui ne dit rien de plus que « le jeton est valide ».

**Toute la configuration est scriptée** en PowerShell sur Microsoft Graph
(`infra/entra/`). Deux exceptions assumées et signalées : la création du locataire, et le
flux d'inscription en libre-service dont l'API Graph est en `beta` pour les locataires
externes (`QT-07`).

**Ce que cela change dans le plan.** La création du locataire devient le **tout premier
élément livré**, avant même la sonde de faisabilité — voir `01` §7 des spécifications
fonctionnelles. Ce n'est pas un caprice d'ordonnancement : tout ce qui s'authentifie
dépend de son existence, et le délai de propagation d'un locataire neuf n'est pas
maîtrisé.

---

## `DT-11` — Base SQL en palier fixe `S1`, sortie du serverless à pause automatique

**Contexte.** La base `vole-papillon-damour-db` est un `GP_S_Gen5_1` **serverless avec
`autoPauseDelayMinutes: 60`**. Ce fait, relevé tardivement (`revue.md` `R-01`), n'était
documenté que dans `infra/README.md` et n'avait été pris en compte nulle part dans ce
dossier.

**Le problème, et il joue dans les deux sens.**

Le serverless facture **0,5218 $ par vCore-heure**, plancher à 0,5 vCore, soit **0,26 $
de l'heure dès que la base est éveillée, quoi qu'elle fasse**. Elle ne s'endort qu'après
soixante minutes sans la moindre activité.

| Scénario | Heures actives/mois | Compute |
|---|---|---|
| La base dort vraiment (≈3 h/j) | 91 | ~24 $ |
| Trafic du site étalé dans la journée (≈12 h/j) | 365 | ~95 $ |
| **Avec le balayage de `06` §3 toutes les 5 min** | 730 | **~190 $** |

Le worker ne consomme presque rien lui-même — moins d'un euro, comme le dit `06` §8 —
mais **il empêche la base de dormir 24 h/24**. Le coût réel du module livres n'était donc
pas celui qui était chiffré.

Et laisser la base s'endormir n'est pas la sortie : la reprise prend des dizaines de
secondes, payées par le **premier scan d'une session de tri**, c'est-à-dire par `ENF-01`
— l'exigence sur laquelle le bénévole juge l'outil en trois secondes. Le profil d'usage
en dents de scie décrit en `DT-07` est précisément le pire cas pour une pause
automatique.

**Décision.** Quitter le serverless pour le palier **`S1` (Standard, 20 DTU, 250 Go)**,
à tarif fixe et sans pause. Un paramètre dans `main.dev.bicepparam`, plus le type
correspondant dans le module `SqlServer`. La montée en gamme se fait en ligne.

**Motivation.**

*Le tarif fixe est en baisse, pas en hausse.* `S1` coûte de l'ordre de **30 $ par mois**,
contre une facture serverless qui est déjà probablement au-dessus aujourd'hui dès que le
site public reçoit des visites étalées dans la journée. Ce n'est pas une dépense imputable
à la bourse aux livres : c'est une correction qui se justifie seule, et qui lui profite.

*Elle débloque le worker.* La cadence de cinq minutes de `RG-44` et `RG-43` cesse d'avoir
un coût. Le dimensionnement du balayage redevient une question de fraîcheur métier, pas
de facture.

*Elle supprime le démarrage à froid.* `ENF-01` et `ENF-08` ne dépendent plus de l'heure
de la dernière visite.

**Alternatives écartées.**

| Écartée | Motif |
|---|---|
| **Serverless, pause désactivée** | ~190 $/mois pour le même service que `S1` à 30 $. Le pire rapport du tableau |
| **Serverless + balayage espacé** | ~95 à 130 $/mois **et** un démarrage à froid conservé : plus cher et dégradé |
| **vCore General Purpose provisionné** | Démarre à **2 vCore**, ~368 $/mois. Sans objet à cette échelle |
| **`S0` (10 DTU)** | ~15 $/mois, tentant, mais 10 DTU pour du rendu serveur plus une recherche plein texte plus les salves de scan est trop juste pour être choisi sans mesure |
| **PostgreSQL Flexible Server `B1ms`** | ~16 $/mois, soit **150 € d'économie par an**. En face : changer le fournisseur EF Core d'une application en service, régénérer toutes les migrations, remplacer `rowversion` — qui n'existe pas — par `xmin`, réécrire `DT-07` en `tsvector`, refaire le câblage Aspire, les workflows `db-import.yml` et `storage-migrate.yml`, et la procédure de sauvegarde. `ENF-24` tranche seul : 150 € par an n'achètent pas ce chantier pour une personne. Le seul argument sérieux en face — `pg_trgm` fermerait `QT-06` sans Azure AI Search — ne justifie pas à lui seul de migrer un système en service |

**Réserve — `QT-09`.** `Basic`, `S0` et `S1` stockent les fichiers de base sur du
**stockage Standard sur disque dur**, là où `S2` et au-delà sont sur SSD. Le jeu de
données est minuscule — moins de 100 Mo après cinq ans (`02` §7) — donc l'essentiel doit
tenir en mémoire tampon et la latence disque ne devrait mordre qu'au démarrage. « Devrait »
n'est pas « mesuré » : `QT-09` fait la mesure au palier 1, sur la requête de désengorgement
de `05` §5 et sur la recherche plein texte. `S2` (~74 $/mois) est à un paramètre de
distance, et c'est la sortie si la mesure est mauvaise.

**Ce qui reste vrai.** `DT-02` n'est pas touchée : tout reste dans SQL Server, et les
seuils qui rouvriraient le sujet d'un magasin séparé sont inchangés. Cette décision porte
sur le mode de facturation et le palier, pas sur le choix du moteur.

---

## `DT-12` — Azure Communication Services Email, sur un sous-domaine d'envoi dédié

**Contexte.** `07` §7 décrivait le contenu des alertes, leur regroupement et le traitement
des rebonds — sans jamais dire **qui envoie**. Le fournisseur n'avait pas été choisi
(`revue.md` `R-05`).

**Décision.** **Azure Communication Services Email**, avec un **sous-domaine d'envoi
dédié** — `mail.volepapillondamour.fr` — vérifié au préalable, en même temps que le
locataire d'identité.

**Motivation.**

*C'est le principe directeur n°3 appliqué.* Un fournisseur tiers, c'est un compte de plus,
une clé à faire tourner, un tableau de bord de plus, et une panne hors d'Azure. ACS est
une ressource déclarée dans le même Bicep, facturée sur la même note, jointe par
**identité managée — donc sans aucun secret à garder** —, et journalisée au même endroit
que le reste. Pour un système maintenu par une personne, un compte tiers survit rarement
à celui qui l'a créé (`ENF-24`).

*Le coût est nul en pratique.* 0,00025 $ par message et 0,00012 $ par mégaoctet. À
quelques dizaines de messages par semaine, la facture se compte en centimes.

*Les rebonds de `RG-31` sont natifs.* Les rapports de remise et de rebond arrivent par
Event Grid, ce qui donne la source de l'endpoint décrit en `03` §5.

**Le sous-domaine d'envoi n'est pas un détail.** ACS exige une correspondance SPF
**exacte**, avec `-all`, et ne tolère ni `~all` ni les enregistrements composés de
plusieurs mécanismes `include`. C'est incompatible avec le SPF de la messagerie existante
de l'association sur `volepapillondamour.fr`. La parade est un sous-domaine dédié à
l'envoi applicatif — et c'est de toute façon la bonne pratique : elle **isole la
réputation d'envoi de l'application de celle de la messagerie humaine**. Un rattrapage
d'alertes qui partirait de travers n'entacherait pas les e-mails que les bénévoles
s'envoient.

**Alternatives écartées.**

| Écartée | Motif |
|---|---|
| **Brevo, Mailjet, Resend** | Paliers gratuits largement suffisants au volume, installation en trente minutes. Écartés pour la seule raison ci-dessus : un compte tiers et une clé à porter, hors du périmètre Azure. **Conservés en repli** si la plomberie Event Grid des rebonds se révèle disproportionnée face à ce que demande vraiment `RG-31` |
| **Postmark** | ~15 $/mois de plancher, pour une délivrabilité dont ce volume n'a pas besoin |
| **SMTP de la messagerie de l'association** | Aucun rappel de rebond, donc `RG-31` inapplicable. Quotas d'envoi. Et un envoi groupé depuis une boîte humaine abîme la réputation du domaine principal — exactement ce que le sous-domaine évite |

**Conséquence sur l'ordonnancement.** La ressource et la vérification du domaine passent
au **préalable**, avec le locataire d'identité, et non au palier 3. Deux raisons : la
configuration DNS se fait une fois, en même temps que celle de `DT-13` ; et surtout la
**réputation d'envoi se construit sur des semaines**. Un domaine neuf qui émettrait d'un
coup son premier lot d'alertes groupées partirait en indésirables — `RG-28` et l'objectif
`O5` échoueraient en silence.

---

## `DT-13` — Catalogue sur `livres.volepapillondamour.fr`, URL en slug + ISBN

**Contexte.** `ENF-09` fait du référencement « le principal canal d'acquisition gratuit de
l'association ». Le catalogue est une Container App distincte, donc par défaut sur un
`*.azurecontainerapps.io`. Aucun document ne traitait le nom de domaine, le certificat ni
la forme des URL (`revue.md` `R-07`). Or **une URL indexée ne se change pas** : le jour où
le catalogue est référencé, la décision est prise pour de bon.

**Décision.**

1. Le catalogue est servi sur **`livres.volepapillondamour.fr`**, domaine personnalisé
   déclaré sur la Container App, avec son **certificat managé gratuit**.
2. Une fiche a pour URL **`/livres/{slug-titre-auteur}-{isbn13}`**.
3. Le regroupement en œuvres de `RG-46` porte une page canonique `/oeuvre/{workId}`, vers
   laquelle les éditions pointent — voir la réserve ci-dessous.

**Motivation.**

*Le sous-domaine coûte zéro et ne perd presque rien.* L'objection théorique — un moteur
traite un sous-domaine comme un site largement distinct — suppose qu'il y ait une autorité
à hériter. À l'échelle de l'association, il n'y en a quasiment pas. Ce qui amènera du
monde, ce sont des requêtes de longue traîne qui atterrissent **directement sur une
fiche**, et cela fonctionne à l'identique. Un lien bien placé depuis la navigation du site
principal fait le reste, et il est gratuit.

*Le chemin `volepapillondamour.fr/livres` aurait coûté plus cher que la base.* Deux
Container Apps derrière un seul nom d'hôte imposent un routeur en amont : Azure Front Door
Standard, **35 $ par mois de base** plus le trafic. C'est davantage que `DT-11` pour
l'ensemble des données. L'héberger dans le `Website` existant est exclu par la décision
fonctionnelle `01` §6.

*Le slug porte les mots-clés, l'ISBN porte la stabilité.* Un titre corrigé plus tard
(`RG-05`) change le slug ; l'ISBN reste, ce qui permet de résoudre l'ancienne URL et de
rediriger en permanent plutôt que de perdre la page.

**Alternatives écartées.**

| Écartée | Motif |
|---|---|
| **Chemin sur le domaine principal** | 35 $/mois d'Azure Front Door, ou une fusion avec le `Website` contraire à `01` §6 |
| **Domaine séparé** (`bourseauxlivres.fr`) | Repart de zéro en autorité, divise l'identité de l'association, et ajoute une location à renouveler |
| **`/livres/{isbn13}` seul** | Stable mais sans aucun mot-clé, là où l'URL est un des rares leviers gratuits de `ENF-09` |

**Quand rouvrir le chemin.** Le jour où un CDN se justifierait de toute façon. Un Front
Door devant le catalogue mettrait aussi en cache les pages publiques et **protégerait la
base du trafic des robots** — ce qui n'est pas rien au vu de `DT-11`. Aujourd'hui, 35 $
par mois pour cela ne se défend pas ; à trafic réel connu, la question sera légitime.

**Ce que cette décision n'épuise pas.** Le reste du référencement — sitemap dynamique pour
quinze mille fiches, URL canoniques entre éditions d'une même œuvre, `robots.txt`, données
structurées, et le traitement des fiches épuisées que `RG-26` maintient au catalogue —
reste à traiter en `05` §1. C'est le constat `R-09` de la revue, et il est lié : le choix
de canoniser vers l'œuvre est ce qui évite des milliers de pages à contenu mince.

**La configuration DNS n'est pas un obstacle.** L'association détient le domaine et en a
la main pleine et entière. Les enregistrements de `DT-12` et de `DT-13` se posent en une
fois : `CNAME` et `TXT asuid` pour le catalogue, `TXT` de propriété plus SPF et DKIM pour
le sous-domaine d'envoi, `DMARC`, et le `TXT` de vérification de la Search Console.

---

## `DT-14` — Une seule table de personnes, rapprochée par `oid`

**Contexte.** Le modèle comptait deux tables de personnes : `Users`, existante, portant
les comptes du back-office et l'attribution des gestes (`RG-41`) ; et `Members`, nouvelle,
portant les membres du site et leur liste de recherche. Elles étaient rapprochées de
l'annuaire par **deux revendications différentes** — `oid` pour l'une, `sub` pour l'autre
(`revue.md` `R-04`).

**Les deux défauts.**

*La clé ne rapproche rien.* Dans un locataire externe, **`sub` est appairé par
application** : le même compte présente un `sub` différent au catalogue et à l'application
de scan. Une ligne `Members` créée depuis le catalogue ne serait pas reconnue par un jeton
émis pour le scan. `oid` est stable à l'échelle du locataire ; c'est la seule des deux qui
peut servir de clé de rapprochement.

*La même personne aurait deux lignes.* `01` §3 des spécifications pose que la même personne
est souvent bénévole **et** membre inscrite. Deux tables, c'est deux e-mails à tenir à
jour, deux cycles de vie, et une suppression `ENF-12` qui n'en efface qu'une — la ligne
`Users` survivrait avec son adresse, ce qui n'est pas une suppression. C'est exactement le
défaut que `DT-10` reproche aux rôles en base : deux annuaires divergent au premier compte
désactivé d'un côté sans l'être de l'autre.

**Décision.** **Une seule table de personnes**, la table `Users` existante, rapprochée de
l'annuaire par **`ExternalId` = `oid`** et par lui seul. La table `Members` est abandonnée.

**Ce qui rend la décision peu coûteuse aujourd'hui.** `User` ne porte que `Email`,
`Password`, `Salt`, `Name` et `Role` ; **trois fichiers seulement le référencent** ;
`Order` ne pointe pas dessus ; et il ne contient qu'une poignée de comptes
administrateurs, dont `DT-10` supprime déjà les colonnes de secret. Le même changement,
fait après avoir inscrit des bénévoles et des membres, porterait sur tout le monde. C'est
le raisonnement qui a déjà fait remonter le socle d'identité en premier.

**Ce que la table ne gagne pas.** `User` gagne `ExternalId`, `CreatedAt`, `LastSeenAt` et
`AnonymizedAt`, et perd ses trois colonnes de secret. **Elle ne gagne pas la facette
« membre »** : le statut d'alerte et le compteur de rebonds de `RG-31` vivent sur
l'agrégat `Watchlist`, pas sur l'identité.

C'est le point de conception qui n'était évident ni dans un modèle ni dans l'autre. Le
statut d'alerte et le compteur de rebonds ne décrivent pas une personne, ils décrivent
l'usage qu'elle fait des alertes. Les loger sur `User` obligerait le domaine `Books` à
écrire dans l'agrégat d'identité à chaque rebond, et donnerait des colonnes vides à toute
personne qui ne s'est jamais inscrite à quoi que ce soit — c'est-à-dire à tous les
bénévoles. Sur `Watchlist`, la ligne **n'existe que pour qui se sert de la fonction**, et
le domaine `Books` reste maître de ses propres données.

Ce faisant, « membre inscrit » reste ce que `DT-10` en a fait : **un compte valide sans
aucun rôle**, éventuellement doté d'une liste de recherche. Aucun statut à écrire nulle
part à l'inscription.

**La suppression de `ENF-12`, et sa nuance.** Une personne qui demande l'effacement n'est
pas toujours effaçable en une ligne : `RG-41` exige que tout mouvement porte l'identité du
bénévole qui l'a produit, et `ENF-12` conserve explicitement les mouvements de vente,
« qui ne contiennent aucune donnée personnelle ». Deux cas, donc :

| Cas | Traitement |
|---|---|
| Aucun mouvement ne pointe vers la personne — le cas d'un membre du public | **Suppression de la ligne**, cascade sur la liste et l'historique d'alertes, suppression du compte dans l'annuaire |
| Des mouvements y pointent — une bénévole | **Anonymisation** : `Email`, `Name` et `ExternalId` effacés, `AnonymizedAt` horodaté, cascade identique, suppression du compte dans l'annuaire. Les mouvements continuent de pointer vers une ligne qui n'identifie plus personne |

`ExternalId` est donc **nullable**, avec un index unique **filtré** sur les valeurs non
nulles — sans quoi la deuxième anonymisation entrerait en collision avec la première.

**Alternatives écartées.**

| Écartée | Motif |
|---|---|
| **Deux tables rapprochées par `oid`** | Corrige la clé, pas le doublon. Une bénévole également membre garde deux lignes, deux adresses à synchroniser, et une suppression à tenir des deux côtés |
| **Renommer `User` en `Person`** | Plus juste sémantiquement, et le coût est faible — trois fichiers. Écarté quand même : le renommage traverse le dépôt existant, l'agrégat, son identifiant fortement typé, son dépôt, sa configuration EF et une migration de renommage de table, pour un gain de vocabulaire. `ENF-24` |
| **Garder `Members` pour le seul domaine `Books`** | C'était l'intention d'origine — préserver la frontière du domaine. Mais l'identité n'est pas une donnée du domaine `Books` : `RG-41` la référence déjà depuis les mouvements. La frontière se tient autrement, en laissant la facette « membre » sur `Watchlist` |

**Ce que cette décision ne règle pas.** La suppression du compte **dans le locataire**
reste à concevoir : elle suppose un appel Microsoft Graph applicatif, donc un
enregistrement d'application, un secret, et une exposition à l'authentification M2M que
`QT-04` déclarait nulle. C'est le constat `R-06` de la revue, et il reste ouvert. `DT-14`
en fixe seulement la moitié qui nous appartient — la nôtre.

---

## `DT-15` — Socle d'exécution unifié : .NET 10 partout, Aspire à jour, versions centralisées

**Contexte.** Le dépôt part avec un socle presque homogène, et deux dérives déjà
installées.

| Constat | État relevé |
|---|---|
| Cibles | **Neuf projets en `net10.0`**, `MauiCashApp` seul en `net9.0-android;net9.0-ios;net9.0-maccatalyst` (+ `net9.0-windows`) |
| Aspire | **13.3.0** pour `Aspire.Hosting.AppHost`, `Azure.Storage` et `SqlServer` — mais **`Aspire.Hosting.NodeJs` en 9.5.2**, dans le même AppHost |
| Versions | Ni `Directory.Packages.props`, ni `Directory.Build.props`, ni `global.json`. `Microsoft.Extensions.*` cohabite en **10.0.7, 9.0.8 et 9.0.5** |

**Décision.** Quatre points, tous à traiter **avant** d'ajouter le projet worker.

1. **`net10.0` partout**, `MauiCashApp` comprise.
2. **Aspire à la dernière version** — 13.4.6 au moment d'écrire —, `Aspire.Hosting.NodeJs`
   aligné sur les autres.
3. **Gestion centralisée des paquets** par un `Directory.Packages.props` à la racine de la
   solution.
4. **`global.json`** épinglant la version du SDK.

**Motivation.**

*Le moment est le bon, et il ne se représentera pas.* Le module livres ajoute **deux
projets** — le worker et ses tests — et trois applications. Unifier un socle à neuf projets
coûte moins qu'à douze, et le faire avant d'écrire du code neuf évite d'écrire ce code
contre une version qu'on va changer.

*La MAUI se met à jour en même temps que son authentification.* `DT-10` impose de toute
façon d'y remplacer le jeton maison par MSAL.NET, donc de reconstruire et de
**redistribuer l'application sur les appareils de caisse** — le seul composant du système
qui ne se met pas à jour par un déploiement. Faire les deux dans la même livraison, c'est
une redistribution au lieu de deux. Séparées, ce sont deux passages sur chaque appareil,
dont le second sera oublié.

*La gestion centralisée n'est pas du confort.* Trois versions de `Microsoft.Extensions.*`
dans une même solution produisent des avertissements de rétrogradation aujourd'hui et des
résolutions surprenantes demain. Avec l'API et le worker qui **doivent être construits
depuis le même commit** ([`06`](06-traitements-differes.md) §2), un écart de version entre
les deux hôtes sur une bibliothèque partagée est précisément le genre de panne qu'on
diagnostique mal.

*`global.json` répond à une contrainte d'exploitation réelle.* Le développement se fait
**sur plusieurs machines successivement**, et la construction en intégration continue sur
une troisième. Sans épinglage, ces trois environnements peuvent compiler avec des SDK
différents — et le jour où l'une produit une erreur que les autres n'ont pas, on cherche
au mauvais endroit.

*Aspire 13.2 a apporté ce dont ce projet a besoin ensuite.* Sortie structurée en ligne de
commande, mode détaché, blocage sur les contrôles de santé et **diffusion de la
télémétrie** — c'est directement le socle du montage local décrit en
[`11-observabilite.md`](11-observabilite.md) §7.

**Ce que cela ne décide pas.** Que l'intégration Azure Functions d'Aspire existe et
fonctionne à cette version pour un worker isolé `.NET 10` reste **à vérifier** — `DT-04`
s'en prévaut sans que le paquet soit référencé aujourd'hui. C'est le constat `R-27` de
[`revue.md`](revue.md), et cette décision ne le referme pas : elle en fait seulement le
premier essai à tenter, sur un socle propre.

**Ordre.** Ces quatre points forment le premier lot du palier 1, avant la migration de
données et avant le worker. Ils ne produisent aucune fonctionnalité, ce qui est
exactement pourquoi ils ne se feront jamais si on ne les ordonnance pas.

---

## `DT-16` — L'observabilité s'écrit avec la fonctionnalité, jamais après

**Contexte.** Le dossier mentionne l'observabilité en un seul endroit —
[`06`](06-traitements-differes.md) §9, trois mesures à exposer — et rien n'en dit la forme,
le coût, ni qui regarde. L'état du dépôt est plus net encore : **`ILogger` n'apparaît que
dans un seul fichier de production** de tout le backend. Il n'y a pas de pratique à faire
évoluer, il y a tout à poser.

Or ce système a une propriété qui rend l'instrumentation non négociable : **ses pannes
sont silencieuses**. Une alerte qui ne part pas, une annonce qui ne bascule pas, un
appareil dont la file ne se vide pas — rien ne lève d'exception, rien ne renvoie 500,
personne ne se plaint. On l'apprend une bourse plus tard, par un membre qui n'a pas été
prévenu.

**Décision.** L'instrumentation fait partie de la définition de « terminé », au même titre
que les tests. Une tranche livrée sans ses traces, ses mesures et — si elle peut échouer en
silence — **son alerte** n'est pas livrée.

Concrètement, quatre règles, détaillées en [`11-observabilite.md`](11-observabilite.md) :

1. **OpenTelemetry**, exporté vers l'Application Insights déjà en place. Aucun second
   système d'observabilité, aucun agent tiers (`ENF-24`).
2. **Aucun échantillonnage en v1**, et le réglage est explicite plutôt que subi (§5).
3. **Les identifiants de corrélation traversent la frontière hors ligne** : le
   `ClientGestureId` de [`02`](02-modele-de-donnees.md) §2 est la clé qui relie un geste
   fait à 14 h sans réseau à sa transmission de 17 h 32 (§3).
4. **Un journal est une donnée personnelle.** `RG-42` et `ENF-10` s'appliquent à ce qu'on
   écrit dans Log Analytics exactement comme à ce qu'on renvoie dans une réponse HTTP (§6).

**Pourquoi une décision et pas une bonne intention.** Parce que l'instrumentation ajoutée
après coup est toujours la mauvaise : on instrumente ce qu'on se rappelle, pas ce qui a
cassé, et jamais le chemin qui n'a pas été emprunté. La mesure qui compte le plus dans ce
système — l'âge du plus vieux message échu non traité — ne s'observe pas depuis le code qui
fonctionne, mais depuis celui qui ne s'exécute pas. Elle ne peut être posée qu'en écrivant
le traitement.

**Ce que cela coûte.** Du temps sur chaque tranche, et de l'ingestion Log Analytics —
chiffrée et plafonnée en [`08`](08-infrastructure.md) §7. Pas de ressource nouvelle : les
Application Insights et le Log Analytics existent déjà.

**Ce que cela rapporte, et c'est mesurable.** `ENF-24` pose qu'une personne seule maintient
le système. Le temps de cette personne est la ressource rare du projet — pas le processeur,
pas les euros. Une panne silencieuse trouvée en trois requêtes plutôt qu'en une soirée de
reconstitution, c'est le meilleur rendement disponible.
