# Authentification locale de l'API

L'API garde Microsoft Entra pour les déploiements Azure. En local, l'AppHost Aspire
démarre Keycloak et charge `src/Backend/Vole_Papillon_Damour.AppHost/Realms/vpd-realm.json`.
Le realm fournit des identités et des rôles déterministes pour les tests de l'API.

## Démarrer les services

Docker Desktop doit exposer son moteur Linux pour que les conteneurs Aspire démarrent.
Vérifie-le avec `rtk docker ps` avant d'attendre Keycloak.

Avant le premier démarrage, définis le mot de passe local de la console Keycloak dans les
.NET User Secrets de l'AppHost. Il n'est pas nécessaire pour obtenir un jeton persona, mais
Aspire en a besoin pour configurer son compte d'administration :

```powershell
dotnet user-secrets set "Parameters:keycloak-admin-password" "<mot de passe local fort>" --project src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj
```

Cette valeur reste hors du dépôt. Le conteneur Keycloak et son volume sont persistants : le
realm est importé lorsque le volume est vide, puis ses données survivent aux arrêts d'Aspire.
En worktree, garde le mode isolé :

```powershell
rtk aspire start --isolated --non-interactive
rtk aspire wait keycloak --timeout 300 --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --non-interactive
rtk aspire wait api --timeout 300 --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --non-interactive
rtk aspire describe --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --non-interactive
```

L'API locale utilise `Authentication:Provider=Keycloak`, l'autorité HTTPS du realm `vpd`
publiée par Aspire et l'audience `vpd-api`. En mode isolé, Aspire choisit un port local
aléatoire et transmet le même endpoint à l'API; `Get-LocalApiToken.ps1` le découvre avec
`aspire describe`. Le helper accepte aussi `-KeycloakBaseUrl` pour un endpoint fourni à la main.
Le code ne tolère HTTP que pour une autorité loopback; une autorité Keycloak distante doit
utiliser HTTPS. Bicep fixe explicitement `Authentication__Provider=Entra` pour les Container
Apps Azure.

L'AppHost expose aussi le contrôle de santé API `/health` au dashboard Aspire. Une modification
de `vpd-realm.json` n'est importée qu'après réinitialisation du volume Keycloak. Pour la charger
à nouveau sans effacer SQL :

```powershell
rtk aspire stop --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --force
rtk docker volume ls --filter name=keycloak-data
rtk docker volume rm <nom-exact-du-volume-keycloak>
```

Choisis uniquement le volume dont le nom commence par `vole_papillon_damour.apphost-` et
remplace le dernier argument par son nom exact. Ne supprime pas le volume `sql-server-data`
si tu veux garder les données SQL locales, puis redémarre Aspire.

## Personas disponibles

Le mot de passe de toutes les identités ci-dessous est `VpdLocalDev!2026`. Ces comptes,
leurs mots de passe et le client à accès direct ne sont que des données de test locales.

| Persona | Identifiant | Rôles API |
|---|---|---|
| Administration | `local-admin` | `Administration`, `LivresRares`, `Tri`, `Caisse` |
| Bénévole tri | `local-tri` | `Tri` |
| Bénévole caisse | `local-caisse` | `Caisse` |
| Adhérente | `local-member` | aucun |

Pour obtenir un jeton sans interaction navigateur, utilise le helper qui lit le compte et
son mot de passe directement depuis le realm importé et résout l'endpoint Keycloak Aspire :

```powershell
$token = rtk powershell -NoProfile -File .\tools\Get-LocalApiToken.ps1 -Persona tri-volunteer
$headers = @{ Authorization = "Bearer $token" }
```

Récupère l'URL de l'API avec `rtk aspire describe`, puis appelle l'endpoint couvert par la
feature avec `$headers`. Vérifie aussi le refus attendu avec le persona `member` ou un
persona qui ne porte pas le rôle exigé.

Le client `vpd-local-test` utilise le flux OAuth2 Direct Access Grant pour rendre cette
récupération non interactive. Ce flux est réservé à cet environnement local; il n'est pas
configuré dans Bicep. Le BackOffice local utilise un client navigateur séparé,
`vpd-backoffice-local`, avec Authorization Code et PKCE S256, sans secret client ni accès
direct par mot de passe. Son URI de redirection est limitée à `http://localhost:4200/*`.
Le scope `vpd-api-access` est ajouté par défaut; il émet l'audience `vpd-api`, les rôles VPD
dans `roles`, le courriel dans `email`, les noms dans `given_name` et `family_name`, ainsi
que l'identifiant externe stable (UUID) dans `oid`. Ces claims sont attendus par l'API et
permettent au BackOffice d'afficher le compte connecté.

Keycloak précise que Direct Access Grant ne fait pas partie d'OpenID Connect et qu'il est
retiré d'OAuth 2.1; ce client local ne doit pas devenir un flux de connexion applicatif
([documentation Keycloak](https://www.keycloak.org/securing-apps/oidc-layers)).

## Ajouter une persona

Ajoute l'utilisateur dans `vpd-realm.json` avec un UUID `oid` unique, une adresse courriel,
un prénom, un nom, un mot de passe local et les rôles client de `vpd-api` voulus. Ajoute
ensuite son nom dans `tools/Get-LocalApiToken.ps1` si le helper doit l'exposer comme choix.
Réinitialise le seul volume Keycloak pour importer le realm modifié. Ne donne pas à une
persona les rôles d'administration Keycloak : les rôles `Administration`, `Tri`, `Caisse`
et `LivresRares` sont des rôles métier de l'API.

En local, le BackOffice utilise Keycloak pour sa connexion navigateur et les API; le Website,
Catalog et Scan conservent leur connexion Entra. En production, tous les frontends gardent
Entra. Le realm charge `vpd-backoffice-local` uniquement quand son volume est initialisé.
Un volume Keycloak déjà persistant ne récupère pas les nouveaux clients automatiquement :
ajoute le client avec les mêmes paramètres dans la console d'administration, ou réimporte
le realm après avoir arrêté Aspire et supprimé uniquement le volume Keycloak selon la
procédure ci-dessus. Le client `vpd-local-test` reste réservé aux appels API automatisés.
