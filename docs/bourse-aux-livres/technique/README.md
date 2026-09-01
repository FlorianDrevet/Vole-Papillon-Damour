# Bourse aux livres — architecture technique

Ce dossier décrit **comment** construire ce que décrivent les spécifications
fonctionnelles du dossier parent. Il ne redéfinit aucune règle métier : il s'y réfère
par leurs identifiants (`RG-nn`, `ENF-nn`, `Q-nn`).

> **Prérequis de lecture.** Les spécifications fonctionnelles
> ([`../README.md`](../README.md)) sont la référence. En cas de contradiction entre ce
> dossier et elles, **elles gagnent** — et la contradiction est un défaut à corriger ici.

## Par où commencer

| Vous cherchez… | Lisez |
|---|---|
| Pourquoi tel choix technique a été fait | [`01-decisions.md`](01-decisions.md) — le journal des décisions, avec les alternatives écartées |
| Une vue générale du système | [`00-vue-densemble.md`](00-vue-densemble.md) |
| Ce qu'il reste à trancher avant de coder | [`09-questions-techniques.md`](09-questions-techniques.md) |
| À implémenter une brique précise | Le document correspondant ci-dessous |

## Contenu

| Document | Objet |
|---|---|
| [`00-vue-densemble.md`](00-vue-densemble.md) | Composants, flux, ce qui est nouveau et ce qui est réutilisé |
| [`01-decisions.md`](01-decisions.md) | Décisions techniques `DT-nn`, motivations, alternatives écartées |
| [`02-modele-de-donnees.md`](02-modele-de-donnees.md) | Agrégats, tables, configuration EF Core, migrations |
| [`03-backend.md`](03-backend.md) | Découpage CQRS, endpoints, conventions à respecter |
| [`04-app-scan.md`](04-app-scan.md) | PWA de scan, fonctionnement hors ligne, synchronisation |
| [`05-site-public.md`](05-site-public.md) | Application publique, SSR, référencement, Entra External ID |
| [`06-traitements-differes.md`](06-traitements-differes.md) | Outbox, Functions sur Container Apps, tâches planifiées |
| [`07-integrations-externes.md`](07-integrations-externes.md) | BnF, Open Library, résolution des métadonnées, e-mail |
| [`08-infrastructure.md`](08-infrastructure.md) | Bicep, déploiement, CI, coûts |
| [`09-questions-techniques.md`](09-questions-techniques.md) | Points ouverts `QT-nn` et mesures à faire au palier 0 |

## Conventions

- Les décisions techniques sont numérotées `DT-nn` et **ne se réécrivent pas** : une
  décision remplacée reste, marquée comme telle, et une nouvelle la supersède. On doit
  pouvoir relire pourquoi un choix a été fait à un moment donné.
- Les questions techniques ouvertes sont numérotées `QT-nn`.
- Les renvois `RG-nn`, `ENF-nn`, `Q-nn` pointent vers les spécifications fonctionnelles
  du dossier parent.

## Statut

**Brouillon.** Les trois décisions structurantes sont prises — sources
bibliographiques, stockage, traitements différés. Deux mesures conditionnent la suite
et sont à faire **avant** d'écrire du code de production : voir
[`09-questions-techniques.md`](09-questions-techniques.md).
