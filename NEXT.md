# NEXT — où en est la bourse aux livres

> **À lire en premier en arrivant sur une machine. À mettre à jour en dernier avant de la
> quitter**, même en pleine étape.
>
> Ce fichier porte **ce que git ne sait pas** : l'état d'Azure, du locataire, du DNS, les
> mesures en cours, les tests manuels passés. Les étapes, elles, sont dans
> [`docs/bourse-aux-livres/plan/`](docs/bourse-aux-livres/plan/README.md).

---

## En un coup d'œil

| | |
|---|---|
| **Lot en cours** | `L0-6` — santé de l'API et sondes posées, PR #6 en attente de validation |
| **Prochaine action** | `L0-7` — passer SQL en S1 après fusion de cette PR ([lot 0](docs/bourse-aux-livres/plan/00-socle-et-prealable.md)) |
| **Dernière machine** | *(à renseigner)* |
| **Dernière mise à jour** | 2026-09-02 |
| **Branche** | `feature/l0-4-front-reproductible` |

---

## Décisions prises

Les quatre arbitrages que le plan ne pouvait pas prendre seul sont tranchés. Rien n'attend
plus de réponse ; ceci est un rappel, le détail est dans les documents cités.

| Sujet | Décision |
|---|---|
| Caisse | **Android seul**, téléphones et tablettes. iOS, Mac Catalyst et Windows retirés du `.csproj`. APK signé, posé à la main sur chaque appareil (`L0-10`) |
| Suppression du compte dans le locataire | **Au préalable d'identité** (`L0-11`, étape 8), pendant qu'il n'y a encore personne à supprimer |
| Genres et classement | **Depuis les sources bibliographiques**, et le site n'indique **jamais** où se trouve un livre dans le local (`Q-07`) |
| Repli d'exploitation | **Aucun.** Une panne fait vendre sans enregistrer, rien n'est rattrapé. Le hors-ligne de la caisse devient la seule protection (`ENF-21`, `P1-10`) |

Reste à écrire, mais rien ne le bloque : les **chiffres cibles du palier 0** (`S0-1`, avant
la campagne) et le **choix du matériel de scan** (`Q-08`, après la campagne, s'il s'avère
nécessaire).

---

## Le mode de travail

**Pas de date, pas d'échéance.** On construit au fil de l'eau, on teste en même temps, et
on ne montre l'ensemble que lorsqu'il tient debout. Les paliers restent un **ordre de
construction** : ils disent quoi écrire avant quoi, pas quand livrer.

Les 🧪 des lots sont des tests à faire soi-même, seul. Trois choses y échappent et
attendront l'usage réel : le ressenti d'un bénévole sur le geste de scan, la discipline de
scan en caisse un jour de bourse, et la réputation du domaine d'envoi — d'où `L0-9`, très
en avance sur son besoin.

---

## Reprendre le travail

```bash
git pull
# puis lire ce fichier en entier avant de toucher à quoi que ce soit
```

**Prérequis sur une machine neuve** — à compléter au fil du lot 0 :

| Outil | Version | Posé ? |
|---|---|---|
| SDK .NET | `10.0.203` | Oui |
| Node | `24.15.0` | Oui |
| CLI Aspire | `13.5.3` | Oui |
| Azure CLI, connecté au bon abonnement | — | — |
| PowerShell 7 + modules Microsoft.Graph | pour `infra/entra/` | — |
| Docker | pour les images | — |

## En cours

`L0-6` est implémenté côté dépôt. L'API expose `GET /health` avec un contrôle de connexion à
la base dans Infrastructure ; les trois sondes API de `infra/parameters/main.dev.bicepparam`
ciblent `/health` sur le port `8080`. Les sondes Website et BackOffice restent désactivées,
car le plan ne spécifie pas de endpoint de santé pour ces applications. Aspire a confirmé
localement `https://localhost:7246/health` en `200 OK` avec `Healthy`, et le Bicep principal
ainsi que les paramètres compilent. Aucun déploiement Azure n'a été effectué.

`L0-5` est implémenté avec `.github/workflows/ci.yml`, déclenché sur `push` et `pull_request`.
Il restaure et compile la solution backend `.slnx`, exécute les trois projets de tests,
installe le workload MAUI Android et compile `net9.0-android`, puis installe et construit les
deux applications Angular avec la version de Node de `.nvmrc`. Localement, le backend et les
fronts passent ; la compilation MAUI reste bloquée sur cette machine par l'absence du SDK
Android natif (`XA5300`). La validation GitHub de la PR est en cours.

> Ce qui va ici : une étape commencée et non finie, avec **l'état exact** — quel fichier,
> quelle idée, ce qui reste. Écrire deux lignes ici coûte moins qu'une demi-heure de
> reconstitution.
>
> Cas particulier : `L0-11` se déploie **en trois passages** étalés sur plusieurs jours.
> Noter lequel est fait.

---

## En attente d'un délai externe

*Rien.*

