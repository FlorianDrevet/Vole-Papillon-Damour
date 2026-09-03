# Memory Changelog

| Date | Change |
|------|--------|
| 2026-09-03 | Fixed the BackOffice blank-page refresh race by awaiting `MsalService.initialize()` through Angular's application initializer before cached-account services and MSAL redirect handling start. |
| 2026-09-03 | Fixed Entra app-role authorization for BackOffice writes: inbound claim mapping is disabled for the Entra bearer scheme so the token's `roles` claim satisfies the `Administration` policy. |
| 2026-09-03 | Corrected the Entra v2 API audience configuration for BackOffice writes: API validation now receives the application ID carried by the token, while the delegated `api://.../access_as_user` scope remains unchanged. |
| 2026-09-03 | Consolidated the current delivery state: refreshed Website editorial content and association imagery; harmonized the four Maxence daily-life detail pages with a shared chapter header, return navigation, and wider reading column; staged Entra/MSAL identity across API, BackOffice, and MAUI with account deletion; delivered and deployed the Scan ISBN probe plus the private Worker; fixed BackOffice bootstrap, Scan async rendering, and cover fallback. Local validation results are recorded, including the remaining MAUI/CI target drift (`net10.0-android` in the project versus `net9.0-android` in the workflow). |
| 2026-09-02 | Advanced the platform foundation with Entra/Graph setup, health probes, the Azure SQL S1 parameter, SharedUi linking, Graphify, CI build coverage, public product visibility, GA4/SEO, event and design refinements, and the bourse aux livres technical documentation. |
| 2026-09-01 | Added concrete association actions and supplied photos, skeleton loading states, production-host configuration, and the first complete bourse aux livres functional/technical specifications. |
| 2026-08-31 | Clarified the bourse aux livres scan workflow, pre-scan availability modes, deferred valuation, and delayed post-session alerts. |
| 2026-08-25 | Updated Website legal information and contact details for the compliance pages. |
| 2026-08-24 | Improved Website navigation, presentation, event locations, viewport/accessibility behavior, responsive layouts, and production data snapshots; the association review-of-press page remains a placeholder. |
| 2026-08-23 | Hardened deployed Website SSR proxy/host handling, frontend styles and consent shell, container deployment defaults, and repository cleanup.
