# 10 — Identité et droits

## 1. Un seul fournisseur d'identité

**Microsoft Entra External ID pour tout le monde** : membres du public, bénévoles,
administrateurs. C'est `DT-10`, et c'est un renversement — le plan précédent gardait
l'authentification maison du backend pour les bénévoles et n'utilisait Entra que pour le
public.

L'authentification maison est **supprimée**, pas mise de côté. Tant qu'elle existe, elle
est le chemin le plus court vers une compromission : c'est le seul endroit du système où
l'association détient des mots de passe.

| Public | Avant | Après |
|---|---|---|
| Membres du site | Entra External ID | Entra External ID |
| Bénévoles (scan, caisse) | JWT maison, mot de passe en base | Entra External ID |
| Administrateurs (back-office) | JWT maison, rôle `Admin` en base | Entra External ID |
| Caisse MAUI | JWT maison | Entra External ID |

## 2. Un locataire, trois publics

Un **locataire externe** unique, distinct du locataire de travail de l'association.

Ce n'est pas l'usage canonique : Microsoft présente le locataire externe comme destiné
aux clients d'une application, et le locataire de travail comme destiné au personnel. La
séparation stricte donnerait deux fournisseurs d'identité, deux configurations MSAL, deux
jeux de redirections et deux endroits où chercher quand quelqu'un ne peut plus se
connecter.

**Pour une association de bénévoles, ça ne vaut pas le prix.** La frontière entre « le
personnel » et « le public » n'existe pas ici : une bénévole trieuse est aussi une membre
inscrite. Un locataire, un annuaire, un endroit où regarder.

Ce que ce choix coûte, en clair : les comptes des administrateurs vivent dans un
locataire de type externe, dont les fonctions de protection des comptes à privilèges ne
sont pas celles d'un locataire de travail. Pour trois ou quatre administrateurs d'une
association, la contrepartie est acceptable ; elle ne le serait pas dans une entreprise.

**La distinction se fait par le flux, pas par le locataire :**

| Application | Création de compte |
|---|---|
| Catalogue public | **Inscription en libre-service**, au clic sur « me prévenir » (`04` §6 fonctionnel) |
| Scan, back-office, caisse | **Aucun flux d'inscription.** Les comptes sont créés par un administrateur |

Une application sans flux d'inscription rattaché n'offre aucun moyen de créer un compte
depuis son écran de connexion. C'est la barrière entre « n'importe qui peut s'inscrire »
et « les bénévoles sont désignés » — et elle tient à la configuration, pas à du code.
`QT-07` en vérifie le comportement exact.

## 3. Les enregistrements d'application

Cinq, tous créés par `infra/entra/Configure-EntraApps.ps1`.

| Enregistrement | Type | Ce qu'il porte |
|---|---|---|
| `vpd-api` | API | La portée `access_as_user` **et les trois rôles applicatifs** |
| `vpd-catalog` | SPA | Site public, seul rattaché au flux d'inscription |
| `vpd-scan` | SPA (PWA) | Application de tri et de caisse |
| `vpd-backoffice` | SPA | Administration |
| `vpd-caisse` | Client public | Application MAUI |

**Les rôles sont déclarés sur l'API, pas sur les clients.** Un seul endroit où lire la
liste des droits, un seul endroit où les attribuer, et la revendication `roles` arrive
dans le jeton d'accès que l'API valide. Déclarer les rôles côté client obligerait à les
tenir à jour en autant d'exemplaires qu'il y a de fronts.

## 4. Le modèle de droits : des rôles applicatifs, pas des groupes

Trois rôles, un par droit métier, et rien de plus.

| Rôle | Ouvre | Origine |
|---|---|---|
| `Tri` | Sessions de tri, décisions gardé/écarté | `RG-40` |
| `Caisse` | Mode vente, scan de sortie | `RG-40` |
| `Administration` | Back-office, zone d'administration du site | `ENF-18` |