> Ce qui va ici : ce qui avance sans vous et qu'il faut penser à relever.
>
> | Sujet | Lancé le | Relevable à partir du |
> |---|---|---|
> | `QT-08` — session de 48 h puis ouverture en mode avion (page jetable, `L0-12`) | | |
> | Propagation DNS | | |
> | Vérification du domaine d'envoi ACS | | |
> | Réputation du domaine d'envoi | *(des semaines — lancer tôt)* | |

---

## État hors dépôt

**La section qui justifie ce fichier.** Tout ce qui a été fait à la main, ou qui existe
dans Azure sans être déductible du dépôt.

### Azure

| Ressource | État | Depuis |
|---|---|---|
| Base SQL | `GP_S_Gen5_1` serverless, pause auto 60 min — **à passer en `S1` (`L0-7`)** | — |
| Sondes de santé | Paramètres API posés dans le dépôt (`/health`, port `8080`, `L0-6`) ; Azure non modifié | — |
| Container Apps | `api`, `website`, `backOffice` à `minReplicas: 1` | `36b0e50` |
| Locataire Entra External ID | **Pas créé** | — |
| ACS Email | **Pas créé** | — |
| Plafonds journaliers App Insights | **Non posés** | — |
| Règles d'alerte | **Aucune** | — |

### DNS — `volepapillondamour.fr`

Domaine détenu et administré par l'association, main pleine et entière.

| Enregistrement | Lot | Posé ? | Le |
|---|---|---|---|
| `TXT` propriété + SPF + DKIM sur `mail` | `L0-8` | Non | — |
| `DMARC` | `L0-8` | Non | — |
| `TXT` Search Console | `L0-8` | Non | — |
| `CNAME` + `TXT asuid` sur `livres` | **Palier 2** — le `CNAME` a besoin du FQDN de la Container App du catalogue, qui n'existe pas avant | Non | — |

### Entra

| Élément | État |
|---|---|
| Locataire | Non créé |
| Enregistrements d'application | Non exécutés (`Configure-EntraApps.ps1` existe, jamais lancé) |
| Comptes administrateurs recréés | Aucun |
| Appareils de caisse mis à jour | **Aucun** — voir `L0-10` et `L0-11`, ils ne se mettent pas à jour tout seuls |

### Appareils de caisse

**Liste à tenir, elle ne se déduit de rien** — c'est ce qu'on cherchera à chaque
livraison, et le jour où `/auth/login` disparaît, un appareil oublié est un appareil hors
service.

| Appareil | Modèle / Android | Version installée | Mise à jour le |
|---|---|---|---|
| *(à recenser en `L0-10`)* | | | |

Le **magasin de clés de signature** de l'APK vit hors du dépôt. Noter ici *où il est et qui
en a une copie* — jamais son mot de passe. Le perdre interdit toute mise à jour des
installations existantes.

### Secrets GitHub

*Inventaire à compléter au lot 0. Les noms seulement, jamais les valeurs.*

---

## Mesures faites

| # | Sujet | Résultat | Le |
|---|---|---|---|
| `QT-01` | Couverture des sources bibliographiques | — | — |
| `QT-02` | Déclencheur planifié à zéro réplica | — | — |
| `QT-03` | Lecture du code-barres au navigateur | — | — |
| `QT-04` | Dimensionnement Entra | Coût tranché : gratuit à notre échelle | doc |
| `QT-07` | Connexion seule, sans inscription | — | — |
| `QT-08` *(partie jeton, `L0-12`)* | Durée de vie des jetons hors ligne | — | — |
| `QT-08` *(partie geste, `P1-5`)* | Scan possible hors ligne après 48 h | — | — |
| `QT-09` | Tenue de `S1` sur disque dur | — | — |

**Chiffres cibles du palier 0** *(à écrire en `S0-1`, avant la campagne — pas après)* :

| Mesure | Cible |
|---|---|
| Taux de lecture au premier essai | — |
| Taux de métadonnées trouvées | — |
| Cadence tenable au bout de 200 livres | — |

---

## Tests manuels passés

| Test | Résultat | Le |
|---|---|---|
| *(aucun)* | | |

> Un test manuel non consigné sera refait. Noter au minimum : quoi, quand, et ce qui a été
> observé — pas seulement « OK ».

---

## Journal

Une ligne par session de travail. Le plus récent en haut.

