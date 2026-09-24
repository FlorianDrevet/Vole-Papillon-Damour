# 10 — Compte membre : Ma sélection, carte de compte et Mes achats

> **Référence fonctionnelle — 22 septembre 2026**
>
> **Statut : code CS-1 à CS-26 implémenté dans la PR #224.** L'acceptation terrain des
> parcours de F-10 §14 reste à effectuer ; son résultat n'est pas présumé.

Ce document complète les spécifications de la bourse aux livres. Il part de trois
capacités déjà distinctes :

- le catalogue public, consultable sans compte ;
- la liste de recherche, qui sert à demander une alerte lorsqu'un livre arrive ;
- la caisse de la Scanette, qui enregistre la sortie d'un livre sans encaisser d'argent.

Il ajoute une capacité de préparation et de mémoire de visite, sans transformer le
catalogue en boutique en ligne :

1. un membre peut préparer une **sélection pour sa prochaine visite** ;
2. il peut présenter une **carte de compte** à la caisse pour rattacher
   facultativement un passage à son compte ;
3. il peut ensuite consulter **ses achats**.

Les décisions déjà prises restent applicables : pas de paiement en ligne, pas de
réservation, pas de prix ordinaire stocké, pas d'exemplaire physique individuel et
aucune obligation de créer un compte pour acheter.

## 1. Problème à résoudre

Le catalogue répond aujourd'hui à la question « qu'est-ce qui est disponible ? ».
Il ne permet pas encore à une personne de préparer sa visite ni de retrouver les
livres sortis lors d'une bourse précédente.

Le besoin se décompose en trois moments :

| Moment | Besoin du membre |
|---|---|
| Avant la bourse | Garder les livres qui l'intéressent pour les retrouver sur place |
| À la caisse | Dire rapidement que la sortie doit être associée à son compte |
| Après la bourse | Relire les livres achetés et retrouver les informations de l'édition |

Le compte ne doit pas devenir un prérequis pour consulter le catalogue. La valeur
principale doit rester accessible au visiteur anonyme.

## 2. Objectifs et limites

### 2.1 Objectifs

- Donner au membre un espace personnel compréhensible depuis son téléphone.
- Ne pas mélanger les livres recherchés avec les livres déjà repérés dans le catalogue.
- Réduire le temps d'identification à la caisse à un seul geste client.
- Conserver l'association d'une vente sans exposer l'adresse e-mail au bénévole.
- Faire fonctionner la vente même si l'appareil de caisse est temporairement hors ligne.
- Permettre au membre de retrouver un historique fiable, daté et compréhensible.
- Respecter la minimisation des données et la suppression du lien personnel lorsque
  le compte est supprimé.

### 2.2 Hors périmètre

Cette évolution ne fournit pas :

- de vente en ligne ou de paiement ;
- de panier de commande ;
- de réservation, de mise de côté ou de garantie de disponibilité ;
- de prix calculé ou de ticket de caisse financier ;
- de recherche d'un compte par e-mail comme parcours nominal ;
- d'association automatique d'une vente anonyme avec une personne ;
- de partage familial ou de profils multiples dans un même compte ;
- de notification automatique lorsqu'un livre de la sélection devient disponible ;
- de preuve juridique de propriété du livre.

Le mot **panier** est donc volontairement évité dans les écrans et les documents.
Le terme retenu est **Ma sélection**.

## 3. Vocabulaire fonctionnel

Le vocabulaire ci-dessous est obligatoire dans les écrans et les futurs contrats.

| Terme | Définition |
|---|---|
| **Liste de recherche** | Liste existante des œuvres ou éditions qu'un membre souhaite voir arriver. Elle peut déclencher une alerte. |
| **Ma sélection** | Liste personnelle de fiches déjà visibles dans le catalogue, préparée pour une visite. Elle ne déclenche aucune alerte et ne réserve rien. |
| **Carte de compte** | Écran personnel présentant un QR code ou un code de secours permettant de rattacher un passage en caisse au compte. |
| **Passage en caisse** | Ensemble des livres scannés entre l'ouverture d'une vente et l'action VALIDER. |
| **Vente associée** | Passage en caisse dont le membre a demandé le rattachement à son compte avant validation. |
| **Mes achats** | Historique personnel des lignes d'une vente associée. |
| **Association** | Opération qui relie un passage en caisse à un compte membre. Elle est facultative et réversible avant validation. |
| **Livre repéré** | Livre ajouté à Ma sélection depuis une fiche publique. Ce terme ne signifie ni réservation ni achat. |

