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
| **Lot en cours** | Aucun — le plan vient d'être écrit |
| **Prochaine action** | `L0-1` — créer le `global.json` ([lot 0](docs/bourse-aux-livres/plan/00-socle-et-prealable.md)) |
| **Dernière machine** | *(à renseigner)* |
| **Dernière mise à jour** | 2026-09-02 |
| **Branche** | `docs/bourse-aux-livres` |

---

## Reprendre le travail

```bash
git pull
# puis lire ce fichier en entier avant de toucher à quoi que ce soit
```

**Prérequis sur une machine neuve** — à compléter au fil du lot 0 :

| Outil | Version | Posé ? |
|---|---|---|
| SDK .NET | *(à épingler en `L0-1`)* | — |
| CLI Aspire | *(à fixer en `L0-3`)* | — |
| Azure CLI, connecté au bon abonnement | — | — |
| PowerShell 7 + modules Microsoft.Graph | pour `infra/entra/` | — |
| Docker | pour les images | — |

---

## En cours

*Rien.*

> Ce qui va ici : une étape commencée et non finie, avec **l'état exact** — quel fichier,
> quelle idée, ce qui reste. Écrire deux lignes ici coûte moins qu'une demi-heure de
> reconstitution.

---

## En attente d'un délai externe

*Rien.*

> Ce qui va ici : ce qui avance sans vous et qu'il faut penser à relever.
>
> | Sujet | Lancé le | Relevable à partir du |
> |---|---|---|
> | `QT-08` — session de 48 h puis ouverture en mode avion | | |
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
| Base SQL | `GP_S_Gen5_1` serverless, pause auto 60 min — **à passer en `S1` (`L0-6`)** | — |
| Sondes de santé | Désactivées (chemins vides, ports à 0) — **à poser (`L0-5`)** | — |
| Container Apps | `api`, `website`, `backOffice` à `minReplicas: 1` | `36b0e50` |
| Locataire Entra External ID | **Pas créé** | — |
| ACS Email | **Pas créé** | — |
| Plafonds journaliers App Insights | **Non posés** | — |
| Règles d'alerte | **Aucune** | — |

### DNS — `volepapillondamour.fr`

Domaine détenu et administré par l'association, main pleine et entière.

| Enregistrement | Posé ? | Le |
|---|---|---|
| `CNAME` + `TXT asuid` sur `livres` | Non | — |
| `TXT` propriété + SPF + DKIM sur `mail` | Non | — |
| `DMARC` | Non | — |
| `TXT` Search Console | Non | — |

### Entra

| Élément | État |
|---|---|
| Locataire | Non créé |
| Enregistrements d'application | Non exécutés (`Configure-EntraApps.ps1` existe, jamais lancé) |
| Comptes administrateurs recréés | Aucun |
| Appareils de caisse mis à jour | **Aucun** — voir `L0-9`, ils ne se mettent pas à jour tout seuls |

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
| `QT-08` | Durée de vie des jetons hors ligne | — | — |
| `QT-09` | Tenue de `S1` sur disque dur | — | — |

**Chiffres cibles du palier 0** *(à convenir avec l'association en `S0-1`, avant le test)* :

| Mesure | Cible |
|---|---|
| Taux de lecture au premier essai | — |
| Taux de métadonnées trouvées | — |
| Cadence tenable | — |

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