| Date | Machine | Ce qui a avancé |
|---|---|---|
| 2026-09-02 | - | **L0-6 — points de santé et sondes.** Ajout de `GET /health` avec contrôle de connexion SQL, test de l'enregistrement du contrôle, sondes API readiness/liveness/startup sur `/health:8080` dans les paramètres dev, et validation locale Aspire (`200 OK`, `Healthy`). La solution backend restaure, compile et passe ses 70 tests ; les deux fichiers Bicep compilent. Aucun déploiement Azure ni test manuel de révision cassée n'a été effectué. |
| 2026-09-02 | - | **L0-5 — CI de compilation et tests.** Ajout de `.github/workflows/ci.yml` sur `push` et `pull_request` : SDK .NET `10.0.203`, solution backend `.slnx`, tests Domain/Application/Infrastructure, workload et build MAUI Android, puis `npm ci`/`npm run build` pour BackOffice et Website avec `.nvmrc`. Validation locale backend et fronts réussie ; le workflow GitHub n'a pas été lancé, et la caisse reste localement bloquée par l'absence du SDK Android natif. |
| 2026-09-02 | - | **L0-4 — socle front reproductible.** Déplacement de `link-shared-ui.mjs` dans `src/SharedUi/scripts`, ajout des hooks `prebuild`/`prestart` dans Website et BackOffice, et résolution du lien vers les `node_modules` de l'application appelante. Test Node ciblé : 3/3 ; `npm ci` puis `npm run build` réussissent dans BackOffice seul, et Website compile également. |
| 2026-09-02 | - | **Images Docker — runtime et restore.** Le Dockerfile API copie `Directory.Packages.props` avant le restore centralisé et épingle son SDK sur `10.0.203`, car le contexte Docker ne contient pas le `global.json` racine. Les images Website et BackOffice utilisent désormais Node `24.15.0`, aligné sur `.nvmrc` et leurs champs `engines`. |
| 2026-09-02 | - | **L0-3 — migrations au démarrage.** À la demande, reprise du mécanisme de `infra-pipeline-editor` : `ProjectDbContext` est migré par un hosted service Infrastructure avant que l'API soit prête, avec stratégie d'exécution EF et source de trace `DbMigrations`. Une base SQL temporaire neuve a reçu les 10 migrations existantes avant l'écoute HTTP ; avec la base Aspire, `/actuality/latest` et `/asso-events` répondent HTTP 200. La spécification technique prévoit encore une migration explicite en déploiement ; décision à réarbitrer avant une production multi-réplique. |
| 2026-09-02 | - | **L0-3 — correction du lancement frontend.** `AddJavaScriptApp` conserve la commande npm, avec `--` ajouté avant les arguments Angular. `aspire run` démarre SQL, stockage, API, Website et BackOffice ; les ports 4200 et 4201 répondent HTTP 200. |
| 2026-09-02 | - | **Format des solutions.** Conversion des solutions backend et MAUI de `.sln` vers `.slnx`, puis suppression des anciens fichiers. La restauration et la compilation backend passent avec `.slnx`. La restauration MAUI reste bloquée localement faute du workload `maui-android`. |
| 2026-09-02 | — | **L0-3 — mise à jour.** Alignement du SDK AppHost, des hébergements SQL/stockage/JavaScript et de la CLI Aspire en `13.5.3`, avec `AspireUseCliBundle=true` dans l'AppHost, après vérification de la disponibilité des packages. `dotnet restore` et `dotnet build` passent sur cette version ; le lancement manuel de l'AppHost reste à faire. |
| 2026-09-02 | — | **L0-3.** Aspire, AppHost SDK et hébergements SQL/stockage/JavaScript en `13.4.6`. Remplacement de l'intégration legacy `Aspire.Hosting.NodeJs`/`AddNpmApp` par `Aspire.Hosting.JavaScript`/`AddJavaScriptApp`, avec conservation du script `start` et des arguments Angular. CLI Aspire `13.4.6`. `dotnet restore` et `dotnet build` passent ; le lancement manuel de l'AppHost reste à faire. |
| 2026-09-02 | — | **Arbitrages.** Caisse Android seule, `R-06` ramené en `L0-11`, `Q-07` tranchée (genres depuis les sources, aucun emplacement affiché), `ENF-21` réécrit : aucun repli d'exploitation. Plan débarrassé de ses échéances, tests reformulés pour être faits seul. |
| 2026-09-02 | — | **Revue du plan.** Lot 0 renuméroté (`L0-1` à `L0-13`) : ajout de l'épinglage Node, de la reproductibilité de `SharedUi`, de la préparation de la distribution de la caisse, et découpage de `L0-11` en trois déploiements. DNS du catalogue renvoyé au palier 2. `QT-07`/`QT-08` dotées d'un support de mesure. Lot 2 : stratégie de test des fronts en `P1-2`, `R-13`/`R-14` en `P1-5`, `R-24`/`R-30` en `P1-8`, repli d'exploitation en `P1-10`. Convention de renvoi `F-nn`/`T-nn`. **Rien d'implémenté.** |
| 2026-09-02 | — | Revue de la doc technique (30 constats `R-nn`), décisions `DT-11` à `DT-16`, chapitre observabilité, plan d'exécution et ce fichier. **Rien d'implémenté.** |

---

## Rituel de sortie

Avant de quitter une machine, dans cet ordre :

1. Mettre à jour **En cours**, **État hors dépôt** et **Journal** ci-dessus.
2. Commiter, **y compris du travail incomplet**, sur une branche de travail.
3. Pousser.

**Une branche non poussée est du travail perdu** dès qu'on change de machine.

Un commit de ce fichier seul se nomme `chore(plan): ...` et ne mélange pas de code : un
conflit se résout alors en gardant les deux moitiés, sans réfléchir.
