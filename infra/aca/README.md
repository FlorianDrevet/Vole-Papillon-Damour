# ACA deployment for Vole-Papillon-Damour

This folder complements the Infra Flow Sculptor project created through MCP.

## Infra Flow Sculptor project

- Project name: `Vole-Papillon-Damour`
- Project ID: `c4761bed-a6a6-45c5-9c9d-799def6a6683`
- Layout: `AllInOne`
- Environments: `dev`, `prod`
- Shared infra config created by Infra Flow Sculptor: `Vole-Papillon-Damour-config`
- Application infra config created manually via MCP: `VpdApplications`
- Shared resource group created via MCP: `rg-vpd-common`
- Application resource groups modelled locally: `rg-vpd-dev`, `rg-vpd-prod`

## Current Infra Flow Sculptor state

- Infra Flow Sculptor created the project and generated Bicep metadata for the shared resources.
- ACR and Log Analytics Workspace were created in the project.
- Automatic creation of the Container Apps environment and the three Container Apps failed server-side with a `CompileException`.
- The local Bicep template in this folder covers that missing ACA part explicitly.

## Docker images

Build contexts:

- API: `docker build -f .\src\Backend\Vole_Papillon_Damour.Api\Dockerfile .\src\Backend`
- BackOffice: `docker build -f .\src\BackOffice\Dockerfile .\src`
- Website: `docker build -f .\src\Website\Dockerfile .\src`

The frontend images intentionally use the `src` folder as Docker build context so the path-mapped shared UI library under `src/SharedUi/` is available during Angular compilation.

The frontend Dockerfiles patch the production Angular environment at build time:

- `API_URL` is injected into the BackOffice and Website production builds.
- `WEBSITE_URL` is injected into the BackOffice production build.

Use `infra\aca\build-and-push.ps1` to build and push the three images to ACR.

## Deploy Bicep

Update the ACR name in both parameter files to a globally unique value before the first deployment.

Development:

```powershell
az deployment sub create --location FranceCentral --template-file .\infra\aca\main.bicep --parameters .\infra\aca\parameters\main.dev.bicepparam
```

Production:

```powershell
az deployment sub create --location FranceCentral --template-file .\infra\aca\main.bicep --parameters .\infra\aca\parameters\main.prod.bicepparam
```

## Expected image tags

- `vpd-api:dev`
- `vpd-backoffice:dev`
- `vpd-website:dev`
- `vpd-api:prod`
- `vpd-backoffice:prod`
- `vpd-website:prod`