### 3.1 Termes à ne pas employer

| À éviter | Pourquoi | Employer |
|---|---|---|
| Panier | Suggère une commande, un total ou une réservation | Ma sélection |
| Commande | Réservé à l'agrégat de la buvette | Passage en caisse ou vente |
| Carte de fidélité | Suggère des points ou une remise | Carte de compte |
| Réserver / mettre de côté | La sélection ne modifie pas le stock physique | Ajouter à Ma sélection |
| Achat garanti | Le livre peut être vendu avant l'arrivée du membre | Livre repéré / disponibilité indicative |
| Client recherché par e-mail | Expose une donnée et augmente le risque d'erreur | Compte reconnu par QR |

## 4. Acteurs et droits

| Acteur | Peut faire | Ne peut pas faire |
|---|---|---|
| Visiteur anonyme | Consulter le catalogue ; constituer une sélection locale sur son appareil | Synchroniser une sélection ou consulter des achats |
| Membre connecté | Synchroniser Ma sélection ; afficher sa carte ; consulter ses achats ; gérer sa liste de recherche | Modifier le registre de vente ou consulter un autre compte |
| Acheteur non membre | Acheter anonymement | Obtenir un historique sans avoir présenté un compte |
| Bénévole de caisse | Scanner une carte ; modifier l'association avant validation ; valider ou annuler selon les règles de caisse | Voir l'e-mail, la liste de recherche ou l'historique complet du membre |
| Administrateur | Corriger une association erronée avec traçabilité ; traiter les demandes RGPD | Modifier silencieusement le registre append-only |

Une même personne peut être membre, acheteur et bénévole à des moments différents.
Les droits de caisse restent ceux du rôle Caisse. Le fait qu'un bénévole possède
lui-même un compte membre ne lui donne aucun accès supplémentaire aux comptes des
clients.

## 5. Espace « Mon compte »

Le compte conserve la navigation existante et ajoute deux espaces. Sur mobile, les
sections peuvent être présentées comme des onglets ou des cartes empilées.

~~~text
Mon compte

  Ma sélection          Préparer ma prochaine visite
  Mes achats            Retrouver mes sorties associées
  Mes recherches        Être prévenu lorsqu'un livre arrive
  Ma carte              Présenter mon compte à la caisse
  Compte et données     Préférences, alertes et suppression
~~~

### 5.1 État sans contenu

Chaque espace doit avoir un état vide utile :

- **Ma sélection** : « Ajoutez un livre depuis le catalogue pour le retrouver à la
  bourse. »
- **Mes achats** : « Aucun achat n'est encore associé à ce compte. »
- **Ma carte** : la carte est disponible dès la création du compte, même si la
  personne n'a encore rien acheté.

L'état vide ne doit pas inciter à créer un deuxième compte ni faire croire qu'une
vente anonyme peut être retrouvée automatiquement.

### 5.2 Connexion et création du compte

Le compte continue d'utiliser Microsoft Entra External ID. Le catalogue reste
consultable sans connexion.

La connexion peut être proposée :

- lorsqu'une personne appuie sur Ajouter à Ma sélection depuis un navigateur
  anonyme et souhaite synchroniser la liste ;
- lorsqu'elle ouvre Ma carte ;
- lorsqu'elle demande un service déjà lié à la liste de recherche.

La création du compte ne doit pas apparaître comme une étape obligatoire avant de
parcourir le catalogue ou d'acheter à la bourse.

## 6. Ma sélection

### 6.1 Ajout depuis le catalogue

Une fiche publique affiche une action secondaire :

~~~text
[ Ajouter à Ma sélection ]
~~~

Après ajout, l'action devient :

~~~text
[ Dans Ma sélection ]       [ Retirer ]
~~~

L'action est disponible pour :

- une édition ordinaire actuellement disponible ;
- une édition annoncée pour une bourse future ;
- une édition épuisée ;
- une fiche rare publiée et encore visible, si la section rare est incluse dans le
  périmètre de la sélection.

