# 00 — Vue d'ensemble

## 1. Principes directeurs

Quatre principes gouvernent les décisions de ce dossier. Quand un arbitrage est
ambigu, c'est l'ordre ci-dessous qui tranche.

**1. Le verdict de tri ne dépend d'aucune ressource externe.** Les signaux qui font
décider un bénévole — doublons, ventes passées, demande (`RG-10`, `RG-12`, `RG-13`) —
proviennent tous des données de l'association. Aucune API tierce n'est sur le chemin
critique. Les métadonnées bibliographiques sont du confort, pas la décision (`ENF-02`).

**2. Ce qui n'est pas maintenable par une personne seule ne se construit pas.**
`ENF-24` prime sur l'élégance. Chaque ressource ajoutée doit justifier sa propre
existence : un composant de plus, c'est une sauvegarde de plus, un montage local de
plus, et une panne de plus à diagnostiquer la veille d'une bourse.

**3. On réutilise l'existant avant d'ajouter.** Le dépôt a déjà un backend .NET en
CQRS, deux applications Angular, une bibliothèque partagée `SharedUi`, une base SQL,
un compte de stockage et un environnement Container Apps. Tout ce qui peut s'y loger
s'y loge.

**4. Ce qui n'est pas mesuré n'est pas décidé.** Un point bloquant reste ouvert et sera
tranché par une mesure au palier 0, pas par un avis : la couverture réelle des sources
bibliographiques ([`09-questions-techniques.md`](09-questions-techniques.md)).

## 2. Ce qui existe déjà

| Composant | Nature | Réutilisé ? |
|---|---|---|
| `Vole_Papillon_Damour.Api` | ASP.NET Core, contrôleurs, MediatR, rate limiting, Azure Monitor | **Oui**, étendu |
| `Application` / `Domain` / `Infrastructure` / `Contracts` | Découpage CQRS en tranches par fonctionnalité | **Oui**, nouvelles tranches |
| `ProjectDbContext` | EF Core, `ApplyConfigurationsFromAssembly` | **Oui**, nouveaux `DbSet` |
| `Website` | Angular 18 avec SSR | Non — le catalogue est une application distincte (décision fonctionnelle `01` §6) |
| `BackOffice` | Angular 18, auth admin | Non — l'administration du catalogue vit dans la nouvelle application |
| `SharedUi` (`@vpd/ui`) | Composants Angular partagés | **Oui**, par les deux nouvelles applications |
| `MauiCashApp` | Caisse buvette | Non — les livres ont leur propre mode caisse |
| `AssoEvents` type `Books` | Agrégat événement | **Oui**, sa date pilote la bascule (`RG-23`, `RG-36`) |
| SQL Server, Storage Account, Key Vault, ACR, App Insights, Log Analytics | Bicep `infra/` | **Oui** |
| Container Apps Environment | `Microsoft.App/managedEnvironments` | **Oui**, accueille les nouvelles applications |

## 3. Ce qui est ajouté

Deux applications conteneurisées dans **l'environnement Container Apps existant**, et
rien d'autre en matière de ressources Azure.

| Nouveau composant | Nature | Pourquoi |
|---|---|---|
| **App de scan** | PWA Angular, hors ligne | Outil des bénévoles : tri, caisse, consultation |
| **Site catalogue** | Angular avec SSR, plus zone d'administration | Public et administration (`04`, `05` fonctionnels) |
| Tranches `Books` dans le backend | Domain, Application, Contracts, Infrastructure | Le métier |
| Services hébergés dans l'API | `BackgroundService` | Alertes, bascule, rattrapage (`DT-09`) |

**Aucune base supplémentaire, aucun broker de messages, aucun cache distribué, aucun
worker séparé.** Justifications en [`01-decisions.md`](01-decisions.md).

L'API passe à `minReplicas: 1` — elle ne peut pas être indisponible pour le site web.
C'est ce qui permet d'héberger les traitements différés en son sein plutôt que dans une
application dédiée.

## 4. Carte des composants

