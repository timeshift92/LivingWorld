# Contributing to Living World

Thanks for your interest in Living World — a standalone, persistent world-simulation
core for RimWorld. This guide explains how to build, test and submit changes.

## Guiding principles

Living World follows a few hard rules. Read them before writing code.

- **Original implementation only.** Living World is a clean-room project. Study ideas
  from other mods, but never copy source code, DLLs, XML, formulas, balancing tables,
  text or assets. See [docs/research/legal-position.md](docs/research/legal-position.md)
  and [docs/research/implementation-policy.md](docs/research/implementation-policy.md).
- **Ledger-first.** `WorldState` is the source of truth. Nothing appears from nowhere:
  every raider, trader, caravan and animal must trace back to a real world entity.
- **Thin Harmony adapters.** Keep game-facing patches small and additive (prefer
  `Postfix`/observation over `Prefix`/replacement). Put real logic in pure,
  unit-testable `LivingWorld.Core` services and let patches call into them.
- **Never break vanilla.** Living World augments or steps aside; it must not cancel
  vanilla content or leave the game in a broken state when the ledger is empty.

## Getting started

Prerequisites: the .NET SDK (the solution multi-targets for the RimWorld runtime).

```bash
# Build everything
dotnet build LivingWorld.sln

# Run the test harness (custom console runner, not xUnit)
dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj -c Debug
```

A change is ready when the build reports **0 warnings, 0 errors** and the test
harness prints all tests passing.

## Test-driven development

Every feature and bug fix is expected to follow TDD:

1. Add a failing test in `src/LivingWorld.Tests/Program.cs` (register it in the list
   at the top and add its method).
2. Watch it fail for the right reason.
3. Write the minimal code to pass.
4. Keep the whole suite green.

Prefer testing pure `Core` logic. RimWorld-layer patches are intentionally thin so
their decision logic lives in `Core` and can be tested without the game running.

## Pull requests

- Branch from `main`, keep changes focused, and open a PR against `main`.
- Fill in the pull request template, including the clean-room checklist.
- Make sure the build is clean and all tests pass before requesting review.
- Write clear commit messages; describe the "why", not just the "what".

## Reporting bugs and requesting features

Use the issue templates. For security-sensitive reports, follow
[SECURITY.md](SECURITY.md) instead of opening a public issue.

## Code of conduct

By participating you agree to abide by the [Code of Conduct](CODE_OF_CONDUCT.md).
