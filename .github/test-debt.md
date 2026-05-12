# Test Debt Tracker

> Tracks known test gaps detected during implementation.
> Each entry records the symbol/module, the reason for deferral, and a target date.

| Date | Symbol / Module | Reason | Target |
|------|-----------------|--------|--------|
| 2026-05-12 | Vole_Papillon_Damour.Infrastructure local fallbacks | `DisabledEmailService` and `DisabledOcrService` were added to keep local startup working without OCR/Email secrets, but no Infrastructure test project is in scope for TDD coverage in this task. | Add focused Infrastructure tests when a dedicated backend test project is introduced |
| 2026-05-12 | BackOffice Angular 21 migration | The Angular migration changed bootstrapping and multiple component files, but `src/BackOffice/tsconfig.spec.json` currently matches no `*.spec.ts` files, so `npm test` cannot exercise this surface yet. | Add focused BackOffice component and service specs before the next behavior change in the admin UI |
| 2026-05-12 | Mailing-list removal in Application/Infrastructure | The mailing-list CQRS slice and Azure email/table integrations were removed, but there is no dedicated backend test project covering Application and Infrastructure handlers/services for this slice in the current solution. | Add focused backend tests if a non-domain test project is introduced for Application/Infrastructure changes |
