# RimWorld Loadable Mod Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a minimal Living World RimWorld mod package that can be copied into `C:\Games\RimWorld\Mods\LivingWorld`, enabled in RimWorld 1.6, and visibly loaded.

**Architecture:** Keep `LivingWorld.Core` as the current net8 simulation/test project. Add a thin `LivingWorld.RimWorld` net472 loader assembly that references RimWorld managed assemblies and only owns mod startup/settings UI for now. Package source mod metadata under `mod/` and install compiled assemblies into the local RimWorld `Mods` folder.

**Tech Stack:** C#, SDK-style csproj, net472 for RimWorld loader, RimWorld 1.6 managed assemblies from `C:\Games\RimWorld\RimWorldWin64_Data\Managed`, PowerShell install script, console test harness.

---

### Task 1: Source Mod Metadata Contract

**Files:**
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Tests\Program.cs`
- Create: `C:\Games\LivingWorld\mod\About\About.xml`
- Create: `C:\Games\LivingWorld\mod\LoadFolders.xml`

- [ ] **Step 1: Write failing tests**

Add tests that require `mod/About/About.xml` to define package id `nakhmedov.livingworld`, supported RimWorld version `1.6`, and `mod/LoadFolders.xml` to load `/` plus `1.6`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj`
Expected: FAIL because `mod/About/About.xml` does not exist.

- [ ] **Step 3: Add source metadata**

Create About.xml and LoadFolders.xml with no required dependencies. Harmony remains optional until patches are added.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj`
Expected: PASS.

### Task 2: RimWorld Loader Assembly

**Files:**
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Tests\Program.cs`
- Create: `C:\Games\LivingWorld\src\LivingWorld.RimWorld\LivingWorld.RimWorld.csproj`
- Create: `C:\Games\LivingWorld\src\LivingWorld.RimWorld\LivingWorldMod.cs`
- Modify: `C:\Games\LivingWorld\LivingWorld.sln`

- [ ] **Step 1: Write failing tests**

Add tests that require the loader project file, `TargetFramework` `net472`, references to `Assembly-CSharp` and `UnityEngine.CoreModule`, and source text containing `sealed class LivingWorldMod : Mod`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj`
Expected: FAIL because loader project does not exist.

- [ ] **Step 3: Add loader project and class**

Create a minimal `Verse.Mod` subclass that logs startup and exposes settings category/window.

- [ ] **Step 4: Add project to solution**

Run: `dotnet sln LivingWorld.sln add src\LivingWorld.RimWorld\LivingWorld.RimWorld.csproj`

- [ ] **Step 5: Run test and build**

Run: `dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj`
Run: `dotnet build src\LivingWorld.RimWorld\LivingWorld.RimWorld.csproj`
Expected: both pass.

### Task 3: Local RimWorld Install Script

**Files:**
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Tests\Program.cs`
- Create: `C:\Games\LivingWorld\tools\install-rimworld-mod.ps1`
- Output: `C:\Games\RimWorld\Mods\LivingWorld`

- [ ] **Step 1: Write failing tests**

Add tests that require `tools/install-rimworld-mod.ps1` and check it contains the source mod path, local RimWorld mods destination, and compiled DLL copy into `1.6\Assemblies`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj`
Expected: FAIL because install script does not exist.

- [ ] **Step 3: Add install script**

Script builds `LivingWorld.RimWorld`, recreates only `C:\Games\RimWorld\Mods\LivingWorld`, copies `mod/*`, creates `1.6\Assemblies`, and copies `LivingWorld.RimWorld.dll`.

- [ ] **Step 4: Run test and install**

Run: `dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj`
Run: `powershell -ExecutionPolicy Bypass -File tools\install-rimworld-mod.ps1`
Expected: tests pass and installed mod folder contains About.xml, LoadFolders.xml, and DLL.

### Task 4: Final Verification

**Files:**
- Installed: `C:\Games\RimWorld\Mods\LivingWorld`

- [ ] **Step 1: Build solution**

Run: `dotnet build LivingWorld.sln`
Expected: 0 warnings, 0 errors.

- [ ] **Step 2: Run tests**

Run: `dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 3: Validate installed mod**

Run PowerShell checks for:
- `C:\Games\RimWorld\Mods\LivingWorld\About\About.xml`
- `C:\Games\RimWorld\Mods\LivingWorld\LoadFolders.xml`
- `C:\Games\RimWorld\Mods\LivingWorld\1.6\Assemblies\LivingWorld.RimWorld.dll`

Expected: all files exist.