**Le membre du public n'a aucun rôle.** C'est la décision la plus structurante de ce
chapitre. « Membre inscrit » est l'absence de rôle : un compte valide, sans rien de plus.
L'alternative — un rôle `Membre` attribué à chaque inscription — obligerait à écrire dans
l'annuaire à chaque création de compte en libre-service, donc à brancher une extension
d'authentification personnalisée sur le flux d'inscription, à la surveiller, et à la
rejouer quand elle échoue. Pour un droit qui ne dit rien de plus que « le jeton est
valide ».

L'API distingue donc trois niveaux, du plus large au plus étroit : anonyme (le catalogue
en lecture), authentifié sans rôle (ma liste de recherche), authentifié avec rôle (tri,
caisse, administration).

### Pourquoi pas des groupes de sécurité

C'est la solution réflexe, et elle est moins bonne **ici** :

*Le jeton transporte des GUID.* Un groupe arrive dans la revendication `groups` sous
forme d'identifiant d'objet. L'API devrait tenir une table de correspondance GUID → droit
et la maintenir en parallèle de l'annuaire. Un rôle applicatif arrive sous forme de
chaîne lisible : `roles: ["Caisse"]`.

*L'attribution d'un rôle applicatif à un groupe n'est pas gratuite.* Elle relève des
paliers payants dans un locataire de travail, et son comportement dans un locataire
externe fait partie de ce qui reste à vérifier. Attribuer directement aux comptes marche
partout, sans licence.

*Le volume ne le justifie pas.* Quelques administrateurs, quelques dizaines de bénévoles.
Les groupes payent quand on gère des centaines de personnes par lot ; ils coûtent une
indirection quand on en gère trente.

**Quand rouvrir.** Si l'association dépassait la centaine de bénévoles, ou si les droits
devaient suivre une structure existante (une section, une antenne), les groupes
redeviendraient le bon outil. Le passage se ferait sans toucher au code : l'API lit
`roles`, et c'est l'attribution en amont qui changerait de forme.

### Pourquoi pas des rôles en base

C'est ce que fait le système actuel : une colonne `Role` sur la table des utilisateurs,
lue à la génération du jeton. Le garder signifierait deux annuaires — Entra pour
l'identité, SQL pour les droits — qui divergeraient au premier compte désactivé d'un côté
sans l'être de l'autre.

## 5. Ce que devient l'utilisateur en base

Le domaine garde **une seule ligne par personne connue** — `DT-14` —, et **elle ne porte
plus aucun secret**.

| Champ | Avant | Après |
|---|---|---|
| `Email` | Source de vérité | Copie d'affichage, rafraîchie à la connexion |
| `Password`, `Salt` | Empreinte du mot de passe | **Supprimés** |
| `Role` | Source de vérité des droits | **Supprimé** — le jeton fait foi |
| `ExternalId` | — | **Nouveau.** Identifiant d'objet Entra (`oid`), clé de rapprochement |
| `CreatedAt`, `LastSeenAt` | — | **Nouveaux.** `ENF-13` s'appuie sur le second |
| `AnonymizedAt` | — | **Nouveau.** `ENF-12`, voir [`02`](02-modele-de-donnees.md) §2 |

Cette ligne reste nécessaire : `RG-41` impose que tout mouvement porte l'identité du
bénévole qui l'a produit, et une clé étrangère vers un annuaire externe n'existe pas. Le
`VolunteerId` des mouvements continue donc de pointer vers la table locale, elle-même
rattachée au compte Entra par `ExternalId`.