Une référence qui n'a jamais été reçue par l'association appartient uniquement à la
liste de recherche. Elle ne peut pas être ajoutée à Ma sélection, car il n'existe
pas encore de fiche de visite dans le catalogue.

### 6.2 Granularité

Ma sélection suit la fiche regardée, donc une **édition précise** identifiée par son
ISBN. Cela évite qu'un membre pense avoir sélectionné une édition alors que le
catalogue lui en présenterait une autre.

Le souhait « n'importe quelle édition » reste le rôle de la liste de recherche.
Depuis une page d'œuvre, l'interface doit demander de choisir une édition avant
d'ajouter une sélection.

### 6.3 Information affichée

Chaque ligne de Ma sélection présente :

- couverture ou placeholder ;
- titre et auteur ;
- éditeur, année et format lorsqu'ils sont connus ;
- ISBN si utile pour retrouver rapidement la fiche ;
- disponibilité actuelle : disponible, annoncé, épuisé ou fiche vendue ;
- prochaine bourse et date lorsqu'elles sont connues ;
- date d'ajout à la sélection ;
- état personnel : à prendre, acheté, pas trouvé ou à revoir.

La date de fraîcheur de la disponibilité reste visible. Un livre sélectionné ne
devient jamais une promesse de présence le jour de la visite.

### 6.4 États personnels

L'état initial est À PRENDRE.

Le membre peut ensuite choisir :

| État | Signification |
|---|---|
| À PRENDRE | Le membre souhaite encore regarder ce livre |
| ACHETÉ | Le livre a été retrouvé dans une vente associée au compte |
| PAS TROUVÉ | Le membre l'a cherché sur place sans le trouver |
| À REVOIR | Le membre conserve la fiche pour une prochaine visite |

Le système ne doit pas déduire PAS TROUVÉ à partir d'une vente anonyme ou d'une
absence de consultation. Seul le membre peut poser cet état.

### 6.5 Visite et archivage

Après une bourse, les lignes restent consultables. Elles affichent la disponibilité
actuelle et peuvent être filtrées par :

- prochaine visite ;
- encore disponible ;
- acheté ;
- pas trouvé ;
- fiches devenues indisponibles.

Une sélection n'est pas supprimée automatiquement à la fin d'une bourse. Le membre
peut la retirer ou la conserver comme historique personnel.

### 6.6 Visiteur anonyme et fusion

Avant la connexion, la sélection est stockée uniquement dans le navigateur utilisé.
Elle n'est pas envoyée à l'association et n'est pas accessible depuis un autre
appareil.

Lors de la connexion :

1. la sélection locale est comparée à celle du compte ;
2. les doublons sont fusionnés par identifiant de fiche ;
3. les entrées locales absentes du compte sont proposées ;
4. aucune entrée distante n'est supprimée sans action explicite ;
5. l'utilisateur confirme la fusion si les deux listes contiennent des entrées.

Si la personne refuse la fusion, la sélection locale peut rester disponible sur cet
appareil mais ne doit pas être présentée comme synchronisée.

## 7. Carte de compte et identification en caisse

### 7.1 Parcours nominal

Depuis Mon compte > Ma carte, le membre affiche une carte utilisable à la caisse :

~~~text
┌──────────────────────────────────┐
│  Vole Papillon d'Amour           │
│  Ma carte de compte              │
│                                  │
│           [ QR CODE ]             │
│                                  │
│  Camille                          │
│  Code de secours : LUNE-42       │
│                                  │
│  Présentez cette carte avant     │
│  la validation de votre passage. │
└──────────────────────────────────┘
~~~

Le prénom ou le nom affiché sert uniquement à confirmer visuellement que le bon
compte a été reconnu. L'adresse e-mail n'apparaît ni sur la carte ni dans l'écran
de caisse.

Le QR code contient un jeton opaque, non interprétable par un lecteur humain. Il ne
doit pas contenir l'adresse e-mail, le nom en clair ou l'identifiant Entra brut.
Le jeton doit pouvoir être renouvelé ou révoqué en cas de téléphone perdu ou de
partage involontaire.

### 7.2 Alternatives

Les parcours sont classés ainsi :

1. **QR code dans le compte web** : parcours nominal ;
2. **code court de secours** affiché sous le QR : repli lorsque la caméra ne lit pas ;
3. **e-mail vérifié** : repli assisté, uniquement si la caisse est en ligne et après
   confirmation de l'utilisateur.

