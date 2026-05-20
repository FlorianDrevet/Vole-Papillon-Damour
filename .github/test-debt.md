# Test Debt Tracker

> Tracks known test gaps detected during implementation.
> Each entry records the symbol/module, the reason for deferral, and a target date.

| Date | Symbol / Module | Reason | Target |
|------|-----------------|--------|--------|
| 2026-05-12 | Vole_Papillon_Damour.Infrastructure local fallbacks | `DisabledEmailService` was added to keep local startup working without email secrets, but no Infrastructure test project is in scope for TDD coverage in this task. | Add focused Infrastructure tests when a dedicated backend test project is introduced |
| 2026-05-12 | BackOffice Angular 21 migration | The Angular migration changed bootstrapping and multiple component files, but `src/BackOffice/tsconfig.spec.json` currently matches no `*.spec.ts` files, so `npm test` cannot exercise this surface yet. | Add focused BackOffice component and service specs before the next behavior change in the admin UI |
| 2026-05-12 | Mailing-list removal in Application/Infrastructure | The mailing-list CQRS slice and Azure email/table integrations were removed, but there is no dedicated backend test project covering Application and Infrastructure handlers/services for this slice in the current solution. | Add focused backend tests if a non-domain test project is introduced for Application/Infrastructure changes |
| 2026-05-12 | Website public shell visual refresh | The first Website modernization wave changed shared navigation and home templates/styles only, and there are still no focused Website specs covering shell rendering, mobile-menu states, or responsive presentation. | Add focused Website shell specs before the next behavior change in navigation or home |
| 2026-05-18 | Bingo-card OCR removal in Api/Application/Infrastructure | The OCR bingo-card route and service slice are being removed, but the solution still has no dedicated backend test project for Api, Application, or Infrastructure, so strict TDD is not feasible without introducing out-of-scope test infrastructure. | Add focused backend tests when a non-domain test project is introduced for CQRS handlers and infrastructure adapters |
| 2026-05-18 | BackOffice OCR admin surface removal | Removing the BackOffice scan dialog, facade, and shared bingo-card components changes executable Angular code, but `src/BackOffice/tsconfig.spec.json` still matches no `*.spec.ts` files, so there is no real spec harness for RED/GREEN on this slice without adding out-of-scope test infrastructure. | Add focused BackOffice specs for `vpd-events` and shared dialogs before the next admin behavior change |