**Une seule table, et `oid` comme seule clé.** Le plan initial ajoutait une table
`Members` distincte pour le public, rapprochée par la revendication `sub`. `DT-14` l'a
écartée pour deux raisons. D'abord parce que **`sub` est appairé par application** dans un
locataire externe : le même compte présente un `sub` différent au catalogue et à
l'application de scan, donc cette clé ne rapproche rien — un défaut invisible tant qu'on
ne teste qu'avec une seule application. Ensuite parce que deux tables auraient donné deux
lignes à la même personne, alors que `01` §3 des spécifications pose qu'une bénévole
trieuse est souvent aussi une membre inscrite. C'est exactement ce que ce chapitre reproche
aux rôles en base : deux annuaires qui divergent.

**Ce que la ligne ne porte pas.** Le statut d'alerte et le compteur de rebonds de `RG-31`
vivent sur l'agrégat `Watchlist`, pas ici. Ils décrivent l'usage qu'une personne fait des
alertes, pas son identité — et la ligne `Watchlist` n'existe que pour qui se sert de la
fonction, ce qui évite des colonnes vides sur tous les bénévoles.

**Le rapprochement se fait à la première connexion**, pas par une synchronisation : au
premier appel authentifié, si aucune ligne ne porte cet `oid`, l'API en crée une avec le
nom et l'e-mail du jeton. Pas de tâche de fond, pas de dérive possible.

## 6. Ce qui est supprimé

L'inventaire, parce qu'une suppression à moitié faite est pire que pas de suppression.

| Couche | À retirer |
|---|---|
| `Domain` | `User.Password`, `User.Salt`, `User.Role` |
| `Application` | `Authentication/Commands/Register`, `Authentication/Queries/Login`, `IHashPassword`, `IJwtGenerator`, `AuthenticationResult` |
| `Infrastructure` | `HashPassword`, `JwtGenerator`, `JwtSettings`, la configuration `AddJwtBearer` symétrique |
| `Api` | `AuthenticationController` (`/auth/register`, `/auth/login`), la politique de limitation `Login` |
| `Contracts` | `LoginRequest`, `RegisterRequest`, `AuthenticationResponse` |
| `BackOffice` | `authentication.service.ts`, `authentication.facade.service.ts`, `authentication.guard.ts`, `login.component`, `button-login`, les dépendances `@auth0/angular-jwt` et `ngx-cookie-service` |
| `MauiCashApp` | Le porteur de jeton maison dans `IVpdApi` |
| Base | Migration de suppression des colonnes `Password`, `Salt`, `Role` ; ajout de `ExternalId`, `CreatedAt`, `LastSeenAt`, `AnonymizedAt` (`DT-14`) |
| Key Vault | **Le secret de signature JWT.** Plus rien ne le lit |

Ce dernier point est le meilleur indicateur que la migration est terminée : tant que la
clé de signature sert à quelque chose, c'est qu'un chemin d'authentification maison est
resté ouvert.

### Les comptes existants

Le `BackOffice` est en service : il y a des comptes administrateurs en base. Ils ne se
migrent pas — on ne transfère pas une empreinte de mot de passe vers un fournisseur
d'identité, et c'est heureux. **Ils se recréent** dans le locataire, à la main, une fois,
et reçoivent le rôle `Administration`. Ils sont une poignée ; c'est précisément
l'argument pour le faire maintenant plutôt qu'après avoir inscrit des bénévoles et des
membres.

La ligne locale correspondante n'est pas supprimée pour autant : elle perd ses colonnes
de secret et gagne son `ExternalId` au premier appel authentifié, ce qui préserve
l'historique des gestes déjà attribués.

## 7. Côté API

`Microsoft.Identity.Web`, validation du jeton émis par le locataire externe. Deux
vérifications non négociables : l'**audience** doit être l'identifiant de l'application
API, et l'**autorité** doit être celle du locataire externe (`*.ciamlogin.com`).

Les politiques d'autorisation deviennent la traduction directe du tableau du §4 :

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Tri",            p => p.RequireRole("Tri"))
    .AddPolicy("Caisse",         p => p.RequireRole("Caisse"))
    .AddPolicy("Administration", p => p.RequireRole("Administration"));
