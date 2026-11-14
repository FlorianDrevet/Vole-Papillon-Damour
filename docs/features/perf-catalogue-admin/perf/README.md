# Reproduction locale et mesures

Base locale Aspire (SQL Server 2022 en conteneur), peuplée comme la production :
19 500 fiches (16 500 visibles), 28 genres dont « Roman » environ 23 %, un mouvement
d'entrée par fiche, `RawPayload` d'environ 6 Ko et `Summary` d'environ 1 Ko.

## Exécuter un script

Depuis la racine du worktree, Aspire démarré (`docs/local-authentication.md`). Le nom du
conteneur SQL est `sql-server-<hash>` (`podman ps`). Le mot de passe reste dans le
conteneur :

```powershell
$c = '<nom du conteneur sql-server>'
podman cp docs/features/perf-catalogue-admin/perf/01-seed-catalogue.sql "${c}:/tmp/s.sql"
podman exec $c bash -c '/opt/mssql-tools18/bin/sqlcmd -C -I -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -W -s "|" -i /tmp/s.sql'
```

`-I` est obligatoire, car les index filtrés exigent `QUOTED_IDENTIFIER ON`. Avec Docker,
remplacer `podman` par `docker`.

| Script | Rôle |
|---|---|
| `01-seed-catalogue.sql` | Peuple une base **vide** (refuse sinon). ISBN synthétiques `9781000…`. |
| `02-genres-rares.sql` | Ajoute 12 genres à une fiche pour retrouver les 28 genres distincts de la prod, puis vide le cache de plans. |
| `10-requetes-en-cache.sql` | Lectures logiques, mémoire et débordements des requêtes du catalogue en cache. |
| `11-operateurs-du-plan.sql` | Opérateurs du plan le plus coûteux d'une requête paramétrée par `@genre`. |
| `12-texte-sql-genre.sql` | Texte SQL complet de cette requête. |
| `99-nettoyage.sql` | Supprime les fiches synthétiques. |
| `avant-requete-page-genre.sql` | Requête EF d'origine, pour comparaison. |

## Reproduire le parameter sniffing (CA-01)

1. `DBCC FREEPROCCACHE`, puis appeler
   `GET /catalog/search?genre=Amour&availability=available&sort=recent&pageSize=24`
   (genre à une seule fiche) : le plan est compilé pour un genre rare.
2. Appeler la même URL avec `genre=Roman`, puis lancer `10-requetes-en-cache.sql`.
3. **Avant correctif** (14 novembre 2026) : environ 16 000 lectures, 1 554 débordements,
   et en production 3,1 à 3,9 s pour `genre=Roman`.
4. **Attendu après** : moins de 2 000 lectures, aucun débordement. En production, moins de
   1 s côté serveur.

Mesures de référence, plan forcé « Amour » exécuté avec Roman, à froid :

| Variante | Lectures `Books` | Lectures `BookMovements` | Temps local |
|---|---|---|---|
| Requête actuelle | 11 226 | 20 268 | 124 ms |
| Actuelle + `OPTION (RECOMPILE)` | 985 | 2 | 19 ms |
| ISBN seuls, sans `NOT EXISTS` des sessions de scan, + `RECOMPILE` | 1 195 | 0 | 4 ms |
| Index couvrant seul, plan sniffé | 11 226 | 0 | 149 ms (ne suffit pas) |

## Mesures de production avant correctif (App Insights, 14 novembre 2026)

| Endpoint | p50 | p90 |
|---|---|---|
| `GET /catalog/search` genre Roman, disponibles, tri récent | 3,5 s | 3,9 s |
| `GET /books/admin/overview` | 5,0 s | 10,3 s |
| `GET /books/admin/books` | 2,1 s | 5,6 s |
| `GET /books/admin/isbndb/status` | 1,7 s | 4,5 s |
| `GET /books/admin/books/isbndb-absent` | 1,1 s | 2,1 s |
| `GET /books/admin/books/metadata-queue` | 0,5 s | 2,0 s |
| `GET /scan/me/statistics` | 7,2 s | 12,7 s |
| `GET /books/admin/catalogue/flow-stats` | 7,3 s | — |
| `GET /books/admin/volunteers/stats` | 6,9 s | — |

Ouverture du tableau de bord : 24 s, 27 appels dont `overview` ×2, `isbndb/status` ×2 et
les quatre compteurs `pageSize=1` ×2. La base S0 culmine à 87 % de ses DTU. La mémoire de
l'API tourne en médiane à 443 Mo pour une limite de 512 Mo.