La recherche libre d'un compte par e-mail est déconseillée. Si elle est conservée
pour le support, elle doit être réservée à un écran explicitement intitulé
Rattacher un compte, protégée par le rôle de caisse et suivie d'une confirmation.

### 7.3 Apple Wallet et autres portefeuilles

Une carte Apple Wallet ou Google Wallet peut être ajoutée dans une version ultérieure.
Elle doit réutiliser le même principe de jeton opaque et le même parcours de
révocation que la carte web.

Le portefeuille mobile n'est pas un moyen de paiement dans cette fonctionnalité.
Il sert uniquement à afficher la carte de compte et son QR code. La première version
doit valider l'usage de la carte web avant d'engager la gestion de certificats,
de passes et de mises à jour de portefeuille.

## 8. Parcours de caisse

### 8.1 Écran initial

Le mode CAISSE conserve son fonctionnement actuel et ajoute un état d'association
facultatif :

~~~text
┌───────────────────────────────────────┐
│ CAISSE  ·  Bourse du 14 mars          │
├───────────────────────────────────────┤
│ Client : vente anonyme                │
│                                       │
│ [ Associer un compte ]                │
│                                       │
│ Livres scannés                        │
│  Le Petit Prince                      │
│  Astérix chez les Belges              │
│                                       │
│                    [ VALIDER ]        │
└───────────────────────────────────────┘
~~~

L'état Vente anonyme doit être explicite. L'absence de compte n'est jamais une
erreur et ne doit pas produire d'avertissement bloquant.

### 8.2 Association avant validation

Le parcours nominal est :

1. le bénévole appuie sur Associer un compte ;
2. le membre affiche sa carte ;
3. le bénévole scanne le QR ;
4. l'écran confirme le prénom ou le libellé public minimal du compte ;
5. les livres sont scannés comme aujourd'hui ;
6. le bénévole et le membre vérifient le compte affiché ;
7. VALIDER enregistre la sortie et son association.

Le QR n'est lu qu'une fois par passage. Il n'est pas demandé pour chaque livre.

Le bénévole peut changer de compte ou revenir à Vente anonyme tant que la vente
n'est pas validée. Un compte différent ne peut pas être appliqué silencieusement
à une vente déjà validée.

### 8.3 Confirmation et erreur de lecture

Après lecture du QR, la Scanette affiche une confirmation courte :

~~~text
Compte reconnu : Camille
Les livres de ce passage seront visibles dans Mes achats.
[ Continuer ]   [ Changer de compte ]
~~~

Si le QR est illisible ou expiré :

- le bénévole peut relancer la lecture ;
- le membre peut afficher le code de secours ;
- la vente peut être validée anonymement ;
- aucun livre ne doit être perdu ni bloqué à cause de l'identification.

La Scanette ne montre pas la liste de recherche, les achats précédents ou l'adresse
e-mail du membre.

### 8.4 Vente mixte et livre rare

Le rattachement concerne tout le passage validé, qu'il contienne :

- des livres ordinaires ;
- des livres rares ;
- plusieurs exemplaires d'une même édition ;
- un mélange de lignes ordinaires et rares.

Le rattachement ne change ni le prix lu pour un livre rare ni la règle selon laquelle
l'application n'enregistre pas le montant encaissé.

## 9. Mes achats

### 9.1 Présentation

L'historique est regroupé par passage ou par bourse, du plus récent au plus ancien :

~~~text
Mes achats

14 mars 2026 · Bourse aux livres
  3 livres
  Le Petit Prince · Gallimard · 1999
  Astérix chez les Belges · Dargaud · 1979
  L'Écume des jours · Le Livre de Poche · 2008

02 novembre 2025 · Bourse aux livres
  1 livre
  ...
~~~

Chaque ligne doit conserver au minimum :

- le titre et l'auteur tels qu'ils étaient connus au moment de la vente ;
- l'édition : ISBN, éditeur, année et format lorsque disponibles ;
- la quantité ;
- la date et l'heure du passage ;
- la bourse de rattachement lorsqu'elle existe ;
- l'état de la ligne : associée, corrigée ou annulée.

