# Authentification locale de l'API

L'API garde Microsoft Entra pour les déploiements Azure. En local, l'AppHost Aspire
démarre Keycloak et charge `src/Backend/Vole_Papillon_Damour.AppHost/Realms/vpd-realm.json`.
Le realm fournit des identités et des rôles déterministes pour les tests de l'API.

## Démarrer les services

Docker Desktop doit exposer son moteur Linux pour que les conteneurs Aspire démarrent.
Vérifie-le avec `rtk docker ps` avant d'attendre Keycloak.

Démarre l'AppHost depuis la racine du dépôt. Aspire génère et conserve un mot de passe
administrateur local si l'Admin Console est nécessaire ; les personas de test et le helper
ci-dessous n'en ont pas besoin. En worktree, garde le mode isolé :

```powershell
rtk aspire start --isolated --non-interactive
rtk aspire wait keycloak --timeout 300 --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --non-interactive
rtk aspire wait api --timeout 300 --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --non-interactive
rtk aspire describe --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --non-interactive
```

L'API locale utilise `Authentication:Provider=Keycloak`, l'autorité
`http://localhost:8080/realms/vpd` et l'audience `vpd-api`. Le code n'autorise HTTP que
pour une autorité loopback; toute autorité Keycloak distante doit utiliser HTTPS. Bicep fixe explicitement
`Authentication__Provider=Entra` pour les Container Apps Azure.

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
son mot de passe directement depuis le realm importé :

```powershell
$token = rtk powershell -NoProfile -File .\tools\Get-LocalApiToken.ps1 -Persona tri-volunteer
$headers = @{ Authorization = "Bearer $token" }
```

Récupère l'URL de l'API avec `rtk aspire describe`, puis appelle l'endpoint couvert par la
feature avec `$headers`. Vérifie aussi le refus attendu avec le persona `member` ou un
persona qui ne porte pas le rôle exigé.

Le client `vpd-local-test` utilise le flux OAuth2 Direct Access Grant pour rendre cette
récupération non interactive. Ce flux est réservé à cet environnement local; il n'est pas
configuré dans Bicep. La validation des jetons garde l'audience et les rôles. Le mapper
Keycloak expose les rôles VPD dans `roles`, l'adresse courriel dans `email` et l'identifiant
externe stable (UUID) dans `oid`, comme attendus par l'API.

Keycloak précise que Direct Access Grant ne fait pas partie d'OpenID Connect et qu'il est
retiré d'OAuth 2.1; ce client local ne doit pas devenir un flux de connexion applicatif
([documentation Keycloak](https://www.keycloak.org/securing-apps/oidc-layers)).

## Ajouter une persona

Ajoute l'utilisateur dans `vpd-realm.json` avec un UUID `oid` unique, une adresse courriel,
un prénom, un nom, un mot de passe local et les rôles client de `vpd-api` voulus. Ajoute
ensuite son nom dans `tools/Get-LocalApiToken.ps1` si le helper doit l'exposer comme choix.
Redémarre Aspire pour importer le realm modifié. Ne donne pas à une persona les rôles
d'administration Keycloak : les rôles `Administration`, `Tri`, `Caisse` et `LivresRares`
sont des rôles métier de l'API.

Les frontends Angular conservent leur configuration MSAL/Entra locale. Les jetons Keycloak
de ce dispositif servent aux appels de test directs vers l'API.
