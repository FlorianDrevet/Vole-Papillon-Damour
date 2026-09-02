# 08 — Infrastructure et déploiement

## 1. Ce qui existe

Relevé dans `infra/` : Container Apps et leur environnement, Container Registry,
Key Vault, SQL Server, Storage Account, Application Insights, Log Analytics, identités
managées et attributions de rôles. Le tout piloté par `main.bicep` et des modules par
ressource.

**Aucune Function, aucun Service Bus, aucune file, aucun locataire d'identité** à ce
jour. L'authentification est maison : clé de signature symétrique en Key Vault, mots de
passe en base.

**La base est un `GP_S_Gen5_1` serverless, avec pause automatique après soixante
minutes**, en France Centrale — la seule région où l'abonnement est autorisé à
provisionner de l'Azure SQL. Ce détail n'était relevé que dans `infra/README.md` et
change plus la facture du module livres que tout le reste de ce chapitre : voir `DT-11`
et §7.

Les applications étaient paramétrées avec `minReplicas: 0` et `maxReplicas: 2`. C'est ce
réglage qui a d'abord imposé un worker séparé (`DT-04`).

**Ce n'est plus le cas.** Le commit `36b0e50` — *update minimum replicas for container
apps to ensure availability* — fait passer **les trois applications existantes** à
`minReplicas: 1` dans `infra/parameters/main.dev.bicepparam` : `api`, `website` et
`backOffice`. Ce n'est donc pas un changement à prévoir, c'est un état de fait.

| Application | `minReplicas` | `maxReplicas` | Gabarit |
|---|---|---|---|
| `api` | **1** | 2 | 0,5 vCPU, 1 Gio |
| `website` | **1** | 2 | 0,5 vCPU, 1 Gio |
| `backOffice` | **1** | 2 | 0,25 vCPU, 0,5 Gio |

Le worker reste néanmoins séparé — isolation de la charge de fond, déclencheurs
planifiés déclaratifs, cycles de vie distincts. Le réexamen est en
[`01-decisions.md`](01-decisions.md), l'alternative écartée en `DT-09`.

## 2. Ce qui s'ajoute

**Deux ressources d'un type nouveau**, toutes deux créées au préalable :

| Ressource | Décision | Facturation |
|---|---|---|
| Locataire **Microsoft Entra External ID** | `DT-10` | Utilisateur actif mensuel, gratuit à notre échelle |
| **Azure Communication Services Email**, avec son sous-domaine d'envoi vérifié | `DT-12` | 0,00025 $ par message — quelques centimes par mois |

Tout le reste tient dans l'environnement Container Apps existant.

**Un changement sur une ressource existante**, et il n'est pas optionnel : la base SQL
quitte le serverless à pause automatique pour le palier fixe `S1` (`DT-11`). C'est un
paramètre de `main.dev.bicepparam` et le type correspondant dans le module `SqlServer`,
mais c'est le poste qui change le plus la facture — dans le bon sens. Voir §7.

**Un domaine personnalisé par application publique** (`DT-13`) :
`livres.volepapillondamour.fr` sur la Container App du catalogue, avec son certificat
managé — gratuit, renouvelé automatiquement. L'association détient le domaine et en a la
main, donc les enregistrements DNS de `DT-12` et `DT-13` se posent en une seule fois.

Trois Container Apps de plus :