L'affichage peut proposer la couverture actuelle, mais l'absence ou le changement
ultérieur d'une couverture ne doit pas supprimer l'entrée historique.

### 9.2 Prix et montant

L'historique n'affiche pas de prix pour les livres ordinaires : le système ne les
connaît pas. Il n'affiche pas non plus un montant fictif pour un livre rare. Le prix
ferme d'un livre rare peut être consulté sur sa fiche publique, mais il ne constitue
pas un montant enregistré dans l'achat.

### 9.3 Synchronisation avec Ma sélection

Lorsqu'une vente associée contient une fiche présente dans Ma sélection :

1. la ligne de sélection est conservée ;
2. son état peut passer automatiquement à ACHETÉ ;
3. la date de l'achat est affichée ;
4. aucune autre ligne de sélection n'est supprimée ;
5. une correspondance par œuvre ne doit jamais inventer un achat d'une autre édition.

La correspondance nominale se fait donc par ISBN ou identifiant exact de fiche. Une
correspondance approximative par titre et auteur doit rester manuelle.

### 9.4 Historique absent

Un achat réalisé avant la mise en place de cette fonctionnalité, ou une vente
validée anonymement, n'apparaît pas dans Mes achats. Le site doit le dire clairement :

> « Seuls les passages associés à ce compte sont visibles ici. Les ventes anonymes
> ne peuvent pas être retrouvées automatiquement. »

## 10. Corrections, annulations et cas particuliers

| Situation | Comportement |
|---|---|
| Le membre change d'avis avant VALIDER | Le bénévole retire l'association ou choisit un autre compte. |
| Le QR est présenté après le premier scan mais avant VALIDER | L'association est possible pour tout le passage en cours. |
| Le membre oublie son QR | La vente reste anonyme ; aucun blocage. |
| La caméra ne lit pas le QR | Nouveau scan, code de secours, puis vente anonyme. |
| La connexion disparaît après lecture | Le jeton est conservé localement dans une association en attente ; la vente reste validable. |
| Le serveur ne retrouve jamais le jeton | La vente reste anonyme et un diagnostic non nominatif est conservé. Le système ne devine pas le compte. |
| La vente est annulée avant synchronisation | La ligne locale d'achat n'est jamais publiée dans le compte. |
| La vente est annulée après synchronisation | L'historique affiche Annulée ou retire la ligne selon la décision d'interface ; le mouvement inverse reste auditable. |
| Un compte est associé par erreur | Correction par un bénévole autorisé ou un administrateur, sans modification silencieuse du ledger. |
| Une personne achète des livres pour plusieurs personnes | Tout le passage est visible dans le compte présenté. Le partage familial n'est pas géré en v1. |
| Le compte est bloqué | La carte peut être refusée pour une nouvelle association ; les ventes déjà associées restent traitées selon la politique de conservation. |
| Le compte est supprimé | La sélection et l'historique personnel sont supprimés ; les mouvements nécessaires à l'audit du stock restent anonymisés. |

## 11. Hors-ligne et synchronisation

La caisse fonctionne déjà en mode dégradé. L'identification du compte ne doit pas
introduire une dépendance qui rendrait la vente impossible dans une zone mal couverte.

### 11.1 États visibles

La Scanette distingue au minimum :

- Compte non associé ;
- Compte associé ;
- Association en attente de synchronisation ;
- Vente synchronisée.

Le membre doit être informé si l'association est seulement en attente. La confirmation
ne doit pas prétendre que l'achat est déjà visible dans son compte tant que le serveur
n'a pas accepté la vente.

### 11.2 Données conservées localement

En cas de coupure, l'appareil conserve seulement :

- l'identifiant local de la vente ;
- le jeton opaque présenté par la carte ;
- les lignes de vente déjà prévues par le parcours de caisse ;
- l'état de synchronisation.

L'adresse e-mail, la liste de recherche et l'historique du membre ne doivent jamais
être préchargés dans la Scanette pour faire fonctionner ce parcours.

### 11.3 Rejeu

Le rejeu d'une vente doit rester idempotent. Une reconnexion ou un rafraîchissement
ne doit pas créer une deuxième vente ni une deuxième association. Une association
réussie est liée à l'identifiant idempotent du passage, pas au nombre de tentatives
de synchronisation.

