using System;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class LivingWorldWorldComponent : WorldComponent
{
    private const int WorldSeed = 1604850;
    private const int TicksPerDay = 60_000;
    private const int BirthIntervalDays = 30;
    private const string FoodResourceKey = "PackagedSurvivalMeal";
    private const string SteelResourceKey = "Steel";

    private bool bootstrapped;
    private string serializedState = string.Empty;
    private int lastSimulatedDay;

    public LivingWorldWorldComponent(World world)
        : base(world)
    {
        Instance = this;
        State = new WorldState(WorldSeed);
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

    public string GetSummary()
    {
        return "LW_SummaryLine".Translate(
            State.Settlements.Count.Named("settlements"),
            State.Citizens.Count.Named("citizens"),
            State.Armies.Count.Named("armies"),
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

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        SettlementDailySimulationService.SimulateDay(
            State,
            new SettlementDailySimulationRequest(
                currentDay * TicksPerDay,
                FoodResourceKey,
                settings.foodPerCitizen > 0 ? 1 : 0,
                BirthIntervalDays));
        MigrationService.SimulateDay(
            State,
            new MigrationSimulationRequest(
                currentDay * TicksPerDay,
                FoodResourceKey,
                settings.foodPerCitizen > 0 ? 1 : 0,
                50,
                1));
        lastSimulatedDay = currentDay;
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
        State = new WorldState(WorldSeed);
        var settlement = State.CreateSettlement("debug-settlement", "LW_DebugSettlementName".Translate().ToString(), "LivingWorldDebug");
        var citizenCount = Math.Max(6, settings.baselineHumanSettlementAdults);

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
            State = new WorldState(WorldSeed);
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

                State.RunWithoutEvents(() =>
                {
                    // bootstrap citizen/resource import is bulk state seeding, not world history.
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
}
