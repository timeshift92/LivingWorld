using System;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class LivingWorldWorldComponent : WorldComponent
{
    private const int TicksPerDay = 60_000;
    private const int MaxCatchUpSimulationDays = 7;
    private const int BirthIntervalDays = 30;
    private const int AgeIntervalDays = 60;
    private const int NaturalDeathAge = 85;
    private const int MaxNaturalDeathsPerDay = 5;
    private const string FoodResourceKey = "PackagedSurvivalMeal";
    private const string SteelResourceKey = "Steel";
    private const string MedicineResourceKey = "MedicineIndustrial";
    private const string ComponentResourceKey = "ComponentIndustrial";

    private readonly World rimWorld;
    private bool bootstrapped;
    private string serializedState = string.Empty;
    private int lastSimulatedDay;
    private int cachedWorldPopulation;
    private int cachedTargetPopulation;
    private bool? rimWarActive;
    private bool? empireActive;
    private int lastWorldWarLetterTick = int.MinValue;
    private int notifiedCaptureCount;

    public LivingWorldWorldComponent(World world)
        : base(world)
    {
        rimWorld = world;
        Instance = this;
        State = new WorldState(ResolveWorldSeed(rimWorld));
    }

    public static LivingWorldWorldComponent? Instance { get; private set; }

    public WorldState State { get; private set; }

    public bool IsBootstrapped => bootstrapped;

    public int LastWorldSettlementSourceCount { get; private set; }

    public int TotalWorldObjects { get; private set; }

    public int VanillaSettlementSourceCount { get; private set; }

    public int FactionWorldObjectSourceCount { get; private set; }

    public int ImportableWorldObjectSourceCount { get; private set; }

    public int ScanErrorCount { get; private set; }

    public string RejectedWorldObjectTypes { get; private set; } = "none";

    public string LastBootstrapError { get; private set; } = string.Empty;

    public string LastBootstrapSource { get; private set; } = "not-started";

    public string LastBootstrapStatus { get; private set; } = "not-started";

    public bool WantsDrifterArrival
    {
        get
        {
            if (!bootstrapped)
            {
                return false;
            }

            var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
            if (!settings.drifterFlowEnabled)
            {
                return false;
            }

            return State.Drifters.Count > 0;
        }
    }

    // True when Rim War is driving world factions, so the Living World world-war loop stays off.
    public bool IsRimWarActive => RimWarIsActive;

    // True when Empire is active. Empire manages the player's own empire (player-faction
    // settlements, which K4 already keeps out of the ledger), so there is nothing to disable —
    // this is surfaced only so the UI can tell the player who owns what.
    public bool IsEmpireActive => EmpireIsActive;

    public string GetSummary()
    {
        return "LW_SummaryLine".Translate(
            State.Settlements.Count.Named("settlements"),
            State.Citizens.Count.Named("citizens"),
            State.Armies.Count.Named("armies"),
            State.ProductionProfiles.Count.Named("productionProfiles"),
            State.IntelReports.Count.Named("intelReports"),
            State.RaidOpportunities.Count(opportunity => opportunity.Status == RaidOpportunityStatus.Active).Named("activeRaidOpportunities"),
            State.Events.Count.Named("events"));
    }

    public string GetDiagnosticSummary()
    {
        return "LW_DiagnosticLine".Translate(
            bootstrapped.Named("bootstrapped"),
            LastBootstrapSource.Named("source"),
            LastBootstrapStatus.Named("status"),
            LastWorldSettlementSourceCount.Named("worldObjects"),
            TotalWorldObjects.Named("totalWorldObjects"),
            VanillaSettlementSourceCount.Named("vanillaSettlements"),
            FactionWorldObjectSourceCount.Named("factionWorldObjects"),
            ImportableWorldObjectSourceCount.Named("importableWorldObjects"),
            ScanErrorCount.Named("scanErrors"),
            LastBootstrapError.Named("error"),
            RejectedWorldObjectTypes.Named("rejectedTypes"));
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        BootstrapFromRimWorldSettlements();
        RepairMissingProductionProfilesFromRimWorldSettlements();
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        if (!bootstrapped)
        {
            return;
        }

        var currentTick = Find.TickManager?.TicksGame ?? 0;
        var currentDay = currentTick / TicksPerDay;
        if (currentDay <= 0 || currentDay <= lastSimulatedDay)
        {
            return;
        }

        var simulatedDays = 0;
        while (lastSimulatedDay < currentDay && simulatedDays < MaxCatchUpSimulationDays)
        {
            lastSimulatedDay++;
            SimulateWorldDay(lastSimulatedDay);
            simulatedDays++;
        }

        if (lastSimulatedDay < currentDay && (LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Warning($"[LivingWorld] Daily simulation catch-up capped at {MaxCatchUpSimulationDays} days. Remaining days will continue next ticks.");
        }

        // One rate-limited letter AFTER the whole catch-up loop — never one per simulated day.
        MaybeSendWorldWarLetter(currentTick);
    }

    // Surfaces the world war to the player as an occasional, rate-limited letter that summarizes
    // settlements that changed hands. Silent only when the loop itself is off (disabled or ceded
    // to Rim War) or during initial seeding; captures accumulate across the cooldown so nothing is
    // lost, and the whole daily catch-up produces at most one letter (no flood).
    private void MaybeSendWorldWarLetter(int currentTick)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.worldWarEnabled || IsRimWarActive || State.IsInitialWorldSeedingActive)
        {
            return;
        }

        var captureCount = State.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementCaptured);
        var newCaptures = captureCount - notifiedCaptureCount;
        if (newCaptures <= 0)
        {
            return;
        }

        var cooldownTicks = Math.Max(0, settings.worldWarLetterCooldownDays) * TicksPerDay;
        if (currentTick - lastWorldWarLetterTick < cooldownTicks)
        {
            // Within cooldown: hold off, let captures accumulate for the next letter.
            return;
        }

        Find.LetterStack?.ReceiveLetter(
            "LW_WorldWarLetterLabel".Translate(),
            "LW_WorldWarLetterText".Translate(newCaptures.Named("captures")),
            LetterDefOf.NeutralEvent);
        lastWorldWarLetterTick = currentTick;
        notifiedCaptureCount = captureCount;
    }

    private void SimulateWorldDay(int day)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        SettlementProductionService.SimulateDay(
            State,
            new SettlementProductionRequest(
                day * TicksPerDay,
                FoodResourceKey,
                SteelResourceKey,
                MedicineResourceKey,
                ComponentResourceKey));
        SettlementDailySimulationService.SimulateDay(
            State,
            new SettlementDailySimulationRequest(
                day * TicksPerDay,
                FoodResourceKey,
                settings.foodPerCitizen > 0 ? 1 : 0,
                BirthIntervalDays));
        DemographyService.SimulateDay(
            State,
            new DemographySimulationRequest(
                day * TicksPerDay,
                AgeIntervalDays,
                NaturalDeathAge,
                MaxNaturalDeathsPerDay));
        MigrationService.SimulateDay(
            State,
            new MigrationSimulationRequest(
                day * TicksPerDay,
                FoodResourceKey,
                settings.foodPerCitizen > 0 ? 1 : 0,
                50,
                1));

        if (settings.settlementDevelopmentEnabled && !State.IsInitialWorldSeedingActive)
        {
            SettlementDevelopmentService.SimulateDay(
                State,
                new SettlementDevelopmentRequest(
                    day * TicksPerDay,
                    FoodResourceKey,
                    settings.settlementHousingHeadroom,
                    settings.settlementDevelopmentStep,
                    settings.maxSettlementAdults));
        }

        if (settings.drifterFlowEnabled && !State.IsInitialWorldSeedingActive)
        {
            var dayTick = day * TicksPerDay;
            var target = Math.Max(0, State.Settlements.Count * Math.Max(0, settings.targetWorldPopulationPerSettlement));
            var ceiling = Math.Max(target, Math.Max(0, settings.drifterHardCeiling));

            DrifterArrivalService.SimulateArrivals(
                State,
                new DrifterArrivalRequest(dayTick, target, ceiling, settings.maxDrifterArrivalsPerDay));
            DrifterFoundingService.SimulateFounding(
                State,
                new DrifterFoundingRequest(dayTick, settings.drifterMinFounders, settings.drifterLeaderAptitudeThreshold));
            DrifterAssimilationService.SimulateAssimilation(
                State,
                new DrifterAssimilationRequest(dayTick, settings.maxDrifterAssimilationsPerDay));

            cachedTargetPopulation = target;
            cachedWorldPopulation = State.Citizens.Count(citizen => citizen.Status == CitizenStatus.Alive) + State.Drifters.Count;
        }

        // World war runs after population flow and before collapse checks, so faction
        // extinction accounts for the day's battle casualties. It is mutually exclusive with
        // Rim War: if that mod is driving factions, Living World stands down to avoid double
        // driving the same world.
        if (settings.worldWarEnabled && !RimWarIsActive && !State.IsInitialWorldSeedingActive)
        {
            EnsureFactionBehaviors();
            WorldWarService.SimulateDay(
                State,
                new WorldWarRequest(
                    day * TicksPerDay,
                    settings.worldWarTravelDays,
                    settings.worldWarRaidCombatants,
                    settings.worldWarWarbandCooldownDays));
        }

        FactionLifecycleService.SimulateCollapses(
            State,
            new FactionLifecycleRequest(day * TicksPerDay));
    }

    // Rim War (Torann.RimWar) drives world factions the same way; when it is active Living
    // World's own world-war loop stays off so the two never fight over the same world.
    private bool RimWarIsActive => rimWarActive ??= ModsConfig.IsActive("Torann.RimWar");

    // Empire (Matathias.Empire): the player-empire manager. Detected only to inform the player.
    private bool EmpireIsActive => empireActive ??= ModsConfig.IsActive("Matathias.Empire");

    // Gives every ledger faction a behavior once, derived from its RimWorld faction: permanent
    // enemies (pirates) become irreconcilable warmongers, the player is passive, other humanlike
    // factions fight, and non-humanlike factions (mechanoids, insectoids) are excluded.
    private void EnsureFactionBehaviors()
    {
        foreach (var factionId in State.Settlements
            .Select(settlement => settlement.FactionId)
            .Distinct(StringComparer.Ordinal))
        {
            if (State.GetFactionBehavior(factionId) != FactionBehavior.Undefined)
            {
                continue;
            }

            var faction = Find.FactionManager?.AllFactionsListForReading
                .FirstOrDefault(candidate => candidate.def?.defName == factionId);

            if (faction?.def == null || !faction.def.humanlikeFaction)
            {
                State.AssignFactionBehavior(factionId, FactionBehavior.Excluded);
            }
            else if (faction.IsPlayer)
            {
                State.AssignFactionBehavior(factionId, FactionBehavior.Player);
            }
            else if (faction.def.permanentEnemy)
            {
                State.AssignFactionBehavior(factionId, FactionBehavior.Warmonger);
                State.MarkFactionIrreconcilable(factionId);
            }
            else
            {
                State.AssignFactionBehavior(factionId, FactionBehavior.Aggressive);
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            serializedState = WorldStateCodec.Serialize(State);
        }

        Scribe_Values.Look(ref bootstrapped, "livingWorld_bootstrapped", false);
        Scribe_Values.Look(ref serializedState, "livingWorld_serializedState", string.Empty);
        Scribe_Values.Look(ref lastSimulatedDay, "livingWorld_lastSimulatedDay", 0);
        Scribe_Values.Look(ref lastWorldWarLetterTick, "livingWorld_lastWorldWarLetterTick", int.MinValue);
        Scribe_Values.Look(ref notifiedCaptureCount, "livingWorld_notifiedCaptureCount", 0);

        if (Scribe.mode == LoadSaveMode.LoadingVars && !string.IsNullOrWhiteSpace(serializedState))
        {
            State = WorldStateCodec.Deserialize(serializedState);
            LastBootstrapSource = "save";
            LastBootstrapStatus = "loaded";
            LastWorldSettlementSourceCount = State.Settlements.Count;
            ImportableWorldObjectSourceCount = State.Settlements.Count;
        }
    }

    public void RetryBootstrapFromRimWorldSettlements()
    {
        bootstrapped = false;
        BootstrapFromRimWorldSettlements();
    }

    public void CreateDebugLedger()
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        State = new WorldState(ResolveWorldSeed(rimWorld));
        var settlement = State.CreateSettlement("debug-settlement", "LW_DebugSettlementName".Translate().ToString(), "LivingWorldDebug");
        var citizenCount = Math.Max(6, settings.baselineHumanSettlementAdults);
        State.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
            settlement.Id,
            new SettlementProductionEnvironment(
                "TemperateForest",
                "SmallHills",
                "Industrial",
                55,
                850,
                21)));

        for (var i = 0; i < citizenCount; i++)
        {
            var sex = i % 2 == 0 ? Sex.Male : Sex.Female;
            var age = 18 + (i % 42);
            State.CreateCitizen(
                "LW_DebugCitizenName".Translate((i + 1).Named("index")).ToString(),
                age,
                sex,
                "settler",
                settlement.Id);
        }

        State.AddResource(settlement.Id, FoodResourceKey, citizenCount * Math.Max(1, settings.foodPerCitizen));
        State.AddResource(settlement.Id, SteelResourceKey, citizenCount * Math.Max(1, settings.steelPerCitizen));
        PlayerKnowledgeService.RecordPublicSettlementInfo(
            State,
            settlement.Id,
            "debug settlement public disclosure");

        bootstrapped = true;
        LastWorldSettlementSourceCount = 0;
        TotalWorldObjects = 0;
        VanillaSettlementSourceCount = 0;
        FactionWorldObjectSourceCount = 0;
        ImportableWorldObjectSourceCount = 1;
        ScanErrorCount = 0;
        RejectedWorldObjectTypes = "debug-ledger";
        LastBootstrapError = string.Empty;
        LastBootstrapSource = "debug-ledger";
        LastBootstrapStatus = "created";

        if (settings.debugLogging)
        {
            Log.Message($"[LivingWorld] Debug ledger created. {GetSummary()}");
        }
    }

    public void BootstrapFromRimWorldSettlements()
    {
        if (bootstrapped)
        {
            return;
        }

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.bootstrapLedgerDuringWorldGeneration)
        {
            bootstrapped = true;
            LastBootstrapSource = "settings";
            LastBootstrapStatus = "disabled";
            if (settings.debugLogging)
            {
                Log.Message("[LivingWorld] Ledger bootstrap skipped by world generation settings.");
            }

            return;
        }

        try
        {
            State = new WorldState(ResolveWorldSeed(rimWorld));
            LastBootstrapError = string.Empty;

            var scan = new WorldObjectScanner().Scan();
            TotalWorldObjects = scan.Summary.TotalWorldObjects;
            VanillaSettlementSourceCount = scan.Summary.VanillaSettlementSourceCount;
            FactionWorldObjectSourceCount = scan.Summary.FactionWorldObjectSourceCount;
            ImportableWorldObjectSourceCount = scan.Summary.ImportableWorldObjectSourceCount;
            ScanErrorCount = scan.Summary.ScanErrorCount;
            RejectedWorldObjectTypes = scan.Summary.RejectedWorldObjectTypes;
            LastWorldSettlementSourceCount = scan.Summary.VanillaSettlementSourceCount;

            if (scan.Candidates.Count == 0)
            {
                LastBootstrapSource = "world-objects";
                LastBootstrapStatus = "empty-source";
                bootstrapped = false;

                if (settings.debugLogging)
                {
                    Log.Warning($"[LivingWorld] Ledger bootstrap found 0 importable world objects. Total={TotalWorldObjects}, vanilla settlements={VanillaSettlementSourceCount}, faction objects={FactionWorldObjectSourceCount}, scan errors={ScanErrorCount}, rejected={RejectedWorldObjectTypes}.");
                }

                return;
            }

            foreach (var settlement in scan.Candidates)
            {
                var faction = Find.FactionManager.AllFactionsListForReading
                    .FirstOrDefault(candidate => candidate.def?.defName == settlement.FactionId);
                var configuredAdults = faction?.def?.humanlikeFaction == true
                    ? settings.baselineHumanSettlementAdults
                    : settings.baselineNonHumanSettlementAdults;
                var baselineAdults = Math.Max(
                    settings.minSettlementAdults,
                    Math.Min(settings.maxSettlementAdults, configuredAdults));
                var worldSettlement = State.CreateSettlement(settlement.StableKey, settlement.Name, settlement.FactionId);
                var productionProfile = RimWorldSettlementProductionProfileFactory.Create(
                    settlement,
                    worldSettlement.Id,
                    faction);

                State.RunInitialWorldSeeding(() =>
                {
                    // initial world seeding is bulk ledger setup, not runtime world history.
                    State.RecordSettlementProductionProfile(productionProfile);

                    for (var i = 0; i < baselineAdults; i++)
                    {
                        var sex = i % 2 == 0 ? Sex.Male : Sex.Female;
                        var age = 18 + (i % 42);
                        State.CreateCitizen($"{settlement.Name} citizen {i + 1}", age, sex, "settler", worldSettlement.Id);
                    }

                    if (settings.foodPerCitizen > 0)
                    {
                        State.AddResource(worldSettlement.Id, FoodResourceKey, baselineAdults * settings.foodPerCitizen);
                    }

                    if (settings.steelPerCitizen > 0)
                    {
                        State.AddResource(worldSettlement.Id, SteelResourceKey, baselineAdults * settings.steelPerCitizen);
                    }
                });

                PlayerKnowledgeService.RecordPublicSettlementInfo(
                    State,
                    worldSettlement.Id,
                    "settlement public disclosure");
            }

            bootstrapped = true;
            LastBootstrapSource = "world-objects";
            LastBootstrapStatus = "initialized";
            if (settings.debugLogging)
            {
                Log.Message($"[LivingWorld] Ledger initialized. {GetSummary()}");
            }
        }
        catch (Exception ex)
        {
            bootstrapped = false;
            LastBootstrapSource = "world-objects";
            LastBootstrapStatus = "bootstrap-error";
            LastBootstrapError = $"{ex.GetType().Name}: {ex.Message}";
            Log.Error($"[LivingWorld] Ledger bootstrap failed safely: {LastBootstrapError}");
        }
    }

    private void RepairMissingProductionProfilesFromRimWorldSettlements()
    {
        if (!bootstrapped
            || State.Settlements.Count == 0
            || State.ProductionProfiles.Count >= State.Settlements.Count)
        {
            return;
        }

        try
        {
            var scan = new WorldObjectScanner().Scan();
            var repaired = 0;
            foreach (var candidate in scan.Candidates)
            {
                var settlement = State.Settlements.FirstOrDefault(existing => existing.Slug == candidate.StableKey);
                if (settlement == null || State.GetSettlementProductionProfile(settlement.Id) != null)
                {
                    continue;
                }

                var faction = Find.FactionManager.AllFactionsListForReading
                    .FirstOrDefault(existing => existing.def?.defName == candidate.FactionId);
                State.RecordSettlementProductionProfile(
                    RimWorldSettlementProductionProfileFactory.Create(
                        candidate,
                        settlement.Id,
                        faction));
                repaired++;
            }

            if (repaired > 0)
            {
                LastBootstrapStatus = LastBootstrapStatus == "loaded"
                    ? "loaded+production-repair"
                    : LastBootstrapStatus;
                if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
                {
                    Log.Message($"[LivingWorld] Repaired {repaired} missing production profiles.");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Production profile repair skipped safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static int ResolveWorldSeed(World world)
    {
        var seedString = world.info.seedString;
        return string.IsNullOrWhiteSpace(seedString)
            ? StableSeedFromString("LivingWorld")
            : StableSeedFromString(seedString);
    }

    private static int StableSeedFromString(string seedString)
    {
        unchecked
        {
            const int offsetBasis = (int)2166136261;
            const int prime = 16777619;
            var hash = offsetBasis;
            foreach (var character in seedString)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash == int.MinValue
                ? int.MaxValue
                : Math.Abs(hash);
        }
    }
}
