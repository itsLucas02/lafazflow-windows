# Task: Windows Offline MVP Planning

## Plan
- [x] Review current Windows repo instructions and lessons.
- [x] Inspect macOS reference behavior for recorder UI, state, hotkeys, and paste flow.
- [x] Verify local Windows development toolchain.
- [x] Write Windows MVP design spec.
- [x] Write first implementation plan.
- [x] Review docs for ambiguity, accidental secrets, and public-readiness.
- [ ] Commit and push planning docs for owner review.

## Review
- Design spec written at `docs/superpowers/specs/2026-05-12-windows-mvp-design.md`.
- Implementation plan written at `docs/superpowers/plans/2026-05-12-windows-mvp.md`.
- Local toolchain check: .NET SDK 9.0.313 is installed; CMake is not installed.
- Public-readiness scan found no credentials. Matches are documentation references to words such as "secret", "token", and `CancellationToken`.
