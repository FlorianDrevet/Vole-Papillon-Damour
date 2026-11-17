---
name: perf-guardrails
description: "Use before writing or reviewing any list/table/search screen, list or search endpoint, EF Core query, CSV import/export, image or font, outbound HTTP call, nginx/cache config or new Azure resource. Absolute performance rules (always apply, free), plus paid/infra recommendations the agent proposes in one line and the user accepts or declines."
---

# Garde-fous de performance

Synthèse critique, reformulée et adaptée à VPD, d'un guide tiers de 31 correctifs
(« App-Performance Toolkit », BreakingBreach). Le guide d'origine est en usage personnel :
ne le recopie pas. Ce skill garde ce qui est vrai pour **notre** stack et écarte le reste.

Contexte VPD qui explique les règles : **Azure SQL Standard S0 (10 DTU)**, très sensible
aux lectures logiques, aux scans et aux plans qui régressent ; Container Apps en France
Central ; API .NET 10 + EF Core ; Worker existant ; fronts Angular 21 ; App Insights déjà
en place.

## Comment l'utiliser

Avant d'écrire ou de relire du code qui touche l'un des déclencheurs de la description :

1. Applique **§A** sans demander : ce sont des règles, pas des options. Une dérogation exige
   l'accord explicite de l'utilisateur, notée dans la PR.
2. Si un cas relève de **§B**, propose-le en **une ligne** (format ci-dessous) et attends la
   réponse. N'implémente jamais une reco §B sans « oui ».
3. **§C** : cas par cas, gratuit, à juger.
4. Termine par la **vérification §E** et résume le résultat en une ligne dans la PR
   (« Validation »).