## 12. Données personnelles et sécurité

Le rattachement d'un achat rend la date, le contenu et le lieu de passage des données
personnelles. Il doit être présenté comme un service facultatif, avec une information
compréhensible avant confirmation.

### 12.1 Information du membre

Le membre doit savoir :

- quelles données sont associées : compte, date, bourse, titres, éditions et quantités ;
- pourquoi elles le sont : afficher son historique et synchroniser sa sélection ;
- que la vente anonyme reste possible ;
- que le bénévole ne voit pas son historique ni son e-mail ;
- comment demander l'accès, la correction ou la suppression ;
- ce qui peut rester dans le registre anonymisé de l'association.

La page Vos données et le RGPD doit être mise à jour avant l'ouverture publique de
la fonctionnalité.

### 12.2 Minimisation

- Le QR ne contient aucune donnée personnelle en clair.
- La caisse n'affiche qu'un libellé minimal de confirmation.
- Les bénévoles ne peuvent pas rechercher les achats d'un membre.
- L'association ne doit pas utiliser l'historique pour une prospection commerciale
  sans information et base juridique distinctes.
- La sélection anonyme reste dans le navigateur et peut être effacée par l'utilisateur.

### 12.3 Suppression et anonymisation

La suppression du compte doit :

1. supprimer Ma sélection ;
2. supprimer l'accès à Mes achats ;
3. supprimer ou anonymiser le lien entre les achats et la personne ;
4. conserver uniquement les mouvements nécessaires aux quantités, statistiques ou
   obligations d'audit ;
5. supprimer ou révoquer les jetons de carte encore actifs.

Le membre ne doit pas être informé que « tout le ledger » est supprimé si les règles
de traçabilité imposent de conserver un mouvement anonymisé.

## 13. Règles métier proposées

Les règles suivantes prolongent les règles existantes de 06-regles-metier.md.
Elles sont proposées avec les identifiants RG-52 à RG-66 pour faciliter leur
intégration au catalogue global des règles.

### RG-52 — Les listes ont des finalités distinctes

La liste de recherche sert à suivre une œuvre ou une édition non disponible et peut
déclencher une alerte. Ma sélection sert à préparer une visite à partir d'une fiche
publique existante et ne déclenche aucune alerte.

### RG-53 — Ma sélection vise une fiche précise

Une entrée de Ma sélection désigne une édition par son ISBN ou une fiche rare par son
identifiant. Un ajout depuis une page d'œuvre impose de choisir l'édition concernée.

### RG-54 — Ma sélection ne réserve rien

L'ajout, la consultation ou le partage d'une sélection ne décrémente pas le stock,
ne met pas un livre de côté et ne bloque pas une vente à un autre visiteur.

### RG-55 — La sélection anonyme reste locale

Avant connexion, la sélection n'est pas une donnée membre. Elle est conservée sur
l'appareil et n'est synchronisée qu'après une connexion explicite.

### RG-56 — La fusion ne supprime pas silencieusement

La fusion d'une sélection locale et d'une sélection de compte déduplique les mêmes
fiches, mais ne supprime aucune entrée distante sans confirmation.

### RG-57 — L'association d'une vente est facultative

Une vente anonyme est toujours valide. L'absence de QR, de code ou de réseau ne bloque
pas l'enregistrement de la sortie.

### RG-58 — Une association couvre le passage validé

Un compte sélectionné avant VALIDER est appliqué à toutes les lignes du passage.
Le compte peut être changé ou retiré avant validation, mais pas deviné après coup.

### RG-59 — Le QR ne porte pas de donnée personnelle lisible

Le QR utilise un jeton opaque renouvelable ou révocable. Il ne contient pas d'e-mail,
de nom en clair ou d'identifiant Entra directement exploitable.

### RG-60 — Le hors-ligne ne crée pas d'achat fantôme

Une association hors ligne est une intention en attente jusqu'à l'acceptation serveur.
Le rejeu idempotent ne peut créer ni une vente ni une ligne d'historique en double.

### RG-61 — Seules les ventes associées sont visibles dans Mes achats

Une vente anonyme ou une vente réalisée avant l'activation de la fonctionnalité ne
peut pas être retrouvée automatiquement dans le compte.

### RG-62 — L'historique conserve le contexte de la vente

