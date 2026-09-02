# 09 — Questions techniques ouvertes

**À lire avant d'écrire du code de production.** Plusieurs de ces points se règlent par
une mesure, pas par un avis, et certains peuvent invalider une décision déjà prise.

| # | Sujet | Statut | Quand |
|---|---|---|---|
| `QT-01` | Couverture réelle des sources bibliographiques | 🔴 **Bloquant** | Palier 0 |
| `QT-02` | Déclencheur planifié et mise à l'échelle à zéro | 🔴 **Bloquant** | Avant le palier 1 |
| `QT-03` | Lecture du code-barres au navigateur | 🟠 À mesurer | Palier 0 |
| `QT-04` | Dimensionnement Entra External ID | 🟢 **Coût tranché**, reste le parcours et `ENF-12` | Au préalable d'identité |
| `QT-05` | Unité de travail et `BaseRepository` | 🟢 Tranchée, à cadrer | Palier 1 |
| `QT-06` | Tolérance aux fautes de la recherche | 🟢 Différée | Après le palier 2 |
| `QT-07` | Inscription en libre-service limitée au catalogue | 🔴 **Bloquant** | Au préalable d'identité |
| `QT-08` | Durée de vie des jetons face au hors ligne | 🔴 **Bloquant** | Avant le palier 1 |
| `QT-09` | Tenue du palier `S1` sur disque dur | 🟠 À mesurer | Palier 1 |

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
[`06-traitements-differes.md`](06-traitements-differes.md) §7 : `minReplicas: 0` si le
réveil fonctionne ; `minReplicas: 1` sinon, au prix d'un conteneur permanent ; ou
temporisation par file Azure Queue Storage, dont le déclencheur réveille bien depuis
zéro.

**Pourquoi l'API à `minReplicas: 1` ne referme pas la question.** L'API tourne désormais
en permanence, mais le worker n'est pas l'API : il vit à zéro réplica et c'est lui qui
porte le minuteur. Dissoudre les traitements différés dans l'API refermerait la question
d'un coup — c'est précisément ce que proposait `DT-09`, écartée au profit de
l'isolation. La mesure reste donc à faire.

**Ce que le résultat décide.** Le coût mensuel du worker et, éventuellement, la forme du
délai de `RG-44`. Une issue à `minReplicas: 1` en ferait le **quatrième** conteneur
allumé en permanence — les trois applications existantes y sont déjà depuis `36b0e50` —
et rouvrirait légitimement `DT-09`.

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

> 🟢 **La question du coût est tranchée** : gratuit à notre échelle, voir ci-dessous.
> Restent deux points de parcours, à traiter **au préalable d'identité** et non plus au
> palier 3 — `DT-10` fait du locataire le premier élément livré.

`DT-10` retient Entra External ID pour le public, les bénévoles et les administrateurs.

### Ce qui est établi (documentation Microsoft, juin 2026)

**Facturation à l'utilisateur actif mensuel**, c'est-à-dire au nombre d'utilisateurs
uniques qui s'authentifient au moins une fois dans le mois civil. **Les 50 000 premiers
sont gratuits.**

Dans un locataire externe, **tout le monde compte** : la facturation s'applique quel que
soit le `UserType`, et la documentation cite explicitement les administrateurs porteurs
de rôles d'annuaire. Un bénévole qui se connecte pèse donc autant qu'un membre du public.

Avec quelques centaines de membres et quelques dizaines de bénévoles, on est **trois
ordres de grandeur sous le seuil**. Le volume ne coûtera rien. Ce qui peut coûter, ce
sont les compléments facturés à part :

| Complément | Facturation | Notre exposition |
|---|---|---|
| **Authentification SMS** | À la tentative de vérification, tarif par pays | **À éviter.** Rester sur le code à usage unique par e-mail, qui n'est pas facturé à l'acte |
| **Authentification M2M** | À la transaction (flux `client_credentials`) | Nulle aujourd'hui : le worker attaque SQL en direct (`06` §2), il n'appelle pas l'API. **À surveiller** si un composant devait un jour s'authentifier sans utilisateur |
| Go-Local (résidence des données) | Utilisateur actif | Sans objet : Australie et Japon uniquement |
| ID Governance, GSA | Utilisateur actif | Sans objet : locataires de travail uniquement |

**Les rôles applicatifs ne sont pas facturés.** Les déclarer sur l'enregistrement de
l'API et les attribuer aux comptes relève des fonctions d'annuaire incluses. Aucun
complément de la liste ci-dessus ne porte sur l'autorisation.

La restriction connue — « l'attribution par groupe exige un palier payant » — est
documentée pour les **locataires de travail** et les applications d'entreprise. Elle ne
se transpose pas telle quelle à un locataire externe, dont le modèle est l'utilisateur
actif et non le palier P1/P2. C'est une raison de plus de s'en tenir à l'attribution
directe aux comptes (`10` §4) : elle ne dépend d'aucune licence, dans aucune
configuration.

**Le locataire doit être rattaché à un abonnement Azure** pour être facturé et pour
accéder à ses fonctions. Le rattachement se fait à un abonnement détenu par un locataire
de travail — un locataire externe n'a pas de capacité de gestion d'abonnement. À faire au
préalable d'identité, pas après.

### Ce qui reste à vérifier

- Le parcours d'inscription peut-il rester aussi léger que le veut `04` §6 fonctionnel —
  proposé seulement au clic sur « me prévenir » ?
- La suppression exigée par `ENF-12` s'applique-t-elle bien des deux côtés, chez le
  fournisseur d'identité **et** dans notre base ?

Le second point est le plus facile à rater : effacer nos données en laissant l'identité
vivante n'est pas une suppression.

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

## `QT-07` — Inscription en libre-service limitée au catalogue