Ne pas optimiser au-delà de §A/§B sans mesure (App Insights, plan d'exécution, profiler).

---

## A. Règles absolues (gratuites — toujours)

### A1. Pagination, filtre, tri et recherche : TOUJOURS côté serveur
Toute liste ou tableau qui peut dépasser ~50 lignes :
- `Skip/Take` (ou keyset) **en SQL** ; filtres, tri et recherche **dans la requête**.
  Jamais « tout charger puis filtrer/trier/paginer dans Angular ».
- Taille de page par défaut 20–25, **maximum imposé par le serveur** (ex. 100) : on
  plafonne, on ne fait pas confiance au client.
- Tri limité à une **liste blanche** de colonnes, avec un départage par clé unique (`Id`)
  pour que les pages ne sautent ni ne répètent de lignes.
- Page/pageSize pour les tableaux d'admin à boutons de pages ; keyset (curseur) pour les
  flux « charger plus » ou les très grosses tables. (Le guide prescrit le keyset partout :
  irréaliste avec un tri multi-colonnes.)
- Renvoyer un total seulement si l'UI l'affiche ; sinon `hasMore` (lire `pageSize + 1`).
- L'état des filtres vit dans l'URL (query params), pas seulement en mémoire.
- Exception : liste bornée par nature (< ~50 : rôles, genres…) → peut rester complète,
  à signaler par un commentaire.

### A2. Recherche : debounce, annulation, SQL sargable
- Front : debounce ~300 ms, annulation de la requête précédente (`switchMap`), minimum
  2 caractères, saisie jamais bloquée.
- SQL : pas de `LIKE '%x%'` sur une table qui grossit sans index adapté. Préfixe
  (`LIKE 'x%'`), colonne normalisée indexée ou full-text.

### A3. Lectures EF Core
- `AsNoTracking()` pour toute lecture sans modification ; `Select` vers un DTO, jamais
  d'entité renvoyée ni de colonnes longues/blob dans une liste.
- Zéro accès base dans une boucle ou un `map` (N+1) ; `Include` seulement de ce qui sert ;
  plusieurs collections incluses → `AsSplitQuery()`.
- Tout filtre est traduit en SQL avant `ToListAsync` (pas d'évaluation côté client).
- `CancellationToken` propagé jusqu'à la requête.

### A4. Index livrés avec la requête
Toute nouvelle requête filtrée, triée ou paginée livre ses index **dans la même
migration** : clés étrangères, colonnes de `WHERE`/`ORDER BY`, composites dans le bon ordre.
Pas d'index « au cas où » (chacun coûte en écriture). Sur une grosse table de prod,
`ONLINE = ON`. À vérifier sur la base locale avec un volume réaliste (§E).

### A5. Imports et exports volumineux
- Import : lecture en flux, traitement **par lots** (ordre de 500–1000), un `SaveChanges`
  par lot — jamais un par ligne.
- Export : écrit en flux dans la réponse (`IAsyncEnumerable`/stream), jamais construit
  entièrement en mémoire.
- Au-delà de ~2 s ou d'un volume qui peut croître : A6.

### A6. Travail lent hors de la requête
Tout ce qui dépasse ~1–2 s (import, enrichissement, e-mails en masse, appels externes en
lot) part dans le **Worker existant** ; la requête rend vite avec un statut consultable.
Les traitements sont **idempotents** (un retry ne duplique rien). Aucun nouveau
broker/service de file sans passer par §B.

### A7. Appels sortants
Timeout **explicite** sur tout `HttpClient`/SDK (le défaut est 100 s), `CancellationToken`,
message d'erreur propre à l'échéance. Retry uniquement sur appel idempotent, avec backoff
et nombre d'essais borné ; jamais de retry aveugle sur une écriture ou un paiement.

### A8. Parallélisme
- Front : requêtes indépendantes en parallèle (`forkJoin`, `resource`), pas en cascade.
- Back : **jamais** `Task.WhenAll` sur un même `DbContext` (non thread-safe) ni de rafale
  de requêtes sur S0. Autorisé pour des appels externes indépendants avec plafond de
  concurrence.

### A9. Pas de doublons
Bouton d'envoi désactivé pendant la requête ; une donnée = une requête partagée (service,
signal, `resource`), pas un fetch par composant ; création/import protégés contre la
double soumission (idempotence ou verrou métier).

### A10. Chargement front
- Routes en lazy (`loadComponent`/`loadChildren`).
- Bibliothèques lourdes (graphiques, éditeur, PDF, scanner, carte) via `import()` ou
  `@defer`, jamais dans le shell commun.
- Pas de nouvelle dépendance si le natif suffit (`Intl`, `Date`, `fetch`…).

### A11. Rendu Angular
Composants standalone en `ChangeDetectionStrategy.OnPush`, état en signals, `@for` avec
`track` stable (jamais l'index d'une liste qui bouge), aucun appel de fonction coûteux dans
un template (`computed`), pas de `subscribe` manuel sans nettoyage (`async`, `toSignal`,
`takeUntilDestroyed`).

### A12. Listes rendues côté client
Si une liste peut afficher plus de ~100 éléments : **paginer d'abord** (A1). Virtualiser
(`@angular/cdk` scrolling) uniquement quand la pagination est impossible (sélecteur long,
vue de scan continue), en gardant clavier et accessibilité.

### A13. Images, polices, scripts tiers
- Toute image a `width`/`height` (ou `aspect-ratio`) ; `NgOptimizedImage` ou
  `loading="lazy"` sous le pli ; l'image principale (LCP) est prioritaire, **jamais** lazy.
- Dimensions adaptées à l'affichage : pas de photo 4000 px dans un emplacement de 400 px.
  Couvertures et uploads : redimensionnés/convertis (WebP/AVIF) à l'enregistrement ou via
  variantes, pas à chaque affichage.
- Polices en WOFF2 auto-hébergées, 2–3 graisses (ou une variable), `font-display: swap`.
- Script tiers jamais bloquant (`defer`/`async`), widget lourd chargé à l'interaction.

### A14. États de chargement
Écran de liste ou de fiche : **skeleton aux dimensions exactes du contenu** (pas de saut de
mise en page), `aria-busy`, animation coupée avec `prefers-reduced-motion`. Pas de spinner
seul. Voir aussi `ui-ux-front-saas`.

### A15. Cache HTTP et compression
- Fichiers à empreinte (hash) : `Cache-Control: public, max-age=31536000, immutable`.
- `index.html` : `no-cache`. Réponse API contenant des données utilisateur : **jamais**
  `public`.
- Texte (HTML, CSS, JS, JSON, SVG) compressé en Brotli/gzip ; ni images, ni vidéos, ni
  archives. Un flux (SSE) ne doit pas être mis en tampon.

### A16. Ressources et état serveur
- Toute nouvelle ressource Azure : **même région que la base** (France Central).
- Pas de `new SqlConnection` ni de `new HttpClient` par appel : injection (pool et
  `IHttpClientFactory`).
- Aucun état en mémoire serveur qui casserait avec 2 réplicas (sessions, caches non
  invalidables, compteurs, tâches planifiées qui se doublent).

---

## B. Recommandations payantes ou infra (l'agent propose, l'utilisateur décide)

Ne rien implémenter, ne rien créer sans « oui ». Propose seulement quand un **déclencheur
mesuré ou évident** existe, jamais « au cas où », au plus une reco par sujet et par session.

Format (une ligne, pas de pavé) :

> 💡 **Reco perf (payante)** : *<quoi>* — gain : *<mesuré/estimé>* — coût : *<estimé, à
> vérifier>* — déclencheur : *<ce qui l'a provoquée>*. Tu valides ?

Consigne la réponse (acceptée, refusée, reportée) dans
[`docs/development/perf-decisions.md`](../../../docs/development/perf-decisions.md) pour ne
pas reposer la question à l'identique. Une reco refusée ne se représente que si le
déclencheur change.

| Reco | Déclencheur typique | Avant de payer |
|------|---------------------|----------------|
| **Cache Redis** | Même lecture lourde répétée, ou plusieurs réplicas qui doivent partager un cache | Essayer d'abord `IMemoryCache`/`HybridCache` (gratuit, TTL court) et A1–A4. Clé toujours préfixée par l'utilisateur/compte pour les données personnelles ; repli sur la base si le cache tombe. |
| **Monter le palier Azure SQL** (S1+, serverless) | DTU > ~80 % soutenu **après** application de A1–A5 | Montrer la métrique et les requêtes coupables : un palier ne répare pas un scan. |
| **CDN / Front Door** pour assets et couvertures | TTFB ou coût de sortie mesurés sur les assets statiques/images | Vérifier A13/A15 d'abord ; jamais de cache CDN sur réponse personnalisée. |
| **Réplicas / autoscale** (Container Apps) | Latence ou CPU saturés en charge réelle (ex. jour de bourse) | A16 respectée ; toujours fixer un `maxReplicas` pour plafonner la facture. |
| **Nouveau service de file** (Service Bus, Hangfire…) | Le Worker existant ne suffit plus, fiabilité/volume prouvés | Le Worker et A6 d'abord. |
| **Mesure d'usage réel (Web Vitals via App Insights navigateur)** | Besoin de chiffres LCP/INP/CLS par page | Consentement RGPD, aucune donnée personnelle dans les événements. |
| **Résilience Azure SQL** (`EnableRetryOnFailure`) | Erreurs transitoires observées | Impose `CreateExecutionStrategy` pour les transactions manuelles : changement de comportement, à valider. |
| **Rendu serveur/statique** (SSR/SSG) | Pages publiques à SEO ou partage de lien (Website, Catalog) | Plus de calcul serveur ; jamais de donnée privée dans le HTML rendu. |
| **Contrôle de perf en CI** (Lighthouse CI, budgets) | Régressions de perf répétées | Pas de pipeline CI aujourd'hui : c'est un projet à part. |

---

## C. Cas par cas (gratuit, à juger)

- **UI optimiste** : uniquement pour une action réversible et sans risque (sélection,
  favori, bascule, ordre). Rollback + message d'erreur si l'API échoue ; le serveur reste
  la source de vérité. **Jamais** pour la caisse, une vente, un stock, un paiement, une
  suppression ou une permission.
- **Mémoïsation** : `computed` et `OnPush` d'abord ; ne mémoïse que ce que le profiler
  désigne.
- **Nettoyage de dépendances** : hygiène périodique (`npm`/NuGet inutilisés, analyse de
  bundle), pas une règle d'écriture de code. Vérifie les usages en config et en build avant
  de supprimer.

## D. Écarté ou non applicable (et pourquoi)

| Élément du guide | Décision |
|------------------|----------|
| Pooling de connexions | ADO.NET/EF poolent déjà. Seule règle utile : A16 (pas de connexion par appel). |
| Load balancer manuel | Container Apps répartit déjà ; le seul sujet est `maxReplicas` (§B). |
| Mémoïsation React, `Promise.all` générique | Remplacés par A8 (EF non thread-safe) et A11 (OnPush/signals). |
| `CREATE INDEX CONCURRENTLY` | Spécifique à PostgreSQL → `ONLINE = ON` (A4). |
| Tracing OpenTelemetry | App Insights et Aspire sont déjà en place : pas de second système. |
| Streaming de réponses IA | Aucune fonctionnalité IA aujourd'hui. Si elle arrive : streaming + timeout + bouton Stop. |
| Test sur vrais téléphones bas de gamme | Remplacé par §E (390 px, CPU ralenti) ; un vrai Android seulement pour la caisse MAUI. |

---

## E. Vérification (avant de dire « terminé »)

Pour toute feature qui touche une liste, une requête ou une page :

1. **Aucune requête sans borne** : un test prouve que `pageSize` énorme est plafonné.
2. **Nombre de requêtes SQL constant** que la page affiche 10 ou 1 000 éléments (journal
   EF activé en local).
3. **Plan d'exécution** : recherche d'index (`Seek`), pas de `Scan` sur une table qui
   grossit, vérifié sur la base locale Aspire avec un jeu réaliste (≥ 10 000 lignes ;
   sqlcmd avec `-I` pour les index filtrés).
4. **Saisie** : onglet Réseau, une seule requête après la pause de frappe.
5. **Écran liste** : à 390 px, CPU ralenti ×4 et réseau « Slow 4G » : ni saut de mise en page,
   ni défilement horizontal, squelette visible immédiatement.
6. Une ligne « Perf : … » dans la section « Validation » de la PR (ce qui a été vérifié,
   ce qui ne l'a pas été).