| Application | Nature | Ingress | Réplicas |
|---|---|---|---|
| `scan` | PWA Angular | Externe | **1 → 2** |
| `catalog` | Angular SSR + administration | Externe | **1 → 2** |
| `worker` | `kind=functionapp` | Interne (requis pour la mise à l'échelle) | 0 → 1, **sous réserve de `QT-02`** |

Le module `ContainerApp` existant se réutilise tel quel. Pour le worker, le delta est
`kind: functionapp` sur `Microsoft.App/containerApps`.

L'API ne demande **aucun changement de paramétrage** : `containerAppApiScaling` est déjà
à `minReplicas: 1` depuis `36b0e50`.

**`scan` et `catalog` suivent la même convention**, à `minReplicas: 1`. C'est cohérent
avec `36b0e50` : tout ce qui sert des requêtes tourne en permanence. `catalog` rend du SSR
indexable, où un démarrage à froid se paie en référencement autant qu'en confort ; `scan`
est utilisée par salves, et une attente en début de session de tri se remarquerait
immédiatement — c'est précisément ce que `ENF-01` protège.

**Seul le worker reste à zéro**, parce que rien ne l'appelle : il se réveille sur
minuteur. C'est ce qui fait de `QT-02` une question qui n'a pas disparu.

**Ne pas confondre les deux voies** : celle par `Microsoft.Web/sites` avec
`managedEnvironmentId` est marquée *legacy* dans la documentation. C'est `Microsoft.App`
qu'il faut.

Points d'attention pour le worker :

- **Compte de stockage obligatoire** pour toute Function sur Container Apps — celui du
  projet convient.
- **Ingress requis** pour la mise à l'échelle événementielle, même sans endpoint public.
  Un ingress interne suffit.
- **Pas de slots de déploiement**, pas de clés de fonction générées depuis le portail.
  Utiliser Key Vault, déjà en place.
- **Révision unique** de préférence : le mode multi-révision impose un compte de
  stockage par révision pour éviter les conflits de déclencheurs.

## 3. Secrets et identité

**Un secret disparaît : la clé de signature JWT.** `DT-10` supprime l'authentification
maison, donc plus rien ne la lit. Tant qu'elle reste en Key Vault et qu'une application
sait s'en servir, la migration n'est pas finie — c'est le meilleur indicateur d'achèvement
du préalable d'identité (`10` §6).

Rien de nouveau : Key Vault et identités managées sont déjà câblés, y compris pour le
tirage d'images depuis l'ACR.

| Secret | Emplacement |
|---|---|
| Chaîne SQL | Key Vault, accès par identité managée |
| Clé d'envoi d'e-mails | Key Vault |
| Secret du rappel de rebond (`RG-31`) | Key Vault |
| Configuration Entra External ID | Paramètres d'application |

**Aucun secret dans le dépôt.** La mémoire projet le rappelle déjà ; les nouvelles
applications suivent la même règle.

## 4. Construction des images

Piège documenté dans la mémoire projet : les Dockerfiles Angular se construisent
**depuis le contexte `src/`**, pas depuis le dossier de l'application, car les chemins
TypeScript résolvent `@vpd/ui` via `../SharedUi`. Les deux nouvelles applications
Angular y sont soumises.

Les URL d'API sont injectées **à la construction** par arguments de build, comme pour
les images existantes — pas de substitution au démarrage.

## 5. Intégration continue

**Point à traiter, et il n'est pas optionnel.** La mémoire projet note qu'aucun fichier
de CI n'existe. Or Functions sur Container Apps **ne supporte pas le déploiement continu
intégré** : il faut GitHub Actions ou Azure Pipelines.

Le minimum utile, par ordre de valeur :

1. Compilation de la solution et exécution des tests à chaque poussée.
2. Construction et publication des images vers l'ACR.
3. Mise à jour des révisions Container Apps.
4. Application des migrations EF Core **en étape explicite**, pas au démarrage de
   l'API — sinon deux répliques peuvent migrer en même temps.

Le point 1 a de la valeur même seul : la mémoire projet signale que `npm test` échoue
côté `BackOffice` faute de fichiers de test, et que la validation locale est aujourd'hui
le seul filet.

## 6. Environnements

Le dépôt a un jeu de paramètres `dev`. Pour un projet bénévole, **un seul environnement
réel est défendable** — le coût d'un second est surtout en attention, pas en euros.

Si un environnement de test est ajouté, il doit rester à `minReplicas: 0` partout — la
contrainte de disponibilité ne vaut que pour la production. À noter : le seul jeu de
paramètres du dépôt s'appelle `main.dev.bicepparam` mais décrit l'environnement réel,
d'où ses `minReplicas: 1`.

## 7. Coûts

| Poste | Coût attendu |
|---|---|
| **SQL Server** | **~30 $/mois fixe** en `S1` (`DT-11`) — voir ci-dessous, c'est le poste qui a changé |
| Container Apps — les trois applications existantes à `minReplicas: 1` | **Déjà engagé, hors module livres.** Voir ci-dessous |
| Container Apps — `scan` et `catalog` | **Deux réplicas permanents de plus**, au même tarif que les trois existants |
| Container Apps — `worker` | ~21 600 vCPU-s/mois, soit moins d'un euro — sauf si `QT-02` impose `minReplicas: 1` |
| Entra External ID | Utilisateur actif mensuel, bénévoles compris. **Les 50 000 premiers sont gratuits** (`QT-04`) : sans objet à notre échelle |
| Envoi d'e-mails — ACS (`DT-12`) | 0,00025 $ par message. À quelques dizaines par semaine : **quelques centimes** |
| Domaine et certificat (`DT-13`) | **0 €** — certificat managé gratuit sur Container Apps |
| Storage | Couvertures, quelques Go. Négligeable |
| BnF, Open Library | **0 €** |
| Log Analytics | Ingestion en hausse avec trois applications de plus, et sans échantillonnage (`11` §5). **Plafond journalier obligatoire** sur chaque Application Insights — voir ci-dessous |

### La base de données, et pourquoi elle a changé de nature

Le dossier a longtemps traité la base comme un poste inchangé — « le module livres ajoute
moins de 100 Mo ». C'était vrai sur le volume et faux sur la facture.

La base est un `GP_S_Gen5_1` **serverless avec pause automatique à 60 minutes**, facturé
**0,26 $ de l'heure dès qu'elle est éveillée**. Le balayage de `06` §3 toutes les cinq
minutes l'aurait tenue éveillée en permanence : **~190 $ par mois**, soit deux ordres de
grandeur au-dessus de ce que ce chapitre annonçait pour le worker. Et la laisser dormir
n'était pas la sortie : la reprise se serait payée sur le premier scan d'une session,
c'est-à-dire sur `ENF-01`.

`DT-11` tranche : palier fixe **`S1`, ~30 $ par mois**, sans pause et sans démarrage à
froid. Le point qui rend la décision facile est qu'elle est vraisemblablement **en
baisse** par rapport à la facture serverless actuelle, dès lors que le site public reçoit
des visites étalées dans la journée. Ce n'est donc pas une dépense imputable à la bourse
aux livres.

`QT-09` mesure au palier 1 si `S1` tient sur son stockage à disque dur ; `S2` (~74 $) est
à un paramètre de distance.

### L'observabilité, et son seul risque financier

`DT-16` retient de **ne pas échantillonner** : les volumes sont trop faibles pour que jeter
des traces ait un sens, et l'événement qu'on cherchera arrive une fois par mois (`11` §5).
Le raisonnement tient — mais il retire le garde-fou habituel.

Le risque n'est donc pas le régime nominal, qui reste très en dessous de ce qui compte. Le
risque est **une boucle bavarde** : un rattrapage mal réglé, une journalisation SQL laissée
active, un réessai en cascade. Ça monte vite, et en silence.

| Garde-fou | Où |
|---|---|
| **Plafond journalier** sur chaque Application Insights | En Bicep, dès la création de la ressource — pas après la première facture |
| Rétention à la durée incluse | Au-delà, le diagnostic passe par les données métier, conservées par ailleurs (`ENF-22`) |
| Journalisation SQL désactivée en production | Premier poste d'ingestion inutile, et il contient des valeurs de paramètres |

Trois applications de plus signifient aussi **trois Application Insights et trois identités
managées de plus** à déclarer — `main.bicep` n'ayant aucune boucle, c'est du Bicep explicite
(voir `revue.md` `R-22`). Le Log Analytics, lui, **reste unique** : c'est ce qui permet de
suivre une trace du scan jusqu'au worker (`11` §2).

### Cinq réplicas permanents

Un réplica de 0,5 vCPU allumé en continu consomme de l'ordre de **1,3 million de
vCPU-secondes par mois**, contre 180 000 offertes. Le quota gratuit est donc **dépassé
par une seule application**, et `36b0e50` en allume trois. Le module livres en ajoute
deux : **cinq réplicas permanents** à terme.

Deux éléments atténuent la facture sans l'annuler : Container Apps facture un réplica
inactif à un **tarif de veille** nettement inférieur au tarif actif, et les gabarits
pourraient être réduits puisque la charge servie est modeste.

**À chiffrer sur le calculateur Azure**, et c'est désormais le poste dominant de
l'infrastructure. Trois de ces réplicas sont **antérieurs et étrangers au module
livres** : ils ont été engagés pour la disponibilité du site associatif. Les deux autres
lui sont imputables, et le quota gratuit ne les couvre plus.

C'est le seul endroit du dossier où une décision de confort — pas de démarrage à froid —
se paie en euros récurrents. Si la facture devait être réduite, c'est `scan` qu'il
faudrait redescendre à zéro en premier : c'est l'application dont les usagers sont les
plus tolérants, parce qu'ils sont prévenus et sur place.

### Le worker

À zéro réplica, sa consommation est dérisoire : 288 exécutions/jour × 10 s × 0,25 vCPU
≈ 21 600 vCPU-secondes par mois. Ce n'est plus « gratuit » au sens strict, puisque le
quota mensuel est déjà absorbé par les applications permanentes, mais **moins d'un euro
par mois** au tarif actif.

**Le poste susceptible de basculer** est ce même worker si `QT-02` impose
`minReplicas: 1` : un conteneur permanent de plus, de l'ordre d'une dizaine d'euros par
mois. C'est la raison d'être de cette mesure, et elle pèse plus lourd qu'avant : ce
serait le **sixième** conteneur allumé en continu, pour un travail qui tient en quelques
requêtes SQL toutes les cinq minutes. Ce jour-là, `DT-09` se rouvre légitimement.

## 8. Sauvegarde et restauration

`ENF-22` : les mouvements sont l'historique comptable de l'activité.

- Sauvegarde SQL au niveau déjà retenu pour les données existantes. Le module livres ne
  justifie pas un régime particulier — il justifie de **vérifier que celui en place
  fonctionne**.
- Les couvertures en blob sont **reconstructibles** depuis les sources : elles ne
  méritent pas le même niveau de protection.
- La copie embarquée sur les appareils est jetable. **La file de sortie ne l'est pas**,
  mais elle ne se sauvegarde pas : elle se vide en synchronisant. D'où `ENF-07`, qui
  impose de rendre visible le nombre de gestes en attente.

## 9. Ordre de déploiement

Aligné sur les paliers fonctionnels de `01` §7 :

| Palier | Infrastructure |
|---|---|
| **Préalable — identité et délais externes** | **Locataire Entra External ID**, enregistrements et rôles par script (`infra/entra/`), suppression de l'authentification maison. **Ressource ACS Email et vérification du sous-domaine d'envoi** (`DT-12`). **Enregistrements DNS** du catalogue et de l'envoi, posés en une fois (`DT-13`) |
| 0 — sonde | **Rien de plus.** Application locale, aucun déploiement |
| 1 — socle | **Socle d'exécution unifié** (`DT-15`), **passage de la base en `S1`** (`DT-11`), migrations 1, application `scan`, worker, compilation et tests au push, **premières règles d'alerte** (`11` §8) |
| 2 — vitrine | Application `catalog`, **domaine `livres.volepapillondamour.fr` et certificat managé**, index plein texte |
| 3 — alertes | Ouverture de l'inscription en libre-service, envoi effectif des e-mails, migrations 3 |

**Deux choses remontent au préalable, et pour la même raison : elles ont un délai
externe.**

Le locataire d'identité, parce que sa propagation n'est pas maîtrisée et que tout ce qui
s'authentifie en dépend. Et **l'envoi d'e-mails**, parce que la réputation d'un domaine
d'envoi se construit sur des semaines : un domaine neuf qui émettrait d'un coup son
premier lot d'alertes groupées partirait en indésirables, et `RG-28` échouerait en
silence. On crée donc la ressource et on vérifie le domaine des mois avant le premier
envoi utile.

Le passage en `S1` n'a pas de délai externe mais doit précéder le worker : c'est lui qui
rend la cadence de cinq minutes gratuite.

Le palier 0 ne déploie rien : c'est ce qui permet de l'abandonner sans coût si les
mesures sont mauvaises.

Le préalable, lui, déploie quelque chose — un locataire d'identité — et ne s'abandonne
pas aussi facilement. C'est le prix de le faire en premier, et c'est un prix acceptable :
un locataire à quelques comptes ne coûte presque rien, et le refaire plus tard, sur un
système en service et avec des bénévoles à réinscrire, en coûterait beaucoup.
