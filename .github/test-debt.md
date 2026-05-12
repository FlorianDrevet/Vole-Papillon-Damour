# Test Debt Tracker

> Tracks known test gaps detected during implementation.
> Each entry records the symbol/module, the reason for deferral, and a target date.

| Date | Symbol / Module | Reason | Target |
|------|-----------------|--------|--------|
| 2026-05-12 | Vole_Papillon_Damour.Infrastructure local fallbacks | `DisabledEmailService` and `DisabledOcrService` were added to keep local startup working without OCR/Email secrets, but no Infrastructure test project is in scope for TDD coverage in this task. | Add focused Infrastructure tests when a dedicated backend test project is introduced |
