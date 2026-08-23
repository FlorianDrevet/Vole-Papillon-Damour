---
description: 'Conventions obligatoires pour la creation de Pull Requests par les agents GitHub Copilot.'
---

# Conventions Pull Request

## 1. Format du titre de PR

```
type(scope): description courte du but principal
```

### Types autorises

| Type | Quand l'utiliser |
|------|-----------------|
| `feat` | Nouvelle fonctionnalite |
| `fix` | Correction d'un bug |
| `refactor` | Restructuration sans changement fonctionnel |
| `perf` | Amelioration des performances |
| `docs` | Documentation uniquement |
| `test` | Ajout ou modification de tests |
| `chore` | Maintenance, mise a jour des dependances |
| `ci` | Changements lies aux pipelines CI/CD |
| `style` | Formatage, indentation, lint |
| `revert` | Annulation d'un commit ou d'une feature |

### Exemples corrects

```
feat(storage-account): add StorageAccount aggregate with full CRUD
fix(key-vault): correct EF Core LINQ translation
refactor(member): extract MemberCommandHelper
```

---

## 2. Description de la PR

### Ce qui est obligatoire

1. **But principal** - une phrase resumant l'objectif global
2. **Type de changement** - cocher les cases correspondantes
3. **Changements par couche** - decrire par couche impactee, pas par fichier
4. **Migration** - indiquer si une migration a ete ajoutee
5. **Tickets lies** - syntaxe appropriee au provider (AB#, #issue, etc.)
6. **Checklist** - valider chaque point avant de soumettre

---

## 3. Protocole de creation de PR pour les agents

1. **Identifier le but principal** de l'ensemble du travail.
2. **Construire le titre** selon le format `type(scope): description`.
3. **Rediger la description** : une phrase par couche impactee.
4. **S'assurer que le build passe** avant de soumettre.
5. **Deleguer la documentation a `documentation-professor`** si des changements impactent la doc.
6. **Mettre a jour `MEMORY.md`** si des conventions nouvelles ne sont pas couvertes.