```
        BÉNÉVOLES                    PUBLIC                    ADMIN
            │                          │                         │
     ┌──────▼───────┐          ┌───────▼────────┐                │
     │  App de scan │          │ Site catalogue │◄───────────────┘
     │     (PWA)    │          │  Angular SSR   │
     │  IndexedDB   │          └───────┬────────┘
     └──────┬───────┘                  │
            │  REST + sync delta       │  REST
            └───────────┬──────────────┘
                        ▼
         ┌───────────────────────────────────┐
         │        API  (minReplicas: 1)      │
         │                                   │
         │  contrôleurs + MediatR            │
         │  ─────────────────────────────    │
         │  services hébergés (DT-09) :      │──────►  BnF SRU
         │   · balayage toutes les 5 min     │──────►  Open Library
         │   · enrichissement horaire        │──────►  Envoi d'e-mails
         └──────────────┬────────────────────┘
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
   ┌─────────┐   ┌────────────┐  ┌──────────────┐
   │   SQL   │   │   Blob     │  │Entra Ext. ID │
   │ Server  │   │couvertures │  │   (public)   │
   │ fiches, │   └────────────┘  └──────────────┘
   │ outbox  │
   └─────────┘
```

Les traitements différés vivent **dans le processus de l'API**, en services hébergés.
Ils n'appellent pas l'API en HTTP : ils passent par les mêmes handlers MediatR et les
mêmes bibliothèques `Application`, `Domain` et `Infrastructure` que les contrôleurs.
Rien n'est dupliqué.

C'est possible parce que l'API tourne en permanence. Le détail du câblage, les
contraintes que cela impose et la sortie de secours si le travail de fond devenait lourd
sont en [`06-traitements-differes.md`](06-traitements-differes.md) §1 et §2.

## 5. Les trois flux qui comptent

### Scanner un livre au tri

1. L'appareil cherche l'ISBN dans sa copie locale du catalogue (IndexedDB).
2. **Trouvé** — verdict et titre affichés instantanément, aucun réseau.
3. **Absent** — le verdict « premier exemplaire » s'affiche immédiatement, puisque
   inconnu signifie zéro exemplaire et zéro vente. Une requête de métadonnées part
   **en parallèle**, c'est-à-dire sans que l'interface l'attende : le titre remplit sa
   zone s'il arrive à temps, et son échec est sans conséquence (`RG-03`).
4. Le geste est écrit dans la file de sortie locale **avant toute tentative d'envoi**,
   puis transmis dès que le réseau le permet (`ENF-05`, `ENF-07`).

Il n'existe pas de chemin « en ligne » distinct : la file est toujours empruntée, et
être connecté signifie seulement qu'elle se vide tout de suite. Le déroulé complet, les
pièges du parallélisme et le fonctionnement hors ligne sont détaillés en
[`04-app-scan.md`](04-app-scan.md) §3 et §4.

### Clôturer une session de tri

1. La clôture — manuelle, par inactivité, par déconnexion ou par jeton expiré
   (`RG-43`) — écrit **dans une seule transaction** le statut de la session et les
   lignes d'outbox des alertes, échéance à +2 h (`RG-44`).
2. Le balayage relève les lignes échues, relit l'état en base, regroupe par membre
   (`RG-29`) et envoie.
3. Tant que l'échéance n'est pas atteinte, l'administration peut annuler ou forcer
   l'envoi (`RG-45`).

### Rendre disponible à la date d'une bourse

Le balayage relève les annonces dont la bourse de rattachement a commencé et bascule les
quantités (`RG-23`). Aucun geste humain, et aucune alerte à ce moment : elle est déjà
partie à l'annonce (`RG-28`).

## 6. Frontières

| Frontière | Règle |
|---|---|
| Domaine `Books` ↔ `Product` / `Order` | **Aucune relation.** Les livres ne sont pas des produits (fonctionnel `02` §5) |
| Domaine `Books` ↔ `AssoEvents` | Référence par identifiant. Les livres lisent la date d'une bourse, ne la modifient jamais |
| Contrôleurs ↔ services hébergés | Même processus, mêmes couches, mêmes handlers (`DT-09`) |
| App de scan ↔ site catalogue | Deux applications sans dépendance, deux publics, deux modes d'authentification |
| Bénévoles ↔ membres du public | Deux systèmes d'identité disjoints (`ENF-16`, `ENF-17`) |

## 7. Ce que cette architecture ne fait pas

- Pas de microservices : une seule API, un seul modèle de données.
- Pas d'événementiel distribué : l'outbox est une table, pas un bus (`DT-03`).
- Pas de séparation physique lecture/écriture : CQRS est ici un découpage de code, pas
  deux magasins.
- Pas de multi-région ni de haute disponibilité active/active. Une association, un
  local, une bourse par mois.
