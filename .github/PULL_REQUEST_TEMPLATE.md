## Summary

What does this change and why? Which simulation layer(s) does it touch?

## Related issues

Closes #...

## How it was tested

- [ ] `dotnet build LivingWorld.sln` — 0 warnings, 0 errors
- [ ] Test harness passes (`dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`)
- [ ] New behavior is covered by a test that was written first and watched fail (TDD)

## Design checklist

- [ ] Ledger-first: nothing appears from nowhere; entities trace back to `WorldState`
- [ ] Vanilla is never cancelled — the change augments or steps aside, and fails open
- [ ] Harmony patches (if any) are thin adapters; real logic lives in testable `Core`
- [ ] Save compatibility preserved (serialization is additive / back-compatible)

## Clean-room checklist (original implementation)

- [ ] Code was written from scratch for Living World
- [ ] No source, DLLs, XML, formulas, balancing tables, text or assets copied from other mods
- [ ] Class names, APIs and data models are original
- [ ] Any inspiration is credited only as research / prior art

## Notes for reviewers

Anything you want reviewers to focus on, trade-offs, or follow-ups.