Une ligne d'achat conserve les informations nécessaires à sa lecture historique,
même si la fiche actuelle est masquée, fusionnée ou enrichie plus tard.

### RG-63 — Aucun prix fictif n'est affiché

Mes achats ne calcule ni prix ordinaire, ni total, ni montant payé à partir des lignes
de vente.

### RG-64 — Une annulation reste traçable

L'annulation d'une vente retire ou marque l'achat dans la vue membre selon le choix
d'interface, mais produit toujours la correction append-only prévue pour le stock.

### RG-65 — La sélection peut devenir acheté

Une sélection passe automatiquement à ACHETÉ uniquement lorsqu'une ligne associée
correspond exactement à sa fiche. Elle n'est jamais supprimée automatiquement.

### RG-66 — La suppression retire le lien personnel

La suppression du compte supprime Ma sélection et l'accès à Mes achats. Les mouvements
nécessaires à l'audit sont conservés sans identité exploitable.

## 14. Critères d'acceptation

### Parcours A — Visiteur qui prépare sa visite

1. Un visiteur ouvre une fiche disponible.
2. Il clique sur Ajouter à Ma sélection.
3. La fiche apparaît dans la sélection locale.
4. Il peut fermer puis rouvrir le navigateur et retrouver la fiche sur cet appareil.
5. Aucun compte n'est créé sans action explicite.

### Parcours B — Synchronisation d'une sélection

1. Le visiteur ajoute deux fiches anonymement.
2. Il se connecte à un compte possédant déjà une troisième fiche.
3. Le site affiche la proposition de fusion.
4. Après confirmation, les trois fiches apparaissent une seule fois.
5. La liste de recherche reste inchangée.

### Parcours C — Vente associée en ligne

1. Le bénévole ouvre CAISSE.
2. Le membre affiche sa carte.
3. Le bénévole scanne le QR une fois.
4. Le nom minimal de confirmation correspond au membre.
5. Deux livres sont scannés.
6. VALIDER est pressé.
7. Les deux livres apparaissent dans Mes achats, regroupés dans le même passage.

### Parcours D — Vente anonyme

1. Le membre ne présente pas de carte.
2. Le bénévole scanne les livres.
3. VALIDER fonctionne sans avertissement bloquant.
4. Rien n'apparaît dans Mes achats.

### Parcours E — Vente hors ligne

1. Le QR est lu alors que la Scanette n'a plus de réseau.
2. L'écran affiche Association en attente.
3. Le bénévole valide la vente.
4. La sortie reste conservée localement.
5. Au retour du réseau, la vente est rejouée une seule fois.
6. Le membre voit ensuite le passage dans Mes achats.

### Parcours F — Sélection et achat

1. Le membre a sélectionné une fiche ISBN précise.
2. Une vente associée contient ce même ISBN.
3. Après synchronisation, l'entrée devient ACHETÉ.
4. Une autre édition du même titre ne déclenche pas cette transition.

### Parcours G — Correction

1. Une vente associée est annulée selon la fenêtre de correction existante.
2. Le mouvement inverse est visible dans l'audit.
3. La ligne n'est plus présentée comme un achat actif.
4. La quantité du catalogue est rétablie selon les règles de caisse.

### Parcours H — Suppression

1. Le membre demande la suppression de son compte.
2. Ma sélection disparaît.
3. Mes achats n'est plus accessible.
4. Les données de compte et le jeton de carte sont supprimés ou révoqués.
5. Les mouvements de stock nécessaires à l'audit restent anonymisés.

## 15. Exigences non fonctionnelles spécifiques

| Domaine | Exigence |
|---|---|
| Mobile | Ma sélection, la carte et Mes achats sont utilisables à 390 px de largeur sans défilement horizontal. |
| Caisse | L'association ajoute un scan de carte par passage, pas un geste par livre. |
| Hors-ligne | La vente reste possible sans réseau et l'état d'attente est visible. |
| Performance | L'ouverture de la carte ne dépend pas du chargement de l'historique. |
| Historique | Mes achats est paginé ou chargé par bourse ; le navigateur ne reçoit pas toute l'histoire si elle devient volumineuse. |
| Accessibilité | Le QR possède un code de secours textuel ; toutes les actions ont un libellé accessible ; l'état d'association est annoncé. |
| Sécurité | Le QR ne révèle aucune identité lisible et un jeton perdu peut être révoqué. |
| Audit | Toute association, correction, annulation et résolution de jeton est journalisée sans journaliser inutilement l'e-mail dans la Scanette. |
| Compatibilité | Une version de Scanette ne comprenant pas l'association doit continuer à enregistrer une vente anonyme. |