```

`RequireRole` lit la revendication `roles` du jeton. Aucun appel à l'annuaire, aucune
lecture en base : l'autorisation est locale au processus, ce qui la rend utilisable dans
les traitements différés comme dans les contrôleurs.

**L'acteur système reste explicite.** Le worker n'a pas d'utilisateur connecté ; c'est
déjà une des trois contraintes de conception de
[`06-traitements-differes.md`](06-traitements-differes.md) §2, et elle ne change pas.

## 8. Côté fronts

MSAL, dans sa déclinaison par plateforme : `@azure/msal-angular` pour le catalogue, le
scan et le back-office ; MSAL.NET pour la caisse MAUI.

Le site catalogue mérite une précision : ses pages indexables sont rendues côté serveur
et **restent anonymes** (`05` §2). L'authentification est purement cliente et ne concerne
que « mon compte » et la liste de recherche. Un rendu serveur authentifié imposerait de
transporter la session jusqu'au serveur de rendu, pour des pages qui n'ont aucune raison
d'être indexées.

## 9. Le point dur : la durée de vie des jetons face au hors ligne

C'est le vrai risque de ce chapitre, et il vaut mieux le regarder maintenant.

`ENF-17` exige une session longue sur l'appareil : « la reconnexion à chaque session de
tri est exclue ». Or un jeton d'accès vit une heure, et **le jeton de rafraîchissement
délivré à une application monopage est plafonné à vingt-quatre heures**, sans
renouvellement au-delà. Les sessions de tri, elles, sont espacées de plusieurs jours.

Trois éléments, dans cet ordre :

1. **Le maintien de session** (*keep me signed in*) rend le renouvellement silencieux :
   le navigateur repasse par le locataire, qui reconnaît son cookie de session et
   redonne un jeton sans rien demander. La bénévole ne voit rien. **Mais il faut le
   réseau.**
2. **Le mode hors ligne absorbe le reste.** L'application de scan est conçue pour
   travailler sans réseau et synchroniser ensuite (`04` §4). Un jeton expiré ne doit donc
   jamais bloquer le geste : il bloque la synchronisation, pas le scan.
3. **Le cas qui reste** : une bénévole qui arrive dans un local sans réseau, avec une
   session de plus de vingt-quatre heures, ne peut pas s'authentifier du tout. Si
   l'application exige une identité pour ouvrir une session de tri, elle est bloquée.

**Conséquence de conception, à tenir dès le palier 1** : l'identité du bénévole est lue
dans le stockage local de l'appareil, pas exigée du fournisseur d'identité à chaque
ouverture de session. Le jeton sert à synchroniser ; l'identité locale sert à attribuer
les gestes (`RG-41`). C'est ce qui rend `ENF-17` tenable sans compromis sur
l'authentification.

`QT-08` mesure ce comportement avant qu'on construise dessus.

## 10. Toute la configuration est scriptée

Aucune configuration du locataire ne se fait à la main dans le portail. Les scripts, leur
ordre d'exécution et les prérequis sont dans
[`infra/entra/README.md`](../../../infra/entra/README.md).

| Script | Rôle |
|---|---|
| `Configure-EntraApps.ps1` | Enregistrements, portée, rôles, consentements. Rejouable |
| `Set-VpdUserRole.ps1` | Attribue ou retire un rôle à un compte |
| `Get-VpdUserRoles.ps1` | Liste qui détient quel rôle |

Deux choses restent hors des scripts : la **création du locataire** lui-même, et le
**flux d'inscription en libre-service**, dont l'API Graph est encore en `beta` pour les
locataires externes. Les deux sont signalées comme telles, elles ne sont pas oubliées.

**Le retrait d'un rôle ne prend effet qu'au renouvellement du jeton.** Pour une révocation
immédiate — un compte compromis, un départ conflictuel — il faut désactiver le compte
dans le locataire, ce que `Set-VpdUserRole.ps1` ne fait pas et ne doit pas faire.
