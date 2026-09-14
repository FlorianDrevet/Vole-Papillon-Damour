## 🎯 But principal

<!-- Décrivez en une phrase claire l'objectif de cette PR. -->


---

## 📋 Type de changement

- [ ] `feat` - Nouvelle fonctionnalité
- [ ] `fix` - Correction de bug
- [ ] `refactor` - Refactoring sans changement fonctionnel
- [ ] `perf` - Amélioration des performances
- [ ] `docs` - Documentation uniquement
- [ ] `test` - Ajout ou modification de tests
- [ ] `chore` - Maintenance, dépendances, CI
- [ ] `ci` - Changements liés aux pipelines CI/CD
- [ ] `style` - Formatage, lint (sans impact fonctionnel)
- [ ] `revert` - Annulation d'un commit précédent

---

## 🗂️ Changements par couche / zone

<!-- Adapter les sections ci-dessous à l'architecture réelle du projet.
     memory-bootstrap peut personnaliser ce template si nécessaire. -->

### Backend
<!-- Nouveaux fichiers, modifications, suppressions côté backend -->
- 

### Frontend
<!-- Nouveaux fichiers, modifications, suppressions côté frontend (si applicable) -->
- 

### Infrastructure / Configuration / CI
<!-- Configs, appsettings, Dockerfile, workflows, orchestration -->
- 

### Tests
<!-- Tests ajoutés ou modifiés -->
- 

### Documentation
<!-- MEMORY.md, README, docs/ -->
- 

---

## 🗄️ Base de données

- [ ] Cette PR inclut une **migration**
- [ ] Aucune migration nécessaire

Nom de la migration : 

---

## ✅ Checklist avant merge

- [ ] Build backend réussi
- [ ] Build frontend réussi (si applicable)
- [ ] Aucun avertissement de compilation introduit
- [ ] Conventions du projet respectées (voir MEMORY.md)
- [ ] `MEMORY.md` mis à jour avec les nouveautés

---

## 🚀 Pipelines à lancer après merge

<!-- La CI (`CI`) tourne automatiquement sur chaque push et chaque PR : rien à lancer pour la valider.
     Aucun déploiement n'est automatique. Après le merge, lancer manuellement les pipelines cochés
     depuis Actions → <pipeline> → Run workflow, sur la branche `main`, dans l'ordre ci-dessous. -->

- [ ] Aucun déploiement (documentation, tests ou CI uniquement)

**1. Infrastructure**
- [ ] `Infra - deploy` — si `infra/` change : `mode: what-if`, relire le résultat, puis `mode: deploy`

**2. Backend**
- [ ] `Books runtime - deploy` — si `src/Backend/` change (Domain, Application, Infrastructure, API ou Worker) : API et Worker sur la même image. `run_migrations: true` si la PR contient une migration
- [ ] `API - deploy` ou `Worker - deploy` — à la place du précédent, seulement si un seul des deux hôtes est concerné (`run_migrations: true` sur l'API si migration)
- [ ] `ACS Email - configure` — si la configuration e-mail ACS change (après l'API, qui reçoit le webhook)

**3. Frontends** (après le backend dont ils dépendent)
- [ ] `Catalog - deploy` — si `src/Catalog/` change
- [ ] `Scan - deploy` — si `src/Scan/` change
- [ ] `Website - deploy` — si `src/Website/` change
- [ ] `BackOffice - deploy` — si `src/BackOffice/` change

> ⚠️ Les API déployées n'appliquent pas les migrations au démarrage : sans `run_migrations: true`, le schéma de la base n'est pas mis à jour.

---

## 🔗 Issues / tickets liés

<!-- Ex : Closes #42, Fixes AB#65 -->
