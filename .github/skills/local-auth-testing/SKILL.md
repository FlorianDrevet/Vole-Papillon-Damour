---
name: local-auth-testing
description: >-
  Use for local API development and automated tests that need a signed-in VPD member,
  volunteer, cashier, or administrator without Microsoft Entra. Loads the Aspire Keycloak
  realm, obtains a deterministic persona token, and preserves Azure Entra configuration.
---

# Local API authentication testing

Use this skill whenever a backend task needs an authenticated API request, an authorization
check, or a test persona. Read `docs/local-authentication.md` for the exact configuration
and current personas.

## Start and acquire a persona token

1. Verify the configured container runtime with `rtk docker ps`. If its daemon is
   unavailable, report runtime validation as pending and continue with static checks.
2. If the AppHost has no `Parameters:keycloak-admin-password` user secret yet, set one with
   `rtk dotnet user-secrets set "Parameters:keycloak-admin-password" "<local strong password>" --project src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj`.
   This password is only for the local Keycloak console; persona token retrieval does not use it.
3. Start Aspire from the repository root with `rtk aspire start --isolated --non-interactive`.
4. Wait for `keycloak` and `api` with
   `rtk aspire wait <resource> --timeout 300 --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --non-interactive`.
5. Read the API URL from
   `rtk aspire describe --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --non-interactive`.
6. Get a local access token with
   `rtk powershell -NoProfile -File tools/Get-LocalApiToken.ps1 -Persona <admin|tri-volunteer|cashier|member>`.
7. Send it as `Authorization: Bearer <token>` to the endpoint under test. Check one
   authorized persona and one persona that should receive 403 where that distinction
   applies.

The helper reads the seeded username and password from the realm file and discovers the
current Keycloak endpoint from Aspire; do not copy a token into source control, logs,
documentation, or another environment. The AppHost uses HTTPS, while the API also permits
HTTP only for a loopback Keycloak authority; remote authorities must use HTTPS.

## Persona changes

Personas live declaratively in
`src/Backend/Vole_Papillon_Damour.AppHost/Realms/vpd-realm.json`. Add an API test identity
there with:

- a unique UUID in the `oid` user attribute, because VPD member identity and rate limiting
  use that claim;
- `email`, `firstName`, and `lastName` values for member identity mapping;
- the intended API roles under `clientRoles.vpd-api`;
- a fixture-only password credential.

Add the persona name to `tools/Get-LocalApiToken.ps1` when agents should be able to request
it by name. Keycloak imports the realm only into an empty persistent data volume; after
changing the fixture, reset only the Keycloak volume as described in `docs/local-authentication.md`.
The audience mapper must continue to issue `vpd-api`; the role mapper must continue to issue
a top-level `roles` claim. The `vpd-api-access` scope must also map `email`, `given_name`,
`family_name`, and `oid` into access tokens for member identity. API policies use the same
role names as Entra: `Administration`, `LivresRares`, `Tri`, and `Caisse`.

## Boundaries

- Keep `Authentication:Provider=Keycloak` in local development only. Bicep explicitly sets
  `Authentication__Provider=Entra`; do not add Keycloak configuration to cloud parameters.
- The seeded password and `vpd-local-test` direct-grant client are local test fixtures.
  Never use them as a production login flow or put their values in deployment settings.
- Tests that call Microsoft Graph, such as account provisioning/deletion, still need a
  dedicated test seam or mocks; Keycloak does not emulate Entra Graph.
- BackOffice local browser sign-in uses `vpd-backoffice-local` with Authorization Code +
  PKCE S256; do not use the `vpd-local-test` direct-grant client for browser login. Website,
  Catalog, and Scan still use MSAL/Entra locally, and every production frontend uses Entra.
- These personas and the `vpd-local-test` client also support direct API tests. Keycloak does
  not replace Entra for account provisioning or Microsoft Graph operations.
