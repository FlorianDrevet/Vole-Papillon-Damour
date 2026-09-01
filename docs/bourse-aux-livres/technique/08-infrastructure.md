# 08 — Infrastructure et déploiement

## 1. Ce qui existe

Relevé dans `infra/` : Container Apps et leur environnement, Container Registry,
Key Vault, SQL Server, Storage Account, Application Insights, Log Analytics, identités
managées et attributions de rôles. Le tout piloté par `main.bicep` et des modules par
ressource.

**Aucune Function, aucun Service Bus, aucune file** à ce jour.

Les trois applications sont paramétrées avec `minReplicas: 0` et `maxReplicas: 2`.
C'est ce réglage qui impose un worker séparé (`DT-04`).

## 2. Ce qui s'ajoute

**Aucune ressource Azure d'un type nouveau.** Trois Container Apps de plus dans
l'environnement existant :

| Application | Nature | Ingress | Réplicas |
|---|---|---|---|
| `scan` | PWA Angular | Externe | 0 → 2 |
| `catalog` | Angular SSR + administration | Externe | 0 → 2 |
| `worker` | `kind=functionapp` | Interne (requis pour la mise à l'échelle) | 0 → 1, **sous réserve de `QT-02`** |

Le module `ContainerApp` existant se réutilise tel quel. Pour le worker, le delta est
`kind: functionapp` sur `Microsoft.App/containerApps`.

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

Si un environnement de test est ajouté, il reste à `minReplicas: 0` partout et partage
le même quota gratuit.

## 7. Coûts

| Poste | Coût attendu |
|---|---|
| Container Apps | Quota gratuit mensuel : 180 000 vCPU-s, 360 000 Gio-s. Trois applications à zéro réplica plus un worker à ~21 600 vCPU-s tiennent dedans |
| SQL Server | **Inchangé** — le module livres ajoute moins de 100 Mo (`DT-02`) |
| Storage | Couvertures, quelques Go. Négligeable |
| BnF, Open Library | **0 €** |
| Entra External ID | Paliers gratuits pour quelques centaines de comptes ; à confirmer au dimensionnement |
| Envoi d'e-mails | Quelques dizaines par semaine ; palier gratuit chez la plupart des fournisseurs |

**Le seul poste susceptible de basculer** est le worker, si `QT-02` impose
`minReplicas: 1` : de l'ordre d'une dizaine d'euros par mois. C'est la raison d'être de
cette mesure.

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
| 1 — socle | Migrations 1, application `scan`, worker, CI |
| 2 — vitrine | Application `catalog`, index plein texte |
| 3 — alertes | Entra External ID, envoi d'e-mails, migrations 3 |

Le palier 0 ne déploie rien : c'est ce qui permet de l'abandonner sans coût si les
mesures sont mauvaises.