> 🔴 **Bloquant.** Un échec ici ouvre la création de comptes bénévoles à n'importe qui.

**Ce qu'il faut obtenir.** Le site catalogue propose l'inscription en libre-service. Les
applications de scan, de caisse et le back-office ne la proposent pas : leurs comptes
sont créés par un administrateur (`10` §2).

**Le conflit.** Dans un locataire externe, l'expérience de connexion d'une application
passe par un flux d'utilisateur, et la documentation associe ce flux à l'inscription
autant qu'à la connexion. Il faut vérifier qu'une application peut être **en connexion
seule** — soit sans flux rattaché, soit avec un flux dont l'inscription est désactivée —
et que l'écran obtenu n'offre alors aucun chemin de création de compte.

**Le test.** Configurer les deux cas sur un locataire d'essai, ouvrir l'écran de
connexion de `vpd-scan` en navigation privée, et chercher un lien d'inscription.

**Ce que le résultat décide.** Si aucune configuration ne donne la connexion seule, il
faut un autre garde-fou : restreindre l'attribution des rôles suffit à empêcher un
compte auto-créé de faire quoi que ce soit, mais l'annuaire se remplirait de comptes
sans usage, et la facturation à l'utilisateur actif s'en ressentirait.

**Point connexe.** L'API Graph de ces flux est en `beta` pour les locataires externes,
d'où l'exception assumée dans `infra/entra/` : cette partie n'est pas scriptée tant que
l'API n'est pas stable.

---

## `QT-08` — Durée de vie des jetons face au hors ligne

> 🔴 **Bloquant.** C'est `ENF-17` — « la reconnexion à chaque session de tri est exclue »
> — qui est en jeu.

**Le conflit.** Un jeton d'accès vit une heure. Le jeton de rafraîchissement délivré à
une application monopage est **plafonné à vingt-quatre heures**, sans renouvellement
au-delà. Les sessions de tri sont espacées de plusieurs jours. Sans précaution, chaque
session commence par une reconnexion, ce que `ENF-17` exclut explicitement.

**Le test, une journée d'attente.** Se connecter sur l'application de scan avec le
maintien de session activé, ne pas y toucher pendant quarante-huit heures, puis rouvrir
l'application **en mode avion**. Trois observations : la session est-elle rétablie
silencieusement une fois le réseau revenu ? Le geste de scan reste-t-il possible sans
réseau ? L'identité du bénévole est-elle toujours connue de l'appareil ?

**Ce que le résultat décide.** La forme du démarrage de session dans la PWA. La réponse
attendue est décrite en `10` §9 : l'identité vient du stockage local, le jeton ne sert
qu'à synchroniser. Si la mesure infirme cette possibilité, deux issues — accepter une
reconnexion par jour d'utilisation, ce qui contredit `ENF-17` et doit alors être arbitré
avec l'association ; ou reconsidérer `DT-08` pour l'application de scan, une application
native obtenant des sessions bien plus longues qu'une application monopage.

---

## `QT-09` — Tenue du palier `S1` sur disque dur

> 🟠 À mesurer au palier 1. `DT-11` est prise ; c'est son dimensionnement qui est en jeu,
> pas son principe.

**Le conflit.** `DT-11` retient `S1` (Standard, 20 DTU) pour sortir du serverless à pause
automatique. Or `Basic`, `S0` et `S1` stockent les fichiers de base sur du **stockage
Standard sur disque dur** ; `S2` et au-delà sont sur SSD. `ENF-08` demande une recherche
en moins d'une seconde, et `05` §5 abrite la requête la plus lourde du système.

**Pourquoi ce n'est probablement pas un problème.** Le jeu de données est minuscule —
moins de 100 Mo après cinq ans (`02` §7) —, donc l'essentiel doit résider en mémoire
tampon et la latence disque ne mordre qu'aux lectures froides. Et contrairement au
serverless, une base `S1` ne redémarre pas : il n'y a pas de réveil régulier qui viderait
ce cache.

**Le test.** Au palier 1, sur un catalogue chargé à quelques milliers de fiches, relever
trois temps : la recherche plein texte de `DT-07`, la requête de désengorgement de
`05` §5, et l'écriture d'un lot de scans en transaction (`DT-06`). Refaire la mesure une
fois le catalogue à quinze mille fiches.

**Ce que le résultat décide.** Le maintien en `S1` (~30 $/mois) ou la montée en `S2`
(~74 $/mois, SSD). C'est un paramètre dans `main.dev.bicepparam` et une montée en gamme
en ligne — la question est réversible, ce qui est la raison de commencer par le palier bas.

**Ce que le résultat ne décide pas.** Le retour au serverless, qui reste exclu par
`DT-11` quel que soit le résultat : son problème n'est pas la performance, c'est la
facturation à l'heure éveillée et le démarrage à froid.

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
| Fournisseur d'identité | `DT-10` — Entra External ID pour tous les publics. L'authentification maison est supprimée, pas mise de côté |
| Moteur de base de données | `DT-11` — SQL Server, palier fixe `S1`. PostgreSQL est instruit et écarté : même prix, migration d'un système en service |
| Fournisseur d'e-mail | `DT-12` — Azure Communication Services, sur un sous-domaine d'envoi dédié. Brevo et Mailjet restent le repli documenté |
| Adresse du catalogue | `DT-13` — `livres.volepapillondamour.fr`. Le chemin sur le domaine principal se rouvre le jour où un CDN se justifie |
| Une ou deux tables de personnes | `DT-14` — une seule, la table `Users` existante, rapprochée par `oid`. `sub` ne convient pas : il est appairé par application |
| Groupes ou rôles | `DT-10` — rôles applicatifs attribués aux comptes. Les groupes se rouvrent au-delà de la centaine de bénévoles |
