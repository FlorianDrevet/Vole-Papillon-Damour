---
name: book-data-reset
description: Reset deployed Vole-Papillon-Damour book and scan data, deleting only Entra-disabled SQL user profiles while retaining active users, the named iCloud keeper, and association events and news. Use only for an explicit reset request.
---

# Reset the books database

Use this skill only for a reset the user has explicitly requested. Read `NEXT.md` first; it is the source of truth for the deployed environment and outstanding Azure changes.

The current deployed environment is DEV: resource group `rg-vpd-dev`, SQL server `vpd-sql-dev`, database `vole-papillon-damour-db`. Recheck `NEXT.md` and the deployment configuration before every run. Do not infer that another environment is in scope.

## Procedure

1. Follow `.github/skills/delivery-workflow/SKILL.md` for repository changes. Keep the reset procedure and SQL script in the same reviewed PR.
2. Verify the Azure SQL point-in-time restore path with the existing **Database - verify point-in-time restore** workflow. Record its run URL and confirm it completed successfully before applying the reset.
3. Pause book-writing activity for the execution window across Scan, Catalog administration, and member-account book actions. Stop active scan/cash sessions, wait for all synchronization to finish, and pause the book-alert Worker so it cannot send queued test notifications. A database reset cannot clear unsynchronized data held by a Scanette browser or device. Resume the Worker and clients after verification.
4. Once the reviewed workflow is available on `main`, dispatch **Database - reset books data** with `target_confirmation=vole-papillon-damour-db` and `apply_reset=false`. The workflow reads Entra `accountEnabled` state and reports the counts of disabled profiles to remove, SQL profiles to retain, unmatched disabled identities, book tables, and preserved event/news rows. Review the SHA-256 of the disabled Entra identity set and ensure the iCloud keeper account resolves exactly once.
5. For an authorized reset, dispatch the workflow again with `apply_reset=true` and the exact `disabled_users_confirmation` hash from the reviewed dry run. The workflow re-reads Entra; if the disabled-user set changed, it stops before opening the database firewall. The SQL transaction checks the database name, known user foreign keys, triggers on tables it changes, retained user profiles, and event/news row counts. It rolls back on any failed check. The workflow opens a temporary firewall rule for the runner and removes it in an `always()` cleanup step.
6. Confirm the committed result: all listed book and scan tables and queued book-alert emails are empty; only the selected disabled Entra profiles (excluding the iCloud keeper) were removed from `dbo.Users`; every other SQL user profile remains; event and news counts match their pre-reset counts. Book/scan statistics derived from those tables are empty, recommendation generation state is reset, and Azure telemetry is retained. Keep the output and update `NEXT.md` with the date, target, result, and PITR run URL.
7. Repeat the same procedure after the test phase. The reset is idempotent; it does not need a schema migration. Keep `apply_reset=false` for the preview run, then use `true` only for the explicitly requested reset.

## Scope

The script protects `floriandrevet@icloud.com` in `dbo.Users`. `dbo.Users` has no enabled/disabled column, so the workflow queries Microsoft Graph and supplies only object IDs whose `accountEnabled` value is `false`; the SQL script deletes matching `ExternalId` profiles except the iCloud keeper. All other active or unmatched SQL profiles are retained. The reset never deletes or disables Entra identities. It clears catalog, scan, movement, rare-book, manual book request and requester records, member-selection, checkout, watchlist, not-found-report, recommendation, and book-alert data. Manual requests are deleted before requester rows because `ManualBookRequests.RequesterId` has a restrictive foreign key. It keeps `dbo.AssociationSettings` and reassigns its audit user to the retained iCloud account only when needed. The recommendation-generation singleton is reset instead of removed.

It never writes to `dbo.AssoEvents`, its `Parties`, `LinePartie`, or `Lots` tables, `dbo.Actualities`, or `dbo.SocialPostImports`. The script also leaves products and orders untouched. It operates on SQL rows only: Entra identities and blobs, including rare-book photos in Storage, are outside this request and remain in place. Existing account-deletion outbox jobs are preserved; the reset does not delete Entra accounts.

If the schema has a new book table, an unexpected foreign key into `Users`, a trigger on a table being changed, a missing iCloud keeper account, or a different target database, stop and update the reviewed scope before execution. Never bypass a failed guard by disabling constraints or triggers.
