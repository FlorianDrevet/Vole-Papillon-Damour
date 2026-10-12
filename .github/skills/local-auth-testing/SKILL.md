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
2. Start Aspire from the repository root with `rtk aspire start --isolated --non-interactive`.
3. Wait for `keycloak` and `api` with
   `rtk aspire wait <resource> --timeout 300 --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --non-interactive`.
4. Read the API URL from
   `rtk aspire describe --apphost src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj --non-interactive`.
5. Get a local access token with
   `rtk powershell -NoProfile -File tools/Get-LocalApiToken.ps1 -Persona <admin|tri-volunteer|cashier|member>`.
6. Send it as `Authorization: Bearer <token>` to the endpoint under test. Check one
   authorized persona and one persona that should receive 403 where that distinction
   applies.

The helper reads the seeded username and password from the realm file; do not copy a token
into source control, logs, documentation, or another environment. The local Keycloak authority
uses HTTP only on loopback; remote Keycloak authorities must use HTTPS.

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
it by name, then restart Aspire so Keycloak imports the updated realm. The audience mapper
must continue to issue `vpd-api`; the role mapper must continue to issue a top-level `roles`
claim. API policies use the same role names as Entra: `Administration`, `LivresRares`,
`Tri`, and `Caisse`.

## Boundaries

- Keep `Authentication:Provider=Keycloak` in local development only. Bicep explicitly sets
  `Authentication__Provider=Entra`; do not add Keycloak configuration to cloud parameters.
- The seeded password and `vpd-local-test` direct-grant client are local test fixtures.
  Never use them as a production login flow or put their values in deployment settings.
- Tests that call Microsoft Graph, such as account provisioning/deletion, still need a
  dedicated test seam or mocks; Keycloak does not emulate Entra Graph.
- The Angular apps still authenticate through MSAL/Entra. Use these personas for direct API
  tests, not as a claim that the browser apps support Keycloak login.
