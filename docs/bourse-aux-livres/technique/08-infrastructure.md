# 08 — Infrastructure et déploiement

## 1. Ce qui existe

Relevé dans `infra/` : Container Apps et leur environnement, Container Registry,
Key Vault, SQL Server, Storage Account, Application Insights, Log Analytics, identités
managées et attributions de rôles. Le tout piloté par `main.bicep` et des modules par
ressource.

**Aucune Function, aucun Service Bus, aucune file** à ce jour.

Les applications étaient paramétrées avec `minReplicas: 0` et `maxReplicas: 2`.

**L'API passe à `minReplicas: 1`** : elle ne peut pas être indisponible pour le site
web. Ce changement retire sa raison d'être au worker séparé et fonde `DT-09`.

## 2. Ce qui s'ajoute

**Aucune ressource Azure d'un type nouveau, et aucun worker.** Deux Container Apps de
plus dans l'environnement existant :

| Application | Nature | Ingress | Réplicas |
|---|---|---|---|
| `scan` | PWA Angular | Externe | 0 → 2 |
| `catalog` | Angular SSR + administration | Externe | 0 → 2 |

Le module `ContainerApp` existant se réutilise tel quel.

Les traitements différés sont des services hébergés **dans l'API** (`DT-09`) : pas
d'application supplémentaire, pas d'image de base Functions, pas de compte de stockage
dédié, pas de contrainte d'ingress interne, pas de question de révisions multiples.

L'API, elle, change de paramétrage dans `infra/parameters/` :

| Paramètre | Avant | Après |
|---|---|---|
| `containerAppApiScaling.minReplicas` | 0 | **1** |
| `containerAppApiScaling.maxReplicas` | 2 | 2 (inchangé) |

`maxReplicas: 2` implique que deux répliques peuvent exécuter les mêmes balayages. Les
opérations étant conçues en réclamation conditionnelle, c'est sans danger — voir
[`06-traitements-differes.md`](06-traitements-differes.md) §2.

## 3. Secrets et identité

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

**Point à traiter.** La mémoire projet note qu'aucun fichier de CI n'existe.

L'abandon de Functions (`DT-09`) retire la contrainte qui rendait la CI *obligatoire* —
Functions sur Container Apps ne supporte pas le déploiement continu intégré. Elle reste
néanmoins **fortement souhaitable**, pour une raison plus banale : trois applications à
construire et publier, et des migrations à appliquer.

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

Si un environnement de test est ajouté, il reste à `minReplicas: 0` partout et partage
le même quota gratuit.

## 7. Coûts

| Poste | Coût attendu |
|---|---|
| Container Apps — API à `minReplicas: 1` | **Le seul poste réellement nouveau.** Voir ci-dessous |
| Container Apps — `scan` et `catalog` | À zéro réplica la plupart du temps, dans le quota gratuit |
| Traitements différés | **0 €** — hébergés dans l'API, qui tourne déjà (`DT-09`) |
| SQL Server | **Inchangé** — le module livres ajoute moins de 100 Mo (`DT-02`) |
| Storage | Couvertures, quelques Go. Négligeable |
| BnF, Open Library | **0 €** |
| Entra External ID | Paliers gratuits pour quelques centaines de comptes ; à confirmer (`QT-04`) |
| Envoi d'e-mails | Quelques dizaines par semaine ; palier gratuit chez la plupart des fournisseurs |

### L'API en permanence

Avec le gabarit actuel — 0,5 vCPU, 1 Gio — un réplica allumé en continu consomme de
l'ordre de **1,3 million de vCPU-secondes par mois**, contre 180 000 offertes. Le quota
gratuit des Container Apps est donc dépassé, et il ne couvre plus les autres
applications.

Deux éléments atténuent la facture, sans l'annuler : Container Apps facture un réplica
inactif à un **tarif de veille** nettement inférieur au tarif actif, et le gabarit de
l'API pourrait être réduit puisqu'elle sert une charge modeste.

**Ordre de grandeur : quelques euros à une quinzaine par mois. À confirmer sur le
calculateur Azure avant de s'engager**, plutôt que sur cette estimation.

C'est un coût décidé pour une raison indépendante du module livres — la disponibilité du
site web — et non induit par lui. Le projet en bénéficie gratuitement : c'est ce qui
permet `DT-09` et referme `QT-02`.

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
| 0 — sonde | **Rien.** Application locale, aucun déploiement |
| 1 — socle | Migrations 1, application `scan`, API à `minReplicas: 1`, CI |
| 2 — vitrine | Application `catalog`, index plein texte |
| 3 — alertes | Entra External ID, envoi d'e-mails, migrations 3 |

Le palier 0 ne déploie rien : c'est ce qui permet de l'abandonner sans coût si les
mesures sont mauvaises.
