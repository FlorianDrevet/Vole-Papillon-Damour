---
name: audit-workflow
description: "Use when: code audit, technical audit, security audit, performance audit, scalability audit, database audit, audit markdown, GitHub audit issues, findings reconciliation, labels sync."
---

# Skill : audit-workflow — Audit technique et reconciliation

> Charger ce skill pour toute tache d'audit technique, que ce soit la production du rapport, la creation d'issues GitHub, ou la reconciliation entre deux audits successifs.

---

## Couverture d'audit

Un audit complet doit couvrir :

1. **Securite** — injection, auth, secrets, IDOR, CSRF, headers
2. **Performance** — N+1, allocations, pagination, index
3. **Maintenabilite** — SOLID, couplage, complexite cyclomatique, dead code
4. **Scalabilite** — contention, parallelisme, caching, limites
5. **Persistance** — migrations, contraintes FK, index, requetes non optimisees
6. **Architecture** — coherence des couches, separation des responsabilites, drift
7. **Tests** — couverture, qualite des assertions, zones sans filet
8. **Observabilite** — logging, tracing, health checks, metriques

---

## Format du rapport

Emplacement : `audits/audit-dd-MM-yyyy.md`

Structure :

```markdown
# Audit Technique — [DATE]

## Resume executif
- CRITICAL: X
- HIGH: Y
- MEDIUM: Z
- LOW: W

## Findings

### CRITICAL

#### SEC-001: [titre]
- **Localisation:** fichier(s) concernes
- **Description:** ce qui est problematique
- **Impact:** consequence si non corrige
- **Recommandation:** correction concrete
- **Effort:** S/M/L

### HIGH
...

### MEDIUM
...

### LOW
...

## Plan d'action
### Phase 1 — Critique (< 1 semaine)
### Phase 2 — Important (< 1 mois)
### Phase 3 — Amelioration continue

## Metriques cibles
```

---

## Identifiants de findings

Chaque finding a un identifiant unique et stable :

- `SEC-001` a `SEC-NNN` pour la securite
- `PERF-001` a `PERF-NNN` pour la performance
- `MAINT-001` a `MAINT-NNN` pour la maintenabilite
- `SCALE-001` a `SCALE-NNN` pour la scalabilite
- `DB-001` a `DB-NNN` pour la persistance
- `ARCH-001` a `ARCH-NNN` pour l'architecture
- `TEST-001` a `TEST-NNN` pour les tests
- `OBS-001` a `OBS-NNN` pour l'observabilite

---

## Reconciliation GitHub

### Cycle de vie des issues

1. **Finding nouveau** → creer une issue avec label `status: new`
2. **Finding toujours present** → mettre a jour l'issue existante
3. **Finding disparu** → fermer l'issue avec un commentaire
4. **Finding ferme qui reapparait** → rouvrir l'issue

### Labels d'audit

Les labels doivent etre organises en deux dimensions :
- **Severite :** `severity: critical`, `severity: high`, `severity: medium`, `severity: low`
- **Categorie :** `category: security`, `category: performance`, `category: maintainability`, etc.
- **Status :** `status: new`, `status: in-progress`, `status: fixed`, `status: wontfix`

---

## Integration avec les agents

- `@audit-expert` charge ce skill en debut de mission
- `@review-expert` peut referencer les findings d'audit dans ses reviews
- `@dev` route vers `@audit-expert` quand une demande d'audit est detectee