## 16. Décisions retenues pour l'implémentation CS-1 à CS-26

Les choix suivants sont implémentés dans la PR #224 :

| Décision | Choix retenu |
|---|---|
| Nom public | Ma sélection, Ma carte / Carte de compte et Mes achats |
| Portée de la sélection | Édition précise ou fiche rare publiée et visible |
| Compte obligatoire à la caisse | Non ; l'association reste facultative et la vente anonyme reste valide |
| Identification | QR web signé par HMAC et code de secours |
| Portée d'une association | Tout le passage, avant validation |
| Fonctionnement hors ligne | Intention conservée puis rejouée de façon idempotente ; la vente reste possible |
| Association après validation | Pas de rattachement public par e-mail ; correction administrative tracée d'une erreur |
| Wallet | Hors de cette livraison |
| Alertes de sélection | Aucune ; les alertes restent attachées à la liste de recherche |
| Achat pour un proche | Visible dans le compte présenté ; pas de partage familial en v1 |
| Livres rares | Inclus dans Ma sélection |

Le comportement hors ligne et l'absence d'effet sur la vente anonyme restent à valider
en conditions réelles avec deux appareils, une coupure réseau au moment du QR, une
reprise de synchronisation, une vente annulée et une Scanette non mise à jour. La
fonctionnalité ne doit pas être déclarée acceptée avant ces vérifications.

## 17. Découpage de livraison recommandé

### Étape 1 — Sélection

- ajout et retrait depuis les fiches ;
- sélection locale anonyme ;
- connexion et fusion ;
- vue Ma sélection responsive ;
- états manuels et rafraîchissement de disponibilité.

### Étape 2 — Carte de compte

- écran Ma carte ;
- QR opaque ;
- code de secours ;
- rotation/révocation ;
- tests de lecture sur les appareils réellement utilisés en caisse.

### Étape 3 — Association de vente

- état facultatif dans la caisse ;
- association avant validation ;
- file hors ligne et rejeu idempotent ;
- correction avant et après synchronisation ;
- tests de non-régression de la vente anonyme.

### Étape 4 — Historique

- projection Mes achats ;
- regroupement par passage et par bourse ;
- snapshots de lecture historique ;
- transition automatique de Ma sélection vers ACHETÉ ;
- export et suppression RGPD.

### Étape 5 — Conforts ultérieurs

- carte Apple Wallet et Google Wallet ;
- rattachement après coup par lien e-mail vérifié ;
- listes nommées ou listes familiales ;
- rappel avant une bourse, si l'association souhaite un nouveau canal de notification.

## 18. Mesures de succès

Les premières bourses suivant la mise en service doivent mesurer :

- proportion de passages associés à un compte ;
- taux de lecture du QR au premier essai ;
- durée ajoutée au passage en caisse ;
- proportion d'associations restées en attente après la bourse ;
- nombre de doublons ou corrections d'association ;
- nombre de fiches de sélection marquées ACHETÉ ;
- nombre de sélections supprimées ou laissées sans suite ;
- tickets de support liés à un achat absent ou attribué au mauvais compte.

Un taux élevé d'association n'est pas un objectif suffisant : si le geste rallonge la
caisse ou crée des erreurs de compte, il faut privilégier la vente anonyme fiable.

## 19. Références

- [01 — Vision et périmètre](01-vision-et-perimetre.md)
- [02 — Glossaire et cycle de vie](02-glossaire-et-cycle-de-vie.md)
- [03 — Parcours bénévole Scanette](03-parcours-benevole-scan.md)
- [04 — Site public du catalogue](04-site-public.md)
- [06 — Règles métier](06-regles-metier.md)
- [07 — Exigences non fonctionnelles](07-exigences-non-fonctionnelles.md)
- [09 — RGPD, compte et droits](09-rgpd-compte-et-droits.md)
- [Maquette actuelle de Mon compte](maquettes/catalogue/MonCompte.dc.html)

